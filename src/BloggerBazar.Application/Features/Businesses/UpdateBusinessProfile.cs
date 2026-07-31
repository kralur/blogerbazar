using BloggerBazar.Application.Abstractions.Persistence;
using FluentValidation;
using BloggerBazar.Application.Validation;
using MediatR;

namespace BloggerBazar.Application.Features.Businesses;

public sealed record UpdateBusinessProfileCommand(
    long TelegramUserId,
    string Name,
    string? Username,
    string? City,
    string? LogoUrl,
    string? WebsiteUrl,
    string? Description,
    string? Phone,
    string? Email) : IRequest<BusinessProfileDto>;

public sealed class UpdateBusinessProfileValidator : AbstractValidator<UpdateBusinessProfileCommand>
{
    public UpdateBusinessProfileValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Username).NotEmpty().Must(ContactValidation.IsTelegramUsername);
        RuleFor(command => command.City).NotEmpty().MaximumLength(80);
        RuleFor(command => command.LogoUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.LogoUrl is not null);
        RuleFor(command => command.WebsiteUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.WebsiteUrl is not null);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.Phone).NotEmpty().Must(ContactValidation.IsUzbekPhone);
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

        var usernameOwner = await businesses.GetByUsernameAsync(command.Username!.Trim(), cancellationToken);
        if (usernameOwner is not null && usernameOwner.Id != profile.Id)
        {
            throw new InvalidOperationException("This Telegram username is already used by another business profile.");
        }

        profile.Update(
            command.Name.Trim(),
            command.Username?.Trim(),
            command.City?.Trim(),
            command.LogoUrl,
            command.WebsiteUrl?.Trim(),
            command.Description?.Trim(),
            command.Phone?.Trim(),
            command.Email?.Trim());
        profile.Approve();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BusinessProfileDto.From(profile);
    }
}
