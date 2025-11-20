using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class DockerHubRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache) : RegistryProviderBase(httpClientFactory, memoryCache)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IMemoryCache memoryCache = memoryCache;

    protected override string Name => "DockerHub";

    protected override async Task<HttpRequestMessage> CreateRequest(string imageName, CancellationToken cancellationToken)
    {
        var parts = imageName.Split(':');
        var repo = parts[0].Replace("docker.io/", "");
        var tag = parts.Length > 1 ? parts[1] : "latest";

        if (!repo.Contains('/'))
            repo = $"library/{repo}";

        var token = await GetAuthTokenAsync(repo, cancellationToken) ?? throw new Exception("DockerHub: Could not retrieve auth token for repo");

        var url = $"https://registry-1.docker.io/v2/{repo}/manifests/{tag}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private async Task<string?> GetAuthTokenAsync(string repo, CancellationToken cancellationToken)
    {
        var url = $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{repo}:pull";

        var cachedToken = memoryCache.Get<string>(url);
        if (cachedToken is not null)
            return cachedToken;

        var client = httpClientFactory.CreateClient(nameof(DockerHubRegistryProvider));
        var res = await client.GetAsync(url, cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"DockerHub: Auth request failed for repo (StatusCode: {res.StatusCode}");

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