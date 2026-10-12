using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Sheetloom;

public static class PublicApi
{
    public const string XlsxMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static async Task<IResult> Health(ITemplateService templates, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(2000);
        try
        {
            await templates.CheckHealthAsync(deadline.Token);
            return Results.Ok(new { status = "ok" });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        { return Error(503, "renderer_unavailable", "Template service unavailable"); }
    }

    public static async Task<IResult> Render(string id, HttpContext context, ITemplateService templates,
        IWorkbookRenderer renderer, RenderCapacity capacity, ServiceOptions options, ILoggerFactory loggerFactory)
    {
        if (!templates.Ids.Contains(id, StringComparer.Ordinal)) return Error(404, "unknown_template", "Unknown template");
        if (!context.Request.HasJsonContentType()) return Error(415, "invalid_request", "Content-Type must be application/json");
        byte[] input = await ReadBounded(context.Request.Body, context.Request.ContentLength, options.BodyLimit,
            "body_too_large", context.RequestAborted);
        JsonDocument json;
        try { json = JsonDocument.Parse(input); }
        catch (JsonException) { return Error(400, "invalid_request", "Invalid request"); }
        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) ||
                root.EnumerateObject().Any(p => p.Name is not ("data" or "options")))
                return Error(400, "invalid_request", "Expected data and optional boolean options.precisionAsDisplayed");
            bool precision = false;
            if (root.TryGetProperty("options", out var settings))
            {
                if (settings.ValueKind != JsonValueKind.Object || settings.EnumerateObject().Any(p => p.Name != "precisionAsDisplayed"))
                    return Error(400, "invalid_request", "Invalid options");
                if (settings.TryGetProperty("precisionAsDisplayed", out var value))
                {
                    if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return Error(400, "invalid_request", "Invalid precision option");
                    precision = value.GetBoolean();
                }
            }
            if (!await capacity.Slots.WaitAsync(0, context.RequestAborted)) return Error(503, "busy", "Render capacity reached; retry later");
            bool rendererOwnsSlot = false;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            var elapsed = Stopwatch.StartNew();
            deadline.CancelAfter(options.TimeoutMs);
            try
            {
                string xml = await templates.RenderAsync(id, data, deadline.Token);
                if (Encoding.UTF8.GetByteCount(xml) > ServiceOptions.XmlLimit) return Error(413, "output_too_large", "Rendered XML exceeds limit");
                deadline.Token.ThrowIfCancellationRequested();
                if (elapsed.ElapsedMilliseconds >= options.TimeoutMs) return Error(504, "render_timeout", "Workbook rendering timed out");
                var logger = loggerFactory.CreateLogger("Renderer");
                // ClosedXML is synchronous. Its slot remains occupied even if the HTTP deadline expires.
                var work = Task.Run(() =>
                {
                    try { return (Bytes: renderer.Render(xml, precision), Failed: false); }
                    catch (Exception error)
                    {
                        logger.LogWarning("Workbook rendering failed ({ExceptionType})", error.GetType().Name);
                        return (Bytes: Array.Empty<byte>(), Failed: true);
                    }
                    finally { capacity.Slots.Release(); }
                });
                rendererOwnsSlot = true;
                var output = await work.WaitAsync(deadline.Token);
                // Timer callbacks can be delayed under load; elapsed time still enforces the shared deadline.
                if (elapsed.ElapsedMilliseconds >= options.TimeoutMs) return Error(504, "render_timeout", "Workbook rendering timed out");
                if (output.Failed) return Error(422, "invalid_workbook", "Template produced invalid workbook XML");
                context.Response.Headers.ContentDisposition = $"attachment; filename=\"{id}.xlsx\"";
                context.Response.Headers.CacheControl = "no-store";
                return Results.Bytes(output.Bytes, XlsxMediaType);
            }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
            { return Error(504, "render_timeout", "Workbook rendering timed out"); }
            finally { if (!rendererOwnsSlot) capacity.Slots.Release(); }
        }
    }

    public static async Task<byte[]> ReadBounded(Stream stream, long? length, int limit, string code, CancellationToken token)
    {
        if (length > limit) throw new ServiceError(413, code, "Size limit exceeded");
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (output.Length + count > limit) throw new ServiceError(413, code, "Size limit exceeded");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        return output.ToArray();
    }

    public static IResult Error(int status, string code, string message) =>
        Results.Json(new { error = new { code, message } }, statusCode: status);
}
