namespace TestingDataFactory.Worker.Specifications.Model;

public enum PermanentModelFailure
{
    Rejected,
    NotConfigured
}

/// <summary>Окончательный отказ адаптера. Диагностика провайдера не входит в это исключение.</summary>
public sealed class PermanentModelException(PermanentModelFailure failure)
    : Exception("The specification model request cannot be completed.")
{
    public PermanentModelFailure Failure { get; } = failure;
}
