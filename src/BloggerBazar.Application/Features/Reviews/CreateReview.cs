using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Telegram;
using BloggerBazar.Application.Notifications;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Reviews;

public sealed record CreateReviewCommand(Guid DealId, long TelegramUserId, int Rating, string? Comment) : IRequest<ReviewDto>;

public sealed record ReviewDto(Guid Id, Guid DealId, int TargetType, int Rating, string? Comment, string? ReviewerName, DateTime CreatedAtUtc)
{
    public static ReviewDto From(Review review)
    {
        var reviewerName = review.TargetType switch
        {
            ReviewTargetType.Blogger => review.Deal?.Business?.Name,
            ReviewTargetType.Business => review.Deal?.Blogger?.Name,
            _ => null
        };

        return new(review.Id, review.DealId, (int)review.TargetType, review.Rating, review.Comment, reviewerName, review.CreatedAtUtc);
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
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    ITelegramBotClient? botClient = null,
    ILogger<CreateReviewHandler>? logger = null) : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken cancellationToken)
    {
        var deal = await deals.GetByIdAsync(command.DealId, cancellationToken) ?? throw new InvalidOperationException("Deal was not found.");
        if (deal.Status != DealStatus.Completed)
        {
            throw new InvalidOperationException("Reviews are available only after a deal is completed.");
        }

        if (await reviews.ExistsAsync(deal.Id, command.TelegramUserId, cancellationToken))
        {
            throw new InvalidOperationException("You have already reviewed this deal.");
        }

        var reviewerBlogger = await bloggers.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        var reviewerBusiness = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        Review review;

        if (reviewerBlogger?.Id == deal.BloggerId)
        {
            review = Review.ForBusiness(deal.Id, command.TelegramUserId, deal.BusinessId, command.Rating, command.Comment?.Trim());
        }
        else if (reviewerBusiness?.Id == deal.BusinessId)
        {
            review = Review.ForBlogger(deal.Id, command.TelegramUserId, deal.BloggerId, command.Rating, command.Comment?.Trim());
        }
        else
        {
            throw new UnauthorizedAccessException("You are not a participant in this deal.");
        }

        await reviews.AddAsync(review, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var targetChatId = review.TargetType == ReviewTargetType.Blogger ? (await bloggers.GetByIdAsync(deal.BloggerId, cancellationToken))?.TelegramUserId : (await businesses.GetByIdAsync(deal.BusinessId, cancellationToken))?.TelegramUserId;
        if (targetChatId.HasValue) await BestEffortTelegramNotification.SendAsync(botClient, logger, targetChatId.Value, "BloggerBazar: вы получили новый отзыв о сотрудничестве.", cancellationToken);
        return ReviewDto.From(review);
    }
}
