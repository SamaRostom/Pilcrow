using System.Text.Json;

namespace Pilcrow.Models;

public enum JobSource { Template, Upload }
public enum JobStatus { Queued, Running, Done, Failed, Cancelled }

public class RenderJob
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public JobSource SourceType { get; set; }
    public Guid? TemplateVersionId { get; set; }
    public Guid? UploadId { get; set; }
    public JsonDocument? DataPayload { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Queued;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public string? LockedBy { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public string? IdempotencyKey { get; set; }
    public short Priority { get; set; }

    public string? ResultBlobKey { get; set; }
    public long? ResultBytes { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}