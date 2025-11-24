using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DockerRadar.RegistryProviders;

public class DockerHubRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    protected override string Name => "docker.io";

    protected override string GetManifestUrl(DockerImage image)
    {
        return $"https://registry-1.docker.io/v2/{image.Namespace}/{image.Image}/manifests/{image.Tag}";
    }

    protected override string? GetTokenUrl(DockerImage image)
    {
        return $"https://auth.docker.io/token?service=registry.docker.io&scope=repository:{image.Namespace}/{image.Image}:pull";
    }
}