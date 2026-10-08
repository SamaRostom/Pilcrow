using System.Text.Json;

namespace Pilcrow.Models;

public class TemplateVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TemplateId { get; set; }
    public int VersionNo { get; set; }
    public JsonDocument Doc { get; set; } = null!;
    public int BlockCount { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Template Template { get; set; } = null!;
}