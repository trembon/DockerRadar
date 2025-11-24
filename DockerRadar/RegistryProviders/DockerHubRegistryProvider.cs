using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class DockerHubRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IMemoryCache memoryCache = memoryCache;

    protected override string Name => "docker.io";

    protected override async Task<HttpRequestMessage> CreateRequest(DockerImage image, CancellationToken cancellationToken)
    {
        var token = await GetAuthTokenAsync(image, cancellationToken) ?? throw new Exception($"{Name}: Could not retrieve auth token for repo");

        var url = $"https://registry-1.{image.Registry}/v2/{image.Namespace}/{image.Image}/manifests/{image.Tag}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private async Task<string?> GetAuthTokenAsync(DockerImage image, CancellationToken cancellationToken)
    {
        var url = $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{image.Namespace}/{image.Image}:pull";

        var cachedToken = memoryCache.Get<string>(url);
        if (cachedToken is not null)
            return cachedToken;

        var client = httpClientFactory.CreateClient(nameof(DockerHubRegistryProvider));
        var res = await client.GetAsync(url, cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"{Name}: Auth request failed for repo (StatusCode: {res.StatusCode}");

        string data = await res.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(data);
        if (doc.RootElement.TryGetProperty("token", out var tokenEl))
        {
            memoryCache.Set(url, tokenEl.GetString(), TimeSpan.FromMinutes(10));
            return tokenEl.GetString();
        }

        return null;
    }
}