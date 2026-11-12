using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

// Intermediate representation before resolving cross-table references or ordering tables.
// Source nodes are retained internally until expression and reference checks are separated.
internal sealed record PreparedSpecification(
    SpecificationStructure Structure,
    IReadOnlyDictionary<string, TypeDefinition> Types,
    IReadOnlyDictionary<string, TableDefinition> Tables,
    IReadOnlyDictionary<(string Table, string Field), YamlMappingNode> ReferenceNodes,
    YamlMappingNode TableNodes,
    YamlNode? GenerationNode);

internal sealed record PreparationResult(PreparedSpecification? Specification, IReadOnlyList<SpecificationDiagnostic> Diagnostics);
