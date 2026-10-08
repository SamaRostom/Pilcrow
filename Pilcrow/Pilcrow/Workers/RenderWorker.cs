using Microsoft.EntityFrameworkCore;
using Pilcrow.Data;
using Pilcrow.Models;
using Pilcrow.Services;

namespace Pilcrow.Workers;

public class RenderWorker(
    IServiceProvider services,
    ILogger<RenderWorker> log) : BackgroundService
{
    private readonly string _instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..24];
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan IdleDelay     = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        log.LogInformation("Render worker {Id} started", _instanceId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var claimed = await TryProcessOneAsync(ct);
                if (!claimed) await Task.Delay(IdleDelay, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Worker loop error");
                await Task.Delay(IdleDelay, ct);
            }
        }
    }

    private async Task<bool> TryProcessOneAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PilcrowDbContext>();

        var job = await ClaimNextAsync(db, ct);
        if (job is null) return false;

        log.LogInformation("Claimed job {JobId}, attempt {Attempt}", job.Id, job.Attempts);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(RenderTimeout);

            var renderer = scope.ServiceProvider.GetRequiredService<ITemplateRenderer>();
            var blobs    = scope.ServiceProvider.GetRequiredService<IBlobStore>();

            var version = await db.TemplateVersions.AsNoTracking()
                .FirstAsync(v => v.Id == job.TemplateVersionId, timeout.Token);

            var pdf = await renderer.RenderAsync(version.Doc, job.DataPayload, timeout.Token);

            var key = $"results/{job.UserId}/{job.Id}.pdf";
            await blobs.SaveAsync(key, pdf, timeout.Token);

            job.Status         = JobStatus.Done;
            job.ResultBlobKey  = key;
            job.ResultBytes    = pdf.Length;
            job.FinishedAt     = DateTimeOffset.UtcNow;
            job.ExpiresAt      = DateTimeOffset.UtcNow.AddHours(24);
            job.LockedBy       = null;
            job.LeaseExpiresAt = null;

            await db.SaveChangesAsync(ct);
            log.LogInformation("Job {JobId} done, {Bytes} bytes", job.Id, pdf.Length);
        }
        catch (Exception ex)
        {
            var permanent = job.Attempts >= job.MaxAttempts;

            job.Status         = permanent ? JobStatus.Failed : JobStatus.Queued;
            job.ErrorCode      = ex is OperationCanceledException ? "timeout" : "render_error";
            job.ErrorMessage   = ex.Message[..Math.Min(500, ex.Message.Length)];
            job.LockedBy       = null;
            job.LeaseExpiresAt = null;
            if (permanent) job.FinishedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(CancellationToken.None);
            log.LogWarning(ex, "Job {JobId} failed ({Status})", job.Id, job.Status);
        }

        return true;
    }

    // One statement: find the oldest queued job, lock it, mark it running.
    // SKIP LOCKED lets other workers take different rows instead of waiting.
    private async Task<RenderJob?> ClaimNextAsync(PilcrowDbContext db, CancellationToken ct)
    {
        var sql = """
            UPDATE render_jobs
            SET    status = 'running',
                   attempts = attempts + 1,
                   locked_by = {0},
                   lease_expires_at = now() + {1}::interval,
                   started_at = COALESCE(started_at, now())
            WHERE  id = (
                SELECT id FROM render_jobs
                WHERE  status = 'queued'
                ORDER  BY priority DESC, queued_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING *
            """;

        var jobs = await db.RenderJobs
            .FromSqlRaw(sql, _instanceId, $"{LeaseDuration.TotalSeconds} seconds")
            .ToListAsync(ct);

        return jobs.FirstOrDefault();
    }
}