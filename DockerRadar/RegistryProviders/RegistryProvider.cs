using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DockerRadar.RegistryProviders;

public interface IRegistryProvider
{
    Task<string?> GetRemoteDigest(DockerImage? image, CancellationToken cancellationToken);
}

public abstract class RegistryProviderBase(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : IRegistryProvider
{
    protected abstract string Name { get; }

    public virtual async Task<string?> GetRemoteDigest(DockerImage? image, CancellationToken cancellationToken)
    {
        if (image == null)
            return null;

        // check if disabled in config
        if (!configuration.GetValue($"Provider:{this.Name}:Enabled", true))
            return null;

        // check if we are overloaded (TooManyRequests)
        bool? overloaded = memoryCache.Get<bool>(image.Registry);
        if (overloaded == true)
            return null;

        var cachedResult = memoryCache.Get<string>(image.ToString());
        if (cachedResult is not null)
            return cachedResult;

        var client = httpClientFactory.CreateClient(nameof(RegistryProviderBase));
        var request = await CreateRequest(image, cancellationToken);

        var res = await client.SendAsync(request, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            // if we are being rate limited, cache that info for a while
            if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                memoryCache.Set(image.Registry, true, TimeSpan.FromHours(1));

            throw new Exception($"{Name}: Could not retrieve info for repo (StatusCode: {res.StatusCode})");
        }

        var remoteDigest = res.Headers.GetValues("Docker-Content-Digest").First();
        memoryCache.Set(image.ToString(), remoteDigest, TimeSpan.FromHours(8));
        return remoteDigest;
    }

    protected abstract Task<HttpRequestMessage> CreateRequest(DockerImage image, CancellationToken cancellationToken);
}
