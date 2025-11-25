using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockerRadar.RegistryProviders;

public interface IRegistryProvider
{
    Task<string?> GetRemoteDigest(DockerImage? image, CancellationToken cancellationToken);
}

public abstract class RegistryProviderBase(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : IRegistryProvider
{
    private const string ACCEPTED_MANIFEST_TYPES = "application/vnd.docker.distribution.manifest.list.v2+json,application/vnd.oci.image.index.v1+json,application/vnd.docker.distribution.manifest.v2+json,application/vnd.oci.image.manifest.v1+json";

    protected abstract string Name { get; }

    public virtual async Task<string?> GetRemoteDigest(DockerImage? image, CancellationToken cancellationToken)
    {
        if (image == null)
            return null;

        // check if disabled in config
        if (!configuration.GetValue($"Provider:{Name}:Enabled", true))
            return null;

        var cachedResult = memoryCache.Get<string>(image.ToString());
        if (cachedResult is not null)
            return cachedResult;

        // check if we are overloaded (TooManyRequests)
        bool? overloaded = memoryCache.Get<bool>(image.Registry);
        if (overloaded == true)
            return null;

        var url = GetManifestUrl(image);
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        foreach (string accept in ACCEPTED_MANIFEST_TYPES.Split(','))
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        string? token = await GetAuthenticationToken(image, cancellationToken);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var client = httpClientFactory.CreateClient(Name);
        var res = await client.SendAsync(request, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            // if we are being rate limited, cache that info for a while
            if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                memoryCache.Set(image.Registry, true, TimeSpan.FromHours(1));

            throw new Exception($"{Name}: Could not retrieve info for repo (StatusCode: {res.StatusCode})");
        }

        string json = await res.Content.ReadAsStringAsync(cancellationToken);

        var remoteDigest = await ParseDigest(json, image, cancellationToken);
        memoryCache.Set(image.ToString(), remoteDigest, TimeSpan.FromHours(8));
        return remoteDigest;
    }

    protected virtual async Task<string?> GetAuthenticationToken(DockerImage image, CancellationToken cancellationToken)
    {
        var tokenUrl = GetTokenUrl(image);
        if (tokenUrl is null)
            return null;

        var cachedToken = memoryCache.Get<string>(tokenUrl);
        if (cachedToken is not null)
            return cachedToken;

        // check if we are overloaded (TooManyRequests)
        bool? overloaded = memoryCache.Get<bool>(image.Registry);
        if (overloaded == true)
            return null;

        var request = new HttpRequestMessage(HttpMethod.Get, tokenUrl);

        string? username = configuration.GetValue<string>($"Provider:{image.Registry}:Username");
        string? token = configuration.GetValue<string>($"Provider:{image.Registry}:Token");
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token))
        {
            var authString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{token}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
        }
        else if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var client = httpClientFactory.CreateClient(nameof(GitHubRegistryProvider));
        var res = await client.SendAsync(request, cancellationToken);

        if (!res.IsSuccessStatusCode)
        {
            // if we are being rate limited, cache that info for a while
            if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                memoryCache.Set(image.Registry, true, TimeSpan.FromHours(1));

            throw new Exception($"{Name}: Auth request failed for repo (StatusCode: {res.StatusCode}");
        }

        string data = await res.Content.ReadAsStringAsync(cancellationToken);

        if (res.Headers.TryGetValues("Docker-Content-Digest", out var digestValues))
        {
            Console.WriteLine($"found digest from main-request-header: {digestValues.FirstOrDefault()}");
        }

        using var doc = JsonDocument.Parse(data);
        if (doc.RootElement.TryGetProperty("token", out var tokenEl))
        {
            memoryCache.Set(tokenUrl, tokenEl.GetString(), TimeSpan.FromMinutes(10));
            return tokenEl.GetString();
        }

        return null;
    }

    protected virtual async Task<string?> ParseDigest(string json, DockerImage image, CancellationToken cancellationToken)
    {
        string? manifestDigest = null;
        string? manifestMediaType = null;

        var basicManifest = JsonSerializer.Deserialize<BasicManifest>(json);

        if (basicManifest?.SchemaVersion == 2)
        {
            var manifestWithMediaType = JsonSerializer.Deserialize<ManifestV2WithMediaType>(json);
            string mediaType = manifestWithMediaType?.MediaType ?? "";

            if (mediaType == "application/vnd.docker.distribution.manifest.list.v2+json" || mediaType == "application/vnd.oci.image.index.v1+json")
            {
                var manifestWithManifests = JsonSerializer.Deserialize<ManifestV2WithManifests>(json);
                Console.WriteLine("Found manifests in v2:");
                foreach (var manifest in manifestWithManifests?.Manifests ?? [])
                {
                    Console.WriteLine($" - {manifest.Platform?.OperatingSystem}/{manifest.Platform?.Architecture}: {manifest.Digest}");
                }

                var matchingManifests = manifestWithManifests?.Manifests.Where(x => x.Platform?.Architecture == image.Architecture && x.Platform?.OperatingSystem == image.OperatingSystem).ToList() ?? [];

                if (matchingManifests.Count == 0)
                    return null;

                manifestDigest = matchingManifests.First().Digest;
                manifestMediaType = matchingManifests.First().MediaType;
            }
            else if (mediaType == "application/vnd.docker.distribution.manifest.v2+json" || mediaType == "application/vnd.oci.image.manifest.v1+json")
            {
                var manifestWithConfig = JsonSerializer.Deserialize<ManifestV2WithConfig>(json);
                Console.WriteLine($"found config in v2: {manifestWithConfig?.Config?.Digest}");
                manifestDigest = manifestWithConfig?.Config?.Digest ?? null;
                manifestMediaType = manifestWithConfig?.Config?.MediaType ?? null;
            }
        }
        else if (basicManifest?.SchemaVersion == 1)
        {
            var manifestV1 = JsonSerializer.Deserialize<ManifestV1WithHistory>(json);
            if (manifestV1 is not null && manifestV1.History.Length > 0)
            {
                var v1Compat = JsonSerializer.Deserialize<ManifestV1Compatibility>(manifestV1.History[0].V1Compatibility);
                Console.WriteLine($"getting from history compat: {v1Compat?.Config?.Image}");
                return v1Compat?.Config?.Image ?? null;
            }
        }

        if (manifestDigest is not null)
        {
            if (manifestMediaType == "application/vnd.docker.distribution.manifest.v2+json" || manifestMediaType == "application/vnd.oci.image.manifest.v1+json")
            {
                var url = GetManifestUrl(image, manifestDigest);
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(manifestMediaType));

                string? token = await GetAuthenticationToken(image, cancellationToken);
                if (token is not null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await httpClientFactory.CreateClient(Name).SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode && response.Headers.TryGetValues("Docker-Content-Digest", out var digestValues))
                {
                    Console.WriteLine($"found digest from header: {digestValues.FirstOrDefault()}");
                    return digestValues.FirstOrDefault();
                }
            }
            else if (manifestMediaType == "application/vnd.docker.container.image.v1+json" || manifestMediaType == "application/vnd.oci.image.config.v1+json")
            {
                Console.WriteLine($"found digest from lookup: {manifestDigest}");
                return manifestDigest;
            }
        }

        throw new Exception("No manifest found");
    }

    protected abstract string GetManifestUrl(DockerImage image, string? digest = null);

    protected virtual string? GetTokenUrl(DockerImage image) => null;

    private class BasicManifest
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
    }

    private class ManifestV2WithMediaType : BasicManifest
    {
        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = string.Empty;
    }

    private class ManifestV2WithConfig : BasicManifest
    {
        [JsonPropertyName("config")]
        public ManifestConfig? Config { get; set; }

        public class ManifestConfig
        {
            [JsonPropertyName("digest")]
            public string Digest { get; set; } = string.Empty;

            [JsonPropertyName("mediaType")]
            public string MediaType { get; set; } = string.Empty;
        }
    }

    private class ManifestV2WithManifests : ManifestV2WithMediaType
    {
        [JsonPropertyName("manifests")]
        public ManifestManifest[] Manifests { get; set; } = [];

        public class ManifestManifest
        {
            [JsonPropertyName("mediaType")]
            public string? MediaType { get; set; }

            [JsonPropertyName("digest")]
            public string? Digest { get; set; }

            [JsonPropertyName("platform")]
            public ManifestManifestPlatform? Platform { get; set; }
        }

        public class ManifestManifestPlatform
        {
            [JsonPropertyName("architecture")]
            public string? Architecture { get; set; }

            [JsonPropertyName("os")]
            public string? OperatingSystem { get; set; }
        }
    }

    private class ManifestV1WithHistory : BasicManifest
    {
        [JsonPropertyName("history")]
        public ManifestHistory[] History { get; set; } = [];

        public class ManifestHistory
        {
            [JsonPropertyName("v1Compatibility")]
            public string V1Compatibility { get; set; } = string.Empty;
        }
    }

    private class ManifestV1Compatibility
    {
        [JsonPropertyName("config")]
        public ManifestV1CompatibilityConfig? Config { get; set; }

        public class ManifestV1CompatibilityConfig
        {
            [JsonPropertyName("Image")]
            public string? Image { get; set; }
        }
    }
}
