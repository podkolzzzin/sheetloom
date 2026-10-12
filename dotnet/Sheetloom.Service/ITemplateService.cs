using System.Text.Json;

public interface ITemplateService
{
    IReadOnlyList<string> Ids
    {
        get;
    }
    Task CheckHealthAsync(CancellationToken token);
    Task<string> RenderAsync(string id, JsonElement data, CancellationToken token);
}
