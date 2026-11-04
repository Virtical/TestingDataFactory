namespace TestingDataFactory.Worker.Configuration;

internal sealed class ModelClientOptions
{
    public const string SectionName = "ModelClient";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
