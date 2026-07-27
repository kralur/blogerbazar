using System.Security.Cryptography;
using System.Text;
using BloggerBazar.Application.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace BloggerBazar.Infrastructure.Security;

internal sealed class TelegramWebhookValidator(IOptions<TelegramOptions> options) : ITelegramWebhookValidator
{
    public bool IsValid(string? secretToken)
    {
        var configuredSecret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(configuredSecret) || string.IsNullOrWhiteSpace(secretToken))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(configuredSecret),
            Encoding.UTF8.GetBytes(secretToken));
    }
}
