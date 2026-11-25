using MassTransit;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Contracts;
using TestingDataFactory.Data;

namespace TestingDataFactory.Specifications;

public sealed class SpecificationJobService(AppDbContext db, ISendEndpointProvider sendEndpoints)
{
    private static readonly Uri GenerateSpecificationAddress = new($"queue:{MessagingTopology.GenerateSpecificationQueue}");

    public async Task<SpecificationJob> StartAsync(Guid userId, string prompt, CancellationToken cancellationToken)
    {
        var job = new SpecificationJob(Guid.NewGuid(), userId, DateTimeOffset.UtcNow);
        db.SpecificationJobs.Add(job);

        var endpoint = await sendEndpoints.GetSendEndpoint(GenerateSpecificationAddress);
        await endpoint.Send(new GenerateSpecification(job.JobId, 1, job.CreatedAt, prompt), context =>
        {
            context.MessageId = job.JobId;
            context.CorrelationId = job.JobId;
        }, cancellationToken);

        // Задача и команда из Bus Outbox сохраняются вместе.
        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    public Task<SpecificationJob?> FindAsync(Guid jobId, Guid userId, CancellationToken cancellationToken) =>
        db.SpecificationJobs.AsNoTracking()
            .SingleOrDefaultAsync(job => job.JobId == jobId && job.UserId == userId, cancellationToken);
}
