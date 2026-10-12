# Generate a workbook from your backend

[← README](../../README.md) · In an async C# method, use an existing `HttpClient` named `http` with its `BaseAddress` set to the service URL:

```csharp
using System.Net.Http.Json;

var body = new
{
    data = new
    {
        customer = "Example",
        items = new[] { new { name = "Paper", amount = 12.5 } }
    }
};
using var response = await http.PostAsJsonAsync("/v1/render/invoice", body);
response.EnsureSuccessStatusCode();
await using var output = File.Create("invoice.xlsx");
await response.Content.CopyToAsync(output);
```

**Result:** `invoice.xlsx` contains the generated workbook. Checking the status before creating the file prevents a JSON error response from being saved as Excel.

| Status | What to check |
| --- | --- |
| `400` | The `data` envelope and `options` types. |
| `404` | The template ID against `GET /v1/templates`. |
| `413` | Request and generated XML sizes against configured limits. |
| `415` | Set `Content-Type: application/json`. |
| `422` | Template input and the XML it produces. |
| `502` / `503` / `504` | Availability, load and render time; retry with backoff only when appropriate. |

Errors use `{"error":{"code":"...","message":"..."}}`. Read this body before `EnsureSuccessStatusCode()` if you need to map it to an application error. `GET /health` checks process availability, not spare rendering capacity. In Docker, use the container name on a shared network; `localhost` inside another container does not point to Sheetloom.
