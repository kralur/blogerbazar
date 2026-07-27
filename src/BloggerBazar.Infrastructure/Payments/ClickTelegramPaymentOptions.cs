namespace BloggerBazar.Infrastructure.Payments;

public sealed class ClickTelegramPaymentOptions
{
    public const string SectionName = "Click";

    public string TelegramProviderToken { get; init; } = string.Empty;
    public string Currency { get; init; } = "UZS";
    public int CurrencyExponent { get; init; } = 2;

    public int ToMinorUnits(int amountUzs)
    {
        if (amountUzs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountUzs));
        }

        return checked(amountUzs * GetScale());
    }

    public bool TryGetAmountUzs(string currency, int amountMinorUnits, out int amountUzs)
    {
        amountUzs = 0;
        if (!string.Equals(currency, Currency, StringComparison.OrdinalIgnoreCase)
            || amountMinorUnits <= 0)
        {
            return false;
        }

        var scale = GetScale();
        if (amountMinorUnits % scale != 0)
        {
            return false;
        }

        amountUzs = amountMinorUnits / scale;
        return true;
    }

    private int GetScale()
    {
        if (CurrencyExponent is < 0 or > 4)
        {
            throw new InvalidOperationException("Click:CurrencyExponent must be between 0 and 4.");
        }

        var scale = 1;
        for (var exponent = 0; exponent < CurrencyExponent; exponent++)
        {
            scale *= 10;
        }

        return scale;
    }
}
