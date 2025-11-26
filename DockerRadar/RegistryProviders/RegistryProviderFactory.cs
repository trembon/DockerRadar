using DockerRadar.Models;

namespace DockerRadar.RegistryProviders;

public interface IRegistryProviderFactory
{
    IRegistryProvider? GetRegistryProvider(DockerImage? image);
}

public class RegistryProviderFactory(IServiceProvider serviceProvider) : IRegistryProviderFactory
{
    public IRegistryProvider? GetRegistryProvider(DockerImage? image)
    {
        if (image is null)
            return null;

        if (image.Registry == "docker.io")
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(DockerHubRegistryProvider));

        if (image.Registry == "mcr.microsoft.com")
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(MicrosoftRegistryProvider));

        if (image.Registry == "ghcr.io")
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(GitHubRegistryProvider));

        if (image.Registry == "lscr.io")
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(LinuxServerRegistryProvider));

        return null;
    }
}
