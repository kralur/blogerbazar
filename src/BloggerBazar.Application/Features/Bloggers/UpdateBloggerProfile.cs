using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using BloggerBazar.Application.Validation;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Bloggers;

public sealed record UpdateBloggerProfileCommand(
    long TelegramUserId, string Name, string? LastName, string? Username, string City, IReadOnlyCollection<string> Categories,
    string? Bio, string? AvatarUrl, int TotalFollowers, int? AverageReach, decimal? EngagementRate, int? StoriesPrice,
    int? ReelsPrice, int? PostPrice, int? IntegrationPrice, bool BarterEnabled, string? Phone, string? Email,
    IReadOnlyCollection<PortfolioItemInput>? PortfolioItems,
    string? CoverUrl = null,
    int? Age = null,
    string? Gender = null,
    string? Language = null,
    string? Subcategory = null,
    int? PriceFrom = null,
    int? PriceTo = null,
    string? PriceNote = null,
    IReadOnlyCollection<SocialPlatformInput>? Platforms = null) : IRequest<BloggerProfileDto>;

public sealed class UpdateBloggerProfileValidator : AbstractValidator<UpdateBloggerProfileCommand>
{
    public UpdateBloggerProfileValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Username).NotEmpty().Must(ContactValidation.IsTelegramUsername);
        RuleFor(command => command.City).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Categories).NotEmpty().Must(categories => categories.Count <= 5);
        RuleForEach(command => command.Categories).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Bio).MaximumLength(500).When(command => command.Bio is not null);
        RuleFor(command => command.AvatarUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.AvatarUrl is not null);
        RuleFor(command => command.Phone).NotEmpty().Must(ContactValidation.IsUzbekPhone);
        RuleFor(command => command.Email).EmailAddress().MaximumLength(254).When(command => command.Email is not null);
        RuleFor(command => command.TotalFollowers).GreaterThan(0);
        RuleFor(command => command.AverageReach).NotNull().GreaterThan(0);
        RuleFor(command => command.EngagementRate).NotNull().InclusiveBetween(0.1m, 100m);
        RuleFor(command => command.StoriesPrice).NotNull().GreaterThan(0);
        RuleFor(command => command.ReelsPrice).NotNull().GreaterThan(0);
        RuleFor(command => command.PostPrice).GreaterThanOrEqualTo(0).When(command => command.PostPrice.HasValue);
        RuleFor(command => command.IntegrationPrice).GreaterThanOrEqualTo(0).When(command => command.IntegrationPrice.HasValue);
        RuleFor(command => command.PortfolioItems).Must(items => items is null || items.Count <= 12);
        RuleForEach(command => command.PortfolioItems!).ChildRules(item =>
        {
            item.RuleFor(value => value.Title).NotEmpty().MaximumLength(120);
            item.RuleFor(value => value.Type).IsInEnum();
            item.RuleFor(value => value.Url).Must(ContactValidation.IsHttpsUrl);
        });
        RuleForEach(command => command.Platforms!).ChildRules(item =>
        {
            item.RuleFor(value => value.Type).NotEmpty();
            item.RuleFor(value => value).Must(value => ContactValidation.IsSupportedPlatform(value.Type, value.Url));
        });
    }
}

public sealed class UpdateBloggerProfileHandler(IBloggerProfileRepository profiles, IPortfolioItemRepository portfolioItems, ISocialPlatformRepository platforms, IUnitOfWork unitOfWork, ICatalogCache? cache = null)
    : IRequestHandler<UpdateBloggerProfileCommand, BloggerProfileDto>
{
    public async Task<BloggerProfileDto> Handle(UpdateBloggerProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Create a blogger profile before updating it.");

        var usernameOwner = await profiles.GetByUsernameAsync(command.Username!.Trim(), cancellationToken);
        if (usernameOwner is not null && usernameOwner.Id != profile.Id)
        {
            throw new InvalidOperationException("This Telegram username is already used by another blogger profile.");
        }

        profile.UpdatePublicProfile(command.Name.Trim(), command.LastName?.Trim(), command.Username?.Trim(), command.City.Trim(),
            command.Categories.Select(category => category.Trim()).ToArray(), command.Bio?.Trim(), command.AvatarUrl, command.Phone?.Trim(), command.Email?.Trim(),
            command.TotalFollowers, command.AverageReach, command.EngagementRate, command.StoriesPrice, command.ReelsPrice,
            command.PostPrice, command.IntegrationPrice, command.BarterEnabled);
        profile.UpdateExtendedProfile(command.CoverUrl, command.Age, command.Gender?.Trim(), command.Language?.Trim(), command.Subcategory?.Trim(), command.PriceFrom, command.PriceTo, command.PriceNote?.Trim());
        profile.Approve();
        await portfolioItems.DeleteForBloggerAsync(profile.Id, cancellationToken);
        await portfolioItems.AddRangeAsync((command.PortfolioItems ?? []).Select(item => PortfolioItem.Create(profile.Id, item.Title.Trim(), item.Type, item.Url.Trim())), cancellationToken);
        await platforms.DeleteForBloggerAsync(profile.Id, cancellationToken);
        await platforms.AddRangeAsync((command.Platforms ?? []).Select(platform => SocialPlatform.Create(profile.Id, platform.Type.Trim(), platform.Url.Trim(), platform.Followers, platform.ScreenshotUrl?.Trim())), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        return BloggerProfileDto.From(profile);
    }
}
