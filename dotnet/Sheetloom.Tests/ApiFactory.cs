using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Sheetloom.Tests;

public sealed class TemplateStub : ITemplateService
{
    public IReadOnlyList<string> Ids => ["report"];
    public Func<string, JsonElement, CancellationToken, Task<string>> Render { get; set; } =
        (_, data, _) => Task.FromResult(data.GetString()!);
    public Func<CancellationToken, Task> Health { get; set; } = _ => Task.CompletedTask;
    public Task CheckHealthAsync(CancellationToken token) => Health(token);
    public Task<string> RenderAsync(string id, JsonElement data, CancellationToken token) => Render(id, data, token);
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public TemplateStub Templates { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
    {
        services.Remove(services.Single(service => service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(NodeProcess)));
        services.RemoveAll<ITemplateService>();
        services.AddSingleton<ITemplateService>(Templates);
    });
}
