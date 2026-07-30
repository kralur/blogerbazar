using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
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
        RuleFor(command => command.Username).NotEmpty().Matches("^@[A-Za-z0-9_]{5,32}$");
        RuleFor(command => command.City).NotEmpty().MaximumLength(80);
        RuleFor(command => command.LogoUrl).Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _)).When(command => command.LogoUrl is not null);
        RuleFor(command => command.WebsiteUrl).Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps).When(command => command.WebsiteUrl is not null);
        RuleFor(command => command.Description).NotEmpty().MaximumLength(1000);
        RuleFor(command => command.Phone).NotEmpty().Matches("^\\+?[0-9\\s]{7,20}$");
        RuleFor(command => command.Email).EmailAddress().MaximumLength(254).When(command => command.Email is not null);
    }
}

public sealed class CreateBusinessProfileHandler(IBusinessProfileRepository businesses, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateBusinessProfileCommand, BusinessProfileDto>
{
    public async Task<BusinessProfileDto> Handle(CreateBusinessProfileCommand command, CancellationToken cancellationToken)
    {
        if (await businesses.GetByTelegramUserIdAsync(command.TelegramUserId, cancellationToken) is not null)
        {
            throw new InvalidOperationException("A business profile already exists for this Telegram user.");
        }

        if (await businesses.GetByUsernameAsync(command.Username!.Trim(), cancellationToken) is not null)
        {
            throw new InvalidOperationException("This Telegram username is already used by another business profile.");
        }

        var profile = BusinessProfile.Create(command.TelegramUserId, command.Name.Trim(), command.City?.Trim());
        profile.Update(command.Name.Trim(), command.Username?.Trim(), command.City?.Trim(), command.LogoUrl, command.WebsiteUrl?.Trim(), command.Description?.Trim(), command.Phone?.Trim(), command.Email?.Trim());
        profile.Approve();
        await businesses.AddAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BusinessProfileDto.From(profile);
    }
}
