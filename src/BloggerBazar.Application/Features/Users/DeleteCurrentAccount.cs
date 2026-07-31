using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BloggerBazar.Application.Features.Users;

public sealed record DeleteCurrentAccountCommand(long TelegramUserId, string CorrelationId) : IRequest<AccountDeletionResultDto>;

public sealed record AccountDeletionResultDto(bool AlreadyDeleted);

public sealed class DeleteCurrentAccountHandler(
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBrandFaceProfileRepository brandFaces,
    IBusinessProfileRepository businesses,
    IFavoriteRepository favorites,
    IAuditLogRepository auditLogs,
    IUnitOfWork unitOfWork,
    ICatalogCache cache,
    ILogger<DeleteCurrentAccountHandler> logger) : IRequestHandler<DeleteCurrentAccountCommand, AccountDeletionResultDto>
{
    public async Task<AccountDeletionResultDto> Handle(DeleteCurrentAccountCommand command, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var user = await users.GetByTelegramUserIdAsync(command.TelegramUserId, transactionCancellationToken)
                ?? throw new InvalidOperationException("The platform user was not found.");

            if (user.IsBlocked)
            {
                throw new UnauthorizedAccessException("This Telegram account is not allowed to access the marketplace.");
            }

            if (user.IsDeleted || !await users.SoftDeleteIfActiveAsync(command.TelegramUserId, command.TelegramUserId, transactionCancellationToken))
            {
                logger.LogInformation("An account deletion request was already processed.");
                return new AccountDeletionResultDto(true);
            }

            var blogger = await bloggers.GetIncludingDeletedByTelegramUserIdAsync(command.TelegramUserId, transactionCancellationToken);
            var brandFace = await brandFaces.GetIncludingDeletedByTelegramUserIdAsync(command.TelegramUserId, transactionCancellationToken);
            var business = await businesses.GetIncludingDeletedByTelegramUserIdAsync(command.TelegramUserId, transactionCancellationToken);
            blogger?.SoftDelete();
            brandFace?.SoftDelete();
            business?.SoftDelete();
            await favorites.DeleteForPlatformUserAsync(user.Id, transactionCancellationToken);
            if (blogger is not null)
            {
                await favorites.DeleteForBloggerAsync(blogger.Id, transactionCancellationToken);
            }

            await auditLogs.AddAsync(BloggerBazar.Domain.Entities.AuditLog.Create(
                command.TelegramUserId,
                "platform-user.account-deleted",
                "PlatformUser",
                user.Id.ToString(),
                correlationId: command.CorrelationId), transactionCancellationToken);
            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
            return new AccountDeletionResultDto(false);
        }, cancellationToken);

        if (!result.AlreadyDeleted)
        {
            try
            {
                await cache.RotateNamespaceVersionAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The public catalog cache could not be invalidated after account deletion.");
            }
        }

        return result;
    }
}
