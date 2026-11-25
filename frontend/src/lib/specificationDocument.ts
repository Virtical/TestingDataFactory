import { isMap, parseDocument } from 'yaml';

export type SpecificationFile = {
    fileName: string;
    content: string;
};

export function isSpecificationFile(value: unknown): value is SpecificationFile {
    return isRecord(value) && typeof value.fileName === 'string' && typeof value.content === 'string';
}

export function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function readSpecification(file: SpecificationFile) {
    const format = /\.json$/i.test(file.fileName) ? 'JSON' : 'YAML';
    try {
        if (!file.content.trim()) throw new Error('Спецификация пуста. Добавьте содержимое.');
        if (format === 'JSON') JSON.parse(file.content);
        const document = parseDocument(file.content);
        if (document.errors.length) throw document.errors[0];
        if (!isMap(document.contents)) throw new Error('Спецификация должна содержать объект с параметрами и таблицами.');
        const data: unknown = document.toJS({ maxAliasCount: 100 });
        if (!isRecord(data)) throw new Error('Не удалось прочитать структуру спецификации.');
        return { format, document, data, error: null };
    } catch (error) {
        return {
            format,
            document: null,
            data: null,
            error: error instanceof Error ? error.message : 'Проверьте синтаксис спецификации.',
        };
    }
}

export function specificationTitle(file: SpecificationFile, data: Record<string, unknown> | null) {
    return typeof data?.name === 'string' && data.name.trim()
        ? data.name
        : file.fileName.replace(/\.(ya?ml|json)$/i, '');
}

export function specificationTables(data: Record<string, unknown> | null) {
    if (!isRecord(data?.tables)) return [];
    const generation = isRecord(data.generation) ? data.generation : {};
    const rows = isRecord(generation.rows) ? generation.rows : {};
    return Object.entries(data.tables).filter((entry): entry is [string, Record<string, unknown>] => isRecord(entry[1]))
        .map(([name, table]) => ({
            name,
            rows: typeof rows[name] === 'number' ? rows[name] : typeof table.rows === 'number' ? table.rows : null,
            fields: isRecord(table.fields) ? Object.entries(table.fields) : [],
        }));
}
