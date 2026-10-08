namespace BloggerBazar.Application.Validation;

// Upper bounds for numbers people type in; anything above is a typo or a test of the form, never a real value.
public static class InputLimits
{
    // 1 billion sum: far above any real advertising price or budget in Uzbekistan.
    public const int MaxMoney = 1_000_000_000;

    // Larger than the biggest accounts on any platform.
    public const int MaxFollowers = 500_000_000;
    public const int MaxReach = 1_000_000_000;
}
