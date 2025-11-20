using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class DockerHubRegistryProvider(IHttpClientFactory httpClientFactory) : IRegistryProvider
{
    public async Task<string> GetRemoteDigest(string imageName, CancellationToken cancellationToken)
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

        var client = httpClientFactory.CreateClient(nameof(DockerHubRegistryProvider));
        var res = await client.SendAsync(request, cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"DockerHub: Could not retrieve info for repo (StatusCode: {res.StatusCode}");

        return res.Headers.GetValues("Docker-Content-Digest").First();
    }

    private async Task<string?> GetAuthTokenAsync(string repo, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(DockerHubRegistryProvider));

        var url = $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{repo}:pull";
        var res = await client.GetAsync(url, cancellationToken);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"DockerHub: Auth request failed for repo (StatusCode: {res.StatusCode}");

        string data = await res.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(data);
        if (doc.RootElement.TryGetProperty("token", out var tokenEl))
            return tokenEl.GetString();

        return null;
    }
}