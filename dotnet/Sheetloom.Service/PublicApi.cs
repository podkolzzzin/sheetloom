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
            return Results.Ok(new
            {
                status = "ok"
            });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Error(503, "renderer_unavailable", "Template service unavailable");
        }
    }

    public static async Task<IResult> Render(
        string id, HttpContext context, ITemplateService templates, RenderOperation operation, ServiceOptions options)
    {
        if (!templates.Ids.Contains(id, StringComparer.Ordinal))
        {
            return Error(404, "unknown_template", "Unknown template");
        }

        var request = await RenderRequest.ReadAsync(context.Request, options.BodyLimit, context.RequestAborted);
        byte[] workbook = await operation.RunAsync(id, request, context.RequestAborted);
        return DownloadWorkbook(context.Response, id, workbook);
    }

    private static IResult DownloadWorkbook(HttpResponse response, string id, byte[] workbook)
    {
        response.Headers.ContentDisposition = $"attachment; filename=\"{id}.xlsx\"";
        response.Headers.CacheControl = "no-store";
        return Results.Bytes(workbook, XlsxMediaType);
    }

    public static IResult Error(int status, string code, string message) =>
        Results.Json(new
        {
            error = new
            {
                code,
                message
            }
        }, statusCode: status);
}
