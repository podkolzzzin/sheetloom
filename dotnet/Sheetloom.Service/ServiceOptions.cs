public sealed record ServiceOptions(int Port, int NodePort, int BodyLimit, int TimeoutMs, int Concurrency, string NodeScript)
{
    public const int XmlLimit = 8 * 1024 * 1024;

    public static ServiceOptions Read(IConfiguration config)
    {
        int Integer(string name, int fallback, int max)
        {
            string? value = config[name];
            if (value is null)
            {
                return fallback;
            }

            if (!int.TryParse(value, out int number) || number < 1 || number > max)
            {
                throw new InvalidOperationException($"Invalid {name}");
            }

            return number;
        }
        var options = new ServiceOptions(
            Port: Integer("PORT", 8080, 65535),
            NodePort: Integer("NODE_PORT", 8081, 65535),
            BodyLimit: Integer("BODY_LIMIT_BYTES", 10485760, 67108864),
            TimeoutMs: Integer("RENDER_TIMEOUT_MS", 120000, 600000),
            Concurrency: Integer("RENDER_CONCURRENCY", 2, 32),
            NodeScript: config["NODE_SCRIPT"] ?? "/app/src/main.js");
        if (options.Port == options.NodePort)
        {
            throw new InvalidOperationException("PORT and NODE_PORT must differ.");
        }

        return options;
    }
}

public sealed class ServiceError(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class RenderCapacity(int concurrency) : IDisposable
{
    public SemaphoreSlim Slots { get; } = new(concurrency, concurrency);
    public void Dispose() => Slots.Dispose();
}
