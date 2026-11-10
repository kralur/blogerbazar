using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BloggerBazar.Application.Features.Team;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BloggerBazar.Application.Tests.Integration;

// D64 stage C1 end to end on a real database: the owner of a company "with a team" makes a link, a person opens it and
// asks, the owner sees their confirmed number and lets them in; the person then works for the company until removed.
[Collection(ApiHostCollection.Name)]
public sealed class TeamIntegrationTests(BloggerBazarApiFactory factory) : IClassFixture<BloggerBazarApiFactory>
{
    private const string JoinPrefix = "https://t.me/BloggerBazarIntegrationBot?startapp=join_";

    [IntegrationFact]
    public async Task A_person_joins_the_team_with_a_link_and_works_until_removed()
    {
        const long owner = 1_500_001;
        const long newcomer = 1_500_002;
        const long late = 1_500_003;
        var companyId = await SeedCompanyAsync(owner, "Team Coffee", teamMode: true);
        await SeedUserAsync(newcomer, "+998901230001");
        await SeedUserAsync(late, "+998901230002");
        using var ownerApp = factory.CreateClientFor(owner);
        using var newcomerApp = factory.CreateClientFor(newcomer);
        using var lateApp = factory.CreateClientFor(late);

        var invitation = await ownerApp.PostAsJsonAsync("/api/team/invitations", new { role = TeamRoles.Manager });
        Assert.Equal(HttpStatusCode.OK, invitation.StatusCode);
        var link = (await invitation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("link").GetString()!;
        Assert.StartsWith(JoinPrefix, link, StringComparison.Ordinal);
        var code = link[JoinPrefix.Length..];
        // Only a hash of the code is kept.
        Assert.False(await AnyInvitationWithCodeHashAsync(code));

        var preview = await (await newcomerApp.PostAsJsonAsync("/api/team/invitations/preview", new { code })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Team Coffee", preview.GetProperty("companyName").GetString());
        Assert.Equal(TeamInvitationStates.Open, preview.GetProperty("state").GetString());
        var asked = await newcomerApp.PostAsJsonAsync("/api/team/invitations/request", new { code });
        Assert.Equal(TeamInvitationStates.RequestedByYou, (await asked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString());
        // The link belongs to the first person who asked.
        var lateAsk = await lateApp.PostAsJsonAsync("/api/team/invitations/request", new { code });
        Assert.Equal(HttpStatusCode.Conflict, lateAsk.StatusCode);
        Assert.Equal(TeamCodes.InvitationUsed, (await lateAsk.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var team = await ownerApp.GetFromJsonAsync<JsonElement>("/api/team");
        var request = team.GetProperty("requests")[0];
        Assert.Equal("+998901230001", request.GetProperty("phone").GetString());
        var accepted = await ownerApp.PostAsync($"/api/team/requests/{request.GetProperty("id").GetGuid()}/accept", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(2, (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("seatsUsed").GetInt32());

        var company = await newcomerApp.GetFromJsonAsync<JsonElement>("/api/businesses/me");
        Assert.Equal(companyId, company.GetProperty("id").GetGuid());
        Assert.True(company.GetProperty("teamMode").GetBoolean());
        Assert.Equal(TeamRoles.Manager, company.GetProperty("teamRole").GetString());
        var newcomerTeam = await newcomerApp.GetFromJsonAsync<JsonElement>("/api/team");
        Assert.False(newcomerTeam.GetProperty("canManage").GetBoolean());

        // Back to "alone" only without the team.
        var alone = await ownerApp.PutAsJsonAsync("/api/team/mode", new { teamMode = false });
        Assert.Equal(HttpStatusCode.Conflict, alone.StatusCode);
        Assert.Equal(TeamCodes.TeamNotEmpty, (await alone.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        // So is deleting the owner's account.
        var deletion = await ownerApp.DeleteAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Conflict, deletion.StatusCode);

        var memberId = newcomerTeam.GetProperty("members").EnumerateArray().Single(member => member.GetProperty("isMe").GetBoolean()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await ownerApp.DeleteAsync($"/api/team/members/{memberId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await newcomerApp.GetAsync("/api/businesses/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await newcomerApp.GetAsync("/api/team")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ownerApp.PutAsJsonAsync("/api/team/mode", new { teamMode = false })).StatusCode);
    }

    private async Task SeedUserAsync(long telegramUserId, string? phone, MarketplaceRole? role = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var user = PlatformUser.Create(telegramUserId, "Team", null);
        if (role is { } selected) user.SelectMarketplaceRole(selected);
        if (phone is not null) user.VerifyPhone(phone, DateTime.UtcNow);
        dbContext.Add(user);
        await dbContext.SaveChangesAsync();
    }

    // The owner joins the team automatically when the company is saved (stage A).
    private async Task<Guid> SeedCompanyAsync(long owner, string name, bool teamMode)
    {
        await SeedUserAsync(owner, "+998901239999", MarketplaceRole.Business);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        var company = BusinessProfile.Create(owner, name, "tashkent");
        company.Approve(verified: false);
        company.SetTeamMode(teamMode);
        dbContext.Add(company);
        await dbContext.SaveChangesAsync();
        return company.Id;
    }

    private async Task<bool> AnyInvitationWithCodeHashAsync(string code)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BloggerBazarDbContext>();
        return await dbContext.BusinessInvitations.AnyAsync(invitation => invitation.CodeHash == code);
    }
}
