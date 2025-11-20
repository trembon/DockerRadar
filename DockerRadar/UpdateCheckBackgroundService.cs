using DockerRadar.RegistryProviders;

namespace DockerRadar;

public class UpdateCheckBackgroundService(ILogger<UpdateCheckBackgroundService> logger, IConfiguration configuration, IRegistryProviderFactory registryProviderFactory, IContainerService containerService, ITimeService timeService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue<int>("UpdateCheck:IntervalMinutes", 30);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while checking Docker container updates.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking for container updates");

        var containers = await containerService.GetAll(cancellationToken);
        foreach (var container in containers.Where(x => timeService.Now() > x.NextCheck))
        {
            logger.LogInformation("Checking updates for container {ContainerId} ({Image})", container.Id, container.Image);

            bool hasUpdate = false;
            bool updateCheckFailed = false;

            try
            {
                var provider = registryProviderFactory.GetRegistryProvider(container.Image) ?? throw new ArgumentException("No registry provider exists for image");

                var remoteDigest = await provider.GetRemoteDigests(container.ImageTag ?? container.Image, cancellationToken);

                var matchingRemoteDigest = remoteDigest.Where(x => x.OS == container.ImageOs && x.Architecture == container.ImageArchitecture).FirstOrDefault();
                if (matchingRemoteDigest is not null)
                    hasUpdate = matchingRemoteDigest.Digest != container.ImageDigest;
            }
            catch (Exception ex)
            {
                updateCheckFailed = true;

                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError(ex, "Error checking updates for {Image}", container.Image);
            }

            container.HasUpdate = hasUpdate;
            container.UpdateCheckFailed = updateCheckFailed;
            container.LastChecked = timeService.Now();
            container.NextCheck = timeService.GetNextCheckTime();
        }

        logger.LogInformation("Docker container update check completed");
    }


}
