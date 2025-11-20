using System.Net.Http.Headers;

namespace DockerRadar.RegistryProviders;

public class MicrosoftRegistryProvider(IHttpClientFactory httpClientFactory) : IRegistryProvider
{
    public async Task<string> GetRemoteDigest(string imageName, CancellationToken cancellationToken)
    {
        var parts = imageName.Split(':');
        var repo = parts[0].Replace("mcr.microsoft.com/", "");
        var tag = parts.Length > 1 ? parts[1] : "latest";

        var url = $"https://mcr.microsoft.com/v2/{repo}/manifests/{tag}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

        var client = httpClientFactory.CreateClient(nameof(MicrosoftRegistryProvider));
        var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Microsoft: Could not retrieve V2 info for repo (StatusCode: {response.StatusCode}");

        return response.Headers.GetValues("Docker-Content-Digest").First();
    }
}
