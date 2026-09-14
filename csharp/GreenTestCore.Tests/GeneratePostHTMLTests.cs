using GreenTestCore;

namespace GreenTestCore.Tests;

/// <summary>
/// Integration tests for PostsGenerator.GeneratePostsHtml - the one function
/// in this library that actually touches the filesystem, tying the pure
/// functions tested in PostsGeneratorTests.cs together. Before this file,
/// the only thing that ever exercised it end to end was greentest.cs's own
/// live run against a real vanilla-compost clone - nothing a CI pipeline
/// could catch on its own. Each test gets a fresh temp directory shaped
/// like vanilla-compost/src (html_template.html + posts/), and cleans it
/// up afterward via IDisposable.
/// </summary>
public class GeneratePostsHtmlTests : IDisposable
{
    private readonly string _tempDir;

    public GeneratePostsHtmlTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "GreenTestCoreTests_" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_tempDir, "posts"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void WriteTemplate(string content = "<html><body>\n<!-- posts-->\n</body></html>")
    {
        File.WriteAllText(Path.Combine(_tempDir, "html_template.html"), content);
    }

    private void WritePost(string filename, string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "posts", filename), content);
    }

    [Fact]
    public void GeneratesPostsHtml_WithTitleCommentAndExplicitDate()
    {
        WriteTemplate();
        WritePost("hello-compost.md", "<!-- title: Hello Compost -->\nSome intro.\n\n*January 15, 2024*\n\nBody text.");

        string result = PostsGenerator.GeneratePostsHtml(_tempDir);

        Assert.Contains("href=\"post.html?post=hello-compost\"", result);
        Assert.Contains("Hello Compost", result);
        Assert.Contains("(January 15, 2024)", result);
    }

    [Fact]
    public void WritesPostsHtmlFile_ToSrcDir()
    {
        WriteTemplate();
        WritePost("hello-compost.md", "No title comment.\n\n2024-01-15\n\nBody.");

        PostsGenerator.GeneratePostsHtml(_tempDir);

        string writtenPath = Path.Combine(_tempDir, "posts.html");
        Assert.True(File.Exists(writtenPath), "GeneratePostsHtml should write posts.html into srcDir.");
        string written = File.ReadAllText(writtenPath);
        Assert.Contains("hello-compost", written);
    }

    [Fact]
    public void FallsBackToFileTitle_WhenNoTitleComment()
    {
        WriteTemplate();
        WritePost("hello-compost.md", "No title comment here at all.\n\n2024-01-15\n\nBody.");

        string result = PostsGenerator.GeneratePostsHtml(_tempDir);

        Assert.Contains("Hello Compost", result); // title-cased from the filename
    }

    [Fact]
    public void FallsBackToFileMtime_WhenNoDateInContent()
    {
        WriteTemplate();
        WritePost("hello-compost.md", "<!-- title: Hello Compost -->\nNo date anywhere in this content.");

        string result = PostsGenerator.GeneratePostsHtml(_tempDir);

        // Can't assert an exact date - it's based on the file's mtime at
        // test-run time - but we CAN assert the post still shows up with
        // something in the expected "Month dd, yyyy" shape, rather than
        // being silently dropped or the whole thing crashing.
        Assert.Contains("href=\"post.html?post=hello-compost\"", result);
        Assert.Matches(@"\([A-Za-z]+ \d{2}, \d{4}\)", result);
    }

    [Fact]
    public void SortsMultiplePosts_NewestFirst()
    {
        WriteTemplate();
        WritePost("old-post.md", "<!-- title: Old Post -->\n2023-01-01\n\nBody.");
        WritePost("new-post.md", "<!-- title: New Post -->\n2024-06-01\n\nBody.");

        string result = PostsGenerator.GeneratePostsHtml(_tempDir);

        int newIndex = result.IndexOf("New Post", StringComparison.Ordinal);
        int oldIndex = result.IndexOf("Old Post", StringComparison.Ordinal);
        Assert.True(newIndex >= 0 && oldIndex >= 0, "Both posts should appear in the output.");
        Assert.True(newIndex < oldIndex, "The newer post should appear before the older post.");
    }

    [Fact]
    public void SkipsTemplateAndIndexNamedFiles()
    {
        WriteTemplate();
        WritePost("real-post.md", "<!-- title: Real Post -->\n2024-01-01\n\nBody.");
        WritePost("post-template.md", "<!-- title: Should Be Skipped -->\n2024-01-01\n\nBody.");
        WritePost("index.md", "<!-- title: Also Skipped -->\n2024-01-01\n\nBody.");

        string result = PostsGenerator.GeneratePostsHtml(_tempDir);

        Assert.Contains("Real Post", result);
        Assert.DoesNotContain("Should Be Skipped", result);
        Assert.DoesNotContain("Also Skipped", result);
    }

    [Fact]
    public void ShowsPlaceholder_WhenNoPostsExist()
    {
        WriteTemplate();
        // No posts written at all.

        string result = PostsGenerator.GeneratePostsHtml(_tempDir);

        Assert.Contains("No posts available yet.", result);
    }

    [Fact]
    public void Throws_WhenTemplateMissingPostsMarker()
    {
        WriteTemplate("<html><body>no marker here</body></html>");
        WritePost("hello-compost.md", "<!-- title: Hello Compost -->\n2024-01-01\n\nBody.");

        Assert.Throws<InvalidOperationException>(() => PostsGenerator.GeneratePostsHtml(_tempDir));
    }
}