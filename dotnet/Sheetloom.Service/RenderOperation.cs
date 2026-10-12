using System.Diagnostics;
using System.Text;
using Sheetloom;

public sealed class RenderOperation(
    ITemplateService templates,
    IWorkbookRenderer renderer,
    RenderCapacity capacity,
    ServiceOptions options,
    ILogger<RenderOperation> logger)
{
    public async Task<byte[]> RunAsync(string id, RenderRequest request, CancellationToken cancellationToken)
    {
        if (!await capacity.Slots.WaitAsync(0, cancellationToken))
        {
            throw new ServiceError(503, "busy", "Render capacity reached; retry later");
        }

        bool rendererOwnsSlot = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var elapsed = Stopwatch.StartNew();
        deadline.CancelAfter(options.TimeoutMs);
        try
        {
            string xml = await templates.RenderAsync(id, request.Data, deadline.Token);
            if (Encoding.UTF8.GetByteCount(xml) > ServiceOptions.XmlLimit)
            {
                throw new ServiceError(413, "output_too_large", "Rendered XML exceeds limit");
            }

            deadline.Token.ThrowIfCancellationRequested();
            CheckDeadline(elapsed);

            // ClosedXML is synchronous. Its slot remains occupied even if the HTTP deadline expires.
            var work = Task.Run(() => RenderWorkbook(xml, request.PrecisionAsDisplayed));
            rendererOwnsSlot = true;
            byte[]? output = await work.WaitAsync(deadline.Token);
            CheckDeadline(elapsed);
            return output ?? throw new ServiceError(422, "invalid_workbook", "Template produced invalid workbook XML");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimeoutError();
        }
        finally
        {
            if (!rendererOwnsSlot)
            {
                capacity.Slots.Release();
            }
        }
    }

    private byte[]? RenderWorkbook(string xml, bool precisionAsDisplayed)
    {
        try
        {
            return renderer.Render(xml, precisionAsDisplayed);
        }
        catch (Exception error)
        {
            logger.LogWarning("Workbook rendering failed ({ExceptionType})", error.GetType().Name);
            return null;
        }
        finally
        {
            capacity.Slots.Release();
        }
    }

    private void CheckDeadline(Stopwatch elapsed)
    {
        // Timer callbacks can be delayed under load; elapsed time still enforces the shared deadline.
        if (elapsed.ElapsedMilliseconds >= options.TimeoutMs)
        {
            throw TimeoutError();
        }
    }

    private static ServiceError TimeoutError() =>
        new(504, "render_timeout", "Workbook rendering timed out");
}
