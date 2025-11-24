using DockerRadar.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;

namespace DockerRadar.RegistryProviders;

public class MicrosoftRegistryProvider(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration) : RegistryProviderBase(httpClientFactory, memoryCache, configuration)
{
    protected override string Name => "mcr.microsoft.com";

    protected override Task<HttpRequestMessage> CreateRequest(DockerImage image, CancellationToken cancellationToken)
    {
        var url = $"https://{image.Registry}/v2/{image.Namespace}/{image.Image}/manifests/{image.Tag}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

        return Task.FromResult(request);
    }
}