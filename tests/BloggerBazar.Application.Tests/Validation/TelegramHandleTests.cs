using BloggerBazar.Application.Validation;

namespace BloggerBazar.Application.Tests.Validation;

public sealed class TelegramHandleTests
{
    [Theory]
    [InlineData("samir_uz", "@samir_uz")]
    [InlineData("@samir_uz", "@samir_uz")]
    [InlineData(" samir_uz ", "@samir_uz")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("bad name", null)]
    public void Handle_comes_only_from_a_valid_telegram_username(string? telegramUsername, string? expected) =>
        Assert.Equal(expected, ContactValidation.TelegramHandle(telegramUsername));
}
