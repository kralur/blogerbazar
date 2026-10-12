namespace BloggerBazar.Application.Validation;

// Dates from query strings and JSON ("2026-10-20" from a date input) arrive without a time zone. PostgreSQL
// timestamptz takes only UTC, so an unspecified date is read as that calendar day in UTC instead of failing with 500.
public static class UtcDates
{
    public static DateTime? AsUtc(DateTime? value) => value switch
    {
        null => null,
        { Kind: DateTimeKind.Utc } utc => utc,
        { Kind: DateTimeKind.Local } local => local.ToUniversalTime(),
        { } unspecified => DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)
    };
}
