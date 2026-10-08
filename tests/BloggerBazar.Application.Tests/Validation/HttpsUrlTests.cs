using BloggerBazar.Application.Validation;

namespace BloggerBazar.Application.Tests.Validation;

public sealed class HttpsUrlTests
{
    [Theory]
    [InlineData("https://lumi.uz", true)]
    [InlineData("https://lumi.uz/catalog?x=1", true)]
    [InlineData("http://lumi.uz", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://bank.uz@evil.com", false)]
    [InlineData("https://user:pass@evil.com", false)]
    public void Accepts_only_plain_https_links(string url, bool valid) =>
        Assert.Equal(valid, ContactValidation.IsHttpsUrl(url));
}
