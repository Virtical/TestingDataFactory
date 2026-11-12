using TestingDataFactory.Core.Specifications.Parsing;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Generation;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace TestingDataFactory.Core.Specifications;

/// <summary>Разбирает YAML-спецификацию и возвращает модель или диагностику.</summary>
public sealed class SpecificationParser
{
    private readonly YamlSpecificationParser parser;

    public SpecificationParser() : this(null) { }

    internal SpecificationParser(ISpecificationDictionaryRegistry? dictionaryRegistry) =>
        parser = new YamlSpecificationParser(dictionaryRegistry);

    /// <summary>Разбирает спецификацию, включая раздел параметров генерации.</summary>
    public ParseResult Parse(string yaml) => parser.ParseForValidation(yaml);

    /// <summary>Проверяет связи и правила; при наличии переопределений проверяет параметры запуска.</summary>
    public ValidationResult Validate(ParsedSpecification specification, GenerationOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var result = parser.Finish(specification.Prepared, Array.Empty<SpecificationDiagnostic>(), validateGenerationReferences: false);
        if (!result.IsSuccess) return new(null, null, result.Diagnostics);
        if (overrides is null) return new(result.Specification, null, Array.Empty<SpecificationDiagnostic>());

        var merged = GenerationOptionsMerger.Apply(result.Specification!, overrides);
        if (!merged.IsSuccess) return new(null, null, merged.Diagnostics);
        var errors = ValidateUniqueCapacity(result.Specification!, merged.Options!);
        return errors.Count == 0
            ? new(result.Specification, merged.Options, errors.AsReadOnly())
            : new(null, null, errors.AsReadOnly());
    }

    /// <summary>Проверяет спецификацию и подготавливает поток строк таблиц.</summary>
    public GenerationResult Generate(ParsedSpecification specification, GenerationOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var validation = Validate(specification, overrides ?? new GenerationOverrides());
        if (!validation.IsSuccess) return new(null, null, validation.Diagnostics);
        var errors = TableGenerator.Check(validation.Specification!, validation.Options!);
        if (errors.Count != 0) return new(null, null, errors.AsReadOnly());
        var seed = validation.Options!.Seed ?? BinaryPrimitives.ReadInt32LittleEndian(RandomNumberGenerator.GetBytes(sizeof(int)));
        var options = validation.Options with { Seed = seed, Output = validation.Options.Output ?? new CsvOutput() };
        var columns = validation.Specification!.TableOrder.ToDictionary(name => name,
            name => (IReadOnlyList<string>)Array.AsReadOnly(validation.Specification.Tables[name].Fields.Keys.ToArray()), StringComparer.Ordinal);
        return new GenerationResult(options, TableGenerator.Generate(validation.Specification, options), Array.Empty<SpecificationDiagnostic>())
        {
            Columns = columns.Count == 1 ? columns.Values.Single() : null,
            TableColumns = new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(columns)
        };
    }

    private static List<SpecificationDiagnostic> ValidateUniqueCapacity(Specification specification, GenerationOptions options)
    {
        var errors = new List<SpecificationDiagnostic>();
        if (options.Mode != GenerationMode.Valid) return errors;
        foreach (var (tableName, count) in options.Rows)
        {
            if (count <= 0 || !specification.Tables.TryGetValue(tableName, out var table)) continue;
            foreach (var (fieldName, field) in table.Fields)
            {
                if (!field.Unique || field.Type != FieldType.Integer || field.Min is not { } min || field.Max is not { } max ||
                    field.NullRate is > 0 || field.Derived is not null || field.Reference is not null) continue;
                // Avoid overflowing decimal when the interval reaches the numeric limit.
                var insufficient = min > decimal.MaxValue - (count - 1) || min + (count - 1) > max;
                if (insufficient)
                {
                    var path = "$.tables['" + tableName.Replace("'", "\\'") + "'].fields['" + fieldName.Replace("'", "\\'") + "']";
                    errors.Add(new("INSUFFICIENT_UNIQUE_VALUES", "Число строк превышает количество уникальных целых значений в диапазоне поля.", path));
                }
            }
        }
        return errors;
    }

    internal SpecificationParseResult Parse(string yaml, SpecificationParsePurpose purpose) => parser.Parse(yaml, purpose);
}
