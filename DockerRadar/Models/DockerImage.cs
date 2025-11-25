namespace DockerRadar.Models;

public record DockerImage(string Registry, string Namespace, string Image, string Tag, string? Architecture, string? OperatingSystem)
{
    public override string ToString()
    {
        return $"{Registry}/{Namespace}/{Image}:{Tag}";
    }
};
