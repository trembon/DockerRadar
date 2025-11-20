namespace DockerRadar.Models;

public class ContainerInfoModel
{
    public string Id { get; init; } = null!;
    public IList<string> Names { get; init; } = [];
    public string Image { get; init; } = null!;
    public string? ImageTag { get; init; }
    public string ImageDigest { get; set; } = null!;
    public string ImageOs { get; set; } = null!;
    public string ImageArchitecture { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool HasUpdate { get; set; }
    public bool? UpdateCheckFailed { get; set; }
    public DateTime? LastChecked { get; set; }
    public DateTime NextCheck { get; set; }
}
