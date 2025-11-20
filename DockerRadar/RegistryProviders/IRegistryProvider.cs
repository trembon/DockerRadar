namespace DockerRadar.RegistryProviders;

public interface IRegistryProvider
{
    Task<string> GetRemoteDigest(string imageName, CancellationToken cancellationToken);
}
