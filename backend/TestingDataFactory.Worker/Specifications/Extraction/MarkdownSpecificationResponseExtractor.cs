namespace TestingDataFactory.Worker.Specifications.Extraction;

internal sealed class MarkdownSpecificationResponseExtractor : ISpecificationResponseExtractor
{
    private const string MarkdownFence = "```";
    private const string YamlFence = MarkdownFence + "yaml";
    private const string YmlFence = MarkdownFence + "yml";

    public string ExtractYaml(string response)
    {
        var yaml = response.Trim('\r', '\n');
        var lines = yaml.Split('\n');
        if (lines.Length >= 2
            && lines[0].Trim().ToLowerInvariant() is YamlFence or YmlFence or MarkdownFence
            && lines[^1].Trim() == MarkdownFence)
        {
            return string.Join("\n", lines, 1, lines.Length - 2).TrimEnd('\r');
        }

        return yaml;
    }
}
