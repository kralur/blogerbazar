using BloggerBazar.Domain;
using BloggerBazar.Domain.Entities;

namespace BloggerBazar.Application.Tests.Domain;

// Owner, 2026-11-09: one list, one font. Names typed in fancy Unicode letters are folded to plain ones when saved.
public sealed class PersonNameTests
{
    [Theory]
    [InlineData("\U0001D400\U0001D425\U0001D422", "Ali")]            // mathematical bold
    [InlineData("\U0001D49C\U0001D4C1\U0001D4BE", "Ali")]            // mathematical script
    [InlineData("Ａｌｉ", "Ali")]                          // fullwidth
    [InlineData("ᴀʟɪ", "ali")]                          // small capitals
    [InlineData("Ali​‍ Valiyev", "Ali Valiyev")]             // zero-width characters
    [InlineData("  Madina   Karimova ", "Madina Karimova")]
    [InlineData("Умиджон", "Умиджон")]
    [InlineData("Ali \U0001F600", "Ali \U0001F600")]                   // emoji stay
    public void Folds_fancy_letters_to_plain_ones(string typed, string expected)
    {
        Assert.Equal(expected, PersonName.Normalize(typed));
    }

    [Fact]
    public void Keeps_a_name_made_only_of_symbols_instead_of_emptying_it()
    {
        Assert.Equal("\u200B", PersonName.Normalize("\u200B"));
        Assert.Equal("", PersonName.Normalize("   "));
    }

    [Fact]
    public void Profiles_and_platform_users_save_the_plain_name()
    {
        var blogger = BloggerProfile.Create(1, "\U0001D400\U0001D425\U0001D422", "tashkent", ["beauty"]);
        var business = BusinessProfile.Create(2, "Ｋｏｆｅ Hona", "tashkent");
        var brandFace = BrandFaceProfile.Create(3, "ᴀʟɪ", "tashkent", ["fashion"]);
        var user = PlatformUser.Create(4, "\U0001D400\U0001D425\U0001D422", "ali");
        Assert.Equal("Ali", blogger.Name);
        Assert.Equal("Kofe Hona", business.Name);
        Assert.Equal("ali", brandFace.Name);
        Assert.Equal("Ali", user.FirstName);
        user.SyncTelegramIdentity("\U0001D400\U0001D425\U0001D422", "ali");
        Assert.Equal("Ali", user.FirstName);
    }
}
