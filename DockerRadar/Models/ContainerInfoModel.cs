namespace DockerRadar.Models;

public class ContainerInfoModel
{
    public string Id { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Image { get; init; } = null!;
    public string Digest { get; set; } = null!;
    public string? RemoteDigest { get; set; }
    public string Status { get; set; } = null!;
    public bool HasUpdate { get => RemoteDigest is not null && RemoteDigest != Digest; }
    public bool? UpdateCheckFailed { get; set; }
    public DateTime? LastChecked { get; set; }
    public DateTime NextCheck { get; set; }
}
