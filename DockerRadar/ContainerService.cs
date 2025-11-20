using Docker.DotNet;
using Docker.DotNet.Models;
using DockerRadar.Models;
using System.Collections.Concurrent;

namespace DockerRadar;

public interface IContainerService
{
    Task<ContainerInfoModel[]> GetAll(CancellationToken cancellationToken);
}

public class ContainerService(ITimeService timeService) : IContainerService
{
    private readonly ConcurrentDictionary<string, ContainerInfoModel> cache = [];

    public async Task<ContainerInfoModel[]> GetAll(CancellationToken cancellationToken)
    {
        var docker = new DockerClientConfiguration().CreateClient();
        var containers = await docker.Containers.ListContainersAsync(new ContainersListParameters { All = true }, cancellationToken);
        foreach (var container in containers)
        {
            var image = await docker.Images.InspectImageAsync(container.ImageID, cancellationToken);

            cache.AddOrUpdate(container.ID, new ContainerInfoModel
            {
                Id = container.ID,
                Names = container.Names,
                Image = container.Image,
                ImageTag = image.RepoTags.First(),
                ImageDigest = container.ImageID,
                ImageOs = image.Os,
                ImageArchitecture = image.Architecture,
                Status = container.State,
                HasUpdate = false,
                UpdateCheckFailed = null,
                LastChecked = null,
                NextCheck = timeService.GetNextCheckTime(1, 10)
            }, (key, model) =>
            {
                model.Status = container.State;
                model.ImageDigest = container.ImageID;
                return model;
            });
        }

        foreach (var toDelete in cache.Values.Where(x => !containers.Any(c => c.ID == x.Id)).ToArray())
        {
            cache.TryRemove(toDelete.Id, out _);
        }

        return [.. cache.Values];
    }
}
