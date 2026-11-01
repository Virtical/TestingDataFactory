namespace TestingDataFactory.Configuration;

internal static class DatabaseConfiguration
{
    public const string DefaultSchemaName = "testing-data-factory";
    public const string ConnectionStringName = "Default";
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__" + ConnectionStringName;
}

