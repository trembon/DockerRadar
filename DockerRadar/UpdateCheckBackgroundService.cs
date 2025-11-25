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
            logger.LogInformation("Checking updates for container {Image}", container.Image);

            bool hasUpdate = false;
            bool updateCheckFailed = false;

            try
            {
                var image = DockerImageParser.Parse(container);
                var provider = registryProviderFactory.GetRegistryProvider(image) ?? throw new ArgumentException("No registry provider exists for image");

                var remoteDigest = await provider.GetRemoteDigest(image, cancellationToken);
                container.RemoteDigest = remoteDigest;

                if (remoteDigest != null)
                    hasUpdate = remoteDigest != container.Digest;
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
