namespace TestingDataFactory.Contracts;

/// <summary>Общие имена exchanges событий и очереди команд.</summary>
public static class MessagingTopology
{
    public const string JobStartedExchange = "data-factory-job-started-v1";
    public const string SpecificationGeneratedExchange = "data-factory-specification-generated-v1";
    public const string JobFailedExchange = "data-factory-job-failed-v1";
    public const string GenerateSpecificationQueue = "data-factory-generate-specification-v1";
}
