using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Domain.Entities;

public sealed class PortfolioItem
{
    private PortfolioItem() { }

    private PortfolioItem(Guid bloggerId, string title, PortfolioItemType type, string url)
    {
        Id = Guid.NewGuid();
        BloggerId = bloggerId;
        Title = title;
        Type = type;
        Url = url;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid BloggerId { get; private set; }
    public BloggerProfile Blogger { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public PortfolioItemType Type { get; private set; }
    public string Url { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public static PortfolioItem Create(Guid bloggerId, string title, PortfolioItemType type, string url) =>
        new(bloggerId, title, type, url);
}
