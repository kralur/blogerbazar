namespace BloggerBazar.Infrastructure.Persistence;

internal static class PostgresSearchPattern
{
    public static string Contains(string value) => $"%{value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
}
