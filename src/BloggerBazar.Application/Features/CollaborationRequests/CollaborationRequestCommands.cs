using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Domain.Enums;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.CollaborationRequests;

public sealed record CreateCollaborationRequestCommand(Guid BloggerId, long TelegramUserId, string Message) : IRequest<CollaborationRequestDto>;

public sealed class CreateCollaborationRequestValidator : AbstractValidator<CreateCollaborationRequestCommand>
{
    public CreateCollaborationRequestValidator()
    {
        RuleFor(command => command.BloggerId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Message).NotEmpty().MaximumLength(1000);
    }
}

public sealed class CreateCollaborationRequestHandler(
    IBloggerProfileRepository bloggers,
    IBusinessProfileRepository businesses,
    ICollaborationRequestRepository requests,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateCollaborationRequestCommand, CollaborationRequestDto>
{
    public async Task<CollaborationRequestDto> Handle(CreateCollaborationRequestCommand command, CancellationToken cancellationToken)
    {
        var blogger = await bloggers.GetByIdAsync(command.BloggerId, cancellationToken)
            ?? throw new InvalidOperationException("Blogger profile was not found.");
        var business = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Business profile is required.");
        if (business.TelegramUserId == blogger.TelegramUserId)
        {
            throw new InvalidOperationException("You cannot send a request to your own profile.");
        }

        var request = CollaborationRequest.Create(blogger.Id, business.Id, command.Message.Trim());
        await requests.AddAsync(request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CollaborationRequestDto.From(request, blogger.Name, business.Name);
    }
}

public sealed record UpdateCollaborationRequestStatusCommand(Guid RequestId, long TelegramUserId, CollaborationRequestStatus Status) : IRequest<CollaborationRequestDto>;

public sealed class UpdateCollaborationRequestStatusValidator : AbstractValidator<UpdateCollaborationRequestStatusCommand>
{
    public UpdateCollaborationRequestStatusValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Status).NotEqual(CollaborationRequestStatus.Sent);
    }
}

public sealed class UpdateCollaborationRequestStatusHandler(
    ICollaborationRequestRepository requests,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCollaborationRequestStatusCommand, CollaborationRequestDto>
{
    public async Task<CollaborationRequestDto> Handle(UpdateCollaborationRequestStatusCommand command, CancellationToken cancellationToken)
    {
        var request = await requests.GetByIdAsync(command.RequestId, cancellationToken)
            ?? throw new InvalidOperationException("Collaboration request was not found.");
        if (request.Blogger.TelegramUserId != command.TelegramUserId && request.Business.TelegramUserId != command.TelegramUserId)
        {
            throw new UnauthorizedAccessException("You are not a participant in this request.");
        }

        switch (command.Status)
        {
            case CollaborationRequestStatus.Viewed: request.MarkViewed(); break;
            case CollaborationRequestStatus.Accepted: request.Accept(); break;
            case CollaborationRequestStatus.Declined: request.Decline(); break;
            default: throw new InvalidOperationException("Unsupported collaboration request status.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CollaborationRequestDto.From(request);
    }
}
