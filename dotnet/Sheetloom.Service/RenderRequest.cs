using System.Text.Json;

public sealed record RenderRequest(JsonElement Data, bool PrecisionAsDisplayed)
{
    public static async Task<RenderRequest> ReadAsync(HttpRequest request, int bodyLimit, CancellationToken token)
    {
        if (!request.HasJsonContentType())
        {
            throw new ServiceError(415, "invalid_request", "Content-Type must be application/json");
        }

        byte[] input = await BoundedStream.ReadAsync(
            request.Body, request.ContentLength, bodyLimit, "body_too_large", token);
        try
        {
            using var json = JsonDocument.Parse(input);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data) ||
                root.EnumerateObject().Any(property => property.Name is not ("data" or "options")))
            {
                throw new ServiceError(400, "invalid_request", "Expected data and optional boolean options.precisionAsDisplayed");
            }

            bool precision = ReadPrecisionOption(root);
            return new RenderRequest(data.Clone(), precision);
        }
        catch (JsonException)
        {
            throw new ServiceError(400, "invalid_request", "Invalid request");
        }
    }

    private static bool ReadPrecisionOption(JsonElement root)
    {
        if (!root.TryGetProperty("options", out var settings))
        {
            return false;
        }

        if (settings.ValueKind != JsonValueKind.Object ||
            settings.EnumerateObject().Any(property => property.Name != "precisionAsDisplayed"))
        {
            throw new ServiceError(400, "invalid_request", "Invalid options");
        }

        if (!settings.TryGetProperty("precisionAsDisplayed", out var value))
        {
            return false;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new ServiceError(400, "invalid_request", "Invalid precision option");
        }

        return value.GetBoolean();
    }
}
