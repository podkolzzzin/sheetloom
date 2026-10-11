using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using ClosedXML.Graphics;
using ExcelTemplateEngine;

var builder = WebApplication.CreateBuilder(args);
int port = ReadPositiveInteger(builder.Configuration, "RENDERER_PORT", 8081);
if (port > 65535) throw new InvalidOperationException("RENDERER_PORT must be a valid TCP port.");
int concurrency = ReadPositiveInteger(builder.Configuration, "RENDERER_CONCURRENCY", 2);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, port);
    options.Limits.MaxRequestBodySize = RenderEndpoint.MaxBodyBytes;
});
LoadOptions.DefaultGraphicEngine = new DefaultGraphicEngine(builder.Configuration["RENDERER_FALLBACK_FONT"] ?? "DejaVu Sans");
builder.Services.AddSingleton<IWorkbookRenderer, WorkbookRenderer>();
builder.Services.AddSingleton(new RenderCapacity(concurrency));
var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/render", RenderEndpoint.Handle);
app.Run();

static int ReadPositiveInteger(IConfiguration configuration, string key, int fallback)
{
    string? value = configuration[key];
    if (value is null) return fallback;
    if (!int.TryParse(value, out int number) || number < 1)
        throw new InvalidOperationException($"{key} must be a positive integer.");
    return number;
}

public partial class Program;

public sealed class RenderCapacity(int concurrency) : IDisposable
{
    public SemaphoreSlim Slots { get; } = new(concurrency, concurrency);
    public void Dispose() => Slots.Dispose();
}

public static class RenderEndpoint
{
    public const int MaxBodyBytes = 16 * 1024 * 1024;
    public const int MaxXmlCharacters = 8 * 1024 * 1024;
    public const string XlsxMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static async Task<IResult> Handle(HttpRequest request, IWorkbookRenderer renderer, RenderCapacity capacity,
        ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType()) return Error(415, "invalid_content_type", "Content-Type must be application/json.");
        if (request.ContentLength > MaxBodyBytes) return Error(413, "request_too_large", "Request exceeds the size limit.");
        if (!await capacity.Slots.WaitAsync(0, cancellationToken)) return Error(503, "renderer_busy", "Renderer capacity is exhausted.");
        try
        {
            using var body = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = await request.Body.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (body.Length + count > MaxBodyBytes) return Error(413, "request_too_large", "Request exceeds the size limit.");
                await body.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            using var json = JsonDocument.Parse(body.GetBuffer().AsMemory(0, (int)body.Length));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("xml", out var xmlValue) || xmlValue.ValueKind != JsonValueKind.String ||
                root.EnumerateObject().Any(property => property.Name is not ("xml" or "precisionAsDisplayed")))
                return Error(400, "invalid_request", "Expected xml and optional precisionAsDisplayed.");
            string xml = xmlValue.GetString()!;
            if (string.IsNullOrWhiteSpace(xml)) return Error(400, "invalid_request", "xml must be nonempty.");
            if (xml.Length > MaxXmlCharacters) return Error(413, "request_too_large", "XML exceeds the size limit.");
            bool precision = false;
            if (root.TryGetProperty("precisionAsDisplayed", out var precisionValue))
            {
                if (precisionValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    return Error(400, "invalid_request", "precisionAsDisplayed must be boolean.");
                precision = precisionValue.GetBoolean();
            }
            cancellationToken.ThrowIfCancellationRequested();
            byte[] bytes;
            try { bytes = renderer.Render(xml, precision); }
            catch (Exception exception)
            {
                // Exception messages may contain report data or paths; log only the exception class.
                loggerFactory.CreateLogger("Renderer").LogWarning("Workbook rendering failed ({ExceptionType})", exception.GetType().Name);
                return Error(422, "render_failed", "Workbook could not be rendered.");
            }
            return Results.File(bytes, XlsxMediaType, "workbook.xlsx");
        }
        catch (JsonException) { return Error(400, "invalid_json", "Body must be valid JSON."); }
        catch (BadHttpRequestException exception) when (exception.StatusCode == 413)
        { return Error(413, "request_too_large", "Request exceeds the size limit."); }
        finally { capacity.Slots.Release(); }
    }

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);
}
