namespace BloggerBazar.Application.Features.BrandFaces;

// Fixed camelCase values stored and sent as strings (D26); the frontend translates them (QA Q20).
public static class BrandFaceGenders
{
    public const string Female = "female";
    public const string Male = "male";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Female, Male };
}

public static class BrandFaceFormats
{
    public const string PhotoShoot = "photoShoot";
    public const string Video = "video";
    public const string Ugc = "ugc";
    public const string Event = "event";
    public const string Ambassador = "ambassador";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { PhotoShoot, Video, Ugc, Event, Ambassador };
}

// Spoken languages are ISO codes picked from a list, never free text (D49); the frontend translates them.
public static class SpokenLanguages
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "uz", "ru", "en", "kaa", "tg", "kk", "ky", "tk", "tr", "fa", "ar", "zh", "ko", "ja", "de", "fr", "es", "it", "hi"
    };
}

public static class BrandFaceAge
{
    public const int Min = 16;
    public const int Max = 80;
}
