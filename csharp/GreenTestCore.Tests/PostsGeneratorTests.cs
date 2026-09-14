using GreenTestCore;

namespace GreenTestCore.Tests;

public class ShouldSkipPostFileTests
{
    [Theory]
    [InlineData("post-template.md")]
    [InlineData("template.md")]
    [InlineData("index.md")]
    [InlineData("blog-index-page.md")]
    public void SkipsFilenamesContainingTemplateOrIndex(string filename)
    {
        Assert.True(PostsGenerator.ShouldSkipPostFile(filename));
    }

    [Theory]
    [InlineData("hello-compost.md")]
    [InlineData("greenTest-Message.md")]
    [InlineData("my-first-post.md")]
    public void DoesNotSkipOrdinaryPostFilenames(string filename)
    {
        Assert.False(PostsGenerator.ShouldSkipPostFile(filename));
    }
}

public class ExtractTitleFromContentTests
{
    [Fact]
    public void UsesTitleComment_WhenPresent()
    {
        string content = "<!-- title: My Real Title -->\nSome body text.";
        string title = PostsGenerator.ExtractTitleFromContent(content, "some-fallback-name");
        Assert.Equal("My Real Title", title);
    }

    [Fact]
    public void FallsBackToFilename_WhenNoTitleComment()
    {
        string content = "Just some markdown with no title comment at all.";
        string title = PostsGenerator.ExtractTitleFromContent(content, "hello-compost");
        Assert.Equal("Hello Compost", title);
    }

    [Fact]
    public void TitleComment_TrimsWhitespace()
    {
        string content = "<!--   title:   Spaced Out Title   -->\nBody.";
        string title = PostsGenerator.ExtractTitleFromContent(content, "fallback");
        Assert.Equal("Spaced Out Title", title);
    }
}

public class ExtractDateFromContentTests
{
    [Theory]
    [InlineData("Some intro.\n\n*January 15, 2024*\n\nMore text.", "January 15, 2024")]
    [InlineData("Posted on 2024-01-15 by someone.", "2024-01-15")]
    [InlineData("Written January 15, 2024 while it was raining.", "January 15, 2024")]
    public void MatchesKnownDatePatterns(string content, string expectedDate)
    {
        string? date = PostsGenerator.ExtractDateFromContent(content);
        Assert.Equal(expectedDate, date);
    }

    [Fact]
    public void ReturnsNull_WhenNoDatePatternMatches()
    {
        string? date = PostsGenerator.ExtractDateFromContent("No date anywhere in this text at all.");
        Assert.Null(date);
    }

    [Fact]
    public void PrefersStarredDate_OverBareDate_WhenBothPresent()
    {
        // The starred pattern is checked first - matches the priority order
        // generate_posts.py/.js use.
        string content = "*January 15, 2024* is when this was really posted, not 2024-06-01.";
        string? date = PostsGenerator.ExtractDateFromContent(content);
        Assert.Equal("January 15, 2024", date);
    }
}

public class DateSortKeyTests
{
    [Fact]
    public void ParsesFullMonthNameFormat()
    {
        DateTime result = PostsGenerator.DateSortKey("January 15, 2024");
        Assert.Equal(new DateTime(2024, 1, 15), result);
    }

    [Fact]
    public void ParsesIsoFormat()
    {
        DateTime result = PostsGenerator.DateSortKey("2024-01-15");
        Assert.Equal(new DateTime(2024, 1, 15), result);
    }

    [Fact]
    public void ReturnsMinValue_ForUnparseableString()
    {
        DateTime result = PostsGenerator.DateSortKey("not a date at all");
        Assert.Equal(DateTime.MinValue, result);
    }

    [Fact]
    public void NewerDateSortsAfterOlderDate()
    {
        DateTime older = PostsGenerator.DateSortKey("January 15, 2024");
        DateTime newer = PostsGenerator.DateSortKey("2024-06-01");
        Assert.True(newer > older);
    }

    [Fact]
    public void SameDate_InBothFormats_SortsAsEqual()
    {
        // "January 15, 2024" and "2024-01-15" represent the same day - a
        // post using one format and a post using the other should sort as
        // simultaneous, not as if one were newer than the other just
        // because of which format its author happened to write.
        DateTime fromMonthName = PostsGenerator.DateSortKey("January 15, 2024");
        DateTime fromIso = PostsGenerator.DateSortKey("2024-01-15");
        Assert.Equal(fromMonthName, fromIso);
    }
}

public class BuildPostsListHtmlTests
{
    [Fact]
    public void ListsEachEntry_WithLinkAndDate()
    {
        var entries = new List<PostsGenerator.PostEntry>
        {
            new("hello-compost", "Hello Compost", "July 05, 2026"),
        };

        string html = PostsGenerator.BuildPostsListHtml(entries);

        Assert.Contains("href=\"post.html?post=hello-compost\"", html);
        Assert.Contains("Hello Compost", html);
        Assert.Contains("(July 05, 2026)", html);
    }

    [Fact]
    public void ShowsPlaceholder_WhenNoEntries()
    {
        string html = PostsGenerator.BuildPostsListHtml(new List<PostsGenerator.PostEntry>());
        Assert.Contains("No posts available yet.", html);
    }

    [Fact]
    public void PreservesGivenOrder_DoesNotReSort()
    {
        // BuildPostsListHtml trusts the caller to have already sorted -
        // GeneratePostsHtml is what actually sorts, via DateSortKey. This
        // guards against a future change accidentally adding a second,
        // possibly-conflicting sort in here too: entries given oldest-first
        // should come out oldest-first, even though that's the "wrong"
        // order GeneratePostsHtml would never actually produce.
        var entries = new List<PostsGenerator.PostEntry>
        {
            new("old-post", "Old Post", "2023-01-01"),
            new("new-post", "New Post", "2024-06-01"),
        };

        string html = PostsGenerator.BuildPostsListHtml(entries);

        int oldIndex = html.IndexOf("Old Post", StringComparison.Ordinal);
        int newIndex = html.IndexOf("New Post", StringComparison.Ordinal);
        Assert.True(oldIndex >= 0 && newIndex >= 0, "Both entries should appear in the output.");
        Assert.True(oldIndex < newIndex, "Entries should appear in the order given, not re-sorted.");
    }
}

public class InsertPostsIntoTemplateTests
{
    [Fact]
    public void ReplacesMarker_WithPostsHtml()
    {
        string template = "<html><body>\n<!-- posts-->\n</body></html>";
        string result = PostsGenerator.InsertPostsIntoTemplate(template, "<p>hi</p>\n");

        Assert.Contains("<p>hi</p>", result);
        Assert.DoesNotContain("<!-- posts-->", result);
    }

    [Fact]
    public void Throws_WhenMarkerMissing()
    {
        string template = "<html><body>no marker here</body></html>";
        Assert.Throws<InvalidOperationException>(
            () => PostsGenerator.InsertPostsIntoTemplate(template, "<p>hi</p>"));
    }
}