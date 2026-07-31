namespace BloggerBazar.Application.Abstractions.Caching;

public interface ICatalogCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken) where T : class;
    Task<string> GetNamespaceVersionAsync(CancellationToken cancellationToken) => Task.FromResult("0");
    Task RotateNamespaceVersionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
