using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Features.Deals;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Reviews;

public sealed record CreateReviewCommand(Guid DealId, long TelegramUserId, int Rating, string? Comment) : IRequest<ReviewDto>;

// The reviewer is the other side of the deal: a business reviews a blogger, a blogger reviews a business.
public sealed record ReviewDto(Guid Id, Guid DealId, int TargetType, int Rating, string? Comment, string? ReviewerName, DateTime CreatedAtUtc, Guid? ReviewerProfileId = null, string? ReviewerImageUrl = null, bool ReviewerDeleted = false)
{
    public static ReviewDto From(Review review)
    {
        var reviewerName = review.TargetType switch
        {
            ReviewTargetType.Blogger => review.Deal?.Business?.Name,
            ReviewTargetType.Business => review.Deal?.Blogger?.Name,
            _ => null
        };

        var (reviewerId, reviewerImage) = review.TargetType switch
        {
            ReviewTargetType.Blogger => ((Guid?)review.Deal?.BusinessId, review.Deal?.Business?.LogoUrl),
            ReviewTargetType.Business => ((Guid?)review.Deal?.BloggerId, review.Deal?.Blogger?.AvatarUrl),
            _ => ((Guid?)null, (string?)null)
        };

        return new(review.Id, review.DealId, (int)review.TargetType, review.Rating, review.Comment, reviewerName, review.CreatedAtUtc, reviewerId, reviewerImage);
    }
}

public sealed class CreateReviewValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewValidator()
    {
        RuleFor(command => command.DealId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Rating).InclusiveBetween(1, 5);
        RuleFor(command => command.Comment).MaximumLength(1000).When(command => command.Comment is not null);
    }
}

public sealed class CreateReviewHandler(
    IDealRepository deals,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<CreateReviewHandler>? logger = null) : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken cancellationToken)
    {
        var participant = await DealAccess.FindDealParticipantAsync(users, bloggers, businesses, command.TelegramUserId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        var deal = await deals.GetForParticipantAsync(command.DealId, participant.Role, participant.ProfileId, cancellationToken)
            ?? throw DealAccess.DealNotFound();
        if (deal.Status != DealStatus.Completed)
        {
            throw new InvalidOperationException("Reviews are available only after a deal is completed.");
        }

        var nowUtc = DateTime.UtcNow;
        if (!ReviewWindow.IsOpen(deal.CompletedAtUtc, nowUtc))
        {
            throw new InvalidOperationException("The review window for this deal has closed.");
        }

        if (await reviews.ExistsAsync(deal.Id, command.TelegramUserId, cancellationToken))
        {
            throw AlreadyReviewed();
        }

        var reviewerIsBlogger = participant.Role == MarketplaceRole.Blogger;
        var comment = command.Comment?.Trim();
        var review = reviewerIsBlogger
            ? Review.ForBusiness(deal.Id, command.TelegramUserId, deal.BusinessId, command.Rating, comment)
            : Review.ForBlogger(deal.Id, command.TelegramUserId, deal.BloggerId, command.Rating, comment);

        await reviews.AddAsync(review, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            throw AlreadyReviewed();
        }

        // Blind reviews: this one stays hidden until the partner reviews too or the window ends.
        var revealed = await reviews.PublishRevealedAsync(deal.Id, nowUtc, cancellationToken) > 0;
        var targetChatId = reviewerIsBlogger ? deal.Business.TelegramUserId : deal.Blogger.TelegramUserId;
        var reviewerName = reviewerIsBlogger ? deal.Blogger.Name : deal.Business.Name;
        var text = revealed
            ? BotMessages.ReviewsPublished(reviewerName, DealTopic.Of(deal))
            : BotMessages.PartnerReviewed(reviewerName, DealTopic.Of(deal));
        await BestEffortTelegramNotification.SendAsync(botClient, logger, targetChatId, text, $"/deal/{deal.Id}", cancellationToken);
        return ReviewDto.From(review) with { ReviewerName = reviewerIsBlogger ? deal.Blogger.Name : deal.Business.Name };
    }

    private static InvalidOperationException AlreadyReviewed() => new("You have already reviewed this deal.");
}
