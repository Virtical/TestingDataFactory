export type CelExpression = {
    source: string;
    dependencies: string[];
};

export type RuleSeverity = 'Error' | 'Warning';

export type RuleDefinition = {
    expr: CelExpression;
    when?: CelExpression | null;
    message?: string | null;
    severity: RuleSeverity;
};

export type UniformDistribution = Record<string, never>;
export type LogNormalDistribution = Record<string, never>;
export type NormalDistribution = {
    mean: number;
    standardDeviation: number;
};

export type DistributionDefinition =
    | UniformDistribution
    | LogNormalDistribution
    | NormalDistribution;

export type EnumEntry = {
    value: string;
    weight?: number | null;
};

export type PrimitiveTypeDefinition = {
    locale?: string | null;
    field: FieldDefinition;
};

export type ValuesEnumDefinition = {
    locale?: string | null;
    values: EnumEntry[];
};

export type TableEnumDefinition = {
    locale?: string | null;
    columns: string[];
    rows: string[][];
};

export type DictionaryEnumDefinition = {
    locale?: string | null;
    dictionary: string;
    columns: string[];
    where?: CelExpression | null;
};

export type TypeDefinition =
    | PrimitiveTypeDefinition
    | ValuesEnumDefinition
    | TableEnumDefinition
    | DictionaryEnumDefinition;

export type GenerationMode = 'Valid' | 'EdgeCases' | 'Anomalies';
export type SqlDialect = 'PostgreSql' | 'MySql' | 'SQLite';
export type SqlOutputStyle = 'Script' | 'Insert';

export type CsvOutput = { delimiter: string };
export type JsonOutput = Record<string, never>;
export type SqlOutput = {
    dialect: SqlDialect;
    style?: SqlOutputStyle | null;
};

export type GenerationOutput = CsvOutput | JsonOutput | SqlOutput;

export type GenerationOptions = {
    seed?: number | null;
    rows: Record<string, number>;
    mode: GenerationMode;
    output?: GenerationOutput | null;
};

export type FieldType =
    | 'Integer' | 'Decimal' | 'String' | 'Regex'
    | 'Boolean' | 'Date' | 'DateTime' | 'Time'
    | 'Uuid' | 'Enum' | 'Reference' | 'Dynamic';

export type TemporalBound = {
    date?: string | null;
    time?: string | null;
    utcOffset?: string | null;
};

export type PerParentRange = {
    min: number;
    max: number;
};

export type FieldReference = {
    table: string;
    field: string;
    perParent?: PerParentRange | null;
};

export type FieldDefinition = {
    type: FieldType;
    primary: boolean;
    unique: boolean;
    pii: boolean;
    nullRate?: number | null;
    min?: number | null;
    max?: number | null;
    scale?: number | null;
    minLength?: number | null;
    maxLength?: number | null;
    pattern?: string | null;
    trueRate?: number | null;
    namedType?: string | null;
    column?: string | null;
    reference?: FieldReference | null;
    temporalMin?: TemporalBound | null;
    temporalMax?: TemporalBound | null;
    generator?: string | null;
    locale?: string | null;
    derived?: CelExpression | null;
    distribution?: DistributionDefinition | null;
};

export type TableDefinition = {
    name: string;
    fields: Record<string, FieldDefinition>;
    rules: RuleDefinition[];
};

export type Specification = {
    version: string;
    locale: string;
    types: Record<string, TypeDefinition>;
    tables: Record<string, TableDefinition>;
    tableOrder: string[];
    generation?: GenerationOptions | null;
};

export type SpecificationDiagnostic = {
    code: string;
    message: string;
    path: string;
    line?: number | null;
    column?: number | null;
};

export type SpecificationParseResult = {
    specification?: Specification | null;
    diagnostics: SpecificationDiagnostic[];
    isSuccess: boolean;
};

export async function parseSpecification(text: string): Promise<SpecificationParseResult> {
    const res = await fetch('/api/specifications/parse', { //Уточнить URL
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ text }), //Уточнить, как именно бэкенд ждет текст (поле text, description или raw string)
    });

    const data = await res.json().catch(() => null);

    if (!res.ok) {
        throw {
            code: 'NETWORK_ERROR',
            message: 'Не удалось распознать спецификацию',
        };
    }

    return data as SpecificationParseResult;
}

export type GenerationRunResponse = {
    runId: string;
    status: 'InProgress' | 'Completed' | 'Failed';
};

// Уточнить URL и формат тела
export async function startGeneration(
    _projectId: string | number,
    _type: 'data' | 'sql' | 'validate',
    _options: any // Пока any
): Promise<GenerationRunResponse> {
    // Имитация задержки сети (чтобы увидели "Анализируем...")
    await new Promise(resolve => setTimeout(resolve, 1000));

    // Имитация успешного ответа от сервера
    return {
        runId: Math.random().toString(36).substring(7),
        status: 'InProgress',
    };

    /* С бекендом будет это:
    const res = await fetch(`/api/projects/${projectId}/generate`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ type, ...options }),
    });
    const data = await res.json();
    if (!res.ok) throw { code: 'ERROR', message: 'Ошибка генерации' };
    return data;
    */
}