using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Wallet;

public sealed record WalletDto(int Balance);

public sealed record GetWalletQuery(long TelegramUserId) : IRequest<WalletDto>;

public sealed class GetWalletHandler(ICreditAccountRepository accounts, IUnitOfWork unitOfWork) : IRequestHandler<GetWalletQuery, WalletDto>
{
    public async Task<WalletDto> Handle(GetWalletQuery query, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByTelegramUserIdAsync(query.TelegramUserId, cancellationToken);
        if (account is null)
        {
            account = CreditAccount.Create(query.TelegramUserId);
            await accounts.AddAsync(account, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new WalletDto(account.Balance);
    }
}

public sealed record GrantCreditsCommand(long AdministratorTelegramUserId, long RecipientTelegramUserId, int Amount, CreditSource Source, string? Note) : IRequest<WalletDto>;

public sealed class GrantCreditsValidator : AbstractValidator<GrantCreditsCommand>
{
    public GrantCreditsValidator()
    {
        RuleFor(command => command.AdministratorTelegramUserId).GreaterThan(0);
        RuleFor(command => command.RecipientTelegramUserId).GreaterThan(0);
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Note).MaximumLength(200);
    }
}

public sealed class GrantCreditsHandler(
    ICreditAccountRepository accounts,
    Application.Abstractions.Security.IAdminAccessPolicy adminAccess,
    IUnitOfWork unitOfWork) : IRequestHandler<GrantCreditsCommand, WalletDto>
{
    public async Task<WalletDto> Handle(GrantCreditsCommand command, CancellationToken cancellationToken)
    {
        adminAccess.EnsureAllowed(command.AdministratorTelegramUserId);
        var account = await accounts.GetByTelegramUserIdAsync(command.RecipientTelegramUserId, cancellationToken);
        if (account is null)
        {
            account = CreditAccount.Create(command.RecipientTelegramUserId);
            await accounts.AddAsync(account, cancellationToken);
        }

        account.Apply(command.Amount);
        await accounts.AddEntryAsync(CreditLedgerEntry.Create(command.RecipientTelegramUserId, command.Amount, command.Source, command.Note), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new WalletDto(account.Balance);
    }
}

public sealed record UnlockContactWithCreditsCommand(long TelegramUserId, ContactTargetType TargetType, Guid TargetId) : IRequest<bool>;

public sealed class UnlockContactWithCreditsValidator : AbstractValidator<UnlockContactWithCreditsCommand>
{
    public UnlockContactWithCreditsValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.TargetType).IsInEnum();
        RuleFor(command => command.TargetId).NotEmpty();
    }
}

public sealed class UnlockContactWithCreditsHandler(
    ICreditAccountRepository accounts,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IContactUnlockRepository unlocks,
    IPaymentOrderRepository payments,
    IUnitOfWork unitOfWork) : IRequestHandler<UnlockContactWithCreditsCommand, bool>
{
    public async Task<bool> Handle(UnlockContactWithCreditsCommand command, CancellationToken cancellationToken)
    {
        var targetTelegramUserId = command.TargetType switch
        {
            ContactTargetType.Blogger => (await bloggers.GetByIdAsync(command.TargetId, cancellationToken))?.TelegramUserId,
            ContactTargetType.Business => (await businesses.GetByIdAsync(command.TargetId, cancellationToken))?.TelegramUserId,
            _ => null
        };
        if (targetTelegramUserId is null)
        {
            throw new InvalidOperationException("Contact target was not found.");
        }
        if (targetTelegramUserId == command.TelegramUserId)
        {
            throw new InvalidOperationException("You cannot unlock your own contact.");
        }
        if (await unlocks.GetAsync(command.TelegramUserId, command.TargetType, command.TargetId, cancellationToken) is not null)
        {
            return false;
        }

        var account = await accounts.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Credit account was not found.");
        account.Apply(-1);
        await accounts.AddEntryAsync(CreditLedgerEntry.Create(command.TelegramUserId, -1, CreditSource.ContactUnlock, $"Unlock {command.TargetType}:{command.TargetId}"), cancellationToken);
        var order = PaymentOrder.CreateContactUnlockWithCredits(command.TelegramUserId, command.TargetType, command.TargetId);
        await payments.AddAsync(order, cancellationToken);
        await unlocks.AddAsync(ContactUnlock.Create(order), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
