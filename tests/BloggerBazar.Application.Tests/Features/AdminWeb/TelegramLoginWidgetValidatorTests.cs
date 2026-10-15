using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using BloggerBazar.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Application.Tests.Features.AdminWeb;

public sealed class TelegramLoginWidgetValidatorTests
{
    private const string BotToken = "123456:login-widget-test-token";

    [Fact]
    public void Accepts_the_hash_computed_by_an_independent_implementation()
    {
        // Computed with Python's hashlib/hmac from the algorithm on core.telegram.org/widgets/login.
        var fields = new Dictionary<string, string>
        {
            ["id"] = "5044343262",
            ["first_name"] = "Umid",
            ["last_name"] = "Test",
            ["username"] = "umid_test",
            ["photo_url"] = "https://t.me/i/userpic/320/abc.jpg",
            ["auth_date"] = "1760000000",
            ["hash"] = "8494768bf1e34aa19887efe7bd8b29e5b3ab7afd21474ef2133c667329177b8b"
        };
        var validator = CreateValidator("123456:independent-vector-token", maxAuthAgeSeconds: int.MaxValue);

        var user = validator.Validate(fields);

        Assert.Equal(5044343262, user.Id);
        Assert.Equal("Umid", user.FirstName);
        Assert.Equal("umid_test", user.Username);
        Assert.Equal(fields["hash"], user.Hash);
    }

    [Fact]
    public void Accepts_fresh_signed_data()
    {
        var user = CreateValidator().Validate(LoginWidgetTestData.Signed(BotToken, 42));

        Assert.Equal(42, user.Id);
        Assert.Equal("umid", user.Username);
    }

    [Fact]
    public void Rejects_a_wrong_hash_and_a_changed_field()
    {
        var validator = CreateValidator();
        var wrongHash = LoginWidgetTestData.Signed(BotToken, 42);
        wrongHash["hash"] = new string('0', 64);
        var changedId = LoginWidgetTestData.Signed(BotToken, 42);
        changedId["id"] = "43";
        var otherBot = LoginWidgetTestData.Signed("654321:another-bot-token", 42);

        Assert.Throws<AuthenticationException>(() => validator.Validate(wrongHash));
        Assert.Throws<AuthenticationException>(() => validator.Validate(changedId));
        Assert.Throws<AuthenticationException>(() => validator.Validate(otherBot));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zz94768bf1e34aa19887efe7bd8b29e5b3ab7afd21474ef2133c667329177b8b")]
    public void Rejects_a_missing_or_malformed_hash(string? hash)
    {
        var fields = LoginWidgetTestData.Signed(BotToken, 42);
        if (hash is null) fields.Remove("hash"); else fields["hash"] = hash;

        Assert.Throws<AuthenticationException>(() => CreateValidator().Validate(fields));
    }

    [Fact]
    public void Rejects_data_older_than_the_limit_or_from_the_future()
    {
        var validator = CreateValidator(maxAuthAgeSeconds: 300);

        Assert.Throws<AuthenticationException>(() => validator.Validate(LoginWidgetTestData.Signed(BotToken, 42, DateTimeOffset.UtcNow.AddSeconds(-301))));
        Assert.Throws<AuthenticationException>(() => validator.Validate(LoginWidgetTestData.Signed(BotToken, 42, DateTimeOffset.UtcNow.AddMinutes(5))));
        Assert.Equal(42, validator.Validate(LoginWidgetTestData.Signed(BotToken, 42, DateTimeOffset.UtcNow.AddSeconds(-200))).Id);
    }

    [Fact]
    public void Fields_unknown_today_are_part_of_the_signature()
    {
        var validator = CreateValidator();
        var fields = LoginWidgetTestData.Signed(BotToken, 42, extra: new Dictionary<string, string> { ["photo_url"] = "https://t.me/i/userpic/320/x.jpg", ["new_field"] = "1" });

        Assert.Equal(42, validator.Validate(fields).Id);
        fields.Remove("new_field");
        Assert.Throws<AuthenticationException>(() => validator.Validate(fields));
    }

    [Fact]
    public void The_mini_app_init_data_key_does_not_sign_widget_data()
    {
        var fields = LoginWidgetTestData.Signed(BotToken, 42);
        var dataCheckString = string.Join('\n', fields.Where(pair => pair.Key != "hash").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));
        var webAppSecret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(BotToken));
        fields["hash"] = Convert.ToHexString(HMACSHA256.HashData(webAppSecret, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();

        Assert.Throws<AuthenticationException>(() => CreateValidator().Validate(fields));
    }

    [Fact]
    public void Rejects_data_without_a_user_id()
    {
        var fields = LoginWidgetTestData.Signed(BotToken, 42);
        fields.Remove("id");
        fields["hash"] = LoginWidgetTestData.Sign(BotToken, fields);

        Assert.Throws<AuthenticationException>(() => CreateValidator().Validate(fields));
    }

    [Fact]
    public void Fails_closed_without_a_bot_token()
    {
        Assert.Throws<InvalidOperationException>(() => CreateValidator(botToken: "").Validate(LoginWidgetTestData.Signed(BotToken, 42)));
    }

    private static TelegramLoginWidgetValidator CreateValidator(string botToken = BotToken, int maxAuthAgeSeconds = 300) => new(
        Options.Create(new TelegramOptions { BotToken = botToken, MaxInitDataClockSkewSeconds = 60 }),
        Options.Create(new AdminWebOptions { Origin = "https://admin.example", MaxAuthAgeSeconds = maxAuthAgeSeconds }));
}
