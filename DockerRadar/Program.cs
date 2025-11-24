using DockerRadar;
using DockerRadar.RegistryProviders;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "APP_");

builder.Services.AddOpenApi();

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<ITimeService, TimeService>();
builder.Services.AddSingleton<IContainerService, ContainerService>();

builder.Services.AddSingleton<IRegistryProviderFactory, RegistryProviderFactory>();
builder.Services.AddKeyedSingleton<IRegistryProvider, GitHubRegistryProvider>(nameof(GitHubRegistryProvider));
builder.Services.AddKeyedSingleton<IRegistryProvider, DockerHubRegistryProvider>(nameof(DockerHubRegistryProvider));
builder.Services.AddKeyedSingleton<IRegistryProvider, MicrosoftRegistryProvider>(nameof(MicrosoftRegistryProvider));

builder.Services.AddHostedService<UpdateCheckBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/containers", async (IContainerService containerService, CancellationToken cancellationToken, bool html = false, bool updates = false, bool running = true) =>
{
    var containers = await containerService.GetAll(cancellationToken);
    containers = [.. containers.OrderBy(x => x.Name)];

    if (updates)
        containers = [.. containers.Where(x => x.HasUpdate)];

    if (running)
        containers = [.. containers.Where(x => x.Status != "exited")];

    if (html)
    {
        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine("<table><thead><tr><th>Names</th><th>Image</th><th>Status</th><th>Digest</th><th>Remote Digest</th><th>Has Update</th><th>Update check failed</th><th>Last check</th><th>Next checked</th></tr></thead><tbody>");
        foreach (var container in containers)
        {
            stringBuilder.Append("<tr><td style=\"padding: 5px 20px;\">");
            stringBuilder.Append(container.Name);
            stringBuilder.Append("</td><td style=\"padding: 5px 30px;\">");
            stringBuilder.Append(container.Image);
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.Status);
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.Digest);
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.RemoteDigest);
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.HasUpdate);
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.UpdateCheckFailed);
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.LastChecked?.ToLocalTime());
            stringBuilder.Append("</td><td style=\"padding: 5px 10px;\">");
            stringBuilder.Append(container.NextCheck.ToLocalTime());
            stringBuilder.Append("</td></tr>");
        }
        stringBuilder.AppendLine("</tbody></table>");

        return new HtmlResult(stringBuilder.ToString());
    }
    else
    {
        return Results.Ok(containers);
    }
}).WithName("List all containers").WithTags("Containers");

app.MapGet("/container/stats", async (IContainerService containerService, CancellationToken cancellationToken) =>
{
    var containers = await containerService.GetAll(cancellationToken);
    var running = containers.Where(x => x.Status != "exited");
    var result = new { Total = containers.Length, Running = running.Count(), HaveUpdate = containers.Count(x => x.HasUpdate), RunningAndHaveUpdate = running.Count(x => x.HasUpdate) };
    return Results.Ok(result);
}).WithName("Container statistics").WithTags("Containers");

app.Run();