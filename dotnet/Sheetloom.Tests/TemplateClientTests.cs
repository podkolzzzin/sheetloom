using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Sheetloom.Tests;

public sealed class TemplateClientTests
{
    [Theory]
    [InlineData(400, 502, "renderer_failed")]
    [InlineData(404, 404, "unknown_template")]
    [InlineData(413, 413, "output_too_large")]
    [InlineData(422, 422, "template_failed")]
    [InlineData(500, 502, "renderer_failed")]
    [InlineData(503, 503, "busy")]
    [InlineData(504, 504, "render_timeout")]
    public async Task PrivateErrorsAreMappedWithoutForwardingResponseBodies(int status, int expected, string code)
    {
        await using var server = await StubServer.Start(async context =>
        {
            context.Response.StatusCode = status;
            await context.Response.WriteAsync("PRIVATE upstream response");
        });
        using var data = JsonDocument.Parse("null");
        var error = await Assert.ThrowsAsync<ServiceError>(() => server.Client.RenderAsync("report", data.RootElement, CancellationToken.None));
        Assert.Equal(expected, error.Status);
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain("PRIVATE", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineCoversHeadersAndPartialResponseBodies(bool partialBody)
    {
        int calls = 0;
        await using var server = await StubServer.Start(async context =>
        {
            if (Interlocked.Increment(ref calls) > 1)
            {
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsync("<Workbook/>");
                return;
            }
            if (partialBody)
            {
                context.Response.ContentType = "application/xml";
                await context.Response.WriteAsync("<Workbook>");
                await context.Response.Body.FlushAsync();
            }
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        using var data = JsonDocument.Parse("null");
        using var deadline = new CancellationTokenSource(200);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.Client.RenderAsync("report", data.RootElement, deadline.Token));
        Assert.Equal("<Workbook/>", await server.Client.RenderAsync("report", data.RootElement, CancellationToken.None));
    }

    [Fact]
    public async Task FailedHealthCheckClosesUnfinishedResponse()
    {
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = await StubServer.Start(async context =>
        {
            using var registration = context.RequestAborted.Register(() => closed.TrySetResult());
            context.Response.StatusCode = 503;
            await context.Response.StartAsync();
            await context.Response.Body.FlushAsync();
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });
        using var deadline = new CancellationTokenSource(5000);
        await Assert.ThrowsAsync<HttpRequestException>(() => server.Client.CheckHealthAsync(deadline.Token));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownLengthAndChunkedXmlCannotExceedByteLimit(bool knownLength)
    {
        await using var server = await StubServer.Start(async context =>
        {
            context.Response.ContentType = "application/xml";
            if (knownLength)
                context.Response.ContentLength = ServiceOptions.XmlLimit + 2;
            await context.Response.WriteAsync(new string('ї', ServiceOptions.XmlLimit / 2 + 1));
        });
        using var data = JsonDocument.Parse("null");
        var error = await Assert.ThrowsAsync<ServiceError>(() => server.Client.RenderAsync("report", data.RootElement, CancellationToken.None));
        Assert.Equal(413, error.Status);
    }

    [Fact]
    public async Task RequestDataIsForwardedWithoutEscapingInflationOrOptions()
    {
        string? received = null;
        await using var server = await StubServer.Start(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            received = await reader.ReadToEndAsync();
            context.Response.ContentType = "application/xml";
            await context.Response.WriteAsync("<Workbook/>");
        });
        using var data = JsonDocument.Parse("[\"ї<&\",null,0,false]");
        Assert.Equal("<Workbook/>", await server.Client.RenderAsync("report", data.RootElement, CancellationToken.None));
        Assert.Equal("{\"data\":[\"ї<&\",null,0,false]}", received);
    }

    [Theory]
    [InlineData("text/plain", false)]
    [InlineData("application/xml", true)]
    public async Task InvalidContentTypeAndUtf8NeverReachWorkbookRenderer(string contentType, bool invalidUtf8)
    {
        await using var server = await StubServer.Start(async context =>
        {
            context.Response.ContentType = contentType;
            await context.Response.Body.WriteAsync(invalidUtf8 ? new byte[] { 0xff } : Encoding.UTF8.GetBytes("PRIVATE"));
        });
        using var data = JsonDocument.Parse("null");
        var error = await Assert.ThrowsAsync<ServiceError>(() => server.Client.RenderAsync("report", data.RootElement, CancellationToken.None));
        Assert.Equal(502, error.Status);
        Assert.DoesNotContain("PRIVATE", error.Message);
    }

    [Fact]
    public async Task BrokenConnectionReturnsSafeBadGateway()
    {
        await using var server = await StubServer.Start(context => { context.Abort(); return Task.CompletedTask; });
        using var data = JsonDocument.Parse("null");
        var error = await Assert.ThrowsAsync<ServiceError>(() => server.Client.RenderAsync("report", data.RootElement, CancellationToken.None));
        Assert.Equal(502, error.Status);
    }

    private sealed class StubServer(WebApplication app, HttpClient http) : IAsyncDisposable
    {
        public TemplateClient Client { get; } = new(http);
        public static async Task<StubServer> Start(RequestDelegate handler)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            var app = builder.Build();
            app.Run(async context =>
            {
                try
                {
                    await handler(context);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            });
            await app.StartAsync();
            var http = new HttpClient(new SocketsHttpHandler { MaxResponseDrainSize = 0, AllowAutoRedirect = false, UseProxy = false })
            {
                BaseAddress = new Uri(app.Urls.Single()),
                Timeout = Timeout.InfiniteTimeSpan
            };
            return new StubServer(app, http);
        }
        public async ValueTask DisposeAsync()
        {
            http.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
