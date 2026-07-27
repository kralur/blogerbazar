using BloggerBazar.Application.Features.Bloggers;

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
}
