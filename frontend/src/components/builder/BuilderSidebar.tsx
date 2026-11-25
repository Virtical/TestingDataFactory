import { useState } from 'react';
import { Link } from 'react-router-dom';
import {
    Box,
    ChevronDown,
    ChevronRight,
    Folder2,
    Gear,
    Globe2,
    Layers,
    PlusLg,
    Table,
} from 'react-bootstrap-icons';
import type { BuilderDocument, Selection } from '../../lib/builderModel';

export function BuilderSidebar({
    doc,
    selection,
    onSelect,
    onAddTable,
    onAddType,
}: {
    doc: BuilderDocument;
    selection: Selection;
    onSelect: (selection: Selection) => void;
    onAddTable: () => void;
    onAddType: () => void;
}) {
    const [expanded, setExpanded] = useState<string[]>([]);
    return (
        <aside
            className="builder-sidebar"
            aria-label="Структура спецификации"
        >
            <Link
                to="/dashboard"
                className="builder-sidebar__logo"
            >
                <Box size={27} />
                <span>DataForge</span>
            </Link>
            <div className="builder-sidebar__title">
                <span>Структура спецификации</span>
                <button
                    className="builder-mini-button"
                    title="Добавить таблицу"
                    aria-label="Добавить таблицу в структуру"
                    onClick={onAddTable}
                >
                    <PlusLg />
                </button>
            </div>
            <div className="builder-sidebar__scroll">
                <section className="builder-tree-section">
                    <button
                        className="builder-tree-row"
                        onClick={() => onSelect({ kind: 'generation' })}
                    >
                        <ChevronDown size={11} />
                        <Folder2 size={19} />
                        <strong>spec_version</strong>
                        <span className="builder-count">{doc.version}</span>
                    </button>
                    <button
                        className="builder-tree-row builder-tree-row--indent"
                        onClick={() => onSelect({ kind: 'generation' })}
                    >
                        <Globe2 size={19} />
                        <strong>locale</strong>
                        <span className="builder-count">{doc.locale}</span>
                    </button>
                </section>
                <section className="builder-tree-section">
                    <details open>
                        <summary>
                            <ChevronDown size={11} />
                            <Layers size={19} />
                            <strong>types</strong>
                            <span className="builder-count">{doc.types.length}</span>
                        </summary>
                        {doc.types.map(type => (
                            <button
                                key={type.id}
                                className={`builder-tree-item${
                                    selection.kind === 'type' && selection.typeId === type.id
                                        ? ' is-selected'
                                        : ''
                                }`}
                                onClick={() => onSelect({ kind: 'type', typeId: type.id })}
                            >
                                {type.name || 'Без имени'}
                            </button>
                        ))}
                        {!doc.types.length && (
                            <p className="builder-tree-empty">Пользовательские типы</p>
                        )}
                    </details>
                    <button
                        className="builder-mini-button builder-tree-add"
                        title="Добавить тип"
                        aria-label="Добавить тип"
                        onClick={onAddType}
                    >
                        <PlusLg />
                    </button>
                </section>
                <section className="builder-tree-section">
                    <details open>
                        <summary>
                            <ChevronDown size={11} />
                            <Table size={19} />
                            <strong>tables</strong>
                            <span className="builder-count">{doc.tables.length}</span>
                        </summary>
                        {doc.tables.map(table => (
                            <div key={table.id}>
                                <div
                                    className={`builder-tree-table${
                                        'tableId' in selection && selection.tableId === table.id
                                            ? ' is-selected'
                                            : ''
                                    }`}
                                >
                                    <button
                                        aria-label={`${
                                            expanded.includes(table.id) ? 'Свернуть' : 'Развернуть'
                                        } ${table.name}`}
                                        aria-expanded={expanded.includes(table.id)}
                                        onClick={() =>
                                            setExpanded(
                                                expanded.includes(table.id)
                                                    ? expanded.filter(id => id !== table.id)
                                                    : [...expanded, table.id],
                                            )
                                        }
                                    >
                                        {expanded.includes(table.id) ? (
                                            <ChevronDown size={11} />
                                        ) : (
                                            <ChevronRight size={11} />
                                        )}
                                    </button>
                                    <button
                                        onClick={() =>
                                            onSelect({ kind: 'table', tableId: table.id })
                                        }
                                    >
                                        {table.name || 'Без имени'}
                                    </button>
                                </div>
                                {expanded.includes(table.id) &&
                                    table.fields.map(field => (
                                        <button
                                            key={field.id}
                                            className={`builder-tree-item builder-tree-item--field${
                                                'fieldId' in selection &&
                                                selection.fieldId === field.id
                                                    ? ' is-selected'
                                                    : ''
                                            }`}
                                            onClick={() =>
                                                onSelect({
                                                    kind: 'field',
                                                    tableId: table.id,
                                                    fieldId: field.id,
                                                })
                                            }
                                        >
                                            {field.name}
                                            <span>
                                                {field.primary ? 'PK' : field.reference ? 'FK' : ''}
                                            </span>
                                        </button>
                                    ))}
                            </div>
                        ))}
                        {!doc.tables.length && (
                            <p className="builder-tree-empty">Таблиц пока нет</p>
                        )}
                    </details>
                    <button
                        className="builder-mini-button builder-tree-add"
                        title="Добавить таблицу"
                        aria-label="Добавить таблицу"
                        onClick={onAddTable}
                    >
                        <PlusLg />
                    </button>
                </section>
                <section className="builder-tree-section">
                    <button
                        className={`builder-tree-row${selection.kind === 'generation' ? ' is-selected' : ''}`}
                        onClick={() => onSelect({ kind: 'generation' })}
                    >
                        <ChevronDown size={11} />
                        <Gear size={19} />
                        <strong>generation</strong>
                    </button>
                    <button
                        className="builder-tree-item"
                        onClick={() => onSelect({ kind: 'generation' })}
                    >
                        seed: {doc.seed ?? 'не задан'}
                    </button>
                    <button
                        className="builder-tree-item"
                        onClick={() => onSelect({ kind: 'generation' })}
                        title={doc.tables.map(table => `${table.name}: ${table.rows}`).join(', ')}
                    >
                        rows: {'{ '}
                        {doc.tables.map(table => `${table.name}: ${table.rows}`).join(', ')}
                        {' }'}
                    </button>
                    <button
                        className="builder-tree-item"
                        onClick={() => onSelect({ kind: 'generation' })}
                    >
                        output: {doc.output === 'postgres' ? 'PostgreSQL' : doc.output.toUpperCase()}
                    </button>
                </section>
            </div>
            <div className="builder-sidebar__footer">
                Визуальный конструктор<span>Спецификация · v{doc.version}</span>
            </div>
        </aside>
    );
}
