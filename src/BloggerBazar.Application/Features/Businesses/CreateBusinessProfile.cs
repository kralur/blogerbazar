using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Application.Validation;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.Businesses;

public sealed record CreateBusinessProfileCommand(long TelegramUserId, string Name, string? Username, string? City, string? LogoUrl, string? WebsiteUrl, string? Description, string? Phone, string? Email) : IRequest<BusinessProfileDto>;

public sealed class CreateBusinessProfileValidator : AbstractValidator<CreateBusinessProfileCommand>
{
    public CreateBusinessProfileValidator()
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

public sealed class CreateBusinessProfileHandler(IBusinessProfileRepository businesses, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateBusinessProfileCommand, BusinessProfileDto>
{
    public async Task<BusinessProfileDto> Handle(CreateBusinessProfileCommand command, CancellationToken cancellationToken)
    {
        var existing = await businesses.GetIncludingDeletedByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (existing is not null && !existing.IsDeleted)
        {
            throw new InvalidOperationException("A business profile already exists for this Telegram user.");
        }

        if (await businesses.GetByUsernameAsync(command.Username!.Trim(), cancellationToken) is not null)
        {
            throw new InvalidOperationException("This Telegram username is already used by another business profile.");
        }

        var profile = existing ?? BusinessProfile.Create(command.TelegramUserId, command.Name.Trim(), command.City?.Trim());
        if (existing is not null)
        {
            profile.Restore();
        }
        profile.Update(command.Name.Trim(), command.Username?.Trim(), command.City?.Trim(), command.LogoUrl, command.WebsiteUrl?.Trim(), command.Description?.Trim(), command.Phone?.Trim(), command.Email?.Trim());
        profile.Approve();
        if (existing is null)
        {
            await businesses.AddAsync(profile, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BusinessProfileDto.From(profile);
    }
}
