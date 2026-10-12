using ClosedXML.Excel;
using ClosedXML.Graphics;
using Sheetloom;

var builder = WebApplication.CreateBuilder(args);
var options = ServiceOptions.Read(builder.Configuration);
builder.WebHost.ConfigureKestrel(server =>
{
    server.ListenAnyIP(options.Port);
    server.Limits.MaxRequestBodySize = options.BodyLimit;
    server.AddServerHeader = false;
});
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(10));
LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine(builder.Configuration["RENDERER_FALLBACK_FONT"] ?? "DejaVu Sans");
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IWorkbookRenderer, WorkbookRenderer>();
builder.Services.AddSingleton(new RenderCapacity(options.Concurrency));
builder.Services.AddSingleton<RenderOperation>();
builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseProxy = false,
    MaxResponseDrainSize = 0
})
{
    BaseAddress = new Uri($"http://127.0.0.1:{options.NodePort}/"),
    Timeout = Timeout.InfiniteTimeSpan
});
builder.Services.AddSingleton<TemplateClient>();
builder.Services.AddSingleton<ITemplateService>(services => services.GetRequiredService<TemplateClient>());
builder.Services.AddHostedService<NodeProcess>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        // The client disconnected; there is no response to send.
    }
    catch (ServiceError error)
    {
        await PublicApi.Error(error.Status, error.Code, error.Message).ExecuteAsync(context);
    }
    catch (BadHttpRequestException error)
    {
        await PublicApi.Error(error.StatusCode, error.StatusCode == 413 ? "body_too_large" : "invalid_request", "Invalid request").ExecuteAsync(context);
    }
    catch (Exception error)
    {
        app.Logger.LogWarning("Request failed ({ExceptionType})", error.GetType().Name);
        await PublicApi.Error(500, "internal_error", "Internal error").ExecuteAsync(context);
    }
});
app.MapGet("/health", PublicApi.Health);
app.MapGet("/v1/templates", (ITemplateService templates) => Results.Ok(new { templates = templates.Ids }));
app.MapPost("/v1/render/{id}", PublicApi.Render);
app.MapFallback(() => PublicApi.Error(404, "not_found", "Route not found"));
app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogInformation("ready: public .NET API on port {Port}", options.Port));
app.Run();

public partial class Program;
