using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Features.Admin;
using BloggerBazar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BloggerBazar.Infrastructure.Persistence;

// D59: fixed SQL (no request value is ever part of the text; values go in as {n} parameters). Column names as the
// migrations made them: blogger and business profiles keep telegram_user_id in snake case. Regular expressions here
// contain no braces, because SqlQueryRaw reads braces as parameter placeholders.
internal sealed class AdminSignalsReadModel(BloggerBazarDbContext dbContext) : IAdminSignalsReadModel
{
    // Rows per list before grouping: a group has at least two people, so this covers MaxRows groups comfortably.
    private const int RowsPerGroup = 10;

    public async Task<IReadOnlyList<AdminDuplicateHandleDto>> GetDuplicateHandlesAsync(int take, CancellationToken cancellationToken)
    {
        // A social link written differently is still the same account: no scheme, "www."/"m.", query or trailing
        // slash, lower case. A brand face's Instagram may be "@name", "name" or a link.
        const string sql = """
            WITH handles AS (
                SELECT lower(regexp_replace(regexp_replace(regexp_replace(regexp_replace(trim(sp."Url"), '^https?://', '', 'i'), '^(www|m)\.', '', 'i'), '[?#].*$', ''), '/+$', '')) AS "Handle",
                       p.telegram_user_id AS "TelegramUserId", p."Name" AS "Name"
                FROM social_platforms sp
                JOIN blogger_profiles p ON p."Id" = sp."BloggerId"
                WHERE NOT p."IsDeleted"
                UNION
                SELECT 'instagram.com/' || lower(regexp_replace(regexp_replace(regexp_replace(trim(b."Instagram"), '^https?://(www\.)?instagram\.com/', '', 'i'), '^@', ''), '[/?#].*$', '')),
                       b."TelegramUserId", b."Name"
                FROM brand_face_profiles b
                WHERE NOT b."IsDeleted" AND b."Instagram" IS NOT NULL AND trim(b."Instagram") <> ''
            ),
            shared AS (
                SELECT "Handle" FROM handles
                WHERE "Handle" ~ '/.'
                GROUP BY "Handle"
                HAVING count(DISTINCT "TelegramUserId") > 1
                ORDER BY count(DISTINCT "TelegramUserId") DESC, "Handle"
                LIMIT {0}
            )
            SELECT DISTINCT h."Handle" AS "Key", h."TelegramUserId" AS "TelegramUserId", h."Name" AS "Name"
            FROM handles h JOIN shared s ON s."Handle" = h."Handle"
            ORDER BY 1, 2
            LIMIT {1}
            """;
        var rows = await dbContext.Database.SqlQueryRaw<PersonRow>(sql, take, take * RowsPerGroup).ToListAsync(cancellationToken);
        return Group(rows).Select(group => new AdminDuplicateHandleDto(group.Key, group.People)).ToArray();
    }

    public async Task<IReadOnlyList<AdminSharedPhoneDto>> GetSharedPhonesAsync(int take, CancellationToken cancellationToken)
    {
        // One phone (from Telegram's own contact card, D41) on several accounts: the same person with several accounts.
        const string sql = """
            WITH phones AS (
                SELECT regexp_replace(u."VerifiedPhone", '[^0-9]', '', 'g') AS "Phone", u."TelegramUserId", u."FirstName"
                FROM platform_users u
                WHERE u."VerifiedPhone" IS NOT NULL AND NOT u."IsDeleted"
            ),
            shared AS (
                SELECT "Phone" FROM phones
                WHERE "Phone" <> ''
                GROUP BY "Phone"
                HAVING count(*) > 1
                ORDER BY count(*) DESC, "Phone"
                LIMIT {0}
            )
            SELECT p."Phone" AS "Key", p."TelegramUserId" AS "TelegramUserId", p."FirstName" AS "Name"
            FROM phones p JOIN shared s ON s."Phone" = p."Phone"
            ORDER BY 1, 2
            LIMIT {1}
            """;
        var rows = await dbContext.Database.SqlQueryRaw<PersonRow>(sql, take, take * RowsPerGroup).ToListAsync(cancellationToken);
        return Group(rows).Select(group => new AdminSharedPhoneDto("+" + group.Key, group.People)).ToArray();
    }

    public async Task<IReadOnlyList<AdminQuickDealDto>> GetQuickDealsAsync(DateTime sinceUtc, int maxMinutes, int take, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT d."Id" AS "DealId", d."CampaignTitleSnapshot" AS "Title",
                   b.telegram_user_id AS "BusinessTelegramUserId", b."Name" AS "BusinessName",
                   coalesce(bp.telegram_user_id, bf."TelegramUserId") AS "CreatorTelegramUserId", coalesce(bp."Name", bf."Name") AS "CreatorName",
                   floor(extract(epoch FROM d."CompletedAtUtc" - d."CreatedAtUtc") / 60)::int AS "Minutes",
                   (SELECT count(*) FROM reviews r WHERE r."DealId" = d."Id")::int AS "Reviews",
                   d."CompletedAtUtc" AS "CompletedAtUtc"
            FROM deals d
            JOIN business_profiles b ON b."Id" = d."BusinessId"
            LEFT JOIN blogger_profiles bp ON bp."Id" = d."BloggerId"
            LEFT JOIN brand_face_profiles bf ON bf."Id" = d."BrandFaceId"
            WHERE d."Status" = {0}
              AND d."CompletedAtUtc" >= {1}
              AND d."CompletedAtUtc" - d."CreatedAtUtc" < make_interval(mins => {2})
              AND EXISTS (SELECT 1 FROM reviews r WHERE r."DealId" = d."Id")
            ORDER BY d."CompletedAtUtc" DESC
            LIMIT {3}
            """;
        var rows = await dbContext.Database.SqlQueryRaw<QuickDealRow>(sql, (int)DealStatus.Completed, sinceUtc, maxMinutes, take).ToListAsync(cancellationToken);
        return rows.Select(row => new AdminQuickDealDto(row.DealId, row.Title,
            new AdminSignalPersonDto(row.BusinessTelegramUserId, row.BusinessName),
            new AdminSignalPersonDto(row.CreatorTelegramUserId ?? 0, row.CreatorName ?? ""),
            row.Minutes, row.Reviews, row.CompletedAtUtc)).ToArray();
    }

    public async Task<IReadOnlyList<AdminComplaintTargetDto>> GetComplaintTargetsAsync(DateTime sinceUtc, int minReporters, int take, CancellationToken cancellationToken)
    {
        // Complaints about one profile or campaign from several different people; offers and deals have no single owner.
        const string sql = """
            SELECT c."TargetType" AS "TargetType", c."TargetId" AS "TargetId",
                   count(DISTINCT c."ReporterTelegramUserId")::int AS "Reporters",
                   max(c."CreatedAtUtc") AS "LastAtUtc",
                   CASE c."TargetType"
                       WHEN 'blogger' THEN (SELECT p."Name" FROM blogger_profiles p WHERE p."Id" = c."TargetId")
                       WHEN 'brandFace' THEN (SELECT p."Name" FROM brand_face_profiles p WHERE p."Id" = c."TargetId")
                       WHEN 'business' THEN (SELECT p."Name" FROM business_profiles p WHERE p."Id" = c."TargetId")
                       WHEN 'campaign' THEN (SELECT x."Title" FROM campaigns x WHERE x."Id" = c."TargetId")
                   END AS "TargetName",
                   CASE c."TargetType"
                       WHEN 'blogger' THEN (SELECT p.telegram_user_id FROM blogger_profiles p WHERE p."Id" = c."TargetId")
                       WHEN 'brandFace' THEN (SELECT p."TelegramUserId" FROM brand_face_profiles p WHERE p."Id" = c."TargetId")
                       WHEN 'business' THEN (SELECT p.telegram_user_id FROM business_profiles p WHERE p."Id" = c."TargetId")
                       WHEN 'campaign' THEN (SELECT p.telegram_user_id FROM campaigns x JOIN business_profiles p ON p."Id" = x."BusinessId" WHERE x."Id" = c."TargetId")
                   END AS "OwnerTelegramUserId"
            FROM complaints c
            WHERE c."CreatedAtUtc" >= {0}
            GROUP BY c."TargetType", c."TargetId"
            HAVING count(DISTINCT c."ReporterTelegramUserId") >= {1}
            ORDER BY "Reporters" DESC, "LastAtUtc" DESC
            LIMIT {2}
            """;
        var rows = await dbContext.Database.SqlQueryRaw<ComplaintTargetRow>(sql, sinceUtc, minReporters, take).ToListAsync(cancellationToken);
        return rows.Select(row => new AdminComplaintTargetDto(row.TargetType, row.TargetId, row.TargetName, row.OwnerTelegramUserId, row.Reporters, row.LastAtUtc)).ToArray();
    }

    public async Task<IReadOnlyList<AdminSignupBurstDto>> GetSignupBurstsAsync(DateTime sinceUtc, int minAccounts, int take, CancellationToken cancellationToken)
    {
        // Many new accounts from one ad label in one hour; the profile share tells real interest from scripted sign-ups.
        const string sql = """
            SELECT u."Source" AS "Source", date_trunc('hour', u."CreatedAtUtc" AT TIME ZONE 'UTC') AS "HourUtc",
                   count(*)::int AS "Accounts",
                   count(*) FILTER (WHERE EXISTS (SELECT 1 FROM blogger_profiles p WHERE p.telegram_user_id = u."TelegramUserId")
                       OR EXISTS (SELECT 1 FROM business_profiles p WHERE p.telegram_user_id = u."TelegramUserId")
                       OR EXISTS (SELECT 1 FROM brand_face_profiles p WHERE p."TelegramUserId" = u."TelegramUserId"))::int AS "WithProfile"
            FROM platform_users u
            WHERE u."Source" IS NOT NULL AND u."CreatedAtUtc" >= {0}
            GROUP BY 1, 2
            HAVING count(*) >= {1}
            ORDER BY 2 DESC, 3 DESC
            LIMIT {2}
            """;
        var rows = await dbContext.Database.SqlQueryRaw<BurstRow>(sql, sinceUtc, minAccounts, take).ToListAsync(cancellationToken);
        return rows.Select(row => new AdminSignupBurstDto(row.Source, DateTime.SpecifyKind(row.HourUtc, DateTimeKind.Utc), row.Accounts, row.WithProfile, false)).ToArray();
    }

    private static IEnumerable<(string Key, IReadOnlyList<AdminSignalPersonDto> People)> Group(IEnumerable<PersonRow> rows) =>
        rows.GroupBy(row => row.Key, StringComparer.Ordinal)
            .Where(group => group.Select(row => row.TelegramUserId).Distinct().Count() > 1)
            .Select(group => (group.Key, (IReadOnlyList<AdminSignalPersonDto>)group
                .GroupBy(row => row.TelegramUserId).Select(person => new AdminSignalPersonDto(person.Key, person.First().Name)).ToArray()));

    private sealed class PersonRow { public string Key { get; set; } = ""; public long TelegramUserId { get; set; } public string Name { get; set; } = ""; }
    private sealed class QuickDealRow
    {
        public Guid DealId { get; set; }
        public string? Title { get; set; }
        public long BusinessTelegramUserId { get; set; }
        public string BusinessName { get; set; } = "";
        public long? CreatorTelegramUserId { get; set; }
        public string? CreatorName { get; set; }
        public int Minutes { get; set; }
        public int Reviews { get; set; }
        public DateTime CompletedAtUtc { get; set; }
    }
    private sealed class ComplaintTargetRow
    {
        public string TargetType { get; set; } = "";
        public Guid TargetId { get; set; }
        public int Reporters { get; set; }
        public DateTime LastAtUtc { get; set; }
        public string? TargetName { get; set; }
        public long? OwnerTelegramUserId { get; set; }
    }
    private sealed class BurstRow { public string Source { get; set; } = ""; public DateTime HourUtc { get; set; } public int Accounts { get; set; } public int WithProfile { get; set; } }
}
