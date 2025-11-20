using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class GitHubRegistryProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IRegistryProvider
{
    public async Task<string> GetRemoteDigest(string imageName, CancellationToken cancellationToken)
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

        var res = await client.SendAsync(request, cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"GitHub: Could not retrieve info for repo (StatusCode: {res.StatusCode}");

        return res.Headers.GetValues("Docker-Content-Digest").First();
    }

    private async Task<string?> GetAuthTokenAsync(string repo, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(GitHubRegistryProvider));

        var url = $"https://ghcr.io/token?service=ghcr.io&scope=repository:{repo}:pull";
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{configuration["GitHub:Username"]}:{configuration["GitHub:Token"]}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var res = await client.SendAsync(request, cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"GitHub: Auth request failed for repo (StatusCode: {res.StatusCode}");

        string data = await res.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(data);
        if (doc.RootElement.TryGetProperty("token", out var tokenEl))
            return tokenEl.GetString();

        return null;
    }
}
