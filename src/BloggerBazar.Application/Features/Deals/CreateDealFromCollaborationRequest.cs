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
    IUnitOfWork unitOfWork) : IRequestHandler<CreateDealFromCollaborationRequestCommand, DealDto>
{
    public async Task<DealDto> Handle(CreateDealFromCollaborationRequestCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("Collaboration request was not found.");
        if (request.Blogger.TelegramUserId != command.TelegramUserId && request.Business.TelegramUserId != command.TelegramUserId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this request.");
        }
        if (await requests.ExistsDealAsync(request.Id, cancellationToken))
        {
            throw new InvalidOperationException("A deal already exists for this request.");
        }

        if (request.Status != Domain.Enums.CollaborationRequestStatus.Accepted)
        {
            request.Accept();
        }
        var deal = Domain.Entities.Deal.CreateFromCollaborationRequest(request.Id, request.BloggerId, request.BusinessId);
        await deals.AddAsync(deal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DealDto.From(deal);
    }
}
