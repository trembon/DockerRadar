using DockerRadar.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class GitHubRegistryProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IRegistryProvider
{
    public async Task<RemoteDigestModel[]> GetRemoteDigests(string imageName, CancellationToken cancellationToken)
    {
        return [];

        var parts = imageName.Split(':');
        var repo = parts[0].Replace("ghcr.io/", "");
        var tag = parts.Length > 1 ? parts[1] : "latest";

        var url = $"https://ghcr.io/v2/{repo}/manifests/{tag}";

        var client = httpClientFactory.CreateClient(nameof(GitHubRegistryProvider));

        var token = await GetAuthTokenAsync(repo, cancellationToken) ?? throw new Exception("DockerHub: Could not retrieve auth token for repo");

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.index.v1+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(request, cancellationToken);
        if (!res.IsSuccessStatusCode)
            throw new Exception($"GitHub: Could not retrieve info for repo (StatusCode: {res.StatusCode}");

        var digest = res.Headers.GetValues("Docker-Content-Digest").First();
        var data = await res.Content.ReadFromJsonAsync<Rootobject>(cancellationToken);

        return data?.manifests.Select(x => new RemoteDigestModel(digest, x.platform.architecture, x.platform.os)).ToArray() ?? [];
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


    public class Rootobject
    {
        public int schemaVersion { get; set; }
        public string mediaType { get; set; }
        public Manifest[] manifests { get; set; }
    }

    public class Manifest
    {
        public string mediaType { get; set; }
        public string digest { get; set; }
        public int size { get; set; }
        public Platform platform { get; set; }
        public Annotations annotations { get; set; }
    }

    public class Platform
    {
        public string architecture { get; set; }
        public string os { get; set; }
    }

    public class Annotations
    {
        public string vnddockerreferencedigest { get; set; }
        public string vnddockerreferencetype { get; set; }
    }

}
