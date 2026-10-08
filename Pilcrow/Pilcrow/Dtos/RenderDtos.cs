using System.Text.Json;

namespace Pilcrow.Dtos;

public record CreateRenderRequest(Guid TemplateId, JsonElement? Data, string? IdempotencyKey);

public record RenderJobAccepted(Guid JobId, string Status);

public record RenderJobStatus(Guid JobId, string Status, string? DownloadUrl,
                              string? ErrorCode, DateTimeOffset QueuedAt,
                              DateTimeOffset? FinishedAt);