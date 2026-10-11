using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sheetloom.Tests;

public sealed class ServiceLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public ServiceLifecycleTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task ChunkedBodyCannotBypassByteLimitAndSubsequentRequestSucceeds()
    {
        using var client = factory.CreateClient();
        using var content = new UnknownLengthContent(RenderEndpoint.MaxBodyBytes + 1);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync("/render", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("request_too_large", await response.Content.ReadAsStringAsync());
        using var good = await client.PostAsJsonAsync("/render", new { xml = RendererTests.Xml("<Row><Cell>after oversized stream</Cell></Row>") });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task PrecisionFlagSurvivesCompleteHttpResponse(bool precision)
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/render", new { xml = RendererTests.Xml("<Row><Cell data-type=\"Number\" style=\"number-format:0.00\">2.345</Cell></Row>"), precisionAsDisplayed = precision });
        response.EnsureSuccessStatusCode();
        using var archive = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        using var workbook = archive.GetEntry("xl/workbook.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        string? actual = XDocument.Load(workbook).Descendants(ns + "calcPr").Single().Attribute("fullPrecision")?.Value;
        Assert.Equal(precision, actual is "0" or "false");
    }

    [Theory]
    [InlineData("<!DOCTYPE Workbook [<!ENTITY private SYSTEM 'file:///secret-test-path'>]><Workbook><Worksheet name='x'><Row><Cell>&private;</Cell></Row></Worksheet></Workbook>")]
    [InlineData("<Workbook><Worksheet name='first'><Row><Cell>valid prefix</Cell></Row></Worksheet><Worksheet name='first'/></Workbook>")]
    [InlineData("<Workbook><Worksheet name='x'><Row><Cell style='font-size:private-invalid-number'>x</Cell></Row></Worksheet></Workbook>")]
    public async Task FailuresAtDifferentRenderStagesReturnSafeErrorsAndReleaseCapacity(string xml)
    {
        using var client = factory.CreateClient();
        // More failures than the default slot count proves failures cannot permanently consume slots.
        for (int i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync("/render", new { xml });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            string error = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("private", error); Assert.DoesNotContain("Exception", error);
        }
        using var health = await client.GetAsync("/health"); Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var good = await client.PostAsJsonAsync("/render", new { xml = RendererTests.Xml("<Row><Cell>recovered</Cell></Row>") });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task CancelledUploadReleasesCapacityBeforeRendererIsInvoked()
    {
        using var capacity = new RenderCapacity(1);
        var renderer = new RecordingRenderer();
        using var cancellation = new CancellationTokenSource();
        var input = new WaitingReadStream();
        var context = new DefaultHttpContext(); context.Request.ContentType = "application/json"; context.Request.Body = input;
        Task<IResult> pending = RenderEndpoint.Handle(context.Request, renderer, capacity, NullLoggerFactory.Instance, cancellation.Token);
        await input.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, capacity.Slots.CurrentCount);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, capacity.Slots.CurrentCount);
        Assert.Equal(0, renderer.Calls);
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"xml\":\"<Workbook/>\"}"));
        await RenderEndpoint.Handle(context.Request, renderer, capacity, NullLoggerFactory.Instance, CancellationToken.None);
        Assert.Equal(1, renderer.Calls);
    }

    [Fact]
    public async Task InterruptedUploadReleasesCapacityEvenWhenBodyReadThrows()
    {
        using var capacity = new RenderCapacity(1);
        var renderer = new RecordingRenderer();
        var context = new DefaultHttpContext(); context.Request.ContentType = "application/json"; context.Request.Body = new BrokenReadStream();
        await Assert.ThrowsAsync<IOException>(() => RenderEndpoint.Handle(context.Request, renderer, capacity, NullLoggerFactory.Instance, CancellationToken.None));
        Assert.Equal(1, capacity.Slots.CurrentCount); Assert.Equal(0, renderer.Calls);
    }

    private sealed class RecordingRenderer : IWorkbookRenderer
    {
        public int Calls { get; private set; }
        public byte[] Render(string xml, bool precisionAsDisplayed) { Calls++; return [1, 2, 3]; }
    }

    private sealed class UnknownLengthContent(int length) : HttpContent
    {
        protected override bool TryComputeLength(out long value) { value = 0; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var chunk = new byte[64 * 1024]; Array.Fill(chunk, (byte)'x');
            for (int remaining = length; remaining > 0; remaining -= chunk.Length)
                await stream.WriteAsync(chunk.AsMemory(0, Math.Min(remaining, chunk.Length)));
        }
    }

    private sealed class WaitingReadStream : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class BrokenReadStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new IOException("Connection terminated during upload."));
    }
}
