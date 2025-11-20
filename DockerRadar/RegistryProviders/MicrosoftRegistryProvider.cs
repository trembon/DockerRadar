using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;

namespace DockerRadar.RegistryProviders;

public class MicrosoftRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache) : RegistryProviderBase(httpClientFactory, memoryCache)
{
    protected override string Name => "Microsoft";

    protected override Task<HttpRequestMessage> CreateRequest(string imageName, CancellationToken cancellationToken)
    {
        var parts = imageName.Split(':');
        var repo = parts[0].Replace("mcr.microsoft.com/", "");
        var tag = parts.Length > 1 ? parts[1] : "latest";

        var url = $"https://mcr.microsoft.com/v2/{repo}/manifests/{tag}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

        return Task.FromResult(request);
    }
}