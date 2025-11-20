using DockerRadar.Models;

namespace DockerRadar.RegistryProviders;

public interface IRegistryProvider
{
    Task<RemoteDigestModel[]> GetRemoteDigests(string imageName, CancellationToken cancellationToken);
}
