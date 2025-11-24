using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class GitHubRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IMemoryCache memoryCache = memoryCache;

    protected override string Name => "ghcr.io";

    protected override async Task<HttpRequestMessage> CreateRequest(DockerImage image, CancellationToken cancellationToken)
    {
        var token = await GetAuthTokenAsync(image, cancellationToken) ?? throw new Exception($"{Name}: Could not retrieve auth token for repo");

        var url = $"https://{image.Registry}/v2/{image.Namespace}/{image.Image}/manifests/{image.Tag}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private async Task<string?> GetAuthTokenAsync(DockerImage image, CancellationToken cancellationToken)
    {
        var url = $"https://{image.Registry}/token?service={image.Registry}&scope=repository:{image.Namespace}/{image.Image}:pull";

        var cachedToken = memoryCache.Get<string>(url);
        if (cachedToken is not null)
            return cachedToken;

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{configuration[$"Provider:{image.Registry}:Username"]}:{configuration[$"Provider:{image.Registry}:Token"]}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var client = httpClientFactory.CreateClient(nameof(GitHubRegistryProvider));
        var res = await client.SendAsync(request, cancellationToken);

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
