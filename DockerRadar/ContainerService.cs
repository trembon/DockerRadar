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
            ImageInspectResponse? image = null;
            try
            {
                image = await docker.Images.InspectImageAsync(container.ImageID, cancellationToken);
            }
            catch { }

            string name = container.Names?.FirstOrDefault()?.Replace("/", "") ?? container.ID;
            string digest = image?.RepoDigests?.FirstOrDefault()?.Split('@').Last() ?? container.ImageID;
            bool ignore = container.Labels.TryGetValue("dockerradar.ignore", out var ignoreLabel) && ignoreLabel.Equals("true", StringComparison.OrdinalIgnoreCase);

            cache.AddOrUpdate(container.ID, new ContainerInfoModel
            {
                Id = container.ID,
                Name = name,
                Image = container.Image,
                Digest = digest,
                Status = container.State,
                UpdateCheckFailed = null,
                LastChecked = null,
                NextCheck = ignore ? null : timeService.GetNextCheckTime(1, 10)
            }, (key, model) =>
            {
                model.Status = container.State;
                model.Digest = digest;

                if (ignore && model.NextCheck != null)
                {
                    logger.LogInformation("Container {Name} is now ignored; skipping update checks", name);
                    model.NextCheck = null;
                    model.RemoteDigest = null;
                }
                else if (!ignore && model.NextCheck == null)
                {
                    logger.LogInformation("Container {Name} is no longer ignored; scheduling update checks", name);
                    model.NextCheck = timeService.GetNextCheckTime(1, 10);
                }

                return model;
            });
        }

        foreach (var toDelete in cache.Values.Where(x => !containers.Any(c => c.ID == x.Id)).ToArray())
        {
            logger.LogInformation("Removing container {Name} from cache because it no longer exists", toDelete.Name);
            cache.TryRemove(toDelete.Id, out _);
        }

        return [.. cache.Values];
    }
}
