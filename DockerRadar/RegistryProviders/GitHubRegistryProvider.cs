using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class GitHubRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache)
{
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly IMemoryCache memoryCache = memoryCache;

    protected override string Name => "GitHub";

    protected override async Task<HttpRequestMessage> CreateRequest(string imageName, CancellationToken cancellationToken)
    {
        var parts = imageName.Split(':');
        var repo = parts[0].Replace("ghcr.io/", "");
        var tag = parts.Length > 1 ? parts[1] : "latest";

        var client = httpClientFactory.CreateClient(nameof(GitHubRegistryProvider));

        var token = await GetAuthTokenAsync(repo, cancellationToken) ?? throw new Exception("DockerHub: Could not retrieve auth token for repo");

        var url = $"https://ghcr.io/v2/{repo}/manifests/{tag}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private async Task<string?> GetAuthTokenAsync(string repo, CancellationToken cancellationToken)
    {
        var url = $"https://ghcr.io/token?service=ghcr.io&scope=repository:{repo}:pull";

        var cachedToken = memoryCache.Get<string>(url);
        if (cachedToken is not null)
            return cachedToken;

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{configuration["GitHub:Username"]}:{configuration["GitHub:Token"]}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var client = httpClientFactory.CreateClient(nameof(GitHubRegistryProvider));
        var res = await client.SendAsync(request, cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"GitHub: Auth request failed for repo (StatusCode: {res.StatusCode}");

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
