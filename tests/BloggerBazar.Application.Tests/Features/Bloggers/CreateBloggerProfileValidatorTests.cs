using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Validation;

namespace BloggerBazar.Application.Tests.Features.Bloggers;

public sealed class CreateBloggerProfileValidatorTests
{
    [Fact]
    public void Rejects_invalid_pricing_and_audience_values()
    {
        var validator = new CreateBloggerProfileValidator();
        var result = validator.Validate(new CreateBloggerProfileCommand(
            1, "Name", null, null, "Ташкент", ["Lifestyle"], null, null,
            -1, null, 101m, -1, null, null, null, false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "TotalFollowers");
        Assert.Contains(result.Errors, error => error.PropertyName == "EngagementRate");
        Assert.Contains(result.Errors, error => error.PropertyName == "StoriesPrice");
    }

    [Fact]
    public void Rejects_numbers_above_any_real_audience_or_price()
    {
        var result = new CreateBloggerProfileValidator().Validate(new CreateBloggerProfileCommand(
            1, "Name", null, null, "Ташкент", ["Lifestyle"], null, null,
            InputLimits.MaxFollowers + 1, InputLimits.MaxReach + 1, 5m, InputLimits.MaxMoney + 1, 1_000, null, null, false));

        Assert.Contains(result.Errors, error => error.PropertyName == "TotalFollowers");
        Assert.Contains(result.Errors, error => error.PropertyName == "AverageReach");
        Assert.Contains(result.Errors, error => error.PropertyName == "StoriesPrice");
    }

    [Fact]
    public void Accepts_a_profile_without_a_telegram_username()
    {
        var result = new CreateBloggerProfileValidator().Validate(new CreateBloggerProfileCommand(
            1, "Name", null, null, "Ташкент", ["Lifestyle"], null, null,
            10_000, 5_000, 5m, 100_000, 200_000, null, null, false, Phone: "+998901234567"));

        Assert.DoesNotContain(result.Errors, error => error.PropertyName == "Username");
    }

    [Theory]
    [InlineData("tashkent-city", true)]
    [InlineData("Samarkand", true)]
    [InlineData("Ташкент", true)]
    [InlineData("mars", false)]
    [InlineData("<script>", false)]
    public void Accepts_only_known_regions(string city, bool valid)
    {
        Assert.Equal(valid, Regions.IsKnown(city));
        var result = new CreateBloggerProfileValidator().Validate(new CreateBloggerProfileCommand(
            1, "Name", null, null, city, ["Lifestyle"], null, null,
            10_000, 5_000, 5m, 100_000, 200_000, null, null, false, Phone: "+998 90 123 45 67"));
        Assert.Equal(!valid, result.Errors.Any(error => error.PropertyName == "City"));
    }

    [Fact]
    public void Rejects_a_last_name_longer_than_the_database_column()
    {
        var result = new CreateBloggerProfileValidator().Validate(new CreateBloggerProfileCommand(
            1, "Name", new string('a', 101), null, "tashkent-city", ["Lifestyle"], null, null,
            10_000, 5_000, 5m, 100_000, 200_000, null, null, false, Phone: "+998 90 123 45 67"));

        Assert.Contains(result.Errors, error => error.PropertyName == "LastName");
    }
}
