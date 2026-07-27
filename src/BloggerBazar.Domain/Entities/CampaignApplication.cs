using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class CampaignApplication
{
    private CampaignApplication() { }

    private CampaignApplication(Guid campaignId, Guid bloggerId, string? message)
    {
        Id = Guid.NewGuid();
        CampaignId = campaignId;
        BloggerId = bloggerId;
        Message = message;
        Status = CampaignApplicationStatus.Sent;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Campaign Campaign { get; private set; } = null!;
    public Guid BloggerId { get; private set; }
    public BloggerProfile Blogger { get; private set; } = null!;
    public string? Message { get; private set; }
    public CampaignApplicationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Deal? Deal { get; private set; }

    public static CampaignApplication Create(Guid campaignId, Guid bloggerId, string? message) => new(campaignId, bloggerId, message);

    public void Accept() => Status = CampaignApplicationStatus.Accepted;
    public void MarkViewed()
    {
        if (Status == CampaignApplicationStatus.Sent)
        {
            Status = CampaignApplicationStatus.Viewed;
        }
    }
    public void Reject() => Status = CampaignApplicationStatus.Rejected;
    public void Withdraw() => Status = CampaignApplicationStatus.Withdrawn;
}
