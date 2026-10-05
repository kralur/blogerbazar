using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Deals;

public sealed record CreateDealFromCollaborationRequestCommand(Guid RequestId, long TelegramUserId) : IRequest<DealDto>;

public sealed class CreateDealFromCollaborationRequestValidator : AbstractValidator<CreateDealFromCollaborationRequestCommand>
{
    public CreateDealFromCollaborationRequestValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
    }
}

public sealed class CreateDealFromCollaborationRequestHandler(
    ICollaborationRequestRepository requests,
    IDealRepository deals,
    IPlatformUserRepository users,
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateDealFromCollaborationRequestCommand, DealDto>
{
    public async Task<DealDto> Handle(CreateDealFromCollaborationRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("Collaboration request was not found.");
        await DealAccess.RequireCollaborationParticipantAsync(users, bloggers, businesses, request, command.TelegramUserId, cancellationToken);
        var existingDeal = await deals.GetByCollaborationRequestIdAsync(request.Id, cancellationToken);
        if (existingDeal is not null)
        {
            return DealDto.From(existingDeal);
        }

        if (request.Status != Domain.Enums.CollaborationRequestStatus.Accepted)
        {
            request.Accept();
        }
        var deal = Domain.Entities.Deal.CreateFromCollaborationRequest(request.Id, request.BloggerId, request.BusinessId);
        await deals.AddAsync(deal, cancellationToken);
        if (!await unitOfWork.TrySaveChangesAsync(cancellationToken))
        {
            var persistedDeal = await deals.GetByCollaborationRequestIdAsync(request.Id, cancellationToken);
            if (persistedDeal is not null)
            {
                return DealDto.From(persistedDeal);
            }

            throw new InvalidOperationException("Collaboration request deal creation conflicts with an existing deal.");
        }
        return DealDto.From(deal);
    }
}
