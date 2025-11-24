namespace DockerRadar.Models;

public record DockerImage(string Registry, string Namespace, string Image, string Tag)
{
    public override string ToString()
    {
        return $"{Registry}/{Namespace}/{Image}:{Tag}";
    }
};
