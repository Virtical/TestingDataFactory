using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TestingDataFactory.Contracts;
using TestingDataFactory.Data;

namespace TestingDataFactory.Specifications;

internal sealed class SpecificationJobEventHandler(
    AppDbContext db,
    ILogger<SpecificationJobEventHandler> logger)
{
    public async Task ApplyAsync(JobStarted message, CancellationToken cancellationToken)
    {
        var updated = await db.SpecificationJobs
            .Where(job => job.JobId == message.JobId && job.Status == SpecificationJobStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, SpecificationJobStatus.Running)
                .SetProperty(job => job.StartedAt, message.StartedAt.ToUniversalTime()), cancellationToken);

        if (updated == 0)
            await GetExistingJobAsync(message.JobId, cancellationToken);

        LogApplied(message.JobId, nameof(JobStarted), updated);
    }

    public async Task ApplyAsync(SpecificationGenerated message, CancellationToken cancellationToken)
    {
        var updated = await UnfinishedJob(message.JobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, SpecificationJobStatus.Succeeded)
                .SetProperty(job => job.CompletedAt, message.GeneratedAt.ToUniversalTime())
                .SetProperty(job => job.SpecificationYaml, message.SpecificationYaml)
                .SetProperty(job => job.ErrorCode, (string?)null)
                .SetProperty(job => job.ErrorMessage, (string?)null), cancellationToken);

        if (updated == 0)
        {
            var existing = await GetExistingJobAsync(message.JobId, cancellationToken);
            if (existing.Status != SpecificationJobStatus.Succeeded
                || existing.SpecificationYaml != message.SpecificationYaml)
                LogConflict(message.JobId, nameof(SpecificationGenerated));
        }

        LogApplied(message.JobId, nameof(SpecificationGenerated), updated);
    }

    public async Task ApplyAsync(JobFailed message, CancellationToken cancellationToken)
    {
        var updated = await UnfinishedJob(message.JobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, SpecificationJobStatus.Failed)
                .SetProperty(job => job.CompletedAt, message.FailedAt.ToUniversalTime())
                .SetProperty(job => job.ErrorCode, message.ErrorCode)
                .SetProperty(job => job.ErrorMessage, message.ErrorMessage)
                .SetProperty(job => job.SpecificationYaml, (string?)null), cancellationToken);

        if (updated == 0)
        {
            var existing = await GetExistingJobAsync(message.JobId, cancellationToken);
            if (existing.Status != SpecificationJobStatus.Failed
                || existing.ErrorCode != message.ErrorCode || existing.ErrorMessage != message.ErrorMessage)
                LogConflict(message.JobId, nameof(JobFailed));
        }

        LogApplied(message.JobId, nameof(JobFailed), updated);
    }

    private IQueryable<SpecificationJob> UnfinishedJob(Guid jobId) => db.SpecificationJobs.Where(job =>
        job.JobId == jobId && (job.Status == SpecificationJobStatus.Queued || job.Status == SpecificationJobStatus.Running));

    private async Task<SpecificationJob> GetExistingJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.SpecificationJobs.AsNoTracking()
            .SingleOrDefaultAsync(job => job.JobId == jobId, cancellationToken);
        if (job is not null) return job;

        logger.LogWarning("Job event rejected: {JobId}, {ErrorCode}", jobId, "UNKNOWN_JOB");
        throw new InvalidOperationException("Cannot apply an event to an unknown specification job.");
    }

    private void LogApplied(Guid jobId, string eventType, int updated) =>
        logger.LogInformation("Job event handled: {JobId}, {EventType}, {Applied}", jobId, eventType, updated != 0);

    private void LogConflict(Guid jobId, string eventType) =>
        logger.LogWarning("Job event ignored: {JobId}, {EventType}, {ErrorCode}", jobId, eventType, "CONFLICTING_JOB_RESULT");
}
