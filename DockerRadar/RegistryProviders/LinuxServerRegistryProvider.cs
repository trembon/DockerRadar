using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DockerRadar.RegistryProviders;

public class LinuxServerRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    protected override string Name => "lscr.io";

    protected override string GetManifestUrl(DockerImage image, string? digest = null)
    {
        return $"https://lscr.io/v2/{image.Namespace}/{image.Image}/manifests/{digest ?? image.Tag}";
    }

    protected override string? GetTokenUrl(DockerImage image)
    {
        return $"https://ghcr.io/token?service=ghcr.io&scope=repository:{image.Namespace}/{image.Image}:pull";
    }
}
