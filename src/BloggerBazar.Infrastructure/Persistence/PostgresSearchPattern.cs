namespace BloggerBazar.Infrastructure.Persistence;

internal static class PostgresSearchPattern
{
    public static string Contains(string value) => $"%{value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";

    // The query in every script it could have been typed in (see SearchScripts), for `column ILIKE ANY (patterns)`.
    public static string[] ContainsAnyScript(string value) => SearchScripts.Variants(value).Select(Contains).ToArray();
}
