namespace BloggerBazar.Application.Validation;

// Cities are keys from the app's region list; anything else would show up as a raw key on screen.
// Older names already stored in profiles stay valid so existing users can still save.
public static class Regions
{
    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        "karakalpakstan", "tashkent-city", "tashkent-region", "andijan", "bukhara", "jizzakh", "kashkadarya",
        "navoi", "namangan", "samarkand", "sirdarya", "surkhandarya", "fergana", "khorezm",
        "tashkent", "uzbekistan", "ташкент", "самарканд", "бухара", "фергана", "андижан", "наманган", "узбекистан"
    };

    public static bool IsKnown(string? city) => city is not null && Known.Contains(city.Trim());
}
