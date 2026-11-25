import { stringify } from 'yaml';

export const FIELD_TYPES = [
    'int',
    'decimal',
    'string',
    'boolean',
    'date',
    'datetime',
    'time',
    'uuid',
] as const;
export const LOCALES = ['ru_RU', 'en_US', 'de_DE'] as const;
export const GENERATORS = [
    '',
    'internet.email',
    'person.fullName',
    'person.firstName',
    'person.lastName',
    'phone.number',
    'address.city',
    'commerce.productName',
    'random.uuid',
] as const;

export type Reference = {
    tableId: string;
    fieldId: string;
    cardinality: 'N:1' | '1:1';
    min: number;
    max: number;
    enforce: boolean;
    restrict: boolean;
    description: string;
};
export type BuilderField = {
    id: string;
    name: string;
    type: string;
    description: string;
    primary: boolean;
    unique: boolean;
    pii: boolean;
    nullRate: number;
    generator: string;
    locale: string;
    min?: number;
    max?: number;
    temporalMin?: string;
    temporalMax?: string;
    precision: number;
    scale: number;
    derived: boolean;
    expression: string;
    distribution: '' | 'uniform' | 'normal' | 'lognormal';
    mean: number;
    std: number;
    reference?: Reference;
};
export type BuilderTable = {
    id: string;
    name: string;
    rows: number;
    x: number;
    y: number;
    description: string;
    locale: string;
    mode: 'unique' | 'distribution';
    seed?: number;
    fields: BuilderField[];
};
export type BuilderType = {
    id: string;
    name: string;
    base: string;
    values: string;
    description: string;
};
export type BuilderRule = { id: string; tableId: string; expression: string; message: string };
export type BuilderDocument = {
    schemaVersion: 1;
    name: string;
    version: string;
    locale: string;
    seed?: number;
    output: 'postgres' | 'mysql' | 'sqlite' | 'json' | 'csv';
    generationMode?: 'valid' | 'edge_cases' | 'anomalies';
    csvDelimiter?: string;
    sqlStyle?: 'script' | 'insert';
    tables: BuilderTable[];
    types: BuilderType[];
    rules: BuilderRule[];
};
export type Selection =
    | { kind: 'table'; tableId: string }
    | { kind: 'field' | 'relation'; tableId: string; fieldId: string }
    | { kind: 'type'; typeId: string }
    | { kind: 'generation' };
export type Diagnostic = { message: string; severity: 'error' | 'warning'; selection?: Selection };

export const newId = () => crypto.randomUUID();
export const emptyDocument = (): BuilderDocument => ({
    schemaVersion: 1,
    name: 'Новая спецификация',
    version: '1.0',
    locale: 'ru_RU',
    seed: 2026,
    output: 'csv',
    tables: [],
    types: [],
    rules: [],
});
export const newField = (name: string, type = 'string'): BuilderField => ({
    id: newId(),
    name,
    type,
    description: '',
    primary: false,
    unique: false,
    pii: false,
    nullRate: 0,
    generator: '',
    locale: '',
    precision: 12,
    scale: 2,
    derived: false,
    expression: '',
    distribution: '',
    mean: 0,
    std: 1,
});
export const newReference = (tableId: string, fieldId: string): Reference => ({
    tableId,
    fieldId,
    cardinality: 'N:1',
    min: 0,
    max: 8,
    enforce: true,
    restrict: true,
    description: '',
});
export const validName = (name: string) => /^[a-zA-Z_][a-zA-Z0-9_]*$/.test(name);
export function uniqueName(prefix: string, names: string[]) {
    if (!names.includes(prefix)) return prefix;
    let index = 2;
    while (names.includes(`${prefix}_${index}`)) index++;
    return `${prefix}_${index}`;
}

export function updateTable(
    doc: BuilderDocument,
    id: string,
    patch: Partial<BuilderTable>,
): BuilderDocument {
    return {
        ...doc,
        tables: doc.tables.map(table => (table.id === id ? { ...table, ...patch } : table)),
    };
}
export function updateField(
    doc: BuilderDocument,
    tableId: string,
    fieldId: string,
    patch: Partial<BuilderField>,
): BuilderDocument {
    return {
        ...doc,
        tables: doc.tables.map(table =>
            table.id === tableId
                ? {
                      ...table,
                      fields: table.fields.map(field =>
                          field.id === fieldId ? { ...field, ...patch } : field,
                      ),
                  }
                : table,
        ),
    };
}
export function removeField(
    doc: BuilderDocument,
    tableId: string,
    fieldId: string,
): BuilderDocument {
    return {
        ...doc,
        tables: doc.tables.map(table => ({
            ...table,
            fields: table.fields
                .filter(field => table.id !== tableId || field.id !== fieldId)
                .map(field =>
                    field.reference?.tableId === tableId && field.reference.fieldId === fieldId
                        ? { ...field, reference: undefined }
                        : field,
                ),
        })),
    };
}
export function removeTable(doc: BuilderDocument, tableId: string): BuilderDocument {
    return {
        ...doc,
        tables: doc.tables
            .filter(table => table.id !== tableId)
            .map(table => ({
                ...table,
                fields: table.fields.map(field =>
                    field.reference?.tableId === tableId
                        ? { ...field, reference: undefined }
                        : field,
                ),
            })),
        rules: doc.rules.filter(rule => rule.tableId !== tableId),
    };
}
export function fieldTypeLabel(field: BuilderField) {
    return field.type === 'decimal' ? `decimal(${field.precision},${field.scale})` : field.type;
}
export function baseType(doc: BuilderDocument, type: string) {
    return doc.types.find(item => item.name === type)?.base ?? type;
}

export function specificationObject(doc: BuilderDocument) {
    const output = doc.output === 'csv'
        ? { csv: doc.csvDelimiter === undefined ? {} : { delimiter: doc.csvDelimiter } }
        : doc.output === 'json'
            ? { json: {} }
            : { sql: { dialect: doc.output === 'postgres' ? 'postgresql' : doc.output,
                ...(doc.sqlStyle ? { style: doc.sqlStyle } : {}) } };
    return {
        spec_version: doc.version,
        locale: doc.locale,
        types: Object.fromEntries(
            doc.types.map(type => [
                type.name,
                {
                    ...(type.base === 'enum'
                        ? {
                              values: type.values
                                  .split(',')
                                  .map(value => value.trim())
                                  .filter(Boolean),
                          }
                        : { type: type.base === 'int' ? 'integer' : type.base }),
                },
            ]),
        ),
        tables: Object.fromEntries(
            doc.tables.map(table => [
                table.name,
                {
                    ...(table.locale ? { locale: table.locale } : {}),
                    fields: Object.fromEntries(
                        table.fields.map(field => {
                            const targetTable = doc.tables.find(
                                item => item.id === field.reference?.tableId,
                            );
                            const targetField = targetTable?.fields.find(
                                item => item.id === field.reference?.fieldId,
                            );
                            const temporal = ['date', 'datetime', 'time'].includes(baseType(doc, field.type));
                            const min = temporal ? field.temporalMin : field.min;
                            const max = temporal ? field.temporalMax : field.max;
                            const distribution = field.distribution === 'normal'
                                ? { normal: { mean: field.mean, std: field.std } }
                                : field.distribution;
                            return [
                                field.name,
                                {
                                    ...(!field.reference ? { type: field.type === 'int' ? 'integer' : field.type } : {}),
                                    ...(field.primary ? { primary: true } : {}),
                                    ...(field.unique || field.reference?.cardinality === '1:1'
                                        ? { unique: true }
                                        : {}),
                                    ...(field.pii ? { pii: true } : {}),
                                    ...(field.nullRate ? { null_rate: field.nullRate / 100 } : {}),
                                    ...(min !== undefined ? { min } : {}),
                                    ...(max !== undefined ? { max } : {}),
                                    ...(baseType(doc, field.type) === 'decimal'
                                        ? { scale: field.scale }
                                        : {}),
                                    ...(field.generator && !field.derived
                                        ? { generator: field.generator }
                                        : {}),
                                    ...(field.locale ? { locale: field.locale } : {}),
                                    ...(field.derived ? { derived: field.expression } : {}),
                                    ...(distribution && !field.derived ? { distribution } : {}),
                                    ...(field.reference && targetTable && targetField
                                        ? {
                                              ref: `${targetTable.name}.${targetField.name}`,
                                              per_parent: {
                                                  min: field.reference.min,
                                                  max: field.reference.max,
                                              },
                                          }
                                        : {}),
                                },
                            ];
                        }),
                    ),
                    rules: doc.rules
                        .filter(rule => rule.tableId === table.id)
                        .map(rule => ({
                            expr: rule.expression,
                            ...(rule.message ? { message: rule.message } : {}),
                        })),
                },
            ]),
        ),
        generation: {
            ...(doc.seed !== undefined ? { seed: doc.seed } : {}),
            ...(doc.generationMode ? { mode: doc.generationMode } : {}),
            rows: Object.fromEntries(doc.tables
                .filter(table => !table.fields.some(field => field.reference))
                .map(table => [table.name, table.rows])),
            output,
        },
    };
}
export function serializeSpecification(doc: BuilderDocument, format: 'yaml' | 'json' = 'yaml') {
    const value = specificationObject(doc);
    return format === 'json' ? JSON.stringify(value, null, 2) : stringify(value);
}

// Parse only arithmetic for the local example. CEL itself is evaluated by the generation service.
export function arithmeticPreview(
    expression: string,
    values: Record<string, number>,
): number | null {
    const tokens =
        expression.match(/[a-zA-Z_][a-zA-Z0-9_]*|(?:\d+(?:\.\d*)?|\.\d+)|[()+*/%-]/g) ?? [];
    if (!tokens.length || tokens.join('') !== expression.replace(/\s/g, '')) return null;
    let index = 0;
    const atom = (): number => {
        const token = tokens[index++];
        if (token === '-') return -atom();
        if (token === '+') return atom();
        if (token === '(') {
            const value = sum();
            if (tokens[index++] !== ')') throw new Error('Missing parenthesis');
            return value;
        }
        if (token !== undefined && Object.hasOwn(values, token)) return values[token];
        if (token === undefined || !/^\d*\.?\d+$|^\d+\.$/.test(token))
            throw new Error('Unsupported expression');
        return Number(token);
    };
    const product = (): number => {
        let value = atom();
        while (['*', '/', '%'].includes(tokens[index])) {
            const operator = tokens[index++];
            const right = atom();
            value =
                operator === '*' ? value * right : operator === '/' ? value / right : value % right;
        }
        return value;
    };
    const sum = (): number => {
        let value = product();
        while (['+', '-'].includes(tokens[index])) {
            const operator = tokens[index++];
            const right = product();
            value = operator === '+' ? value + right : value - right;
        }
        return value;
    };
    try {
        const value = sum();
        return index === tokens.length && Number.isFinite(value) ? value : null;
    } catch {
        return null;
    }
}

export function readDraft(raw: string | null): BuilderDocument | null {
    if (!raw) return null;
    try {
        const doc = JSON.parse(raw) as BuilderDocument;
        if (
            doc.schemaVersion !== 1 ||
            typeof doc.name !== 'string' ||
            typeof doc.locale !== 'string' ||
            typeof doc.version !== 'string' ||
            !['postgres', 'mysql', 'sqlite', 'json', 'csv'].includes(doc.output) ||
            !Array.isArray(doc.tables) ||
            !Array.isArray(doc.types) ||
            !Array.isArray(doc.rules)
        )
            return null;
        if (
            !doc.tables.every(
                table =>
                    typeof table.id === 'string' &&
                    typeof table.name === 'string' &&
                    Number.isFinite(table.x) &&
                    Number.isFinite(table.y) &&
                    Array.isArray(table.fields) &&
                    table.fields.every(
                        field =>
                            typeof field.id === 'string' &&
                            typeof field.name === 'string' &&
                            typeof field.type === 'string' &&
                            typeof field.expression === 'string',
                    ),
            )
        )
            return null;
        if (
            !doc.types.every(
                type => typeof type.name === 'string' && typeof type.values === 'string',
            )
        )
            return null;
        if (
            !doc.rules.every(
                rule => typeof rule.expression === 'string' && typeof rule.tableId === 'string',
            )
        )
            return null;
        return doc;
    } catch {
        return null;
    }
}
