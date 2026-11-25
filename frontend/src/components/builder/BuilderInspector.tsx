import { useState } from 'react';
import { Link45deg, Table, Trash3 } from 'react-bootstrap-icons';
import {
    FIELD_TYPES,
    GENERATORS,
    LOCALES,
    arithmeticPreview,
    baseType,
    newReference,
    updateField,
    updateTable,
    validName,
    type BuilderDocument,
    type BuilderField,
    type BuilderTable,
    type Selection,
} from '../../lib/builderModel';
import { Checkbox, Control, NumberInput, Select, Tabs } from './BuilderControls';

type Props = {
    doc: BuilderDocument;
    selection: Selection;
    onChange: (next: BuilderDocument) => void;
    onSelect: (selection: Selection) => void;
    onDelete: () => void;
    onAddTable: () => void;
};

function LocaleOptions({ inherit = false }: { inherit?: boolean }) {
    return (
        <>
            {inherit && <option value="">Из спецификации</option>}
            {LOCALES.map(locale => (
                <option key={locale}>{locale}</option>
            ))}
        </>
    );
}

function DerivedPreview({ table, field }: { table: BuilderTable; field: BuilderField }) {
    const [values, setValues] = useState<Record<string, number>>({});
    const dependencies = table.fields.filter(
        item =>
            item.id !== field.id &&
            ['int', 'decimal'].includes(item.type) &&
            new RegExp(`\\b${item.name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\b`).test(
                field.expression,
            ),
    );
    const examples = Object.fromEntries(
        dependencies.map(item => [item.name, values[item.id] ?? item.min ?? 10]),
    );
    const result = arithmeticPreview(field.expression, examples);
    return (
        <div className="builder-example">
            <strong>Пример значений</strong>
            {dependencies.map(item => (
                <Control
                    key={item.id}
                    label={item.name}
                >
                    <NumberInput
                        value={examples[item.name]}
                        onChange={value => setValues({ ...values, [item.id]: value ?? 0 })}
                    />
                </Control>
            ))}
            <div className="builder-example__result">
                <span>{field.name}</span>
                <code>
                    {result === null
                        ? '—'
                        : field.type === 'decimal'
                          ? result.toFixed(Math.max(0, Math.min(20, field.scale)))
                          : result}
                </code>
            </div>
            <p className="builder-hint">
                Локальный пример поддерживает арифметику. Полная проверка CEL выполняется при
                генерации.
            </p>
        </div>
    );
}

export function BuilderInspector({
    doc,
    selection,
    onChange,
    onSelect,
    onDelete,
    onAddTable,
}: Props) {
    const [tab, setTab] = useState('Основные');
    const table =
        'tableId' in selection ? doc.tables.find(item => item.id === selection.tableId) : undefined;
    const field =
        table && 'fieldId' in selection
            ? table.fields.find(item => item.id === selection.fieldId)
            : undefined;
    const customType =
        selection.kind === 'type'
            ? doc.types.find(item => item.id === selection.typeId)
            : undefined;
    const [typeNameError, setTypeNameError] = useState('');
    const patchField = (patch: Partial<BuilderField>) => {
        if (table && field) onChange(updateField(doc, table.id, field.id, patch));
    };
    const patchTable = (patch: Partial<BuilderTable>) => {
        if (table) onChange(updateTable(doc, table.id, patch));
    };
    const ref = field?.reference;
    const targetTable = doc.tables.find(item => item.id === ref?.tableId);
    const targetField = targetTable?.fields.find(item => item.id === ref?.fieldId);
    const title =
        selection.kind === 'relation'
            ? `Связь: ${field?.name ?? ''}`
            : field
              ? `Поле: ${field.name}`
              : table
                ? 'Таблица'
                : customType
                  ? `Тип: ${customType.name}`
                  : 'Спецификация';
    const subtitle =
        selection.kind === 'relation'
            ? `${table?.name} → ${targetTable?.name ?? 'Не задана'}.${targetField?.name ?? ''}`
            : table
              ? field
                  ? `Таблица: ${table.name}`
                  : table.name
              : customType
                ? 'Пользовательский тип данных'
                : 'Общие настройки генерации';
    const tabs = [
        'Основные',
        'Описание',
        selection.kind === 'relation' ? 'Целостность' : field ? 'Ограничения' : 'Дополнительно',
    ];
    const description =
        selection.kind === 'relation'
            ? (ref?.description ?? '')
            : (field?.description ?? table?.description ?? customType?.description ?? '');
    const setDescription = (value: string) => {
        if (selection.kind === 'relation' && ref)
            patchField({ reference: { ...ref, description: value } });
        else if (field) patchField({ description: value });
        else if (table) patchTable({ description: value });
        else if (customType)
            onChange({
                ...doc,
                types: doc.types.map(item =>
                    item.id === customType.id ? { ...item, description: value } : item,
                ),
            });
    };

    return (
        <aside
            className="builder-inspector"
            aria-label="Настройки выбранного элемента"
        >
            <div className="builder-inspector__heading">
                <h2 title={title}>{title}</h2>
                <p>{subtitle}</p>
            </div>
            {selection.kind !== 'generation' && (
                <Tabs
                    tabs={tabs}
                    active={tab}
                    onChange={setTab}
                    label="Настройки элемента"
                />
            )}

            {selection.kind !== 'generation' && tab === 'Описание' ? (
                <section className="builder-inspector__section">
                    <Control
                        label="Описание"
                        stacked
                    >
                        <textarea
                            rows={7}
                            placeholder="Добавьте описание…"
                            value={description}
                            onChange={event => setDescription(event.target.value)}
                        />
                    </Control>
                </section>
            ) : selection.kind === 'relation' && table && field && ref ? (
                <>
                    {tab === 'Основные' && (
                        <section className="builder-inspector__section">
                            <Control label="Тип связи">
                                <Select
                                    value={ref.cardinality}
                                    onChange={value =>
                                        patchField({
                                            reference: {
                                                ...ref,
                                                cardinality: value as 'N:1' | '1:1',
                                                max: value === '1:1' ? 1 : ref.max,
                                                min:
                                                    value === '1:1'
                                                        ? Math.min(1, ref.min)
                                                        : ref.min,
                                            },
                                        })
                                    }
                                >
                                    <option value="N:1">Многие к одному (N:1)</option>
                                    <option value="1:1">Один к одному (1:1)</option>
                                </Select>
                            </Control>
                            <Control label="Поле">
                                <input
                                    readOnly
                                    value={`${table.name}.${field.name}`}
                                />
                            </Control>
                            <Control label="Ссылается на">
                                <Select
                                    value={`${ref.tableId}/${ref.fieldId}`}
                                    onChange={value => {
                                        const [tableId, fieldId] = value.split('/');
                                        patchField({ reference: { ...ref, tableId, fieldId } });
                                    }}
                                >
                                    {doc.tables.flatMap(item =>
                                        item.fields
                                            .filter(
                                                candidate =>
                                                    candidate.id !== field.id &&
                                                    (candidate.primary || candidate.unique),
                                            )
                                            .map(candidate => (
                                                <option
                                                    key={candidate.id}
                                                    value={`${item.id}/${candidate.id}`}
                                                >
                                                    {item.name}.{candidate.name}
                                                </option>
                                            )),
                                    )}
                                </Select>
                            </Control>
                        </section>
                    )}
                    <section className="builder-inspector__section">
                        <h3>Количество связанных записей</h3>
                        <div className="builder-control-pair">
                            <Control
                                label="Мин. дочерних"
                                stacked
                            >
                                <NumberInput
                                    value={ref.min}
                                    min={0}
                                    step={1}
                                    onChange={value =>
                                        patchField({ reference: { ...ref, min: value ?? 0 } })
                                    }
                                />
                            </Control>
                            <Control
                                label="Макс. дочерних"
                                stacked
                            >
                                <NumberInput
                                    value={ref.max}
                                    min={0}
                                    step={1}
                                    max={ref.cardinality === '1:1' ? 1 : undefined}
                                    onChange={value =>
                                        patchField({ reference: { ...ref, max: value ?? 0 } })
                                    }
                                />
                            </Control>
                        </div>
                    </section>
                    <section className="builder-inspector__section">
                        <h3>Ссылочная целостность</h3>
                        <Checkbox
                            checked={ref.enforce}
                            onChange={value =>
                                patchField({ reference: { ...ref, enforce: value } })
                            }
                        >
                            Проверять существование записи (FK)
                        </Checkbox>
                        <Checkbox
                            checked={ref.restrict}
                            onChange={value =>
                                patchField({ reference: { ...ref, restrict: value } })
                            }
                        >
                            Запретить удаление родительской записи при наличии дочерних (RESTRICT)
                        </Checkbox>
                    </section>
                </>
            ) : field && table ? (
                <>
                    {tab === 'Основные' && (
                        <section className="builder-inspector__section">
                            <Control label="Имя поля">
                                <input
                                    value={field.name}
                                    onChange={event => patchField({ name: event.target.value })}
                                    spellCheck={false}
                                />
                            </Control>
                            <Control label="Тип данных">
                                <Select
                                    value={field.type}
                                    onChange={value => patchField({ type: value })}
                                >
                                    {[...FIELD_TYPES, ...doc.types.map(item => item.name)].map(
                                        type => (
                                            <option key={type}>{type}</option>
                                        ),
                                    )}
                                </Select>
                            </Control>
                            <Checkbox
                                checked={field.derived}
                                onChange={value => patchField({ derived: value })}
                            >
                                Вычисляемое (derived)
                            </Checkbox>
                            {!field.derived && (
                                <>
                                    <Control label="Генератор">
                                        <Select
                                            value={field.generator}
                                            onChange={value => patchField({ generator: value })}
                                        >
                                            {GENERATORS.map(generator => (
                                                <option
                                                    key={generator}
                                                    value={generator}
                                                >
                                                    {generator || 'Не задан'}
                                                </option>
                                            ))}
                                        </Select>
                                    </Control>
                                    <Control label="Ссылка (ref)">
                                        <Select
                                            value={ref ? `${ref.tableId}/${ref.fieldId}` : ''}
                                            onChange={value => {
                                                const [tableId, fieldId] = value.split('/');
                                                patchField({
                                                    reference: value
                                                        ? newReference(tableId, fieldId)
                                                        : undefined,
                                                });
                                            }}
                                        >
                                            <option value="">Не задана</option>
                                            {doc.tables.flatMap(item =>
                                                item.fields
                                                    .filter(
                                                        candidate =>
                                                            candidate.id !== field.id &&
                                                            (candidate.primary || candidate.unique),
                                                    )
                                                    .map(candidate => (
                                                        <option
                                                            key={candidate.id}
                                                            value={`${item.id}/${candidate.id}`}
                                                        >
                                                            {item.name}.{candidate.name}
                                                        </option>
                                                    )),
                                            )}
                                        </Select>
                                    </Control>
                                    {ref && (
                                        <button
                                            className="builder-inline-link"
                                            onClick={() =>
                                                onSelect({
                                                    kind: 'relation',
                                                    tableId: table.id,
                                                    fieldId: field.id,
                                                })
                                            }
                                        >
                                            <Link45deg /> Настроить связь
                                        </button>
                                    )}
                                </>
                            )}
                            {field.derived && (
                                <Control
                                    label="Выражение (CEL)"
                                    stacked
                                >
                                    <div className="builder-expression">
                                        <span>1</span>
                                        <textarea
                                            rows={3}
                                            value={field.expression}
                                            placeholder="base_price * (100 - discount_pct) / 100"
                                            onChange={event =>
                                                patchField({ expression: event.target.value })
                                            }
                                            spellCheck={false}
                                            aria-label="Выражение CEL"
                                        />
                                    </div>
                                </Control>
                            )}
                        </section>
                    )}
                    {['int', 'decimal'].includes(baseType(doc, field.type)) && (
                        <section className="builder-inspector__section">
                            {!field.derived && (
                                <div className="builder-control-pair">
                                    <Control
                                        label="Минимум"
                                        stacked
                                    >
                                        <NumberInput
                                            value={field.min}
                                            onChange={value => patchField({ min: value })}
                                        />
                                    </Control>
                                    <Control
                                        label="Максимум"
                                        stacked
                                    >
                                        <NumberInput
                                            value={field.max}
                                            onChange={value => patchField({ max: value })}
                                        />
                                    </Control>
                                </div>
                            )}
                            {baseType(doc, field.type) === 'decimal' && (
                                <div className="builder-control-pair">
                                    <Control
                                        label="Точность"
                                        stacked
                                    >
                                        <NumberInput
                                            value={field.precision}
                                            min={1}
                                            step={1}
                                            onChange={value =>
                                                patchField({ precision: value ?? 1 })
                                            }
                                        />
                                    </Control>
                                    <Control
                                        label="Масштаб"
                                        stacked
                                    >
                                        <NumberInput
                                            value={field.scale}
                                            min={0}
                                            step={1}
                                            onChange={value => patchField({ scale: value ?? 0 })}
                                        />
                                    </Control>
                                </div>
                            )}
                        </section>
                    )}
                    {(!field.derived || tab === 'Ограничения') && (
                        <section className="builder-inspector__section">
                            <Checkbox
                                checked={field.primary}
                                onChange={value =>
                                    patchField({
                                        primary: value,
                                        ...(value ? { nullRate: 0 } : {}),
                                    })
                                }
                            >
                                Первичный ключ
                            </Checkbox>
                            <Checkbox
                                checked={field.unique}
                                onChange={value => patchField({ unique: value })}
                            >
                                Уникальное
                            </Checkbox>
                            <Checkbox
                                checked={field.pii}
                                onChange={value => patchField({ pii: value })}
                            >
                                PII (чувствительные данные)
                            </Checkbox>
                            <Control
                                label="Доля NULL, %"
                                stacked
                            >
                                <NumberInput
                                    value={field.nullRate}
                                    min={0}
                                    max={100}
                                    onChange={value => patchField({ nullRate: value ?? 0 })}
                                />
                            </Control>
                        </section>
                    )}
                    {!field.derived && tab === 'Основные' && (
                        <section className="builder-inspector__section">
                            {['int', 'decimal'].includes(baseType(doc, field.type)) && (
                                <>
                                    <Control
                                        label="Распределение"
                                        stacked
                                    >
                                        <Select
                                            value={field.distribution}
                                            onChange={value =>
                                                patchField({
                                                    distribution:
                                                        value as BuilderField['distribution'],
                                                })
                                            }
                                        >
                                            <option value="">Не задано</option>
                                            <option value="uniform">uniform</option>
                                            <option value="normal">normal</option>
                                            <option value="lognormal">lognormal</option>
                                        </Select>
                                    </Control>
                                    {['normal', 'lognormal'].includes(field.distribution) && (
                                        <div className="builder-control-pair">
                                            <Control
                                                label="μ (mean)"
                                                stacked
                                            >
                                                <NumberInput
                                                    value={field.mean}
                                                    onChange={value =>
                                                        patchField({ mean: value ?? 0 })
                                                    }
                                                />
                                            </Control>
                                            <Control
                                                label="σ (std)"
                                                stacked
                                            >
                                                <NumberInput
                                                    value={field.std}
                                                    min={0.01}
                                                    onChange={value =>
                                                        patchField({ std: value ?? 1 })
                                                    }
                                                />
                                            </Control>
                                        </div>
                                    )}
                                </>
                            )}
                            <Control label="Локаль">
                                <Select
                                    value={field.locale}
                                    onChange={value => patchField({ locale: value })}
                                >
                                    <LocaleOptions inherit />
                                </Select>
                            </Control>
                            {field.generator && (
                                <div className="builder-example">
                                    <strong>Пример формата</strong>
                                    <code>
                                        {
                                            (
                                                {
                                                    'internet.email': 'ivan.petrov@example.ru',
                                                    'person.fullName': 'Иван Петров',
                                                    'person.firstName': 'Иван',
                                                    'person.lastName': 'Петров',
                                                    'phone.number': '+7 900 123-45-67',
                                                    'address.city': 'Екатеринбург',
                                                    'commerce.productName': 'Книга',
                                                    'random.uuid':
                                                        '550e8400-e29b-41d4-a716-446655440000',
                                                } as Record<string, string>
                                            )[field.generator]
                                        }
                                    </code>
                                </div>
                            )}
                        </section>
                    )}
                    {field.derived && tab === 'Основные' && (
                        <DerivedPreview
                            table={table}
                            field={field}
                        />
                    )}
                </>
            ) : table ? (
                <section className="builder-inspector__section">
                    {tab === 'Основные' && (
                        <>
                            <Control label="Имя таблицы">
                                <input
                                    value={table.name}
                                    onChange={event => patchTable({ name: event.target.value })}
                                    spellCheck={false}
                                />
                            </Control>
                            <Control label="Количество строк">
                                <NumberInput
                                    value={table.rows}
                                    min={1}
                                    step={1}
                                    onChange={value => patchTable({ rows: value ?? 0 })}
                                />
                            </Control>
                        </>
                    )}
                    <Control label="Локаль">
                        <Select
                            value={table.locale}
                            onChange={value => patchTable({ locale: value })}
                        >
                            <LocaleOptions inherit />
                        </Select>
                    </Control>
                    <Control label="Режим">
                        <Select
                            value={table.mode}
                            onChange={value => patchTable({ mode: value as BuilderTable['mode'] })}
                        >
                            <option value="unique">Уникальные</option>
                            <option value="distribution">Распределение</option>
                        </Select>
                    </Control>
                    <Control label="Seed (опционально)">
                        <NumberInput
                            value={table.seed}
                            step={1}
                            placeholder="Не задан"
                            onChange={value => patchTable({ seed: value })}
                        />
                    </Control>
                </section>
            ) : customType ? (
                <section className="builder-inspector__section">
                    <Control label="Имя типа">
                        <input
                            key={customType.name}
                            defaultValue={customType.name}
                            aria-invalid={Boolean(typeNameError)}
                            onChange={() => setTypeNameError('')}
                            onBlur={event => {
                                const name = event.target.value;
                                if (!validName(name)) {
                                    setTypeNameError(
                                        'Используйте латинские буквы, цифры и _. Начните с буквы или _.',
                                    );
                                    return;
                                }
                                if (
                                    (FIELD_TYPES as readonly string[]).includes(name) ||
                                    doc.types.some(
                                        item => item.id !== customType.id && item.name === name,
                                    )
                                ) {
                                    setTypeNameError('Это имя уже используется другим типом.');
                                    return;
                                }
                                onChange({
                                    ...doc,
                                    types: doc.types.map(item =>
                                        item.id === customType.id ? { ...item, name } : item,
                                    ),
                                    tables: doc.tables.map(item => ({
                                        ...item,
                                        fields: item.fields.map(candidate =>
                                            candidate.type === customType.name
                                                ? { ...candidate, type: name }
                                                : candidate,
                                        ),
                                    })),
                                });
                            }}
                            onKeyDown={event => {
                                if (event.key === 'Enter') event.currentTarget.blur();
                            }}
                        />
                    </Control>
                    {typeNameError && (
                        <p
                            className="builder-form-error"
                            role="alert"
                        >
                            {typeNameError}
                        </p>
                    )}
                    <Control label="Базовый тип">
                        <Select
                            value={customType.base}
                            onChange={value =>
                                onChange({
                                    ...doc,
                                    types: doc.types.map(item =>
                                        item.id === customType.id ? { ...item, base: value } : item,
                                    ),
                                })
                            }
                        >
                            {[...FIELD_TYPES, 'enum'].map(type => (
                                <option key={type}>{type}</option>
                            ))}
                        </Select>
                    </Control>
                    {customType.base === 'enum' && (
                        <Control
                            label="Значения через запятую"
                            stacked
                        >
                            <textarea
                                rows={4}
                                value={customType.values}
                                placeholder="new, processing, completed"
                                onChange={event =>
                                    onChange({
                                        ...doc,
                                        types: doc.types.map(item =>
                                            item.id === customType.id
                                                ? { ...item, values: event.target.value }
                                                : item,
                                        ),
                                    })
                                }
                            />
                        </Control>
                    )}
                </section>
            ) : (
                <>
                    <section className="builder-inspector__section">
                        <Control
                            label="Название"
                            stacked
                        >
                            <input
                                value={doc.name}
                                onChange={event => onChange({ ...doc, name: event.target.value })}
                            />
                        </Control>
                        <Control label="Версия">
                            <input
                                value={doc.version}
                                onChange={event =>
                                    onChange({ ...doc, version: event.target.value })
                                }
                            />
                        </Control>
                        <Control label="Локаль">
                            <Select
                                value={doc.locale}
                                onChange={value => onChange({ ...doc, locale: value })}
                            >
                                <LocaleOptions />
                            </Select>
                        </Control>
                        <Control label="Seed">
                            <NumberInput
                                value={doc.seed}
                                step={1}
                                placeholder="Не задан"
                                onChange={value => onChange({ ...doc, seed: value })}
                            />
                        </Control>
                        <Control label="Формат вывода">
                            <Select
                                value={doc.output}
                                onChange={value =>
                                    onChange({ ...doc, output: value as BuilderDocument['output'] })
                                }
                            >
                                <option value="postgres">SQL · PostgreSQL</option>
                                <option value="mysql">SQL · MySQL</option>
                                <option value="sqlite">SQL · SQLite</option>
                                <option value="json">JSON</option>
                                <option value="csv">CSV</option>
                            </Select>
                        </Control>
                    </section>
                    {!doc.tables.length && (
                        <div className="builder-inspector__empty">
                            <Table size={28} />
                            <p>Начните с таблицы, затем добавьте поля и связи.</p>
                            <button
                                className="builder-button builder-button--primary"
                                onClick={onAddTable}
                            >
                                Добавить таблицу
                            </button>
                        </div>
                    )}
                </>
            )}
            {selection.kind !== 'generation' && (
                <button
                    className="builder-button builder-delete"
                    onClick={onDelete}
                >
                    <Trash3 size={14} />
                    Удалить{' '}
                    {selection.kind === 'relation'
                        ? 'связь'
                        : field
                          ? 'поле'
                          : table
                            ? 'таблицу'
                            : 'тип'}
                </button>
            )}
        </aside>
    );
}
