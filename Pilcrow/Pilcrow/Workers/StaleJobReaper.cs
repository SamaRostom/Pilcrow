using Microsoft.EntityFrameworkCore;
using Pilcrow.Data;

namespace Pilcrow.Workers;

public class StaleJobReaper(IServiceProvider services, ILogger<StaleJobReaper> log)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PilcrowDbContext>();

                // A job stuck in 'running' past its lease means its worker died.
                // Back to the queue, unless it has used up its attempts.
                var reclaimed = await db.Database.ExecuteSqlRawAsync("""
                    UPDATE render_jobs
                    SET    status     = CASE WHEN attempts >= max_attempts THEN 'failed' ELSE 'queued' END,
                           error_code = CASE WHEN attempts >= max_attempts THEN 'worker_timeout' ELSE error_code END,
                           finished_at = CASE WHEN attempts >= max_attempts THEN now() ELSE finished_at END,
                           locked_by = NULL,
                           lease_expires_at = NULL
                    WHERE  status = 'running' AND lease_expires_at < now()
                    """, ct);

                if (reclaimed > 0)
                    log.LogWarning("Reclaimed {Count} stale job(s)", reclaimed);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                log.LogError(ex, "Reaper error");
            }

            await Task.Delay(Interval, ct);
        }
    }
}