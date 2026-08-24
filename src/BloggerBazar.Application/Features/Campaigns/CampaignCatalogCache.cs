namespace BloggerBazar.Application.Features.Campaigns;

internal static class CampaignCatalogCache
{
    internal const string Namespace = "campaigns";

    internal static Task InvalidateAsync(Abstractions.Caching.ICatalogCache cache, CancellationToken cancellationToken) =>
        cache.RotateNamespaceVersionAsync(Namespace, cancellationToken);
}
