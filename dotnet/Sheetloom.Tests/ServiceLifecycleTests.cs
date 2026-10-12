using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Sheetloom.Tests;

public sealed class ServiceLifecycleTests
{
    [Fact]
    public async Task ChunkedBodyCannotBypassByteLimitAndSubsequentRequestSucceeds()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var content = new UnknownLengthContent(10485760 + 1);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await client.PostAsync("/v1/render/report", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Contains("body_too_large", await response.Content.ReadAsStringAsync());
        using var good = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = WorkbookFixture.Xml("<Row><Cell>after oversized stream</Cell></Row>")
        });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrecisionFlagSurvivesCompleteHttpResponse(bool precision)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = WorkbookFixture.Xml("<Row><Cell data-type=\"Number\" style=\"number-format:0.00\">2.345</Cell></Row>"),
            options = new
            {
                precisionAsDisplayed = precision
            }
        });
        response.EnsureSuccessStatusCode();
        Assert.Equal("attachment; filename=\"report.xlsx\"", response.Content.Headers.ContentDisposition!.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
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
    public async Task RenderFailuresReturnSafeErrorsAndReleaseCapacity(string xml)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        for (int i = 0; i < 3; i++)
        {
            using var response = await client.PostAsJsonAsync("/v1/render/report", new
            {
                data = xml
            });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            string error = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("private", error);
            Assert.DoesNotContain("Exception", error);
        }
        using var good = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = WorkbookFixture.Xml("<Row><Cell>recovered</Cell></Row>")
        });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task DiscoveryAndUnknownRoutesPreservePublicContract()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal("{\"templates\":[\"report\"]}", await client.GetStringAsync("/v1/templates"));
        using var missing = await client.PostAsJsonAsync("/v1/render/missing", new
        {
            data = 0
        });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("unknown_template", await missing.Content.ReadAsStringAsync());
        using var old = await client.PostAsJsonAsync("/render", new
        {
            xml = "<Workbook/>"
        });
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        Assert.Contains("not_found", await old.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PrimitiveDataIsNotCoercedAndUnhealthyNodeIsReported()
    {
        using var factory = new ApiFactory();
        var received = new List<string>();
        factory.Templates.Render = (_, data, _) => { received.Add(data.GetRawText()); return Task.FromResult(WorkbookFixture.Xml("<Row><Cell>ok</Cell></Row>")); };
        using var client = factory.CreateClient();
        foreach (string data in new[] { "0", "false", "null", "[]", "\"\"" })
        {
            using var response = await client.PostAsync("/v1/render/report", new StringContent("{\"data\":" + data + "}", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(new[] { "0", "false", "null", "[]", "\"\"" }, received);
        factory.Templates.Health = _ => throw new HttpRequestException("PRIVATE");
        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.DoesNotContain("PRIVATE", await health.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TemplateDeadlineCancelsPrivateWorkAndReleasesSlot()
    {
        using var factory = new ApiFactory();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.Templates.Render = async (_, _, token) =>
        {
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                return "";
            }
            finally { cancelled.TrySetResult(); }
        };
        using var constrained = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ServiceOptions>();
            services.AddSingleton(new ServiceOptions(8080, 8081, 10485760, 150, 1, "unused"));
            services.RemoveAll<RenderCapacity>();
            services.AddSingleton(new RenderCapacity(1));
        }));
        using var client = constrained.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = 0
        });
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Contains("render_timeout", await response.Content.ReadAsStringAsync());
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        factory.Templates.Render = (_, _, _) => Task.FromResult(WorkbookFixture.Xml("<Row><Cell>recovered</Cell></Row>"));
        using var good = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = 0
        });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task OneDeadlineCoversTemplateAndWorkbookStagesTogether()
    {
        using var factory = new ApiFactory();
        factory.Templates.Render = async (_, _, token) =>
        {
            await Task.Delay(300, token);
            return "<Workbook/>";
        };
        var renderer = new DelayedRenderer();
        using var constrained = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWorkbookRenderer>();
            services.AddSingleton<IWorkbookRenderer>(renderer);
            services.RemoveAll<ServiceOptions>();
            services.AddSingleton(new ServiceOptions(8080, 8081, 10485760, 500, 1, "unused"));
            services.RemoveAll<RenderCapacity>();
            services.AddSingleton(new RenderCapacity(1));
        }));
        using var client = constrained.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = 0
        });
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        await renderer.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var capacity = constrained.Services.GetRequiredService<RenderCapacity>();
        Assert.True(await capacity.Slots.WaitAsync(TimeSpan.FromSeconds(5)));
        capacity.Slots.Release();
    }

    private sealed class DelayedRenderer : IWorkbookRenderer
    {
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[] Render(string xml, bool precisionAsDisplayed)
        {
            try
            {
                Thread.Sleep(300);
                return [1, 2, 3];
            }
            finally { Finished.TrySetResult(); }
        }
    }

    [Fact]
    public async Task TimedOutClosedXmlWorkKeepsItsSlotUntilItActuallyFinishes()
    {
        using var factory = new ApiFactory();
        var renderer = new BlockingRenderer();
        using var constrained = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWorkbookRenderer>();
            services.AddSingleton<IWorkbookRenderer>(renderer);
            services.RemoveAll<ServiceOptions>();
            services.AddSingleton(new ServiceOptions(8080, 8081, 10485760, 200, 1, "unused"));
            services.RemoveAll<RenderCapacity>();
            services.AddSingleton(new RenderCapacity(1));
        }));
        using var client = constrained.CreateClient();
        try
        {
            using var first = await client.PostAsJsonAsync("/v1/render/report", new
            {
                data = "<Workbook/>"
            });
            Assert.Equal(HttpStatusCode.GatewayTimeout, first.StatusCode);
            Assert.True(renderer.Entered.Task.IsCompleted);
            using var second = await client.PostAsJsonAsync("/v1/render/report", new
            {
                data = "<Workbook/>"
            });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        }
        finally { renderer.Release.Set(); }
        await renderer.Finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var capacity = constrained.Services.GetRequiredService<RenderCapacity>();
        for (int i = 0; i < 100 && capacity.Slots.CurrentCount == 0; i++)
            await Task.Delay(10);
        using var recovered = await client.PostAsJsonAsync("/v1/render/report", new
        {
            data = "<Workbook/>"
        });
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task CancelledAndInterruptedUploadsNeverInvokeTemplateOrRenderer()
    {
        using var capacity = new RenderCapacity(1);
        var renderer = new BlockingRenderer();
        var templates = new TemplateStub { Render = (_, _, _) => throw new Exception("Should not run") };
        using var cancellation = new CancellationTokenSource();
        var input = new WaitingReadStream();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Body = input;
        context.RequestAborted = cancellation.Token;
        var options = new ServiceOptions(8080, 8081, 1000, 1000, 1, "unused");
        var operation = new RenderOperation(templates, renderer, capacity, options, NullLogger<RenderOperation>.Instance);
        var pending = PublicApi.Render("report", context, templates, operation, options);
        await input.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        context.RequestAborted = CancellationToken.None;
        context.Request.Body = new BrokenReadStream();
        await Assert.ThrowsAsync<IOException>(() => PublicApi.Render("report", context, templates, operation, options));
        Assert.Equal(1, capacity.Slots.CurrentCount);
        Assert.False(renderer.Entered.Task.IsCompleted);
    }

    private sealed class BlockingRenderer : IWorkbookRenderer
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
        public byte[] Render(string xml, bool precisionAsDisplayed)
        {
            Entered.TrySetResult();
            try
            {
                if (!Release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException();
                return [1, 2, 3];
            }
            finally { Finished.TrySetResult(); }
        }
    }
    private sealed class UnknownLengthContent(int length) : HttpContent
    {
        protected override bool TryComputeLength(out long value)
        {
            value = 0;
            return false;
        }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var chunk = new byte[64 * 1024];
            Array.Fill(chunk, (byte)'x');
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
