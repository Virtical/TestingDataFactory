import {
    newField,
    newId,
    newReference,
    type BuilderDocument,
    type BuilderField,
    type BuilderRule,
    type BuilderTable,
    type BuilderType,
} from './builderModel';
import { isRecord, readSpecification } from './specificationDocument';

type Properties = Record<string, unknown>;

function object(value: unknown, path: string): Properties {
    if (!isRecord(value)) throw new Error(`${path}: ожидается объект.`);
    return value;
}

function text(value: unknown, fallback = ''): string {
    if (value === undefined) return fallback;
    if (typeof value !== 'string') throw new Error('Текстовые параметры должны быть строками.');
    return value;
}

function number(value: unknown, fallback?: number): number | undefined {
    if (value === undefined) return fallback;
    if (typeof value !== 'number' || !Number.isFinite(value)) {
        throw new Error('Числовые параметры должны содержать конечные числа.');
    }
    return value;
}

function boolean(value: unknown, fallback = false): boolean {
    if (value === undefined) return fallback;
    if (typeof value !== 'boolean')
        throw new Error('Логические параметры принимают true или false.');
    return value;
}

function choice<T extends string>(value: unknown, options: readonly T[], fallback: T): T {
    if (value === undefined) return fallback;
    if (typeof value !== 'string' || !options.includes(value as T)) {
        throw new Error(`Допустимые значения: ${options.join(', ')}.`);
    }
    return value as T;
}

function knownProperties(value: Properties, keys: string[], path: string) {
    const unknown = Object.keys(value).find(key => !keys.includes(key));
    if (unknown) {
        throw new Error(`${path}.${unknown}: этот параметр не поддерживается конструктором.`);
    }
}

function readField(name: string, value: unknown, previous?: BuilderField): BuilderField {
    const data = typeof value === 'string' ? { type: value } : object(value, `Поле ${name}`);
    knownProperties(
        data,
        [
            'type',
            'description',
            'primary',
            'unique',
            'pii',
            'null_rate',
            'min',
            'max',
            'precision',
            'scale',
            'generator',
            'locale',
            'derived',
            'distribution',
            'ref',
            'per_parent',
            'relationship',
            'integrity',
            'reference_description',
        ],
        `Поле ${name}`,
    );
    const distribution = object(data.distribution ?? {}, `Распределение ${name}`);
    knownProperties(distribution, ['type', 'mean', 'std'], `Распределение ${name}`);

    return {
        ...newField(name),
        id: previous?.id ?? newId(),
        type: text(data.type, 'string'),
        description: text(data.description),
        primary: boolean(data.primary),
        unique: boolean(data.unique),
        pii: boolean(data.pii),
        nullRate: (number(data.null_rate, 0) ?? 0) * 100,
        min: number(data.min),
        max: number(data.max),
        precision: number(data.precision, 12) ?? 12,
        scale: number(data.scale, 2) ?? 2,
        generator: text(data.generator),
        locale: text(data.locale),
        derived: data.derived !== undefined,
        expression: text(data.derived),
        distribution: choice(distribution.type, ['', 'uniform', 'normal', 'lognormal'], ''),
        mean: number(distribution.mean, 0) ?? 0,
        std: number(distribution.std, 1) ?? 1,
    };
}

function readType(name: string, value: unknown, previous?: BuilderType): BuilderType {
    const data = object(value, `Тип ${name}`);
    knownProperties(data, ['type', 'values', 'description'], `Тип ${name}`);
    if (
        data.values !== undefined &&
        (!Array.isArray(data.values) ||
            !data.values.every(item => typeof item === 'string' && !item.includes(',')))
    ) {
        throw new Error(`Тип ${name}: values должен содержать список строк без запятых.`);
    }
    return {
        id: previous?.id ?? newId(),
        name,
        base: data.values !== undefined ? 'enum' : text(data.type, 'string'),
        values: Array.isArray(data.values) ? data.values.join(', ') : '',
        description: text(data.description),
    };
}

export function readBuilderYaml(
    content: string,
    previous: BuilderDocument,
): {
    document: BuilderDocument | null;
    error: string | null;
} {
    const parsed = readSpecification({ fileName: 'specification.yaml', content });
    if (!parsed.data) return { document: null, error: parsed.error };

    try {
        const data = parsed.data;
        knownProperties(
            data,
            ['name', 'spec_version', 'version', 'locale', 'seed', 'types', 'tables', 'generation'],
            'Спецификация',
        );
        const generation = object(data.generation ?? {}, 'generation');
        knownProperties(generation, ['seed', 'rows', 'output'], 'generation');
        const rows = object(generation.rows ?? {}, 'generation.rows');
        const output = object(generation.output ?? {}, 'generation.output');
        knownProperties(output, ['sql', 'format'], 'generation.output');
        const tableEntries = Object.entries(object(data.tables ?? {}, 'tables'));
        const rules: BuilderRule[] = [];

        const tables: BuilderTable[] = tableEntries.map(([name, value], index) => {
            const table = object(value, `Таблица ${name}`);
            knownProperties(
                table,
                ['description', 'locale', 'mode', 'seed', 'rows', 'fields', 'rules'],
                `Таблица ${name}`,
            );
            const existing = previous.tables.find(item => item.name === name);
            const id = existing?.id ?? newId();
            const fields = Object.entries(object(table.fields ?? {}, `Поля ${name}`));
            const tableRules = table.rules ?? [];
            if (!Array.isArray(tableRules)) throw new Error(`${name}.rules: ожидается список.`);
            tableRules.forEach((value, ruleIndex) => {
                const rule = typeof value === 'string' ? { expr: value } : object(value, 'Правило');
                knownProperties(rule, ['expr', 'message'], 'Правило');
                const existingRule = previous.rules.filter(item => item.tableId === id)[ruleIndex];
                rules.push({
                    id: existingRule?.id ?? newId(),
                    tableId: id,
                    expression: text(rule.expr),
                    message: text(rule.message),
                });
            });
            return {
                id,
                name,
                x: existing?.x ?? 60 + (index % 3) * 330,
                y: existing?.y ?? 50 + Math.floor(index / 3) * 300,
                rows: number(rows[name] ?? table.rows, 500) ?? 500,
                description: text(table.description),
                locale: text(table.locale),
                mode: choice(table.mode, ['unique', 'distribution'], 'unique'),
                seed: number(table.seed),
                fields: fields.map(([fieldName, field]) =>
                    readField(
                        fieldName,
                        field,
                        existing?.fields.find(item => item.name === fieldName),
                    ),
                ),
            };
        });

        tables.forEach((table, tableIndex) => {
            const data = object(tableEntries[tableIndex][1], table.name);
            const fields = object(data.fields ?? {}, `${table.name}.fields`);
            table.fields.forEach(field => {
                const value = fields[field.name];
                if (!isRecord(value) || value.ref === undefined) return;
                const reference = text(value.ref);
                const [tableName, fieldName] = reference.split('.');
                const targetTable = tables.find(item => item.name === tableName);
                const targetField = targetTable?.fields.find(item => item.name === fieldName);
                if (!targetTable || !targetField || reference.split('.').length !== 2) {
                    throw new Error(
                        `${table.name}.${field.name}: ссылка «${reference}» не найдена.`,
                    );
                }
                const range = object(value.per_parent ?? {}, 'per_parent');
                const integrity = object(value.integrity ?? {}, 'integrity');
                knownProperties(range, ['min', 'max'], 'per_parent');
                knownProperties(integrity, ['foreign_key', 'on_delete'], 'integrity');
                field.reference = {
                    ...newReference(targetTable.id, targetField.id),
                    cardinality: choice(value.relationship, ['N:1', '1:1'], 'N:1'),
                    min: number(range.min, 0) ?? 0,
                    max: number(range.max, 8) ?? 8,
                    enforce: boolean(integrity.foreign_key, true),
                    restrict:
                        choice(integrity.on_delete, ['restrict', 'no_action'], 'restrict') ===
                        'restrict',
                    description: text(value.reference_description),
                };
            });
        });

        const types = Object.entries(object(data.types ?? {}, 'types')).map(([name, value]) =>
            readType(
                name,
                value,
                previous.types.find(item => item.name === name),
            ),
        );
        const sourceVersion = data.spec_version ?? data.version;
        const version =
            typeof sourceVersion === 'number'
                ? String(sourceVersion)
                : text(sourceVersion, previous.version);

        return {
            document: {
                ...previous,
                name: text(data.name, previous.name),
                version,
                locale: text(data.locale, previous.locale),
                seed: number(generation.seed ?? data.seed),
                output: choice(
                    output.sql ?? output.format,
                    ['postgres', 'mysql', 'sqlite', 'json', 'csv'],
                    previous.output,
                ),
                tables,
                types,
                rules,
            },
            error: null,
        };
    } catch (error) {
        return {
            document: null,
            error: error instanceof Error ? error.message : 'Проверьте структуру YAML.',
        };
    }
}
