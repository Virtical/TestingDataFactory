import { request } from './request';

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

export type SpecificationJob = {
    jobId: string;
    status: 'Queued' | 'Running' | 'Succeeded' | 'Failed';
    specificationYaml: string | null;
    errorCode: string | null;
    errorMessage: string | null;
};

export function startSpecificationJob(prompt: string, signal?: AbortSignal): Promise<SpecificationJob> {
    return request('/api/specification-jobs', { method: 'POST', body: { prompt }, signal });
}

export function getSpecificationJob(jobId: string, signal?: AbortSignal): Promise<SpecificationJob> {
    return request(`/api/specification-jobs/${encodeURIComponent(jobId)}`, { signal });
}
