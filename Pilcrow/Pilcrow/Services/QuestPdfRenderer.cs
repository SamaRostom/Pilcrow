using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace Pilcrow.Services;

public class QuestPdfRenderer : ITemplateRenderer
{
    public Task<byte[]> RenderAsync(JsonDocument doc, JsonDocument? data, CancellationToken ct)
    {
        var root = doc.RootElement;
        var values = data?.RootElement;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(ReadMargin(root), Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    if (!root.TryGetProperty("blocks", out var blocks)) return;

                    foreach (var block in blocks.EnumerateArray())
                    {
                        ct.ThrowIfCancellationRequested();
                        RenderBlock(col, block, values);
                    }
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);
    }

    private static void RenderBlock(ColumnDescriptor col, JsonElement block, JsonElement? values)
    {
        var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;

        switch (type)
        {
            case "heading":
                col.Item().Text(Bind(Text(block), values)).FontSize(20).Bold();
                break;

            case "text":
                col.Item().Text(Bind(Text(block), values));
                break;

            case "spacer":
                col.Item().Height(block.TryGetProperty("height", out var h) ? h.GetSingle() : 12);
                break;

            case "divider":
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                break;

            case "table":
                RenderTable(col, block, values);
                break;
        }
    }

    private static void RenderTable(ColumnDescriptor col, JsonElement block, JsonElement? values)
    {
        if (!block.TryGetProperty("columns", out var columns)) return;

        var headers = columns.EnumerateArray().ToList();

        // Rows come from the data payload, named by the block's "source".
        var rows = new List<JsonElement>();
        if (block.TryGetProperty("source", out var src) &&
            values?.TryGetProperty(src.GetString() ?? "", out var arr) == true &&
            arr.ValueKind == JsonValueKind.Array)
        {
            rows = arr.EnumerateArray().ToList();
        }

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                foreach (var _ in headers) def.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var h in headers)
                {
                    var label = h.TryGetProperty("label", out var l) ? l.GetString() : "";
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(5)
                          .Text(label).Bold();
                }
            });

            foreach (var row in rows)
            {
                foreach (var h in headers)
                {
                    var field = h.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "";
                    var value = row.TryGetProperty(field, out var v) ? v.ToString() : "";
                    table.Cell().Padding(5).Text(value);
                }
            }
        });
    }

    // Replaces {{name}} with the matching value from the data payload.
    private static string Bind(string text, JsonElement? values)
    {
        if (values is null || !text.Contains("{{")) return text;

        return System.Text.RegularExpressions.Regex.Replace(text, @"\{\{(\w+)\}\}", m =>
            values.Value.TryGetProperty(m.Groups[1].Value, out var v) ? v.ToString() : m.Value);
    }

    private static string Text(JsonElement block) =>
        block.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

    private static float ReadMargin(JsonElement root) =>
        root.TryGetProperty("page", out var p) && p.TryGetProperty("margin", out var m)
            ? m.GetSingle() : 40;
}