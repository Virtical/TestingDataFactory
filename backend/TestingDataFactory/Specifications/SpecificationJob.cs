namespace TestingDataFactory.Specifications;

/// <summary>Состояние генерации спецификации</summary>
public class SpecificationJob
{
    private SpecificationJob() { }

    public SpecificationJob(Guid jobId, Guid userId, DateTimeOffset createdAt)
    {
        if (jobId == Guid.Empty) throw new ArgumentException("JobId is required.", nameof(jobId));
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        JobId = jobId;
        UserId = userId;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid JobId { get; private set; }
    public Guid UserId { get; private set; }
    public SpecificationJobStatus Status { get; private set; } = SpecificationJobStatus.Queued;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? SpecificationYaml { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
}
