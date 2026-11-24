using System.Globalization;
using System.Text;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal static class CsvFileWriter
{
    internal static async Task WriteDirectoryAsync(GenerationResult result, string path, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!result.IsSuccess || result.TableColumns is null || result.Options!.Output is not CsvOutput csv)
            throw new InvalidOperationException("Для записи CSV требуется успешный результат генерации с форматом csv.");
        var target = Path.GetFullPath(path);
        if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Итоговый путь уже существует.");
        var parent = Path.GetDirectoryName(target) ?? throw new ArgumentException("Невозможно определить родительскую папку.", nameof(path));
        var names = result.TableColumns.Keys.Select((name, i) => (name, file: $"{i + 1:D4}-{Uri.EscapeDataString(name)}.csv")).ToDictionary(p => p.name, p => p.file, StringComparer.Ordinal);
        if (names.Values.Any(name => name.Length > 200)) throw new ArgumentException("Имя таблицы слишком длинное для имени файла.", nameof(path));
        var temporary = Path.Combine(parent, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            if (Directory.Exists(temporary) || File.Exists(temporary)) throw new IOException("Временная папка уже существует.");
            Directory.CreateDirectory(temporary);
            created = true;
            foreach (var (table, columns) in result.TableColumns)
            {
                token.ThrowIfCancellationRequested();
                await using var writer = new StreamWriter(new FileStream(Path.Combine(temporary, names[table]), FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous), new UTF8Encoding(false));
                await WriteLine(writer, columns, csv.Delimiter, token);
                await writer.FlushAsync(token);
            }
            StreamWriter? current = null;
            string? tableName = null;
            try
            {
                foreach (var row in result.Rows!)
                {
                    token.ThrowIfCancellationRequested();
                    if (row.Table != tableName)
                    {
                        if (current is not null) await current.DisposeAsync();
                        tableName = row.Table;
                        current = new StreamWriter(new FileStream(Path.Combine(temporary, names[tableName]), FileMode.Append, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous), new UTF8Encoding(false));
                    }
                    await WriteLine(current!, result.TableColumns[tableName].Select(column => Format(row.Values[column])), csv.Delimiter, token);
                }
                if (current is not null) await current.FlushAsync(token);
            }
            finally { if (current is not null) await current.DisposeAsync(); }
            token.ThrowIfCancellationRequested();
            Directory.Move(temporary, target);
            created = false;
        }
        finally { if (created) Directory.Delete(temporary, true); }
    }

    internal static async Task WriteAsync(GenerationResult result, string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!result.IsSuccess || result.Columns is null || result.Options!.Output is not CsvOutput csv)
            throw new InvalidOperationException("Для записи CSV требуется успешный результат генерации с форматом csv.");
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath) || Directory.Exists(fullPath)) throw new IOException("Итоговый путь уже существует.");
        var directory = Path.GetDirectoryName(fullPath)!;
        var temporary = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                created = true;
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 65536, leaveOpen: false);
                await WriteLine(writer, result.Columns, csv.Delimiter, cancellationToken);
                foreach (var row in result.Rows!)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await WriteLine(writer, result.Columns.Select(column => Format(row.Values[column])), csv.Delimiter, cancellationToken);
                }
                await writer.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath);
            created = false;
        }
        finally
        {
            if (created) File.Delete(temporary);
        }
    }

    private static async Task WriteLine(StreamWriter writer, IEnumerable<string> values, char delimiter, CancellationToken token)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first) await writer.WriteAsync(delimiter.ToString().AsMemory(), token);
            first = false;
            var escaped = value.IndexOfAny([delimiter, '"', '\r', '\n']) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
            await writer.WriteAsync(escaped.AsMemory(), token);
        }
        await writer.WriteAsync("\r\n".AsMemory(), token);
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        string text => text,
        bool boolean => boolean ? "true" : "false",
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        Guid uuid => uuid.ToString("D"),
        TemporalBound temporal => FormatTemporal(temporal),
        _ => throw new InvalidOperationException("Тип значения пока не поддерживается в CSV: " + value.GetType().Name)
    };

    private static string FormatTemporal(TemporalBound value)
    {
        var date = value.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
        var time = value.Time?.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture) ?? "";
        var zone = value.UtcOffset switch
        {
            null => "",
            { Ticks: 0 } => "Z",
            { } offset => (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture)
        };
        return date + (date.Length != 0 && time.Length != 0 ? "T" : "") + time + zone;
    }
}
