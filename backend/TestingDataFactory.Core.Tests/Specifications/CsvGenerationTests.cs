using System.Globalization;
using System.Text;
using NUnit.Framework;
using TestingDataFactory.Core.Generation;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Tests.Specifications;

public class CsvGenerationTests
{
    [Test]
    public async Task WritesEscapedUtf8WithInvariantValuesAndHeader()
    {
        var path = NewPath();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var row = new GeneratedRow("t", new Dictionary<string, object?>
            {
                ["he;ad"] = "Москва;\"центр\"\r\nулица", ["flag"] = true, ["n"] = 12.5m,
                ["date"] = new TemporalBound(new DateOnly(2024, 1, 2), new TimeOnly(3, 4, 5), TimeSpan.FromHours(5)), ["empty"] = null
            });
            var result = TestResult(new[] { row }, ["he;ad", "flag", "n", "date", "empty"], ';');
            await result.WriteCsvAsync(path);
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xef, 0xbb, 0xbf }));
            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo("\"he;ad\";flag;n;date;empty\r\n\"Москва;\"\"центр\"\"\r\nулица\";true;12.5;2024-01-02T03:04:05+05:00;\r\n"));
        }
        finally { CultureInfo.CurrentCulture = previous; File.Delete(path); }
    }

    [Test]
    public async Task WritesHeadersForEmptyTable()
    {
        var path = NewPath();
        try
        {
            await TestResult([], ["a", "b"], ',').WriteCsvAsync(path);
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("a,b\r\n"));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void RemovesTemporaryFileAfterGenerationFailure()
    {
        var path = NewPath();
        try
        {
            var parser = new SpecificationParser();
            var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { name: { generator: person.full_name, max_length: 1 } } } }");
            var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 1 }, Output: new CsvOutput()));
            Assert.That(result.IsSuccess, Is.True);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await result.WriteCsvAsync(path));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(TemporaryFiles(path), Is.Empty);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task DoesNotOverwriteExistingFile()
    {
        var path = NewPath();
        try
        {
            await File.WriteAllTextAsync(path, "original");
            Assert.ThrowsAsync<IOException>(async () => await TestResult([], ["id"], ',').WriteCsvAsync(path));
            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("original"));
            Assert.That(TemporaryFiles(path), Is.Empty);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void CancellationDoesNotCreateFinalOrTemporaryFile()
    {
        var path = NewPath();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            Assert.CatchAsync<OperationCanceledException>(async () => await TestResult([], ["id"], ',').WriteCsvAsync(path, cts.Token));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(TemporaryFiles(path), Is.Empty);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public async Task GeneratesDocumentedExampleToCsvAndReplaysSeed()
    {
        var path = NewPath();
        try
        {
            var example = Path.Combine(TestContext.CurrentContext.TestDirectory, "Specifications", "Examples", "example-1.yaml");
            var parser = new SpecificationParser();
            var parsed = parser.Parse(await File.ReadAllTextAsync(example));
            Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
            var result = parser.Generate(parsed.Specification!);
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
            Assert.That(result.Seed, Is.EqualTo(42));
            await result.WriteCsvAsync(path);
            var lines = await File.ReadAllLinesAsync(path);
            Assert.That(lines, Has.Length.EqualTo(1001));
            Assert.That(lines[0], Is.EqualTo("id,full_name,email,age,city,is_active,created_at,phone"));
            Assert.That(lines.Skip(1).Any(line => line.Contains("Москва", StringComparison.Ordinal)), Is.True);
            var replay = parser.Generate(parsed.Specification!);
            Assert.That(result.Rows!.Take(5).Select(r => r.Values.Values.ToArray()), Is.EqualTo(replay.Rows!.Take(5).Select(r => r.Values.Values.ToArray())));
        }
        finally { File.Delete(path); }
    }

    private static GenerationResult TestResult(IEnumerable<GeneratedRow> rows, string[] columns, char delimiter) =>
        new(new GenerationOptions(1, new Dictionary<string, int>(), GenerationMode.Valid, new CsvOutput(delimiter)), rows, Array.Empty<SpecificationDiagnostic>()) { Columns = columns };
    private static string NewPath() => Path.Combine(Path.GetTempPath(), "testing-data-factory-" + Guid.NewGuid().ToString("N") + ".csv");
    private static IEnumerable<string> TemporaryFiles(string path) => Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".*.tmp");
}
