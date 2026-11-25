using System.Text.Json;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal static class JsonDatasetWriter
{
    internal static async Task WriteAsync(GenerationResult result, Stream stream, CancellationToken token)
    {
        if (!result.IsSuccess || result.TableColumns is null)
            throw new InvalidOperationException("Для записи JSON требуется успешный результат генерации.");

        using var writer = new Utf8JsonWriter(stream);
        using var rows = result.Rows!.GetEnumerator();
        token.ThrowIfCancellationRequested();
        var hasRow = rows.MoveNext();
        writer.WriteStartObject();
        foreach (var table in result.TableColumns.Keys)
        {
            writer.WriteStartArray(table);
            while (hasRow && rows.Current.Table == table)
            {
                token.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                foreach (var (column, value) in rows.Current.Values)
                {
                    writer.WritePropertyName(column);
                    JsonSerializer.Serialize(writer, value is TemporalBound temporal
                        ? CsvFileWriter.FormatTemporal(temporal)
                        : value);
                }
                writer.WriteEndObject();
                if (writer.BytesPending >= 64 * 1024)
                    await writer.FlushAsync(token);
                hasRow = rows.MoveNext();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
        await writer.FlushAsync(token);
    }
}
