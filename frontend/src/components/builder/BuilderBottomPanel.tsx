import { CheckCircle, Clock, ExclamationCircle, PlusLg, Trash3 } from 'react-bootstrap-icons';
import {
    newId,
    type BuilderDocument,
    type Diagnostic,
    type Selection,
} from '../../lib/builderModel';
import { Select } from './BuilderControls';

export type BuilderEvent = { id: string; message: string; time: string };

export function BuilderBottomPanel({
    doc,
    onChange,
    diagnostics,
    events,
    tab,
    onTabChange,
    selection,
    onSelect,
}: {
    doc: BuilderDocument;
    onChange: (doc: BuilderDocument, event?: string) => void;
    diagnostics: Diagnostic[];
    events: BuilderEvent[];
    tab: string;
    onTabChange: (value: string) => void;
    selection: Selection;
    onSelect: (selection: Selection) => void;
}) {
    const errors = diagnostics.filter(item => item.severity === 'error');

    const addRule = () =>
        onChange(
            {
                ...doc,
                rules: [
                    ...doc.rules,
                    {
                        id: newId(),
                        tableId: 'tableId' in selection ? selection.tableId : doc.tables[0].id,
                        expression: '',
                        message: '',
                    },
                ],
            },
            'Добавлено правило CEL',
        );

    return (
        <section
            className="builder-bottom"
            aria-label="Правила и диагностика"
        >
            <div
                className="builder-bottom__tabs"
                role="tablist"
                aria-label="Проверка спецификации"
            >
                {['Правила (CEL)', 'События', 'Диагностика', 'Валидация'].map(item => (
                    <button
                        role="tab"
                        type="button"
                        key={item}
                        aria-selected={tab === item}
                        className={tab === item ? 'is-active' : ''}
                        onClick={() => onTabChange(item)}
                    >
                        {item}
                        {item === 'Диагностика' && diagnostics.length > 0 && (
                            <span className="builder-count">{diagnostics.length}</span>
                        )}
                        {item === 'Валидация' &&
                            doc.tables.length > 0 &&
                            (errors.length ? (
                                <ExclamationCircle
                                    className="builder-text-warning"
                                    size={17}
                                />
                            ) : (
                                <CheckCircle
                                    className="builder-text-success"
                                    size={17}
                                />
                            ))}
                    </button>
                ))}
            </div>
            <div
                className="builder-bottom__content"
                role="tabpanel"
                aria-label={tab}
            >
                {tab === 'Правила (CEL)' ? (
                    <>
                        {!doc.rules.length && (
                            <div className="builder-bottom__empty">
                                <strong>Правила не добавлены</strong>
                                <p>Добавьте правила для проверки зависимостей между полями</p>
                            </div>
                        )}

                        {doc.rules.map((rule, index) => (
                            <div
                                className="builder-rule"
                                key={rule.id}
                            >
                                <span className="builder-rule__number">{index + 1}</span>
                                <Select
                                    value={rule.tableId}
                                    onChange={value =>
                                        onChange({
                                            ...doc,
                                            rules: doc.rules.map(item =>
                                                item.id === rule.id
                                                    ? { ...item, tableId: value }
                                                    : item,
                                            ),
                                        })
                                    }
                                    label={`Таблица правила ${index + 1}`}
                                >
                                    {doc.tables.map(table => (
                                        <option
                                            key={table.id}
                                            value={table.id}
                                        >
                                            {table.name}
                                        </option>
                                    ))}
                                </Select>
                                <input
                                    aria-label={`Выражение правила ${index + 1}`}
                                    value={rule.expression}
                                    placeholder="end_date == null || end_date >= start_date"
                                    spellCheck={false}
                                    onChange={event =>
                                        onChange({
                                            ...doc,
                                            rules: doc.rules.map(item =>
                                                item.id === rule.id
                                                    ? { ...item, expression: event.target.value }
                                                    : item,
                                            ),
                                        })
                                    }
                                />
                                <Clock
                                    size={16}
                                    title="Ожидает проверки CEL при генерации"
                                />
                                <button
                                    aria-label={`Удалить правило ${index + 1}`}
                                    title="Удалить правило"
                                    onClick={() =>
                                        onChange(
                                            {
                                                ...doc,
                                                rules: doc.rules.filter(
                                                    item => item.id !== rule.id,
                                                ),
                                            },
                                            'Удалено правило CEL',
                                        )
                                    }
                                >
                                    <Trash3 size={14} />
                                </button>
                            </div>
                        ))}
                        <div
                            className={`builder-rule-actions${
                                !doc.rules.length ? ' builder-rule-actions--center' : ''
                            }`}
                        >
                            <button
                                className="builder-button"
                                onClick={addRule}
                                disabled={!doc.tables.length}
                            >
                                <PlusLg size={12} /> Добавить правило
                            </button>
                            {doc.rules.length > 0 && (
                                <span className="builder-hint">CEL проверяется при генерации.</span>
                            )}
                        </div>
                    </>
                ) : tab === 'События' ? (
                    events.length ? (
                        events.map(event => (
                            <div
                                className="builder-event"
                                key={event.id}
                            >
                                <CheckCircle
                                    size={17}
                                    className="builder-text-success"
                                />
                                <span>{event.message}</span>
                                <time>{event.time}</time>
                            </div>
                        ))
                    ) : (
                        <div className="builder-bottom__empty">
                            Здесь появится история действий со схемой.
                        </div>
                    )
                ) : tab === 'Диагностика' ? (
                    diagnostics.length ? (
                        diagnostics.map((item, index) => (
                            <button
                                className="builder-diagnostic"
                                key={index}
                                onClick={() => item.selection && onSelect(item.selection)}
                            >
                                <ExclamationCircle
                                    size={17}
                                    className={
                                        item.severity === 'error'
                                            ? 'builder-text-error'
                                            : 'builder-text-warning'
                                    }
                                />
                                <span>{item.message}</span>
                            </button>
                        ))
                    ) : (
                        <div className="builder-validation">
                            <CheckCircle
                                size={25}
                                className="builder-text-success"
                            />
                            <div>
                                <strong>Ошибок структуры не найдено</strong>
                                <p>Имена, типы, диапазоны и связи согласованы.</p>
                            </div>
                        </div>
                    )
                ) : (
                    <div className="builder-validation">
                        {errors.length || !doc.tables.length ? (
                            <ExclamationCircle
                                size={27}
                                className="builder-text-warning"
                            />
                        ) : (
                            <CheckCircle
                                size={27}
                                className="builder-text-success"
                            />
                        )}
                        <div>
                            <strong>
                                {!doc.tables.length
                                    ? 'Добавьте таблицу для проверки'
                                    : errors.length
                                      ? `Ошибок структуры: ${errors.length}`
                                      : 'Структура спецификации корректна'}
                            </strong>
                            <p>Локально проверены имена, типы полей, связи и ограничения.</p>
                            <p>
                                Выражения CEL и сгенерированные данные требуют проверки на сервере.
                            </p>
                            {errors.length > 0 && (
                                <button
                                    className="builder-inline-link"
                                    onClick={() => onTabChange('Диагностика')}
                                >
                                    Посмотреть диагностику
                                </button>
                            )}
                        </div>
                    </div>
                )}
            </div>
        </section>
    );
}
