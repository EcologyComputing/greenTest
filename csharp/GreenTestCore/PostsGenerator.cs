using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GreenTestCore;

/// <summary>
/// A direct C# port of vanilla-compost's generate_posts.py / generate_posts.js -
/// extracted out of greentest.cs into its own library so it's unit-testable
/// with a real xUnit project (see ../GreenTestCore.Tests), rather than living
/// only as private local functions inside a file-based app.
///
/// The functions here are deliberately split into two groups:
///   - Pure functions (no file I/O) - directly unit-testable with plain
///     strings, no filesystem mocking needed.
///   - GeneratePostsHtml, the orchestrator - touches the real filesystem and
///     ties the pure functions together. This is what greentest.cs calls;
///     the pure functions above it are what the test project actually tests.
/// </summary>
public static class PostsGenerator
{
    private static readonly string[] DatePatterns =
    {
        @"\*([A-Za-z]+ \d{1,2}, \d{4})\*",   // *January 15, 2024*
        @"(\d{4}-\d{2}-\d{2})",              // 2024-01-15
        @"([A-Za-z]+ \d{1,2}, \d{4})",       // January 15, 2024
    };

    private static readonly string[] DateFormats = { "MMMM dd, yyyy", "MMMM d, yyyy", "yyyy-MM-dd" };

    /// <summary>
    /// Extracts a post title from raw markdown content: a leading
    /// &lt;!-- title: ... --&gt; comment if present, otherwise a title-cased
    /// version of <paramref name="fallbackName"/> (typically the post's
    /// filename without extension) with dashes turned into spaces.
    /// </summary>
    public static string ExtractTitleFromContent(string content, string fallbackName)
    {
        string firstLine = (content.Split('\n').FirstOrDefault() ?? "").Trim();
        var match = Regex.Match(firstLine, @"<!--\s*title:\s*(.+?)\s*-->");
        if (match.Success) return match.Groups[1].Value;

        string basename = fallbackName.Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(basename.ToLowerInvariant());
    }

    /// <summary>
    /// Extracts a date string from raw markdown content by matching known
    /// date patterns, in priority order. Returns null if none match -
    /// deliberately doesn't fall back to file mtime itself, since that
    /// requires file access this pure function doesn't have; the caller
    /// (GeneratePostsHtml) decides the fallback.
    /// </summary>
    public static string? ExtractDateFromContent(string content)
    {
        foreach (string pattern in DatePatterns)
        {
            var match = Regex.Match(content, pattern);
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    /// <summary>
    /// Parses a date string against the two specific formats this generator
    /// actually writes/reads, for chronological sorting - not a loose
    /// DateTime.Parse, which would also "succeed" on date-like strings that
    /// aren't really one of these two formats. Unparseable strings sort as
    /// oldest (DateTime.MinValue) rather than throwing.
    /// </summary>
    public static DateTime DateSortKey(string dateStr)
    {
        foreach (string fmt in DateFormats)
        {
            if (DateTime.TryParseExact(dateStr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return dt;
            }
        }
        return DateTime.MinValue;
    }

    /// <summary>
    /// Whether a markdown filename found in posts/ should be skipped rather
    /// than treated as an actual post - matches generate_posts.py's and
    /// generate_posts.js's own filtering (a name containing "template" or
    /// "index" is assumed to be scaffolding, not real content). Pure
    /// function, checks the filename only - no file I/O.
    /// </summary>
    public static bool ShouldSkipPostFile(string filename)
    {
        return filename.Contains("template") || filename.Contains("index");
    }

    public record PostEntry(string Name, string Title, string Date);

    /// <summary>
    /// Builds the &lt;p class="lead"&gt;...&lt;/p&gt; HTML fragment listing
    /// posts, in the order given (callers sort beforehand, typically newest
    /// first via DateSortKey).
    /// </summary>
    public static string BuildPostsListHtml(IReadOnlyList<PostEntry> entries)
    {
        var sb = new StringBuilder();
        sb.Append("<p class=\"lead\">\n  <ul>\n");
        foreach (var e in entries)
        {
            sb.Append($"    <li><a href=\"post.html?post={e.Name}\">{e.Title}</a> <small>({e.Date})</small></li>\n");
        }
        if (entries.Count == 0)
        {
            sb.Append("    <li>No posts available yet.</li>\n");
        }
        sb.Append("  </ul>\n</p>\n");
        return sb.ToString();
    }

    /// <summary>
    /// Inserts a posts-list HTML fragment into a template at the
    /// &lt;!-- posts--&gt; marker. Throws <see cref="InvalidOperationException"/>
    /// if the marker isn't found.
    /// </summary>
    public static string InsertPostsIntoTemplate(string template, string postsHtml)
    {
        const string marker = "<!-- posts-->";
        if (!template.Contains(marker))
        {
            throw new InvalidOperationException("Could not find <!-- posts--> marker in html_template.html");
        }
        return template.Replace(marker, postsHtml);
    }

    // -----------------------------------------------------------------
    // Orchestrator below - real file I/O, ties the pure functions above
    // together. Called directly by greentest.cs.
    // -----------------------------------------------------------------

    /// <summary>
    /// Scans <paramref name="srcDir"/>/posts for markdown posts, extracts a
    /// title/date for each, sorts newest first, and writes the result into
    /// <paramref name="srcDir"/>/posts.html in place of the
    /// &lt;!-- posts--&gt; marker in html_template.html. Returns the
    /// generated HTML.
    /// </summary>
    public static string GeneratePostsHtml(string srcDir)
    {
        string templatePath = Path.Combine(srcDir, "html_template.html");
        string template = File.ReadAllText(templatePath, Encoding.UTF8);

        string postsDir = Path.Combine(srcDir, "posts");
        var entries = new List<PostEntry>();

        foreach (string filePath in Directory.GetFiles(postsDir, "*.md"))
        {
            string filename = Path.GetFileName(filePath);
            if (ShouldSkipPostFile(filename)) continue;

            try
            {
                string name = filename.Substring(0, filename.Length - 3);
                string content = File.ReadAllText(filePath, Encoding.UTF8);

                string title = ExtractTitleFromContent(content, name);
                string? date = ExtractDateFromContent(content);
                if (date is null)
                {
                    DateTime mtime = File.GetLastWriteTime(filePath);
                    date = mtime.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
                }

                entries.Add(new PostEntry(name, title, date));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Skipping {filename}: {ex.Message}");
            }
        }

        var sorted = entries.OrderByDescending(e => DateSortKey(e.Date)).ToList();
        string postsHtml = BuildPostsListHtml(sorted);
        string newContent = InsertPostsIntoTemplate(template, postsHtml);

        string outputPath = Path.Combine(srcDir, "posts.html");
        File.WriteAllText(outputPath, newContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Generated posts.html with {sorted.Count} posts using html_template.html");
        return newContent;
    }
}