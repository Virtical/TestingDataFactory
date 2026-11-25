import { useEffect, useRef, useState, type ReactNode } from 'react';
import { XLg } from 'react-bootstrap-icons';
import { FIELD_TYPES, uniqueName, validName, type BuilderDocument } from '../../lib/builderModel';
import { Control, NumberInput, Select } from './BuilderControls';

export function BuilderDialog({
    title,
    children,
    onClose,
}: {
    title: string;
    children: ReactNode;
    onClose: () => void;
}) {
    const ref = useRef<HTMLDialogElement>(null);
    useEffect(() => {
        const dialog = ref.current;
        const previous =
            document.activeElement instanceof HTMLElement ? document.activeElement : null;
        dialog?.showModal();
        return () => {
            dialog?.close();
            previous?.focus();
        };
    }, []);
    return (
        <dialog
            ref={ref}
            className="builder-dialog"
            aria-labelledby="builder-dialog-title"
            onCancel={event => {
                event.preventDefault();
                onClose();
            }}
        >
            <div className="builder-dialog__header">
                <h2 id="builder-dialog-title">{title}</h2>
                <button
                    type="button"
                    aria-label="Закрыть окно"
                    onClick={onClose}
                >
                    <XLg size={14} />
                </button>
            </div>
            {children}
        </dialog>
    );
}

export function CreateItemDialog({
    kind,
    doc,
    tableId,
    onClose,
    onCreate,
}: {
    kind: 'table' | 'field' | 'type';
    doc: BuilderDocument;
    tableId?: string;
    onClose: () => void;
    onCreate: (name: string, rows: number, type: string) => void;
}) {
    const table = doc.tables.find(item => item.id === tableId);
    const names =
        kind === 'table'
            ? doc.tables.map(item => item.name)
            : kind === 'type'
              ? [...FIELD_TYPES, ...doc.types.map(item => item.name)]
              : (table?.fields.map(item => item.name) ?? []);
    const [name, setName] = useState(
        uniqueName(
            kind === 'table'
                ? 'table_1'
                : kind === 'type'
                  ? 'custom_type'
                  : table?.fields.length
                    ? 'new_field'
                    : 'id',
            names,
        ),
    );
    const [rows, setRows] = useState<number | undefined>(500);
    const [type, setType] = useState(kind === 'field' && !table?.fields.length ? 'int' : 'string');
    const [error, setError] = useState('');
    const title =
        kind === 'table' ? 'Создать таблицу' : kind === 'field' ? 'Добавить поле' : 'Создать тип';
    return (
        <BuilderDialog
            title={title}
            onClose={onClose}
        >
            <form
                className="builder-dialog__body"
                onSubmit={event => {
                    event.preventDefault();
                    if (!validName(name)) {
                        setError('Используйте латинские буквы, цифры и _. Начните с буквы или _.');
                        return;
                    }
                    if (names.includes(name)) {
                        setError('Такое имя уже используется. Укажите другое.');
                        return;
                    }
                    if (kind === 'table' && (!rows || !Number.isSafeInteger(rows) || rows < 1)) {
                        setError('Укажите положительное целое количество строк.');
                        return;
                    }
                    onCreate(name, rows ?? 500, type);
                }}
            >
                <Control
                    label={
                        kind === 'table'
                            ? 'Имя таблицы'
                            : kind === 'field'
                              ? 'Имя поля'
                              : 'Имя типа'
                    }
                    stacked
                >
                    <input
                        autoFocus
                        required
                        value={name}
                        onChange={event => {
                            setName(event.target.value);
                            setError('');
                        }}
                        spellCheck={false}
                    />
                </Control>
                {kind === 'table' ? (
                    <Control
                        label="Количество строк"
                        stacked
                    >
                        <NumberInput
                            value={rows}
                            min={1}
                            step={1}
                            onChange={setRows}
                        />
                    </Control>
                ) : (
                    <Control
                        label={kind === 'type' ? 'Базовый тип' : 'Тип данных'}
                        stacked
                    >
                        <Select
                            value={type}
                            onChange={setType}
                        >
                            {[
                                ...FIELD_TYPES,
                                ...(kind === 'type' ? ['enum'] : doc.types.map(item => item.name)),
                            ].map(item => (
                                <option key={item}>{item}</option>
                            ))}
                        </Select>
                    </Control>
                )}
                {error && (
                    <p
                        role="alert"
                        className="builder-form-error"
                    >
                        {error}
                    </p>
                )}
                <div className="builder-dialog__actions">
                    <button
                        type="submit"
                        className="builder-button builder-button--primary"
                    >
                        Добавить
                    </button>
                    <button
                        type="button"
                        className="builder-button"
                        onClick={onClose}
                    >
                        Отменить
                    </button>
                </div>
            </form>
        </BuilderDialog>
    );
}
