using BloggerBazar.Application.Features.Campaigns;

namespace BloggerBazar.Application.Tests.Features.Campaigns;

public sealed class CreateCampaignValidatorTests
{
    [Fact]
    public void Rejects_budget_range_with_lower_upper_limit()
    {
        var validator = new CreateCampaignValidator();
        var result = validator.Validate(new CreateCampaignCommand(1, "Campaign", "Description", null, ["Lifestyle"], null, 1000000, 500000, null, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "BudgetTo");
    }

    [Fact]
    public void Rejects_a_deadline_that_has_already_passed_but_allows_today()
    {
        var validator = new CreateCampaignValidator();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today = DateTime.UtcNow.Date;

        Assert.Contains(validator.Validate(new CreateCampaignCommand(1, "Campaign", "Description", null, ["Lifestyle"], null, null, null, yesterday, true)).Errors, error => error.PropertyName == "Deadline");
        Assert.True(validator.Validate(new CreateCampaignCommand(1, "Campaign", "Description", null, ["Lifestyle"], null, null, null, today, true)).IsValid);
    }
}
