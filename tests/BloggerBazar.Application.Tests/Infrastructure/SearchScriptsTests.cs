using BloggerBazar.Infrastructure.Persistence;

namespace BloggerBazar.Application.Tests.Infrastructure;

// Owner, 2026-11-09: "I typed али and Ali did not come up". A name query is searched in both scripts.
public sealed class SearchScriptsTests
{
    [Fact]
    public void A_cyrillic_query_gets_its_latin_spellings()
    {
        var variants = SearchScripts.Variants("али");
        Assert.Equal("али", variants[0]);
        Assert.Contains("ali", variants);
    }

    [Fact]
    public void A_latin_query_gets_its_cyrillic_spellings()
    {
        Assert.Contains("али", SearchScripts.Variants("Ali"));
        Assert.Contains("шаҳзода", SearchScripts.Variants("shahzoda"));
        Assert.Contains("шахзода", SearchScripts.Variants("shahzoda"));
    }

    [Fact]
    public void Letters_with_two_spellings_give_both_and_the_list_stays_short()
    {
        var variants = SearchScripts.Variants("шахзода");
        Assert.Contains("shaxzoda", variants);
        Assert.Contains("shahzoda", variants);
        Assert.True(SearchScripts.Variants("хцўғхцўғхцўғ").Count <= 8);
    }

    [Fact]
    public void Digits_and_blank_queries_add_nothing()
    {
        Assert.Equal(["2026"], SearchScripts.Variants("2026"));
        Assert.Empty(SearchScripts.Variants("   "));
    }
}
