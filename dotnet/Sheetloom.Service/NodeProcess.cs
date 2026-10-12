using System.Diagnostics;
using System.Runtime.InteropServices;

public sealed class NodeProcess(
    ServiceOptions options,
    TemplateClient client,
    IHostApplicationLifetime lifetime,
    ILogger<NodeProcess> logger) : IHostedService, IDisposable
{
    private Process? process;
    private volatile bool stopping;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            StartProcess();
            await WaitUntilReadyAsync(startup.Token);
            await client.InitializeAsync(startup.Token);
            EnsureRunning();
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }
    }

    private void StartProcess()
    {
        var start = new ProcessStartInfo("node") { UseShellExecute = false };
        start.ArgumentList.Add(options.NodeScript);
        start.Environment["NODE_PORT"] = options.NodePort.ToString();
        start.Environment["BODY_LIMIT_BYTES"] = options.BodyLimit.ToString();
        start.Environment["RENDER_TIMEOUT_MS"] = options.TimeoutMs.ToString();
        start.Environment["RENDER_CONCURRENCY"] = options.Concurrency.ToString();

        process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.Exited += OnExit;
        if (!process.Start())
        {
            throw new InvalidOperationException("Node could not start");
        }
    }

    private async Task WaitUntilReadyAsync(CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            EnsureRunning();
            using var probe = CancellationTokenSource.CreateLinkedTokenSource(token);
            probe.CancelAfter(1000);
            try
            {
                await client.CheckHealthAsync(probe.Token);
                return;
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
            }
        }
    }

    private void EnsureRunning()
    {
        if (process is null || process.HasExited)
        {
            throw new InvalidOperationException("Node exited during startup");
        }
    }

    private void OnExit(object? sender, EventArgs args)
    {
        if (stopping)
        {
            return;
        }

        logger.LogError("Private Node process exited; stopping the service");
        Environment.ExitCode = 1;
        lifetime.StopApplication();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        stopping = true;
        if (process is null)
        {
            return;
        }

        try
        {
            if (process.HasExited)
            {
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                SendSignal(process.Id, 15);
            }
            else
            {
                process.Kill(entireProcessTree: true);
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(6000);
            try
            {
                await process.WaitForExitAsync(deadline.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
            // The process was not started or already exited.
        }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int pid, int signal);

    public void Dispose() => process?.Dispose();
}
