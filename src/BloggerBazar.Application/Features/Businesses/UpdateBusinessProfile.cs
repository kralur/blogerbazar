using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Businesses;

public sealed record UpdateBusinessProfileCommand(
    long TelegramUserId,
    string Name,
    string? Username,
    string? City,
    string? LogoUrl,
    string? Description,
    string? Phone,
    string? Email) : IRequest<BusinessProfileDto>;

public sealed class UpdateBusinessProfileValidator : AbstractValidator<UpdateBusinessProfileCommand>
{
    public UpdateBusinessProfileValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Username).MaximumLength(64).When(command => command.Username is not null);
        RuleFor(command => command.City).MaximumLength(80).When(command => command.City is not null);
        RuleFor(command => command.LogoUrl).Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _)).When(command => command.LogoUrl is not null);
        RuleFor(command => command.Description).MaximumLength(1000).When(command => command.Description is not null);
        RuleFor(command => command.Phone).Matches("^\\+?[0-9]{7,20}$").When(command => command.Phone is not null);
        RuleFor(command => command.Email).EmailAddress().MaximumLength(254).When(command => command.Email is not null);
    }
}

public sealed class UpdateBusinessProfileHandler(IBusinessProfileRepository businesses, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateBusinessProfileCommand, BusinessProfileDto>
{
    public async Task<BusinessProfileDto> Handle(UpdateBusinessProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken)
            ?? throw new InvalidOperationException("Create a business profile before updating it.");

        profile.Update(
            command.Name.Trim(),
            command.Username?.Trim(),
            command.City?.Trim(),
            command.LogoUrl,
            command.Description?.Trim(),
            command.Phone?.Trim(),
            command.Email?.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BusinessProfileDto.From(profile);
    }
}
