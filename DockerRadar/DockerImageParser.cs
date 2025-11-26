using DockerRadar.Models;

namespace DockerRadar;

public static class DockerImageParser
{
    public static DockerImage? Parse(ContainerInfoModel container)
    {
        string? input = container.Image;

        if (string.IsNullOrWhiteSpace(input))
            return null;

        if (input.StartsWith("sha256:"))
            return null;

        string registry = "";
        string ns = "";
        string image;
        string tag = "latest";

        // Split tag
        var parts = input.ToLowerInvariant().Split(':', 2);
        string withoutTag = parts[0];
        if (parts.Length == 2)
            tag = parts[1];

        // Split path components
        var pathParts = withoutTag.Split('/');

        // Check if first part is a registry
        if (LooksLikeRegistry(pathParts[0]))
        {
            registry = pathParts[0];
            if (pathParts.Length < 2)
                throw new FormatException($"Invalid reference: missing image name in '{input}'.");

            if (pathParts.Length == 2)
            {
                ns = "library"; // Docker Hub default
                image = pathParts[1];
            }
            else
            {
                ns = pathParts[1];
                image = pathParts[2];
            }
        }
        else
        {
            // Default registry
            registry = "docker.io";

            if (pathParts.Length == 1)
            {
                // Official images
                ns = "library";
                image = pathParts[0];
            }
            else
            {
                ns = pathParts[0];
                image = pathParts[1];
            }
        }

        return new DockerImage(registry, ns, image, tag);
    }

    private static bool LooksLikeRegistry(string part)
    {
        return part.Contains('.') ||
               part.Contains(':') ||
               part.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }
}