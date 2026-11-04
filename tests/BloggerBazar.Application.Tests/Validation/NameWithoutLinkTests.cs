using BloggerBazar.Application.Features.Businesses;
using BloggerBazar.Application.Validation;

namespace BloggerBazar.Application.Tests.Validation;

// Pre-launch audit 2026-11-04: names and titles end up in the bot's messages, so they may not carry a link.
public sealed class NameWithoutLinkTests
{
    [Theory]
    [InlineData("Выплата за рекламу: https://evil.example", false)]
    [InlineData("Cafe http://x", false)]
    [InlineData("Заходи на www.promo.example", false)]
    [InlineData("Подпишись t.me/fakechannel", false)]
    [InlineData("TELEGRAM.ME/abc", false)]
    [InlineData("Cafe Darvoza", true)]
    [InlineData("Uzum.uz", true)]
    [InlineData("Cafe @ Chilonzor", true)]
    [InlineData(null, true)]
    public void A_link_is_refused_and_ordinary_names_pass(string? value, bool allowed) =>
        Assert.Equal(allowed, ContactValidation.HasNoLink(value));

    [Fact]
    public void A_company_name_with_a_link_fails_validation()
    {
        var validator = new CreateBusinessProfileValidator();
        var result = validator.Validate(new CreateBusinessProfileCommand(1, "Payout https://evil.example", null, "tashkent", null, null, null, null, null));

        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
    }
}
