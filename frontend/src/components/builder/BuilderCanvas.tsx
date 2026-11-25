import { useRef, useState, type PointerEvent } from 'react';
import {
    ArrowsFullscreen,
    Cursor,
    HandIndex,
    Link45deg,
    PlusSquare,
    ZoomIn,
    ZoomOut,
} from 'react-bootstrap-icons';
import {
    fieldTypeLabel,
    updateTable,
    type BuilderDocument,
    type Selection,
} from '../../lib/builderModel';
import { Select } from './BuilderControls';

const TABLE_WIDTH = 252;
const HEADER_HEIGHT = 42;
const FIELD_HEIGHT = 34;
type Props = {
    doc: BuilderDocument;
    selection: Selection;
    onSelect: (selection: Selection) => void;
    onChange: (doc: BuilderDocument, event?: string) => void;
    onAddTable: () => void;
    onAddField: (id: string) => void;
    onConnect: (
        sourceTableId: string,
        sourceFieldId: string,
        targetTableId: string,
        targetFieldId: string,
    ) => void;
};
type Drag = {
    id: string;
    startX: number;
    startY: number;
    originalX: number;
    originalY: number;
    x: number;
    y: number;
};

export function BuilderCanvas({
    doc,
    selection,
    onSelect,
    onChange,
    onAddTable,
    onAddField,
    onConnect,
}: Props) {
    const viewportRef = useRef<HTMLDivElement>(null);
    const [zoom, setZoom] = useState(1);
    const [tool, setTool] = useState<'select' | 'pan' | 'link'>('select');
    const [source, setSource] = useState<{ tableId: string; fieldId: string } | null>(null);
    const [drag, setDrag] = useState<Drag | null>(null);
    const pan = useRef<{ x: number; y: number; left: number; top: number } | null>(null);
    const tables = doc.tables.map(table =>
        table.id === drag?.id ? { ...table, x: drag.x, y: drag.y } : table,
    );
    const width = Math.max(1200, ...tables.map(table => table.x + TABLE_WIDTH + 100));
    const height = Math.max(
        900,
        ...tables.map(table => table.y + HEADER_HEIGHT + table.fields.length * FIELD_HEIGHT + 100),
    );

    const selectField = (tableId: string, fieldId: string) => {
        if (tool === 'link') {
            if (!source) setSource({ tableId, fieldId });
            else {
                if (source.fieldId !== fieldId)
                    onConnect(source.tableId, source.fieldId, tableId, fieldId);
                setSource(null);
                setTool('select');
            }
        } else onSelect({ kind: 'field', tableId, fieldId });
    };
    const startDrag = (
        event: PointerEvent<HTMLButtonElement>,
        id: string,
        x: number,
        y: number,
    ) => {
        if (event.button !== 0 || tool !== 'select') return;
        event.currentTarget.setPointerCapture(event.pointerId);
        onSelect({ kind: 'table', tableId: id });
        setDrag({
            id,
            startX: event.clientX,
            startY: event.clientY,
            originalX: x,
            originalY: y,
            x,
            y,
        });
    };
    const finishDrag = () => {
        if (drag && (drag.x !== drag.originalX || drag.y !== drag.originalY))
            onChange(
                updateTable(doc, drag.id, { x: drag.x, y: drag.y }),
                'Изменено расположение таблицы',
            );
        setDrag(null);
    };
    const fit = () => {
        const viewport = viewportRef.current;
        if (!viewport || !tables.length) return;
        const left = Math.min(...tables.map(table => table.x));
        const top = Math.min(...tables.map(table => table.y));
        const right = Math.max(...tables.map(table => table.x + TABLE_WIDTH));
        const bottom = Math.max(
            ...tables.map(
                table => table.y + HEADER_HEIGHT + table.fields.length * FIELD_HEIGHT + 34,
            ),
        );
        const nextZoom = Math.max(
            0.25,
            Math.min(
                1.5,
                (viewport.clientWidth - 80) / (right - left),
                (viewport.clientHeight - 80) / (bottom - top),
            ),
        );
        setZoom(nextZoom);
        requestAnimationFrame(() =>
            viewport.scrollTo({
                left: Math.max(0, left * nextZoom - 40),
                top: Math.max(0, top * nextZoom - 40),
            }),
        );
    };

    return (
        <section
            className={`builder-canvas builder-canvas--${tool}`}
            aria-label="Схема таблиц"
        >
            <div className="builder-canvas__toolbar">
                <div className="builder-tool-group">
                    <button
                        title="Выбрать и переместить"
                        aria-label="Выбрать и переместить"
                        aria-pressed={tool === 'select'}
                        className={tool === 'select' ? 'is-active' : ''}
                        onClick={() => {
                            setTool('select');
                            setSource(null);
                        }}
                    >
                        <Cursor size={19} />
                    </button>
                    <button
                        title="Перемещение холста"
                        aria-label="Перемещение холста"
                        aria-pressed={tool === 'pan'}
                        className={tool === 'pan' ? 'is-active' : ''}
                        onClick={() => {
                            setTool('pan');
                            setSource(null);
                        }}
                    >
                        <HandIndex size={19} />
                    </button>
                </div>
                <div className="builder-tool-group">
                    <button
                        title="Добавить таблицу"
                        aria-label="Добавить таблицу на холст"
                        onClick={onAddTable}
                    >
                        <PlusSquare size={19} />
                    </button>
                    <button
                        title="Создать связь"
                        aria-label="Создать связь"
                        aria-pressed={tool === 'link'}
                        className={tool === 'link' ? 'is-active' : ''}
                        onClick={() => {
                            setTool(tool === 'link' ? 'select' : 'link');
                            setSource(null);
                        }}
                    >
                        <Link45deg size={21} />
                    </button>
                </div>
                <div className="builder-canvas__zoom">
                    <span>Масштаб</span>
                    <Select
                        value={String(zoom)}
                        onChange={value => setZoom(Number(value))}
                        label="Масштаб схемы"
                    >
                        {[0.25, 0.5, 0.75, 1, 1.25, 1.5, 2].map(value => (
                            <option
                                key={value}
                                value={value}
                            >
                                {value * 100}%
                            </option>
                        ))}
                        {![0.25, 0.5, 0.75, 1, 1.25, 1.5, 2].includes(zoom) && (
                            <option value={zoom}>{Math.round(zoom * 100)}%</option>
                        )}
                    </Select>
                    <div className="builder-tool-group">
                        <button
                            title="Увеличить"
                            aria-label="Увеличить масштаб"
                            disabled={zoom >= 2}
                            onClick={() => setZoom(Math.min(2, zoom + 0.25))}
                        >
                            <ZoomIn size={19} />
                        </button>
                        <button
                            title="Уменьшить"
                            aria-label="Уменьшить масштаб"
                            disabled={zoom <= 0.25}
                            onClick={() => setZoom(Math.max(0.25, zoom - 0.25))}
                        >
                            <ZoomOut size={19} />
                        </button>
                    </div>
                    <button
                        className="builder-icon-button"
                        title="Показать всю схему"
                        aria-label="Показать всю схему"
                        onClick={fit}
                        disabled={!tables.length}
                    >
                        <ArrowsFullscreen size={18} />
                    </button>
                </div>
            </div>
            {tool === 'link' && (
                <div
                    className="builder-canvas__instruction"
                    role="status"
                >
                    {source
                        ? 'Выберите первичный или уникальный ключ, на который ссылается поле.'
                        : 'Выберите поле внешнего ключа, затем поле, на которое оно ссылается.'}
                    <button
                        onClick={() => {
                            setTool('select');
                            setSource(null);
                        }}
                    >
                        Отмена
                    </button>
                </div>
            )}
            <div
                className="builder-canvas__viewport"
                ref={viewportRef}
                onPointerDown={event => {
                    if (tool !== 'pan' || event.button !== 0) return;
                    event.currentTarget.setPointerCapture(event.pointerId);
                    pan.current = {
                        x: event.clientX,
                        y: event.clientY,
                        left: event.currentTarget.scrollLeft,
                        top: event.currentTarget.scrollTop,
                    };
                }}
                onPointerMove={event => {
                    if (pan.current) {
                        event.currentTarget.scrollLeft =
                            pan.current.left - event.clientX + pan.current.x;
                        event.currentTarget.scrollTop =
                            pan.current.top - event.clientY + pan.current.y;
                    }
                }}
                onPointerUp={() => {
                    pan.current = null;
                }}
                onPointerCancel={() => {
                    pan.current = null;
                }}
            >
                {!tables.length ? (
                    <div className="builder-canvas__empty">
                        <div>
                            <PlusSquare size={30} />
                        </div>
                        <h2>Ваша схема начинается здесь</h2>
                        <p>Добавьте первую таблицу, чтобы настроить поля и связи.</p>
                        <button
                            className="builder-button builder-button--primary"
                            onClick={onAddTable}
                        >
                            Добавить таблицу
                        </button>
                    </div>
                ) : (
                    <div style={{ width: width * zoom, height: height * zoom }}>
                        <div
                            className="builder-canvas__world"
                            style={{ width, height, transform: `scale(${zoom})` }}
                        >
                            <svg
                                className="builder-connections"
                                width={width}
                                height={height}
                                aria-label="Связи таблиц"
                            >
                                {tables.flatMap(table =>
                                    table.fields.map((field, fieldIndex) => {
                                        const ref = field.reference;
                                        if (!ref) return null;
                                        const target = tables.find(item => item.id === ref.tableId);
                                        const targetIndex =
                                            target?.fields.findIndex(
                                                item => item.id === ref.fieldId,
                                            ) ?? -1;
                                        if (!target || targetIndex < 0) return null;
                                        const right = target.x > table.x + TABLE_WIDTH / 2;
                                        const self = target.id === table.id;
                                        const startX = table.x + (right || self ? TABLE_WIDTH : 0);
                                        const startY =
                                            table.y +
                                            HEADER_HEIGHT +
                                            fieldIndex * FIELD_HEIGHT +
                                            FIELD_HEIGHT / 2;
                                        const endX = target.x + (right && !self ? 0 : TABLE_WIDTH);
                                        const endY =
                                            target.y +
                                            HEADER_HEIGHT +
                                            targetIndex * FIELD_HEIGHT +
                                            FIELD_HEIGHT / 2;
                                        const bend = self
                                            ? 100
                                            : Math.max(70, Math.abs(endX - startX) * 0.5);
                                        const path = [
                                            `M ${startX} ${startY}`,
                                            `C ${startX + (right || self ? bend : -bend)} ${startY},`,
                                            `${endX + (right && !self ? -bend : bend)} ${endY},`,
                                            `${endX} ${endY}`,
                                        ].join(' ');
                                        const selected =
                                            selection.kind === 'relation' &&
                                            selection.fieldId === field.id;
                                        const select = () =>
                                            onSelect({
                                                kind: 'relation',
                                                tableId: table.id,
                                                fieldId: field.id,
                                            });
                                        return (
                                            <g
                                                key={field.id}
                                                className={`builder-connection${selected ? ' is-selected' : ''}`}
                                                role="button"
                                                tabIndex={0}
                                                aria-label={`Связь ${table.name}.${field.name} → ${
                                                    target.name
                                                }.${target.fields[targetIndex].name}`}
                                                onClick={select}
                                                onKeyDown={event => {
                                                    if (
                                                        event.key === 'Enter' ||
                                                        event.key === ' '
                                                    ) {
                                                        event.preventDefault();
                                                        select();
                                                    }
                                                }}
                                            >
                                                <path
                                                    className="builder-connection__hit"
                                                    d={path}
                                                />
                                                <path
                                                    className="builder-connection__line"
                                                    d={path}
                                                />
                                                <circle
                                                    cx={startX + (right || self ? 13 : -13)}
                                                    cy={startY}
                                                    r={10}
                                                />
                                                <text
                                                    x={startX + (right || self ? 13 : -13)}
                                                    y={startY + 4}
                                                >
                                                    {ref.cardinality === '1:1' ? '1' : 'N'}
                                                </text>
                                                <circle
                                                    cx={endX + (right && !self ? -13 : 13)}
                                                    cy={endY}
                                                    r={10}
                                                />
                                                <text
                                                    x={endX + (right && !self ? -13 : 13)}
                                                    y={endY + 4}
                                                >
                                                    1
                                                </text>
                                            </g>
                                        );
                                    }),
                                )}
                            </svg>
                            {tables.map(table => (
                                <article
                                    key={table.id}
                                    className={`builder-table${
                                        'tableId' in selection && selection.tableId === table.id
                                            ? ' is-selected'
                                            : ''
                                    }`}
                                    style={{ left: table.x, top: table.y, width: TABLE_WIDTH }}
                                    aria-label={`Таблица ${table.name}`}
                                >
                                    <div className="builder-table__header">
                                        <button
                                            className="builder-table__drag"
                                            title="Перетащите таблицу; стрелки на клавиатуре перемещают её"
                                            onClick={() =>
                                                onSelect({ kind: 'table', tableId: table.id })
                                            }
                                            onPointerDown={event =>
                                                startDrag(event, table.id, table.x, table.y)
                                            }
                                            onPointerMove={event => {
                                                if (drag?.id === table.id)
                                                    setDrag({
                                                        ...drag,
                                                        x: Math.max(
                                                            20,
                                                            drag.originalX +
                                                                (event.clientX - drag.startX) /
                                                                    zoom,
                                                        ),
                                                        y: Math.max(
                                                            20,
                                                            drag.originalY +
                                                                (event.clientY - drag.startY) /
                                                                    zoom,
                                                        ),
                                                    });
                                            }}
                                            onPointerUp={finishDrag}
                                            onPointerCancel={() => setDrag(null)}
                                            onKeyDown={event => {
                                                const moves: Record<string, [number, number]> = {
                                                    ArrowLeft: [-20, 0],
                                                    ArrowRight: [20, 0],
                                                    ArrowUp: [0, -20],
                                                    ArrowDown: [0, 20],
                                                };
                                                const move = moves[event.key];
                                                if (move) {
                                                    event.preventDefault();
                                                    onChange(
                                                        updateTable(doc, table.id, {
                                                            x: Math.max(20, table.x + move[0]),
                                                            y: Math.max(20, table.y + move[1]),
                                                        }),
                                                        'Таблица перемещена',
                                                    );
                                                }
                                            }}
                                        >
                                            {table.name || 'Без имени'}
                                        </button>
                                        <input
                                            type="number"
                                            min={1}
                                            step={1}
                                            aria-label={`Количество строк ${table.name}`}
                                            value={table.rows}
                                            onChange={event =>
                                                onChange(
                                                    updateTable(doc, table.id, {
                                                        rows: Number(event.target.value),
                                                    }),
                                                )
                                            }
                                        />
                                        <span>строк</span>
                                    </div>
                                    {table.fields.map(field => (
                                        <button
                                            key={field.id}
                                            className={`builder-table__field${
                                                ('fieldId' in selection &&
                                                    selection.fieldId === field.id) ||
                                                source?.fieldId === field.id
                                                    ? ' is-selected'
                                                    : ''
                                            }`}
                                            onClick={() => selectField(table.id, field.id)}
                                            aria-label={`Поле ${table.name}.${field.name}`}
                                            aria-pressed={
                                                'fieldId' in selection &&
                                                selection.fieldId === field.id
                                            }
                                        >
                                            <span title={field.name}>
                                                {field.name || 'Без имени'}
                                            </span>
                                            <span
                                                className="builder-table__type"
                                                title={fieldTypeLabel(field)}
                                            >
                                                {fieldTypeLabel(field)}
                                            </span>
                                            <span className="builder-table__badges">
                                                {field.primary ? (
                                                    <b>PK</b>
                                                ) : field.reference ? (
                                                    <b>FK</b>
                                                ) : field.unique ? (
                                                    <b>UQ</b>
                                                ) : field.derived ? (
                                                    <b>ƒx</b>
                                                ) : null}
                                            </span>
                                        </button>
                                    ))}
                                    <button
                                        className="builder-table__add"
                                        onClick={() => onAddField(table.id)}
                                    >
                                        + Добавить поле
                                    </button>
                                </article>
                            ))}
                        </div>
                    </div>
                )}
            </div>
            {tables.length > 0 && (
                <div className="builder-canvas__legend">
                    <span>PK — первичный ключ</span>
                    <span>FK — внешний ключ</span>
                    <span>UQ — уникальное</span>
                </div>
            )}
        </section>
    );
}
