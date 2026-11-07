using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

public sealed partial class YamlSpecificationParser
{
    private static DistributionDefinition? ReadDistribution(YamlMappingNode field, string fieldPath,
        List<SpecificationDiagnostic> errors, FieldType type, bool isType, bool isDerived)
    {
        var node = Get(field, "distribution");
        if (isType) return null; // Настройка относится к полю, а не объявлению типа.
        var numeric = type is FieldType.Integer or FieldType.Decimal;
        if (node is null) return numeric && !isDerived ? new UniformDistribution() : null;
        var path = fieldPath + ".distribution";
        if (!numeric || isDerived)
        {
            Error(errors, "INCOMPATIBLE_DISTRIBUTION", "Распределение поддерживается для генерируемых числовых полей.", path, node);
            return null;
        }

        if (node is YamlScalarNode scalar && IsString(scalar))
        {
            return scalar.Value switch
            {
                "uniform" => new UniformDistribution(),
                "lognormal" => new LogNormalDistribution(),
                _ => InvalidDistribution(node, path, errors)
            };
        }
        if (node is not YamlMappingNode mapping || mapping.Children.Count != 1 ||
            Get(mapping, "normal") is not { } normalNode)
            return InvalidDistribution(node, path, errors);
        if (normalNode is not YamlMappingNode parameters)
        {
            Error(errors, "INVALID_DISTRIBUTION", "Параметры normal должны быть объектом с mean и std.", path + ".normal", normalNode);
            return null;
        }

        var parameterPath = path + ".normal";
        CheckKeys(parameters, ["mean", "std"], parameterPath, errors);
        if (Get(parameters, "mean") is null)
            Error(errors, "MISSING_DISTRIBUTION_PARAMETER", "Укажите mean.", parameterPath + ".mean", normalNode);
        if (Get(parameters, "std") is null)
            Error(errors, "MISSING_DISTRIBUTION_PARAMETER", "Укажите std.", parameterPath + ".std", normalNode);
        var mean = ReadNumber(parameters, "mean", parameterPath, errors);
        var std = ReadNumber(parameters, "std", parameterPath, errors);
        if (std is <= 0)
            Error(errors, "INVALID_STANDARD_DEVIATION", "std должен быть больше нуля.", parameterPath + ".std", Get(parameters, "std")!);
        return mean is not null && std is > 0 ? new NormalDistribution(mean.Value, std.Value) : null;
    }

    private static DistributionDefinition? InvalidDistribution(YamlNode node, string path, List<SpecificationDiagnostic> errors)
    {
        Error(errors, "INVALID_DISTRIBUTION", "Укажите uniform, lognormal или normal с параметрами mean и std.", path, node);
        return null;
    }
}
