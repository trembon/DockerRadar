using Docker.DotNet;
using Docker.DotNet.Models;
using DockerRadar.Models;
using System.Collections.Concurrent;

namespace DockerRadar;

public interface IContainerService
{
    Task<ContainerInfoModel[]> GetAll(CancellationToken cancellationToken);
}

public class ContainerService(ITimeService timeService, ILogger<ContainerService> logger) : IContainerService
{
    private readonly ConcurrentDictionary<string, ContainerInfoModel> cache = [];

    public async Task<ContainerInfoModel[]> GetAll(CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching container list from Docker daemon");

        var docker = new DockerClientConfiguration().CreateClient();
        var containers = await docker.Containers.ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
        foreach (var container in containers)
        {
            logger.LogInformation("Processing container {ContainerImage}", container.Image);

            cache.AddOrUpdate(container.ID, new ContainerInfoModel
            {
                Id = container.ID,
                Names = container.Names,
                Image = container.Image,
                Digest = container.ImageID,
                Status = container.State,
                HasUpdate = false,
                UpdateCheckFailed = null,
                LastChecked = null,
                NextCheck = timeService.GetNextCheckTime(1, 10)
            }, (key, model) =>
            {
                model.Status = container.State;
                model.Digest = container.ImageID;
                return model;
            });
        }

        foreach (var toDelete in cache.Values.Where(x => !containers.Any(c => c.ID == x.Id)).ToArray())
        {
            logger.LogInformation("Removing container {ContainerId} from cache because it no longer exists", toDelete.Id);
            cache.TryRemove(toDelete.Id, out _);
        }

        return [.. cache.Values];
    }
}
