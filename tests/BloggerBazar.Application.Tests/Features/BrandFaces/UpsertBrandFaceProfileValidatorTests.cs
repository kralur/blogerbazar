using BloggerBazar.Application.Features.BrandFaces;

namespace BloggerBazar.Application.Tests.Features.BrandFaces;

// QA Q17/Q20: a business looks at photos on Instagram and picks a face by gender, age and format.
public sealed class UpsertBrandFaceProfileValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a handle")]
    public void Rejects_a_profile_without_a_valid_instagram(string? instagram)
    {
        var result = new UpsertBrandFaceProfileValidator().Validate(Command() with { Instagram = instagram });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpsertBrandFaceProfileCommand.Instagram));
    }

    [Fact]
    public void Accepts_a_complete_profile()
    {
        var result = new UpsertBrandFaceProfileValidator().Validate(Command());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other")]
    public void Rejects_a_missing_or_unknown_gender(string? gender)
    {
        var result = new UpsertBrandFaceProfileValidator().Validate(Command() with { Gender = gender });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpsertBrandFaceProfileCommand.Gender));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(15)]
    [InlineData(81)]
    public void Rejects_a_missing_or_out_of_range_age(int? age)
    {
        var result = new UpsertBrandFaceProfileValidator().Validate(Command() with { Age = age });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpsertBrandFaceProfileCommand.Age));
    }

    [Fact]
    public void Requires_at_least_one_known_format()
    {
        var validator = new UpsertBrandFaceProfileValidator();

        Assert.Contains(validator.Validate(Command() with { Formats = [] }).Errors, error => error.PropertyName == nameof(UpsertBrandFaceProfileCommand.Formats));
        Assert.Contains(validator.Validate(Command() with { Formats = ["dancing"] }).Errors, error => error.PropertyName == nameof(UpsertBrandFaceProfileCommand.Formats));
    }

    // D49: languages come from a fixed list of codes; free text is refused.
    [Theory]
    [InlineData("Русский")]
    [InlineData("russian")]
    [InlineData("")]
    public void Rejects_a_language_outside_the_list(string language)
    {
        var result = new UpsertBrandFaceProfileValidator().Validate(Command() with { Languages = ["uz", language] });

        Assert.Contains(result.Errors, error => error.PropertyName.StartsWith(nameof(UpsertBrandFaceProfileCommand.Languages)));
    }

    [Fact]
    public void Accepts_only_a_secure_showreel_link()
    {
        var result = new UpsertBrandFaceProfileValidator().Validate(Command() with { ShowreelUrl = "http://example.com/reel" });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpsertBrandFaceProfileCommand.ShowreelUrl));
    }

    private static UpsertBrandFaceProfileCommand Command() =>
        new(9001, "Madina", "tashkent", 24, BrandFaceGenders.Female, ["uz"], ["beauty"], null, "@madina.face", "@madina", null, 300_000, null, null,
            [BrandFaceFormats.PhotoShoot, BrandFaceFormats.Video], "https://instagram.com/reel/abc");
}
