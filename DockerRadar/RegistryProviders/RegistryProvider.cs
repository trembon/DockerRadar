using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public interface IRegistryProvider
{
    Task<string?> GetRemoteDigest(DockerImage? image, CancellationToken cancellationToken);
}

public abstract class RegistryProviderBase(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : IRegistryProvider
{
    private const string ACCEPTED_MANIFEST_TYPES = "application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.v2+json, application/vnd.oci.image.manifest.v1+json";

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
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ACCEPTED_MANIFEST_TYPES));

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

        //string json = await res.Content.ReadAsStringAsync(cancellationToken);


        var remoteDigest = res.Headers.GetValues("Docker-Content-Digest").First();
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
        using var doc = JsonDocument.Parse(data);
        if (doc.RootElement.TryGetProperty("token", out var tokenEl))
        {
            memoryCache.Set(tokenUrl, tokenEl.GetString(), TimeSpan.FromMinutes(10));
            return tokenEl.GetString();
        }

        return null;
    }

    //protected virtual string ParseDigest(string json)
    //{

    //}

    protected abstract string GetManifestUrl(DockerImage image);

    protected virtual string? GetTokenUrl(DockerImage image) => null;
}
