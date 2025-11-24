using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;

namespace DockerRadar.RegistryProviders;

public class MicrosoftRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    protected override string Name => "mcr.microsoft.com";

    protected override string GetManifestUrl(DockerImage image)
    {
        return $"https://{image.Registry}/v2/{image.Namespace}/{image.Image}/manifests/{image.Tag}";
    }
}