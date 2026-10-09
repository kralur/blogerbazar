using BloggerBazar.Application.Abstractions.Persistence;
using BloggerBazar.Application.Abstractions.Caching;
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
    string? AvatarUrl,
    IReadOnlyCollection<string>? Formats = null,
    string? ShowreelUrl = null) : IRequest<BrandFaceProfileDto>;

public sealed class UpsertBrandFaceProfileValidator : AbstractValidator<UpsertBrandFaceProfileCommand>
{
    public UpsertBrandFaceProfileValidator()
    {
        RuleFor(command => command.TelegramUserId).GreaterThan(0);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(100);
        RuleFor(command => command.City).NotEmpty().MaximumLength(80).Must(Regions.IsKnown);
        // A business picks a face by gender and age first (QA Q20).
        RuleFor(command => command.Age).NotNull().InclusiveBetween(BrandFaceAge.Min, BrandFaceAge.Max);
        RuleFor(command => command.Gender).NotEmpty().Must(gender => gender is not null && BrandFaceGenders.All.Contains(gender));
        RuleFor(command => command.Formats).NotEmpty().Must(items => items is not null && items.Count <= BrandFaceFormats.All.Count && items.All(BrandFaceFormats.All.Contains));
        RuleFor(command => command.ShowreelUrl).MaximumLength(2048).Must(ContactValidation.IsHttpsUrl).When(command => command.ShowreelUrl is not null);
        RuleFor(command => command.Languages).NotEmpty().Must(items => items.Count <= 5);
        RuleFor(command => command.Categories).NotEmpty().Must(items => items.Count <= 5);
        RuleForEach(command => command.Languages).NotEmpty().Must(SpokenLanguages.All.Contains);
        RuleForEach(command => command.Categories).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Telegram).Must(ContactValidation.IsTelegramUsername).When(command => command.Telegram is not null);
        // A business looks at the brand face's photos on Instagram before an offer (QA Q17).
        RuleFor(command => command.Instagram).NotEmpty().Must(ContactValidation.IsInstagramUsername);
        RuleFor(command => command.Experience).MaximumLength(2000).When(command => command.Experience is not null);
        RuleFor(command => command.Description).MaximumLength(2000).When(command => command.Description is not null);
        RuleFor(command => command.PortfolioUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.PortfolioUrl is not null);
        RuleFor(command => command.AvatarUrl).Must(ContactValidation.IsHttpsUrl).When(command => command.AvatarUrl is not null);
        RuleFor(command => command.CollaborationPrice).GreaterThan(0).LessThanOrEqualTo(InputLimits.MaxMoney).When(command => command.CollaborationPrice.HasValue);
    }
}

public sealed class UpsertBrandFaceProfileHandler(IBrandFaceProfileRepository profiles, IUnitOfWork unitOfWork, ICatalogCache? cache = null)
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

        profile.Update(command.Name.Trim(), command.City.Trim(), command.Age, command.Gender?.Trim(), command.Languages.Select(value => value.Trim()).ToArray(), command.Categories.Select(value => value.Trim()).ToArray(), command.Experience?.Trim(), command.Instagram?.Trim(), command.Telegram?.Trim(), command.PortfolioUrl?.Trim(), command.CollaborationPrice, command.Description?.Trim(), profile.AvatarUrl); // only our upload changes the photo
        profile.SetPresentation(command.Formats ?? [], command.ShowreelUrl?.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (cache is not null) await cache.RotateNamespaceVersionAsync(cancellationToken);
        return BrandFaceProfileDto.From(profile);
    }
}
