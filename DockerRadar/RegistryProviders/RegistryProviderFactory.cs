namespace DockerRadar.RegistryProviders;

public interface IRegistryProviderFactory
{
    IRegistryProvider? GetRegistryProvider(string image);
}

public class RegistryProviderFactory(IServiceProvider serviceProvider) : IRegistryProviderFactory
{
    public IRegistryProvider? GetRegistryProvider(string image)
    {
        if (image is null)
            return null;

        if (image.Contains("docker.io"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(DockerHubRegistryProvider));

        if (image.Contains("mcr.microsoft.com"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(MicrosoftRegistryProvider));

        if (image.Contains("ghcr.io"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(GitHubRegistryProvider));

        if (image.Contains("lscr.io"))
            return null;

        // default to Docker Hub for images without a registry specified
        if (!image.StartsWith("sha256:"))
            return serviceProvider.GetRequiredKeyedService<IRegistryProvider>(nameof(DockerHubRegistryProvider));

        return null;
    }
}
