namespace TestingDataFactory.Worker.Specifications.Model;

/// <summary>Временный сбой модели с безопасным сообщением для инфраструктуры повторов.</summary>
public sealed class TransientModelException() : Exception("The specification model is temporarily unavailable.");
