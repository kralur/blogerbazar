using BloggerBazar.Api.Contracts.Bloggers;
using BloggerBazar.Application.Abstractions.Security;
using BloggerBazar.Application.Features.Bloggers;
using BloggerBazar.Application.Features.Reviews;
using BloggerBazar.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BloggerBazar.Api.Controllers;

[ApiController]
[Route("api/bloggers")]
public sealed class BloggersController(ISender sender, ITelegramWebAppValidator telegramValidator) : TelegramControllerBase(telegramValidator)
{
    [HttpGet("me")]
    [ProducesResponseType<MyBloggerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyBloggerProfileDto>> GetMine(CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var profile = await sender.Send(new GetMyBloggerProfileQuery(actor.Id), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet]
    [ProducesResponseType<SearchBloggersResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchBloggersResponse>> Search(
        [FromQuery] string? query,
        [FromQuery] string? city,
        [FromQuery] string? category,
        [FromQuery] string? platform,
        [FromQuery] int? minFollowers,
        [FromQuery] int? minEr,
        [FromQuery] int? maxEr,
        [FromQuery] int? minPrice,
        [FromQuery] int? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new SearchBloggersQuery(query, city, category, platform, minFollowers, minEr, maxEr, minPrice, maxPrice, sort, page, pageSize), cancellationToken);
        return Ok(SearchBloggersResponse.From(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BloggerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BloggerProfileDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var profile = await sender.Send(new GetBloggerProfileQuery(id), cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpGet("{id:guid}/reviews")]
    [ProducesResponseType<IReadOnlyList<ReviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> GetReviews(Guid id, [FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetBloggerReviewsQuery(id, take), cancellationToken));

    [HttpPost]
    [ProducesResponseType<BloggerProfileDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BloggerProfileDto>> Create(CreateBloggerProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var portfolioItems = ToPortfolioItems(request);
        var profile = await sender.Send(new CreateBloggerProfileCommand(
            actor.Id, request.Name, request.LastName, request.Username, request.City, request.Categories, request.Bio,
            request.AvatarUrl, request.TotalFollowers, request.AverageReach, request.EngagementRate, request.StoriesPrice,
            request.ReelsPrice, request.PostPrice, request.IntegrationPrice, request.BarterEnabled, request.Phone, request.Email,
            portfolioItems, request.CoverUrl, request.Age, request.Gender, request.Language, request.Subcategory,
            request.PriceFrom, request.PriceTo, request.PriceNote, ToPlatforms(request)), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile);
    }

    [HttpPut("me")]
    [ProducesResponseType<BloggerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BloggerProfileDto>> Update(CreateBloggerProfileRequest request, CancellationToken cancellationToken)
    {
        var actor = GetTelegramUser();
        var profile = await sender.Send(new UpdateBloggerProfileCommand(
            actor.Id, request.Name, request.LastName, request.Username, request.City, request.Categories, request.Bio,
            request.AvatarUrl, request.TotalFollowers, request.AverageReach, request.EngagementRate, request.StoriesPrice,
            request.ReelsPrice, request.PostPrice, request.IntegrationPrice, request.BarterEnabled, request.Phone, request.Email,
            ToPortfolioItems(request), request.CoverUrl, request.Age, request.Gender, request.Language, request.Subcategory,
            request.PriceFrom, request.PriceTo, request.PriceNote, ToPlatforms(request)), cancellationToken);
        return Ok(profile);
    }

    private static IReadOnlyCollection<PortfolioItemInput>? ToPortfolioItems(CreateBloggerProfileRequest request) => request.PortfolioItems?.Select(item =>
    {
        if (!Enum.TryParse<PortfolioItemType>(item.Type, true, out var type))
        {
            throw new InvalidOperationException("Portfolio item type must be Image or Video.");
        }

        return new PortfolioItemInput(item.Title, type, item.Url);
    }).ToArray();

    private static IReadOnlyCollection<SocialPlatformInput>? ToPlatforms(CreateBloggerProfileRequest request) => request.Platforms?.Select(platform =>
        new SocialPlatformInput(platform.Type, platform.Url, platform.Followers, platform.ScreenshotUrl)).ToArray();
}
