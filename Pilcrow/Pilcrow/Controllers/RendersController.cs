using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pilcrow.Data;
using Pilcrow.Dtos;
using Pilcrow.Models;
using Pilcrow.Services;
using System.Text.Json;

namespace Pilcrow.Controllers;

[Route("renders")]
public class RendersController(PilcrowDbContext db) : ApiControllerBase
{

    [HttpPost]
    public async Task<ActionResult<RenderJobAccepted>> Create(CreateRenderRequest req)
    {
        // A retried request must not render twice.
        if (!string.IsNullOrWhiteSpace(req.IdempotencyKey))
        {
            var existing = await db.RenderJobs.AsNoTracking().FirstOrDefaultAsync(j =>
                j.UserId == UserId && j.IdempotencyKey == req.IdempotencyKey);

            if (existing is not null)
                return Accepted(new RenderJobAccepted(existing.Id, existing.Status.ToString().ToLowerInvariant()));
        }

        // Pin the version that is current right now, so the output stays
        // reproducible even after the template is edited.
        var version = await db.Templates
            .Where(t => t.Id == req.TemplateId && t.UserId == UserId && t.DeletedAt == null)
            .Join(db.TemplateVersions,
                  t => new { Id = t.Id, V = t.CurrentVersion },
                  v => new { Id = v.TemplateId, V = v.VersionNo },
                  (t, v) => v.Id)
            .FirstOrDefaultAsync();

        if (version == Guid.Empty) return NotFound("Template not found.");

        var job = new RenderJob
        {
            UserId            = UserId,
            SourceType        = JobSource.Template,
            TemplateVersionId = version,
            DataPayload       = req.Data is null ? null : JsonDocument.Parse(req.Data.Value.GetRawText()),
            IdempotencyKey    = string.IsNullOrWhiteSpace(req.IdempotencyKey) ? null : req.IdempotencyKey
        };

        db.RenderJobs.Add(job);
        await db.SaveChangesAsync();

        return Accepted(new RenderJobAccepted(job.Id, "queued"));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RenderJobStatus>> Get(Guid id)
    {
        var job = await db.RenderJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == UserId);

        if (job is null) return NotFound();

        // Local disk for now; becomes a signed blob URL later.
        var url = job.Status == JobStatus.Done && job.ResultBlobKey is not null
            ? $"/renders/{job.Id}/download"
            : null;

        return new RenderJobStatus(
            job.Id,
            job.Status.ToString().ToLowerInvariant(),
            url,
            job.ErrorCode,
            job.QueuedAt,
            job.FinishedAt);
    }
        [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromServices] IBlobStore blobs)
    {
        var job = await db.RenderJobs.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.UserId == UserId);

        if (job is null || job.Status != JobStatus.Done || job.ResultBlobKey is null)
            return NotFound();

        if (job.ExpiresAt < DateTimeOffset.UtcNow)
            return StatusCode(410, "This result has expired. Render again.");

        var bytes = await blobs.ReadAsync(job.ResultBlobKey, HttpContext.RequestAborted);
        return File(bytes, "application/pdf", $"{job.Id}.pdf");
    }
}