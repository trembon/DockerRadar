using DockerRadar.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DockerRadar.RegistryProviders;

public class MicrosoftRegistryProvider(IHttpClientFactory httpClientFactory) : IRegistryProvider
{
    public async Task<RemoteDigestModel[]> GetRemoteDigests(string imageName, CancellationToken cancellationToken)
    {
        return [];

        var parts = imageName.Split(':');
        var repo = parts[0].Replace("mcr.microsoft.com/", "");
        var tag = parts.Length > 1 ? parts[1] : "latest";

        var url = $"https://mcr.microsoft.com/v2/{repo}/manifests/{tag}";
        var requestV2 = new HttpRequestMessage(HttpMethod.Get, url);
        requestV2.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

        var client = httpClientFactory.CreateClient(nameof(MicrosoftRegistryProvider));
        var resV2 = await client.SendAsync(requestV2, cancellationToken);
        if (!resV2.IsSuccessStatusCode)
            throw new Exception($"Microsoft: Could not retrieve V2 info for repo (StatusCode: {resV2.StatusCode}");

        var dataV2 = await resV2.Content.ReadFromJsonAsync<RootobjectV2>(cancellationToken);

        var requestV1 = new HttpRequestMessage(HttpMethod.Get, url);
        requestV1.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(dataV2?.config?.mediaType ?? "application/vnd.docker.container.image.v1+json"));

        var resV1 = await client.SendAsync(requestV1, cancellationToken);
        if (!resV1.IsSuccessStatusCode)
            throw new Exception($"Microsoft: Could not retrieve V1 info for repo (StatusCode: {resV1.StatusCode}");

        var dataV1 = await resV1.Content.ReadFromJsonAsync<RootobjectV1>(cancellationToken);

        var digest = resV2.Headers.GetValues("Docker-Content-Digest").First();
        var historyRow = dataV1?.history.FirstOrDefault();
        if (historyRow != null)
        {
            var historyData = JsonSerializer.Deserialize<HistoryRootobject>(historyRow.v1Compatibility);
            return [new RemoteDigestModel(digest, dataV1?.architecture ?? "unknown", historyData?.os ?? "unknown")];
        }
        else
        {
            return [new RemoteDigestModel(digest, dataV1?.architecture ?? "unknown", "unknown")];
        }
    }

    public class RootobjectV2
    {
        public int schemaVersion { get; set; }
        public string mediaType { get; set; }
        public Config config { get; set; }
        public Layer[] layers { get; set; }
    }

    public class Config
    {
        public string mediaType { get; set; }
        public int size { get; set; }
        public string digest { get; set; }
    }

    public class Layer
    {
        public string mediaType { get; set; }
        public int size { get; set; }
        public string digest { get; set; }
    }


    public class RootobjectV1
    {
        public int schemaVersion { get; set; }
        public string name { get; set; }
        public string tag { get; set; }
        public string architecture { get; set; }
        public History[] history { get; set; }
    }

    public class History
    {
        public string v1Compatibility { get; set; }
    }


    public class HistoryRootobject
    {
        public string architecture { get; set; }
        public string created { get; set; }
        public string id { get; set; }
        public string os { get; set; }
        public string parent { get; set; }
        public bool throwaway { get; set; }
    }
}
