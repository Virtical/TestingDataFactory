import { stringify } from 'yaml';

export const FIELD_TYPES = [
    'int',
    'decimal',
    'string',
    'boolean',
    'date',
    'datetime',
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
    output: 'postgres',
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

export function validateDocument(doc: BuilderDocument): Diagnostic[] {
    const results: Diagnostic[] = [];
    const error = (message: string, selection?: Selection) =>
        results.push({ message, severity: 'error', selection });
    const checkNames = (names: string[], label: string) => {
        const seen = new Set<string>();
        names.forEach(name => {
            if (!validName(name))
                error(
                    `${label} «${name}»: используйте латинские буквы, цифры и _; начните с буквы или _.`,
                );
            if (seen.has(name)) error(`${label} «${name}»: имя повторяется.`);
            seen.add(name);
        });
    };
    checkNames(
        doc.tables.map(table => table.name),
        'Таблица',
    );
    checkNames(
        doc.types.map(type => type.name),
        'Тип',
    );
    doc.types.forEach(type => {
        if ((FIELD_TYPES as readonly string[]).includes(type.name))
            error(`Имя типа «${type.name}» занято встроенным типом.`);
        if (type.base === 'enum' && !type.values.split(',').some(value => value.trim()))
            error(`Тип «${type.name}»: добавьте значения перечисления.`, {
                kind: 'type',
                typeId: type.id,
            });
    });
    if (!doc.tables.length)
        results.push({ severity: 'warning', message: 'Добавьте хотя бы одну таблицу.' });
    if (!doc.name.trim()) error('Укажите название спецификации.', { kind: 'generation' });
    if (!doc.version.trim()) error('Укажите версию спецификации.', { kind: 'generation' });
    if (doc.seed !== undefined && !Number.isSafeInteger(doc.seed))
        error('Seed должен быть целым числом.', { kind: 'generation' });
    for (const table of doc.tables) {
        const tableSelection: Selection = { kind: 'table', tableId: table.id };
        if (!Number.isSafeInteger(table.rows) || table.rows < 1)
            error(
                `${table.name}: количество строк должно быть положительным целым числом.`,
                tableSelection,
            );
        if (table.seed !== undefined && !Number.isSafeInteger(table.seed))
            error(`${table.name}: Seed должен быть целым числом.`, tableSelection);
        if (!table.fields.length)
            error(`${table.name}: добавьте хотя бы одно поле.`, tableSelection);
        checkNames(
            table.fields.map(field => field.name),
            `Поле в ${table.name}`,
        );
        for (const field of table.fields) {
            const selection: Selection = { kind: 'field', tableId: table.id, fieldId: field.id };
            const path = `${table.name}.${field.name}`;
            if (![...FIELD_TYPES, ...doc.types.map(type => type.name)].includes(field.type))
                error(`${path}: неизвестный тип «${field.type}».`, selection);
            if (field.nullRate < 0 || field.nullRate > 100 || !Number.isFinite(field.nullRate))
                error(`${path}: доля NULL должна быть от 0 до 100%.`, selection);
            if (field.primary && field.nullRate > 0)
                error(`${path}: первичный ключ не может содержать NULL.`, selection);
            if (field.min !== undefined && field.max !== undefined && field.min > field.max)
                error(`${path}: минимум больше максимума.`, selection);
            if (
                baseType(doc, field.type) === 'decimal' &&
                (!Number.isInteger(field.precision) ||
                    field.precision < 1 ||
                    !Number.isInteger(field.scale) ||
                    field.scale < 0 ||
                    field.scale > field.precision)
            )
                error(`${path}: масштаб должен быть целым числом от 0 до точности.`, selection);
            if (
                (field.distribution === 'normal' || field.distribution === 'lognormal') &&
                field.std <= 0
            )
                error(`${path}: стандартное отклонение должно быть больше нуля.`, selection);
            if (field.derived && !field.expression.trim())
                error(`${path}: задайте вычисляемое выражение.`, selection);
            if (field.reference) {
                const ref = field.reference;
                const target = doc.tables
                    .find(item => item.id === ref.tableId)
                    ?.fields.find(item => item.id === ref.fieldId);
                const relationSelection: Selection = {
                    kind: 'relation',
                    tableId: table.id,
                    fieldId: field.id,
                };
                if (!target)
                    error(
                        `${path}: поле, на которое ссылается связь, не найдено.`,
                        relationSelection,
                    );
                else {
                    if (baseType(doc, field.type) !== baseType(doc, target.type))
                        error(`${path}: типы полей связи не совпадают.`, relationSelection);
                    if (!target.primary && !target.unique)
                        error(
                            `${path}: связь должна ссылаться на первичный или уникальный ключ.`,
                            relationSelection,
                        );
                }
                if (ref.tableId === table.id && ref.fieldId === field.id)
                    error(`${path}: поле не может ссылаться само на себя.`, relationSelection);
                if (
                    !Number.isInteger(ref.min) ||
                    !Number.isInteger(ref.max) ||
                    ref.min < 0 ||
                    ref.max < ref.min
                )
                    error(`${path}: проверьте диапазон дочерних записей.`, relationSelection);
                if (ref.cardinality === '1:1' && ref.max > 1)
                    error(
                        `${path}: для связи 1:1 допустима одна дочерняя запись.`,
                        relationSelection,
                    );
            }
        }
    }
    doc.rules.forEach(rule => {
        if (!doc.tables.some(table => table.id === rule.tableId))
            error('У правила не выбрана таблица.');
        if (!rule.expression.trim()) error('Заполните или удалите пустое правило CEL.');
    });
    return results;
}

export function specificationObject(doc: BuilderDocument) {
    return {
        name: doc.name,
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
                        : { type: type.base }),
                    ...(type.description ? { description: type.description } : {}),
                },
            ]),
        ),
        tables: Object.fromEntries(
            doc.tables.map(table => [
                table.name,
                {
                    ...(table.description ? { description: table.description } : {}),
                    ...(table.locale ? { locale: table.locale } : {}),
                    mode: table.mode,
                    ...(table.seed !== undefined ? { seed: table.seed } : {}),
                    fields: Object.fromEntries(
                        table.fields.map(field => {
                            const targetTable = doc.tables.find(
                                item => item.id === field.reference?.tableId,
                            );
                            const targetField = targetTable?.fields.find(
                                item => item.id === field.reference?.fieldId,
                            );
                            return [
                                field.name,
                                {
                                    type: field.type,
                                    ...(field.description
                                        ? { description: field.description }
                                        : {}),
                                    ...(field.primary ? { primary: true } : {}),
                                    ...(field.unique || field.reference?.cardinality === '1:1'
                                        ? { unique: true }
                                        : {}),
                                    ...(field.pii ? { pii: true } : {}),
                                    ...(field.nullRate ? { null_rate: field.nullRate / 100 } : {}),
                                    ...(field.min !== undefined ? { min: field.min } : {}),
                                    ...(field.max !== undefined ? { max: field.max } : {}),
                                    ...(baseType(doc, field.type) === 'decimal'
                                        ? { precision: field.precision, scale: field.scale }
                                        : {}),
                                    ...(field.generator && !field.derived
                                        ? { generator: field.generator }
                                        : {}),
                                    ...(field.locale ? { locale: field.locale } : {}),
                                    ...(field.derived ? { derived: field.expression } : {}),
                                    ...(field.distribution && !field.derived
                                        ? {
                                              distribution: {
                                                  type: field.distribution,
                                                  ...(field.distribution !== 'uniform'
                                                      ? { mean: field.mean, std: field.std }
                                                      : {}),
                                              },
                                          }
                                        : {}),
                                    ...(field.reference && targetTable && targetField
                                        ? {
                                              ref: `${targetTable.name}.${targetField.name}`,
                                              per_parent: {
                                                  min: field.reference.min,
                                                  max: field.reference.max,
                                              },
                                              relationship: field.reference.cardinality,
                                              integrity: {
                                                  foreign_key: field.reference.enforce,
                                                  on_delete: field.reference.restrict
                                                      ? 'restrict'
                                                      : 'no_action',
                                              },
                                              ...(field.reference.description
                                                  ? {
                                                        reference_description:
                                                            field.reference.description,
                                                    }
                                                  : {}),
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
            rows: Object.fromEntries(doc.tables.map(table => [table.name, table.rows])),
            output: ['postgres', 'mysql', 'sqlite'].includes(doc.output)
                ? { sql: doc.output }
                : { format: doc.output },
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
