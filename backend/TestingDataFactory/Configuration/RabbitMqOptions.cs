namespace TestingDataFactory.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostAddress { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    internal static bool IsValidHostAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == "rabbitmq" || uri.Scheme == "rabbitmqs")
        && !string.IsNullOrWhiteSpace(uri.Host);
}

