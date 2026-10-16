using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

// Web admin analytics (D57, phase 2). Plain SQL so the grouping by Tashkent day, month, city and category happens in
// PostgreSQL; values are always parameters, only fixed table and column names are written into the text.
// Queries run one after another: one DbContext runs one query at a time.
internal sealed class AdminAnalyticsReadModel(BloggerBazarDbContext dbContext) : IAdminAnalyticsReadModel
{
    private const string Tashkent = "'Asia/Tashkent'";

    public async Task<IReadOnlyList<AdminDailyPointDto>> GetDailyAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var fromUtc = AdminAnalytics.StartUtc(from);
        var toUtc = AdminAnalytics.StartUtc(to.AddDays(1));
        var newUsers = await CountByDayAsync("platform_users", "CreatedAtUtc", null, fromUtc, toUtc, cancellationToken);
        var lastSeen = await CountByDayAsync("platform_users", "LastSeenAtUtc", null, fromUtc, toUtc, cancellationToken);
        var campaigns = await CountByDayAsync("campaigns", "CreatedAtUtc", null, fromUtc, toUtc, cancellationToken);
        var applications = await CountByDayAsync("campaign_applications", "CreatedAtUtc", null, fromUtc, toUtc, cancellationToken);
        var offers = await CountByDayAsync("collaboration_requests", "CreatedAtUtc", null, fromUtc, toUtc, cancellationToken);
        var deals = await CountByDayAsync("deals", "CreatedAtUtc", null, fromUtc, toUtc, cancellationToken);
        var completed = await CountByDayAsync("deals", "CompletedAtUtc", $"t.\"Status\" = {(int)DealStatus.Completed}", fromUtc, toUtc, cancellationToken);
        int Of(Dictionary<DateOnly, int> counts, DateOnly day) => counts.GetValueOrDefault(day);
        return newUsers.Keys.Concat(lastSeen.Keys).Concat(campaigns.Keys).Concat(applications.Keys).Concat(offers.Keys).Concat(deals.Keys).Concat(completed.Keys)
            .Distinct()
            .Order()
            .Select(day => new AdminDailyPointDto(day, Of(newUsers, day), Of(lastSeen, day), Of(campaigns, day), Of(applications, day), Of(offers, day), Of(deals, day), Of(completed, day)))
            .ToArray();
    }

    public async Task<IReadOnlyList<AdminMoneyMonthDto>> GetMoneyAsync(DateOnly firstMonth, CancellationToken cancellationToken)
    {
        var sql = $$"""
            SELECT to_char(d."CompletedAtUtc" AT TIME ZONE {{Tashkent}}, 'YYYY-MM') AS "Month",
                   count(*)::int AS "Completed",
                   count(d."AgreedPrice")::int AS "Priced",
                   coalesce(sum(d."AgreedPrice"), 0)::bigint AS "Total"
            FROM deals d
            WHERE d."Status" = {{(int)DealStatus.Completed}} AND d."CompletedAtUtc" >= {0}
            GROUP BY 1
            """;
        var rows = await dbContext.Database.SqlQueryRaw<MoneyRow>(sql, AdminAnalytics.StartUtc(firstMonth)).ToListAsync(cancellationToken);
        return rows.Select(row => new AdminMoneyMonthDto(row.Month, row.Completed, row.Priced, row.Total,
            row.Priced == 0 ? null : (long)Math.Round((double)row.Total / row.Priced, MidpointRounding.AwayFromZero))).ToArray();
    }

    public async Task<AdminGeographyDto> GetGeographyAsync(CancellationToken cancellationToken)
    {
        // Deals go by the creator's profile (every deal has a blogger or a brand face); campaigns count once published.
        const string creators = """
            FROM deals d
            LEFT JOIN blogger_profiles b ON b."Id" = d."BloggerId"
            LEFT JOIN brand_face_profiles f ON f."Id" = d."BrandFaceId"
            """;
        var cities = await GeoRowsAsync($"""
            SELECT p."City" AS "Key", 'bloggers' AS "Metric", count(*)::int AS "Count" FROM blogger_profiles p WHERE NOT p."IsDeleted" GROUP BY 1
            UNION ALL SELECT p."City", 'brandFaces', count(*)::int FROM brand_face_profiles p WHERE NOT p."IsDeleted" GROUP BY 1
            UNION ALL SELECT c."City", 'campaigns', count(*)::int FROM campaigns c WHERE c."Status" <> {(int)CampaignStatus.Draft} GROUP BY 1
            UNION ALL SELECT coalesce(b."City", f."City"), 'deals', count(*)::int {creators} GROUP BY 1
            """, cancellationToken);
        var categories = await GeoRowsAsync($"""
            SELECT k AS "Key", 'bloggers' AS "Metric", count(*)::int AS "Count" FROM blogger_profiles p CROSS JOIN LATERAL unnest(p."Categories") AS k WHERE NOT p."IsDeleted" GROUP BY 1
            UNION ALL SELECT k, 'brandFaces', count(*)::int FROM brand_face_profiles p CROSS JOIN LATERAL unnest(p."Categories") AS k WHERE NOT p."IsDeleted" GROUP BY 1
            UNION ALL SELECT k, 'campaigns', count(*)::int FROM campaigns c CROSS JOIN LATERAL unnest(c."Categories") AS k WHERE c."Status" <> {(int)CampaignStatus.Draft} GROUP BY 1
            UNION ALL SELECT k, 'deals', count(*)::int {creators} CROSS JOIN LATERAL unnest(coalesce(b."Categories", f."Categories")) AS k GROUP BY 1
            """, cancellationToken);
        return new AdminGeographyDto(cities, categories);
    }

    public async Task<AdminRetentionDto> GetRetentionAsync(DateOnly from, DateOnly to, DateTime utcNow, CancellationToken cancellationToken)
    {
        var horizons = AdminAnalytics.RetentionHorizons;
        // A person can be judged for N days only once N days have passed since their first open.
        var columns = string.Join(",\n", horizons.Select(days => $$"""
            count(*) FILTER (WHERE u."CreatedAtUtc" <= {2} - interval '{{days}} days')::int AS "Eligible{{days}}",
            count(*) FILTER (WHERE u."CreatedAtUtc" <= {2} - interval '{{days}} days' AND u."LastSeenAtUtc" >= u."CreatedAtUtc" + interval '{{days}} days')::int AS "Returned{{days}}"
            """));
        var sql = $$"""
            SELECT count(*)::int AS "Cohort",
            {{columns}}
            FROM platform_users u
            WHERE u."CreatedAtUtc" >= {0} AND u."CreatedAtUtc" < {1}
            """;
        var row = await dbContext.Database.SqlQueryRaw<RetentionRow>(sql, AdminAnalytics.StartUtc(from), AdminAnalytics.StartUtc(to.AddDays(1)), utcNow)
            .SingleAsync(cancellationToken);
        return new AdminRetentionDto(from, to, row.Cohort,
        [
            new AdminRetentionStepDto(1, row.Eligible1, row.Returned1),
            new AdminRetentionStepDto(7, row.Eligible7, row.Returned7),
            new AdminRetentionStepDto(30, row.Eligible30, row.Returned30)
        ]);
    }

    public async Task<IReadOnlyList<AdminSourceRowDto>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        // Column names as the migrations made them: blogger and business profiles keep telegram_user_id in snake case.
        const string sql = """
            SELECT u."Source" AS "Source",
                   count(*)::int AS "Users",
                   count(*) FILTER (WHERE EXISTS (SELECT 1 FROM blogger_profiles p WHERE p.telegram_user_id = u."TelegramUserId")
                       OR EXISTS (SELECT 1 FROM business_profiles p WHERE p.telegram_user_id = u."TelegramUserId")
                       OR EXISTS (SELECT 1 FROM brand_face_profiles p WHERE p."TelegramUserId" = u."TelegramUserId"))::int AS "WithProfile",
                   count(*) FILTER (WHERE EXISTS (SELECT 1 FROM deals d JOIN blogger_profiles p ON p."Id" = d."BloggerId" WHERE p.telegram_user_id = u."TelegramUserId")
                       OR EXISTS (SELECT 1 FROM deals d JOIN brand_face_profiles p ON p."Id" = d."BrandFaceId" WHERE p."TelegramUserId" = u."TelegramUserId")
                       OR EXISTS (SELECT 1 FROM deals d JOIN business_profiles p ON p."Id" = d."BusinessId" WHERE p.telegram_user_id = u."TelegramUserId"))::int AS "WithDeal"
            FROM platform_users u
            GROUP BY u."Source"
            ORDER BY "Users" DESC, "Source" NULLS FIRST
            LIMIT 30
            """;
        var rows = await dbContext.Database.SqlQueryRaw<SourceRow>(sql).ToListAsync(cancellationToken);
        return rows.Select(row => new AdminSourceRowDto(row.Source, row.Users, row.WithProfile, row.WithDeal)).ToArray();
    }

    private async Task<Dictionary<DateOnly, int>> CountByDayAsync(string table, string column, string? condition, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var sql = $$"""
            SELECT (t."{{column}}" AT TIME ZONE {{Tashkent}})::date AS "Day", count(*)::int AS "Count"
            FROM {{table}} t
            WHERE t."{{column}}" >= {0} AND t."{{column}}" < {1}{{(condition is null ? "" : " AND " + condition)}}
            GROUP BY 1
            """;
        var rows = await dbContext.Database.SqlQueryRaw<DayCountRow>(sql, fromUtc, toUtc).ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.Day, row => row.Count);
    }

    private const int GeographyRows = 30;

    private async Task<IReadOnlyList<AdminGeographyRowDto>> GeoRowsAsync(string sql, CancellationToken cancellationToken)
    {
        // sql is one of the fixed queries written above (no request value is ever part of the text), so wrapping it is safe.
        var query = $"""SELECT * FROM ({sql}) g WHERE g."Key" IS NOT NULL AND g."Key" <> ''""";
        var rows = await dbContext.Database.SqlQueryRaw<GeoRow>(query).ToListAsync(cancellationToken);
        return rows.GroupBy(row => row.Key)
            .Select(group => new AdminGeographyRowDto(group.Key,
                group.Where(row => row.Metric == "bloggers").Sum(row => row.Count),
                group.Where(row => row.Metric == "brandFaces").Sum(row => row.Count),
                group.Where(row => row.Metric == "campaigns").Sum(row => row.Count),
                group.Where(row => row.Metric == "deals").Sum(row => row.Count)))
            .OrderByDescending(row => row.Deals).ThenByDescending(row => row.Bloggers + row.BrandFaces + row.Campaigns).ThenBy(row => row.Key, StringComparer.Ordinal)
            .Take(GeographyRows)
            .ToArray();
    }

    private sealed class DayCountRow { public DateOnly Day { get; set; } public int Count { get; set; } }
    private sealed class MoneyRow { public string Month { get; set; } = ""; public int Completed { get; set; } public int Priced { get; set; } public long Total { get; set; } }
    private sealed class GeoRow { public string Key { get; set; } = ""; public string Metric { get; set; } = ""; public int Count { get; set; } }
    private sealed class SourceRow { public string? Source { get; set; } public int Users { get; set; } public int WithProfile { get; set; } public int WithDeal { get; set; } }
    private sealed class RetentionRow
    {
        public int Cohort { get; set; }
        public int Eligible1 { get; set; }
        public int Returned1 { get; set; }
        public int Eligible7 { get; set; }
        public int Returned7 { get; set; }
        public int Eligible30 { get; set; }
        public int Returned30 { get; set; }
    }
}
