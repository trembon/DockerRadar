namespace DockerRadar.RegistryProviders;

public interface IRegistryProviderFactory
{
    IRegistryProvider? GetRegistryProvider(string imageId);
}

public class RegistryProviderFactory(IServiceProvider serviceProvider) : IRegistryProviderFactory
{
    public IRegistryProvider? GetRegistryProvider(string imageId)
    {
        if (imageId is null)
            return null;

        if (imageId.Contains("docker.io"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(DockerHubRegistryProvider));

        if (imageId.Contains("mcr.microsoft.com"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(MicrosoftRegistryProvider));

        if (imageId.Contains("ghcr.io"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(GitHubRegistryProvider));

        return null;
    }
}
