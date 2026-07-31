using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Domain.Entities;
using BloggerBazar.Application.Validation;
using FluentValidation;
using MediatR;

namespace BloggerBazar.Application.Features.BrandFaces;

public sealed record UpsertBrandFaceProfileCommand(
    long TelegramUserId,
    string Name,
    string City,
    int? Age,
    string? Gender,
    IReadOnlyCollection<string> Languages,
    IReadOnlyCollection<string> Categories,
    string? Experience,
    string? Instagram,
    string? Telegram,
    string? PortfolioUrl,
    int? CollaborationPrice,
    string? Description,
    string? AvatarUrl) : IRequest<BrandFaceProfileDto>;

public sealed class UpsertBrandFaceProfileValidator : AbstractValidator<UpsertBrandFaceProfileCommand>
{
    public UpsertBrandFaceProfileValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.City).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Age).InclusiveBetween(13, 100).When(command => command.Age.HasValue);
        RuleFor(command => command.Gender).MaximumLength(32).When(command => command.Gender is not null);
        RuleFor(command => command.Languages).NotEmpty().Must(items => items.Count <= 5);
        RuleFor(command => command.Categories).NotEmpty().Must(items => items.Count <= 5);
        RuleForEach(command => command.Languages).NotEmpty().MaximumLength(32);
        RuleForEach(command => command.Categories).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Telegram).NotEmpty().Must(ContactValidation.IsTelegramUsername);
        RuleFor(command => command.Instagram).Must(ContactValidation.IsInstagramUsername).When(command => command.Instagram is not null);
        RuleFor(command => command.Experience).MaximumLength(2000).When(command => command.Experience is not null);
        RuleFor(command => command.Description).MaximumLength(2000).When(command => command.Description is not null);
        RuleFor(command => command.PortfolioUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.PortfolioUrl is not null);
        RuleFor(command => command.AvatarUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.AvatarUrl is not null);
        RuleFor(command => command.CollaborationPrice).GreaterThan(0).When(command => command.CollaborationPrice.HasValue);
    }
}

public sealed class UpsertBrandFaceProfileHandler(IBrandFaceProfileRepository profiles, IUnitOfWork unitOfWork)
    : IRequestHandler<UpsertBrandFaceProfileCommand, BrandFaceProfileDto>
{
    public async Task<BrandFaceProfileDto> Handle(UpsertBrandFaceProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetIncludingDeletedByTelegramUserIdAsync(command.TelegramUserId, cancellationToken);
        if (profile is null)
        {
            profile = BrandFaceProfile.Create(command.TelegramUserId, command.Name.Trim(), command.City.Trim(), command.Categories.Select(value => value.Trim()).ToArray());
            await profiles.AddAsync(profile, cancellationToken);
        }
        else if (profile.IsDeleted)
        {
            profile.Restore();
        }

        profile.Update(command.Name.Trim(), command.City.Trim(), command.Age, command.Gender?.Trim(), command.Languages.Select(value => value.Trim()).ToArray(), command.Categories.Select(value => value.Trim()).ToArray(), command.Experience?.Trim(), command.Instagram?.Trim(), command.Telegram!.Trim(), command.PortfolioUrl?.Trim(), command.CollaborationPrice, command.Description?.Trim(), command.AvatarUrl?.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BrandFaceProfileDto.From(profile);
    }
}
