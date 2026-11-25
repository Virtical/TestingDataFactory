namespace TestingDataFactory.Worker.Configuration;

internal static class DatabaseConfiguration
{
    public const string DefaultSchemaName = "worker";
    public const string ConnectionStringName = "Default";
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__" + ConnectionStringName;
}

