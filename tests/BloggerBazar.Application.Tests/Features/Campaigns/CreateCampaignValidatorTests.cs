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
}
