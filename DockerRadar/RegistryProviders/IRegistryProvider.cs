using Microsoft.Extensions.Caching.Memory;

namespace DockerRadar.RegistryProviders;

public interface IRegistryProvider
{
    Task<string> GetRemoteDigest(string imageName, CancellationToken cancellationToken);
}

public abstract class RegistryProviderBase(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache) : IRegistryProvider
{
    protected abstract string Name { get; }

    public virtual async Task<string> GetRemoteDigest(string imageName, CancellationToken cancellationToken)
    {
        var cachedResult = memoryCache.Get<string>(imageName);
        if (cachedResult is not null)
            return cachedResult;

        var client = httpClientFactory.CreateClient(nameof(RegistryProviderBase));
        var request = await CreateRequest(imageName, cancellationToken);

        var res = await client.SendAsync(request, cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"{Name}: Could not retrieve info for repo (StatusCode: {res.StatusCode}");

        var digest = res.Headers.GetValues("Docker-Content-Digest").First();
        memoryCache.Set(imageName, digest, TimeSpan.FromMinutes(60));
        return digest;
    }

    protected abstract Task<HttpRequestMessage> CreateRequest(string imageName, CancellationToken cancellationToken);
}
