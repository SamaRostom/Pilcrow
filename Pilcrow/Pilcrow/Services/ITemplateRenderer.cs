using System.Text.Json;

namespace Pilcrow.Services;

public interface ITemplateRenderer
{
    Task<byte[]> RenderAsync(JsonDocument doc, JsonDocument? data, CancellationToken ct);
}

// Placeholder until the real PDF library is wired in.
public class StubRenderer : ITemplateRenderer
{
    public async Task<byte[]> RenderAsync(JsonDocument doc, JsonDocument? data, CancellationToken ct)
    {
        await Task.Delay(2000, ct);   // pretend rendering is slow
        var text = $"PDF placeholder\nDoc: {doc.RootElement}\nData: {data?.RootElement.ToString() ?? "none"}";
        return System.Text.Encoding.UTF8.GetBytes(text);
    }
}