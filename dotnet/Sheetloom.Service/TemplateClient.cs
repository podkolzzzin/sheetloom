using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed class TemplateClient(HttpClient http) : ITemplateService
{
    public IReadOnlyList<string> Ids { get; private set; } = [];

    public async Task InitializeAsync(CancellationToken token)
    {
        Ids = await ReadIdsAsync(token);
    }

    public async Task CheckHealthAsync(CancellationToken token)
    {
        using var response = await http.GetAsync("health", HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException("Template service unavailable");
        }
    }

    public async Task<IReadOnlyList<string>> ReadIdsAsync(CancellationToken token)
    {
        using var response = await http.GetAsync("templates", HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var json = JsonDocument.Parse(await BoundedStream.ReadAsync(stream, response.Content.Headers.ContentLength,
            1024 * 1024, "output_too_large", token));
        var ids = json.RootElement.GetProperty("templates").EnumerateArray().Select(value => value.GetString()!).ToArray();
        if (ids.Length == 0 || ids.Any(id => id is null || !Regex.IsMatch(id, "\\A[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}\\z")) || ids.Distinct().Count() != ids.Length)
        {
            throw new InvalidOperationException("Invalid template discovery response");
        }
        return ids;
    }

    public async Task<string> RenderAsync(string id, JsonElement data, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "render/" + Uri.EscapeDataString(id))
            {
                Content = new StringContent("{\"data\":" + data.GetRawText() + "}", Encoding.UTF8, "application/json")
            };
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
            {
                throw new ServiceError(502, "renderer_failed", "Template service returned invalid output");
            }
            using var stream = await response.Content.ReadAsStreamAsync(token);
            byte[] bytes = await BoundedStream.ReadAsync(stream, response.Content.Headers.ContentLength,
                ServiceOptions.XmlLimit, "output_too_large", token);
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (HttpRequestException)
        {
            throw new ServiceError(502, "renderer_unavailable", "Template service unavailable");
        }
        catch (IOException)
        {
            throw new ServiceError(502, "renderer_unavailable", "Template service unavailable");
        }
        catch (DecoderFallbackException)
        {
            throw new ServiceError(502, "renderer_failed", "Template service returned invalid output");
        }
    }
}

