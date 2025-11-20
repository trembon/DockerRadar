
using DockerRadar.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class DockerHubRegistryProvider(IHttpClientFactory httpClientFactory) : IRegistryProvider
{
    public async Task<RemoteDigestModel[]> GetRemoteDigests(string imageName, CancellationToken cancellationToken)
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

        var digest = res.Headers.GetValues("Docker-Content-Digest").First();
        var data = await res.Content.ReadFromJsonAsync<Rootobject>(cancellationToken);

        return data?.manifests?.Select(x => new RemoteDigestModel(digest, x.platform.architecture, x.platform.os)).ToArray() ?? [];
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

    public class Rootobject
    {
        public Manifest[] manifests { get; set; }
        public string mediaType { get; set; }
        public int schemaVersion { get; set; }
    }

    public class Manifest
    {
        public Annotations annotations { get; set; }
        public string digest { get; set; }
        public string mediaType { get; set; }
        public Platform platform { get; set; }
        public int size { get; set; }
    }

    public class Annotations
    {
        public string comdockerofficialimagesbashbrewarch { get; set; }
        public string orgopencontainersimagebasedigest { get; set; }
        public string orgopencontainersimagebasename { get; set; }
        public DateTime orgopencontainersimagecreated { get; set; }
        public string orgopencontainersimagerevision { get; set; }
        public string orgopencontainersimagesource { get; set; }
        public string orgopencontainersimageurl { get; set; }
        public string orgopencontainersimageversion { get; set; }
        public string vnddockerreferencedigest { get; set; }
        public string vnddockerreferencetype { get; set; }
    }

    public class Platform
    {
        public string architecture { get; set; }
        public string os { get; set; }
        public string variant { get; set; }
    }
}