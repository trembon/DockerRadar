using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DockerRadar.RegistryProviders;

public class GitHubRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    protected override string Name => "ghcr.io";

    protected override string GetManifestUrl(DockerImage image, string? digest = null)
    {
        return $"https://{image.Registry}/v2/{image.Namespace}/{image.Image}/manifests/{digest ?? image.Tag}";
    }

    protected override string? GetTokenUrl(DockerImage image)
    {
        return $"https://{image.Registry}/token?service={image.Registry}&scope=repository:{image.Namespace}/{image.Image}:pull";
    }
}
