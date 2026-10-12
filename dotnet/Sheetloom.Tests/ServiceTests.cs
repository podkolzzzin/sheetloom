using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sheetloom.Tests;

public sealed class ServiceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory;
    public ServiceTests(ApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task HealthAndRenderServeAnActualWorkbook()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        using var response = await client.PostAsJsonAsync("/v1/render/report", new { data = RendererTests.Xml("<Row><Cell>HTTP result</Cell></Row>") });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PublicApi.XlsxMediaType, response.Content.Headers.ContentType!.MediaType);
        using var book = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal("HTTP result", book.Worksheet(1).Cell("A1").GetString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("12")]
    [InlineData("{\"data\":0,\"options\":null}")]
    [InlineData("{\"data\":0,\"options\":[]}")]
    [InlineData("{\"data\":0,\"options\":{\"precisionAsDisplayed\":\"true\"}}")]
    [InlineData("{\"data\":0,\"options\":{\"unknown\":true}}")]
    [InlineData("{\"data\":0,\"unknown\":true}")]
    public async Task InvalidRequestsReturnStructuredErrors(string body)
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/v1/render/report", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"code\":\"invalid_request\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WrongContentTypeIsRejected()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync("/v1/render/report", new StringContent("{}"))).StatusCode);
    }

    [Fact]
    public async Task RenderFailureDoesNotExposeSensitiveInputAndSlotIsReleased()
    {
        using var client = factory.CreateClient();
        using var bad = await client.PostAsJsonAsync("/v1/render/report", new { data = "<Workbook secret='private-path'" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        Assert.DoesNotContain("private-path", await bad.Content.ReadAsStringAsync());
        using var good = await client.PostAsJsonAsync("/v1/render/report", new { data = RendererTests.Xml("<Row><Cell>recovered</Cell></Row>") });
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task XmlByteLimitIsEnforced()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/render/report", new { data = new string('x', ServiceOptions.XmlLimit + 1) });
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task BodyLimitIsEnforced()
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/v1/render/report", new StringContent(new string('x', 10485760 + 1), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task SaturatedRendererRejectsWorkAndRecovers()
    {
        var blocking = new BlockingRenderer();
        using var constrained = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWorkbookRenderer>(); services.AddSingleton<IWorkbookRenderer>(blocking);
            services.RemoveAll<RenderCapacity>(); services.AddSingleton(new RenderCapacity(1));
        }));
        using var client = constrained.CreateClient();
        var first = client.PostAsJsonAsync("/v1/render/report", new { data = "<Workbook/>" });
        try
        {
            await blocking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using var rejected = await client.PostAsJsonAsync("/v1/render/report", new { data = "<Workbook/>" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, rejected.StatusCode);
        }
        finally { blocking.Release.Set(); }
        using var completed = await first; Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var next = await client.PostAsJsonAsync("/v1/render/report", new { data = "<Workbook/>" });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    private sealed class BlockingRenderer : IWorkbookRenderer
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(false);
        public byte[] Render(string xml, bool precisionAsDisplayed)
        {
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException();
            return [1, 2, 3];
        }
    }
}
