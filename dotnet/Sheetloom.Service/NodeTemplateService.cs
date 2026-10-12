using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public interface ITemplateService : IHostedService
{
    IReadOnlyList<string> Ids { get; }
    Task CheckHealthAsync(CancellationToken token);
    Task<string> RenderAsync(string id, JsonElement data, CancellationToken token);
}

public sealed class NodeTemplateClient(HttpClient http)
{
    public async Task CheckHealthAsync(CancellationToken token)
    {
        using var response = await http.GetAsync("health", HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Template service unavailable");
    }

    public async Task<IReadOnlyList<string>> ReadIdsAsync(CancellationToken token)
    {
        using var response = await http.GetAsync("templates", HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var json = JsonDocument.Parse(await PublicApi.ReadBounded(stream, response.Content.Headers.ContentLength,
            1024 * 1024, "output_too_large", token));
        var ids = json.RootElement.GetProperty("templates").EnumerateArray().Select(value => value.GetString()!).ToArray();
        if (ids.Length == 0 || ids.Any(id => id is null || !Regex.IsMatch(id, "\\A[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}\\z")) || ids.Distinct().Count() != ids.Length)
            throw new InvalidOperationException("Invalid template discovery response");
        return ids;
    }

    public async Task<string> RenderAsync(string id, JsonElement data, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "render/" + Uri.EscapeDataString(id))
            { Content = new StringContent("{\"data\":" + data.GetRawText() + "}", Encoding.UTF8, "application/json") };
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode)
            {
                // Never forward private response bodies, which could contain template data.
                throw (int)response.StatusCode switch
                {
                    404 => new ServiceError(404, "unknown_template", "Unknown template"),
                    413 => new ServiceError(413, "output_too_large", "Rendered XML exceeds limit"),
                    422 => new ServiceError(422, "template_failed", "Template rendering failed"),
                    503 => new ServiceError(503, "busy", "Template capacity reached; retry later"),
                    504 => new ServiceError(504, "render_timeout", "Template rendering timed out"),
                    _ => new ServiceError(502, "renderer_failed", "Template service failed")
                };
            }
            if (response.Content.Headers.ContentType?.MediaType != "application/xml")
                throw new ServiceError(502, "renderer_failed", "Template service returned invalid output");
            using var stream = await response.Content.ReadAsStreamAsync(token);
            byte[] bytes = await PublicApi.ReadBounded(stream, response.Content.Headers.ContentLength,
                ServiceOptions.XmlLimit, "output_too_large", token);
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (HttpRequestException) { throw new ServiceError(502, "renderer_unavailable", "Template service unavailable"); }
        catch (IOException) { throw new ServiceError(502, "renderer_unavailable", "Template service unavailable"); }
        catch (DecoderFallbackException) { throw new ServiceError(502, "renderer_failed", "Template service returned invalid output"); }
    }
}

public sealed class NodeTemplateService : ITemplateService, IDisposable
{
    private readonly ServiceOptions options;
    private readonly IHostApplicationLifetime lifetime;
    private readonly ILogger<NodeTemplateService> logger;
    private readonly HttpClient http;
    private readonly NodeTemplateClient client;
    private Process? process;
    private volatile bool stopping;
    public IReadOnlyList<string> Ids { get; private set; } = [];

    public NodeTemplateService(ServiceOptions options, IHostApplicationLifetime lifetime, ILogger<NodeTemplateService> logger)
    {
        this.options = options; this.lifetime = lifetime; this.logger = logger;
        http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, MaxResponseDrainSize = 0 })
        { BaseAddress = new Uri($"http://127.0.0.1:{options.NodePort}/"), Timeout = Timeout.InfiniteTimeSpan };
        client = new NodeTemplateClient(http);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var start = new ProcessStartInfo("node") { UseShellExecute = false };
            start.ArgumentList.Add(options.NodeScript);
            start.Environment["NODE_PORT"] = options.NodePort.ToString();
            start.Environment["BODY_LIMIT_BYTES"] = options.BodyLimit.ToString();
            start.Environment["RENDER_TIMEOUT_MS"] = options.TimeoutMs.ToString();
            start.Environment["RENDER_CONCURRENCY"] = options.Concurrency.ToString();
            process = new Process { StartInfo = start, EnableRaisingEvents = true };
            process.Exited += OnExit;
            if (!process.Start()) throw new InvalidOperationException("Node could not start");
            while (true)
            {
                startup.Token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidOperationException("Node exited during startup");
                using var probe = CancellationTokenSource.CreateLinkedTokenSource(startup.Token);
                probe.CancelAfter(1000);
                try { await client.CheckHealthAsync(probe.Token); break; }
                catch (Exception) when (!startup.IsCancellationRequested) { await Task.Delay(100, startup.Token); }
            }
            Ids = await client.ReadIdsAsync(startup.Token);
            if (process.HasExited) throw new InvalidOperationException("Node exited during startup");
        }
        catch { await StopAsync(CancellationToken.None); throw; }
    }

    private void OnExit(object? sender, EventArgs args)
    {
        if (stopping) return;
        logger.LogError("Private Node process exited; stopping the service");
        Environment.ExitCode = 1;
        lifetime.StopApplication();
    }

    public Task CheckHealthAsync(CancellationToken token) => client.CheckHealthAsync(token);
    public Task<string> RenderAsync(string id, JsonElement data, CancellationToken token) => client.RenderAsync(id, data, token);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        stopping = true;
        if (process is null) return;
        try
        {
            if (process.HasExited) return;
            if (OperatingSystem.IsLinux()) SendSignal(process.Id, 15);
            else process.Kill(entireProcessTree: true);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(6000);
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }
        catch (InvalidOperationException) { /* Process was not started or already exited. */ }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int pid, int signal);

    public void Dispose() { http.Dispose(); process?.Dispose(); }
}
