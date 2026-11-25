import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
    ArrowLeft,
    ArrowReturnLeft,
    ArrowReturnRight,
    Check2,
    ChevronDown,
    Download,
    FileEarmarkCode,
} from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import { BuilderSidebar } from '../components/builder/BuilderSidebar';
import { BuilderCanvas } from '../components/builder/BuilderCanvas';
import { BuilderInspector } from '../components/builder/BuilderInspector';
import { BuilderBottomPanel, type BuilderEvent } from '../components/builder/BuilderBottomPanel';
import { BuilderDialog, CreateItemDialog } from '../components/builder/BuilderDialog';
import { BuilderYamlEditor } from '../components/builder/BuilderYamlEditor';
import { readBuilderYaml } from '../lib/builderYaml';
import {
    baseType,
    emptyDocument,
    newField,
    newId,
    newReference,
    readDraft,
    removeField,
    removeTable,
    serializeSpecification,
    updateField,
    updateTable,
    validateDocument,
    type BuilderDocument,
    type Selection,
} from '../lib/builderModel';
import './BuilderPage.css';

type History = { past: BuilderDocument[]; present: BuilderDocument; future: BuilderDocument[] };
type HistoryAction = { type: 'change'; doc: BuilderDocument } | { type: 'undo' | 'redo' };
function historyReducer(state: History, action: HistoryAction): History {
    if (action.type === 'undo') {
        if (!state.past.length) return state;
        return {
            past: state.past.slice(0, -1),
            present: state.past[state.past.length - 1],
            future: [state.present, ...state.future],
        };
    }
    if (action.type === 'redo') {
        if (!state.future.length) return state;
        return {
            past: [...state.past, state.present],
            present: state.future[0],
            future: state.future.slice(1),
        };
    }
    if ('doc' in action && JSON.stringify(action.doc) !== JSON.stringify(state.present))
        return { past: [...state.past.slice(-59), state.present], present: action.doc, future: [] };
    return state;
}
type Dialog = { kind: 'table' | 'type' } | { kind: 'field'; tableId: string } | { kind: 'delete' };

export function BuilderPage() {
    const { user } = useAuth();
    return (
        <BuilderEditor
            key={user?.email}
            storageKey={`dataforge:builder:v1:${user?.email ?? 'local'}`}
        />
    );
}

function BuilderEditor({ storageKey }: { storageKey: string }) {
    const navigate = useNavigate();
    const [history, dispatch] = useReducer(historyReducer, storageKey, key => {
        let doc: BuilderDocument | null = null;
        try {
            doc = readDraft(localStorage.getItem(key));
        } catch {
            /* Saving reports unavailable browser storage. */
        }
        return { past: [], present: doc ?? emptyDocument(), future: [] };
    });
    const doc = history.present;
    const yamlStorageKey = `${storageKey}:yaml`;
    const [yamlContent, setYamlContent] = useState<string | null>(() => {
        try {
            return localStorage.getItem(yamlStorageKey);
        } catch {
            return null;
        }
    });
    const [view, setView] = useState<'builder' | 'yaml'>(yamlContent === null ? 'builder' : 'yaml');
    const serialized = useMemo(() => serializeSpecification(doc), [doc]);
    const yamlResult = useMemo(
        () =>
            yamlContent === null || yamlContent === serialized
                ? { document: doc, error: null }
                : readBuilderYaml(yamlContent, doc),
        [yamlContent, doc, serialized],
    );
    const currentDocument = view === 'yaml' ? (yamlResult.document ?? doc) : doc;
    const yamlError = view === 'yaml' ? yamlResult.error : null;
    const hasYamlChanges = yamlContent !== null && yamlContent !== serialized;
    const [selection, setSelection] = useState<Selection>({ kind: 'generation' });
    const [dialog, setDialog] = useState<Dialog | null>(null);
    const [events, setEvents] = useState<BuilderEvent[]>([]);
    const [bottomTab, setBottomTab] = useState('Правила (CEL)');
    const [savedDocument, setSavedDocument] = useState<BuilderDocument | null>(null);
    const [saveError, setSaveError] = useState('');
    const [notice, setNotice] = useState('');
    const [menuOpen, setMenuOpen] = useState(false);
    const menuRef = useRef<HTMLDivElement>(null);
    const diagnostics = useMemo(() => validateDocument(currentDocument), [currentDocument]);
    const hasErrors = diagnostics.some(item => item.severity === 'error');
    const selectedTable =
        'tableId' in selection ? doc.tables.find(item => item.id === selection.tableId) : undefined;
    const selectedField =
        selectedTable && 'fieldId' in selection
            ? selectedTable.fields.find(item => item.id === selection.fieldId)
            : undefined;
    const selectedType =
        selection.kind === 'type'
            ? doc.types.find(item => item.id === selection.typeId)
            : undefined;
    const activeSelection: Selection =
        selection.kind === 'generation' ||
        selectedType ||
        (selectedTable &&
            (selection.kind === 'table' ||
                (selectedField && (selection.kind !== 'relation' || selectedField.reference))))
            ? selection
            : { kind: 'generation' };

    const addEvent = useCallback(
        (message: string) =>
            setEvents(previous =>
                [
                    { id: newId(), message, time: new Date().toLocaleTimeString('ru-RU') },
                    ...previous,
                ].slice(0, 80),
            ),
        [],
    );
    const change = (next: BuilderDocument, message = 'Изменены настройки спецификации') => {
        dispatch({ type: 'change', doc: next });
        if (message !== 'Изменены настройки спецификации') addEvent(message);
    };
    const switchView = (nextView: 'builder' | 'yaml') => {
        if (nextView === view) return true;
        if (nextView === 'builder') {
            if (yamlError) {
                setNotice('Исправьте ошибку YAML или отмените изменения, чтобы вернуться к схеме.');
                return false;
            }
            if (hasYamlChanges) change(currentDocument, 'Применены изменения из YAML');
        } else {
            const names = [
                doc.tables.map(table => table.name),
                doc.types.map(type => type.name),
                ...doc.tables.map(table => table.fields.map(field => field.name)),
            ];
            if (names.some(items => new Set(items).size !== items.length)) {
                setBottomTab('Диагностика');
                setNotice('Устраните повторяющиеся имена перед переключением на YAML.');
                return false;
            }
        }
        setYamlContent(null);
        setView(nextView);
        setMenuOpen(false);
        return true;
    };
    const selectItem = (nextSelection: Selection) => {
        if (switchView('builder')) setSelection(nextSelection);
    };
    const save = useCallback(
        (manual = false) => {
            if (yamlError) {
                setNotice(
                    'Не удалось сохранить: исправьте ошибку YAML. Введённый текст сохранён в редакторе.',
                );
                return false;
            }
            try {
                localStorage.setItem(storageKey, JSON.stringify(currentDocument));
                localStorage.removeItem(yamlStorageKey);
                if (currentDocument !== doc) {
                    dispatch({ type: 'change', doc: currentDocument });
                }
                setYamlContent(null);
                setSavedDocument(currentDocument);
                setSaveError('');
                if (manual) {
                    setNotice('Спецификация сохранена в этом браузере');
                    addEvent('Спецификация сохранена в браузере');
                }
                return true;
            } catch {
                setSaveError(
                    'Не удалось сохранить черновик в браузере. Скачайте YAML или JSON, чтобы сохранить работу.',
                );
                return false;
            }
        },
        [doc, currentDocument, yamlError, storageKey, yamlStorageKey, addEvent],
    );
    useEffect(() => {
        try {
            if (yamlContent === null) localStorage.removeItem(yamlStorageKey);
            else localStorage.setItem(yamlStorageKey, yamlContent);
        } catch {
            setSaveError('Не удалось сохранить черновик YAML в браузере. Не закрывайте редактор.');
        }
    }, [yamlContent, yamlStorageKey]);
    useEffect(() => {
        if (view === 'yaml') return;
        const timeout = window.setTimeout(() => save(), 350);
        return () => window.clearTimeout(timeout);
    }, [save, view]);
    useEffect(() => {
        const handler = (event: BeforeUnloadEvent) => {
            if (savedDocument !== doc || hasYamlChanges) event.preventDefault();
        };
        window.addEventListener('beforeunload', handler);
        return () => window.removeEventListener('beforeunload', handler);
    }, [doc, savedDocument, hasYamlChanges]);
    useEffect(() => {
        if (!notice) return;
        const timeout = window.setTimeout(() => setNotice(''), 4500);
        return () => window.clearTimeout(timeout);
    }, [notice]);
    useEffect(() => {
        const handler = (event: KeyboardEvent) => {
            if (!(event.ctrlKey || event.metaKey)) return;
            if (event.key.toLowerCase() === 's') {
                event.preventDefault();
                save(true);
                return;
            }
            if (view === 'yaml') return;
            if (
                event.target instanceof HTMLElement &&
                (['INPUT', 'TEXTAREA', 'SELECT'].includes(event.target.tagName) ||
                    event.target.isContentEditable)
            )
                return;
            if (event.key.toLowerCase() === 'z') {
                event.preventDefault();
                dispatch({ type: event.shiftKey ? 'redo' : 'undo' });
            }
            if (event.key.toLowerCase() === 'y') {
                event.preventDefault();
                dispatch({ type: 'redo' });
            }
        };
        window.addEventListener('keydown', handler);
        return () => window.removeEventListener('keydown', handler);
    }, [save, view]);
    useEffect(() => {
        if (!menuOpen) return;
        const outside = (event: PointerEvent) => {
            if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false);
        };
        const escape = (event: KeyboardEvent) => {
            if (event.key === 'Escape') setMenuOpen(false);
        };
        document.addEventListener('pointerdown', outside);
        document.addEventListener('keydown', escape);
        return () => {
            document.removeEventListener('pointerdown', outside);
            document.removeEventListener('keydown', escape);
        };
    }, [menuOpen]);

    const file = () => ({
        fileName: `${
            currentDocument.name.replace(/[<>:"/\\|?*\u0000-\u001f]/g, '_').trim() ||
            'specification'
        }.yaml`,
        content: view === 'yaml' ? (yamlContent ?? serialized) : serialized,
    });
    const download = (format: 'yaml' | 'json') => {
        if (yamlError || hasErrors || !currentDocument.tables.length) {
            setBottomTab('Диагностика');
            setNotice(
                'Исправьте ошибки структуры перед экспортом. Черновик можно сохранить кнопкой «Сохранить».',
            );
            setMenuOpen(false);
            return;
        }
        const url = URL.createObjectURL(
            new Blob(
                [
                    format === 'yaml'
                        ? file().content
                        : serializeSpecification(currentDocument, format),
                ],
                {
                    type: format === 'json' ? 'application/json' : 'application/yaml',
                },
            ),
        );
        const link = document.createElement('a');
        link.href = url;
        link.download = file().fileName.replace(/\.yaml$/, `.${format}`);
        link.click();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        setMenuOpen(false);
        addEvent(`Спецификация выгружена в ${format.toUpperCase()}`);
    };
    const create = (name: string, rows: number, type: string) => {
        if (dialog?.kind === 'table') {
            const id = newId();
            change(
                {
                    ...doc,
                    tables: [
                        ...doc.tables,
                        {
                            id,
                            name,
                            rows,
                            x: 60 + (doc.tables.length % 3) * 330,
                            y: 50 + Math.floor(doc.tables.length / 3) * 300,
                            description: '',
                            locale: '',
                            mode: 'unique',
                            fields: [],
                        },
                    ],
                },
                `Создана таблица ${name}`,
            );
            setSelection({ kind: 'table', tableId: id });
        } else if (dialog?.kind === 'field') {
            const table = doc.tables.find(item => item.id === dialog.tableId);
            if (!table) return;
            const field = newField(name, type);
            if (name === 'id' && !table.fields.length) field.primary = true;
            change(
                updateTable(doc, table.id, { fields: [...table.fields, field] }),
                `Добавлено поле ${table.name}.${name}`,
            );
            setSelection({ kind: 'field', tableId: table.id, fieldId: field.id });
        } else if (dialog?.kind === 'type') {
            const id = newId();
            change(
                {
                    ...doc,
                    types: [...doc.types, { id, name, base: type, values: '', description: '' }],
                },
                `Добавлен тип ${name}`,
            );
            setSelection({ kind: 'type', typeId: id });
        }
        setDialog(null);
    };
    const removeSelected = () => {
        if (selection.kind === 'table' && selectedTable) {
            change(removeTable(doc, selectedTable.id), `Удалена таблица ${selectedTable.name}`);
            setSelection({ kind: 'generation' });
        } else if (selectedTable && selectedField) {
            if (selection.kind === 'relation') {
                change(
                    updateField(doc, selectedTable.id, selectedField.id, { reference: undefined }),
                    `Удалена связь поля ${selectedField.name}`,
                );
                setSelection({
                    kind: 'field',
                    tableId: selectedTable.id,
                    fieldId: selectedField.id,
                });
            } else {
                change(
                    removeField(doc, selectedTable.id, selectedField.id),
                    `Удалено поле ${selectedTable.name}.${selectedField.name}`,
                );
                setSelection({ kind: 'table', tableId: selectedTable.id });
            }
        } else if (selectedType) {
            change(
                {
                    ...doc,
                    types: doc.types.filter(type => type.id !== selectedType.id),
                    tables: doc.tables.map(table => ({
                        ...table,
                        fields: table.fields.map(field =>
                            field.type === selectedType.name
                                ? {
                                      ...field,
                                      type:
                                          selectedType.base === 'enum'
                                              ? 'string'
                                              : selectedType.base,
                                  }
                                : field,
                        ),
                    })),
                },
                `Удалён тип ${selectedType.name}`,
            );
            setSelection({ kind: 'generation' });
        }
        setDialog(null);
    };
    const connect = (
        sourceTableId: string,
        sourceFieldId: string,
        targetTableId: string,
        targetFieldId: string,
    ) => {
        const source = doc.tables
            .find(table => table.id === sourceTableId)
            ?.fields.find(field => field.id === sourceFieldId);
        const target = doc.tables
            .find(table => table.id === targetTableId)
            ?.fields.find(field => field.id === targetFieldId);
        if (!source || !target) return;
        if (!target.primary && !target.unique) {
            setNotice('Выберите первичный или уникальный ключ в качестве цели связи.');
            return;
        }
        if (baseType(doc, source.type) !== baseType(doc, target.type)) {
            setNotice('Для связи типы исходного и целевого полей должны совпадать.');
            return;
        }
        change(
            updateField(doc, sourceTableId, sourceFieldId, {
                reference: newReference(targetTableId, targetFieldId),
            }),
            `Создана связь ${source.name} → ${target.name}`,
        );
        setSelection({ kind: 'relation', tableId: sourceTableId, fieldId: sourceFieldId });
    };
    const deleteName =
        selection.kind === 'relation'
            ? `связь поля «${selectedField?.name}»`
            : selectedField
              ? `поле «${selectedField.name}»`
              : selectedTable
                ? `таблицу «${selectedTable.name}»`
                : `тип «${selectedType?.name}»`;

    return (
        <main className={`builder-layout${view === 'yaml' ? ' builder-layout--yaml' : ''}`}>
            <BuilderSidebar
                doc={doc}
                selection={activeSelection}
                onSelect={selectItem}
                onAddTable={() => {
                    if (switchView('builder')) setDialog({ kind: 'table' });
                }}
                onAddType={() => {
                    if (switchView('builder')) setDialog({ kind: 'type' });
                }}
            />
            <header className="builder-header">
                <button
                    className="builder-icon-button"
                    aria-label="Вернуться к проектам"
                    title="Вернуться к проектам"
                    onClick={() => {
                        if (save()) navigate('/dashboard');
                    }}
                >
                    <ArrowLeft size={19} />
                </button>
                <div className="builder-header__title">
                    <h1>Конструктор спецификации</h1>
                    <button
                        title="Изменить название и настройки"
                        onClick={() => selectItem({ kind: 'generation' })}
                    >
                        {doc.name || 'Без названия'}
                    </button>
                </div>
                <span className="builder-count">v{doc.version}</span>
                <div
                    className="builder-view-tabs"
                    role="group"
                    aria-label="Режим редактирования"
                >
                    <button
                        type="button"
                        className={view === 'builder' ? 'is-active' : ''}
                        aria-pressed={view === 'builder'}
                        onClick={() => switchView('builder')}
                    >
                        Конструктор
                    </button>
                    <button
                        type="button"
                        className={view === 'yaml' ? 'is-active' : ''}
                        aria-pressed={view === 'yaml'}
                        onClick={() => switchView('yaml')}
                    >
                        YAML
                    </button>
                </div>
                <span
                    className="builder-save-state"
                    role="status"
                >
                    {yamlError || hasYamlChanges ? (
                        'Изменения YAML не применены'
                    ) : saveError ? (
                        'Не сохранено'
                    ) : savedDocument === doc ? (
                        <>
                            <Check2 size={14} /> Сохранено в браузере
                        </>
                    ) : view === 'yaml' ? (
                        'Есть несохранённые изменения'
                    ) : (
                        'Сохранение…'
                    )}
                </span>
                <div className="builder-header__history">
                    <button
                        title="Отменить (Ctrl+Z)"
                        aria-label="Отменить действие"
                        disabled={view === 'yaml' || !history.past.length}
                        onClick={() => {
                            dispatch({ type: 'undo' });
                            addEvent('Действие отменено');
                        }}
                    >
                        <ArrowReturnLeft size={19} />
                    </button>
                    <button
                        title="Повторить (Ctrl+Shift+Z)"
                        aria-label="Повторить действие"
                        disabled={view === 'yaml' || !history.future.length}
                        onClick={() => {
                            dispatch({ type: 'redo' });
                            addEvent('Действие повторено');
                        }}
                    >
                        <ArrowReturnRight size={19} />
                    </button>
                </div>
                <div
                    className="builder-save"
                    ref={menuRef}
                >
                    <div className="builder-save__buttons">
                        <button onClick={() => save(true)}>Сохранить</button>
                        <button
                            aria-label="Варианты сохранения"
                            aria-expanded={menuOpen}
                            onClick={() => setMenuOpen(!menuOpen)}
                        >
                            <ChevronDown size={13} />
                        </button>
                    </div>
                    {menuOpen && (
                        <div className="builder-save__menu">
                            <button onClick={() => download('yaml')}>
                                <Download size={16} />
                                Скачать YAML
                            </button>
                            <button onClick={() => download('json')}>
                                <Download size={16} />
                                Скачать JSON
                            </button>
                            <button
                                onClick={() => {
                                    switchView('yaml');
                                    setMenuOpen(false);
                                }}
                            >
                                <FileEarmarkCode size={16} />
                                Открыть YAML
                            </button>
                            <button
                                disabled={
                                    Boolean(yamlError) ||
                                    hasErrors ||
                                    !currentDocument.tables.length
                                }
                                onClick={() => {
                                    if (save()) navigate('/project/builder', { state: file() });
                                }}
                            >
                                <Check2 size={16} />
                                Спецификация готова
                            </button>
                            <p>
                                {view === 'yaml'
                                    ? 'Чтобы применить YAML, нажмите «Сохранить».'
                                    : 'Черновик автоматически сохраняется в этом браузере.'}
                            </p>
                        </div>
                    )}
                </div>
            </header>
            <div
                className="builder-workspace"
                hidden={view === 'yaml'}
            >
                <BuilderCanvas
                    doc={doc}
                    selection={activeSelection}
                    onSelect={setSelection}
                    onChange={change}
                    onAddTable={() => setDialog({ kind: 'table' })}
                    onAddField={tableId => setDialog({ kind: 'field', tableId })}
                    onConnect={connect}
                />
                <BuilderBottomPanel
                    doc={doc}
                    onChange={change}
                    diagnostics={diagnostics}
                    events={events}
                    tab={bottomTab}
                    onTabChange={setBottomTab}
                    selection={activeSelection}
                    onSelect={setSelection}
                />
            </div>
            {view === 'yaml' && (
                <BuilderYamlEditor
                    content={yamlContent ?? serialized}
                    error={yamlError}
                    hasChanges={hasYamlChanges}
                    onChange={setYamlContent}
                    onReset={() => setYamlContent(null)}
                />
            )}
            {view === 'builder' && (
                <BuilderInspector
                    key={JSON.stringify(activeSelection)}
                    doc={doc}
                    selection={activeSelection}
                    onChange={change}
                    onSelect={setSelection}
                    onDelete={() => setDialog({ kind: 'delete' })}
                    onAddTable={() => setDialog({ kind: 'table' })}
                />
            )}
            {(notice || saveError) && (
                <div
                    className={`builder-notice${saveError ? ' builder-notice--error' : ''}`}
                    role={saveError ? 'alert' : 'status'}
                >
                    {saveError || notice}
                    <button
                        aria-label="Закрыть уведомление"
                        onClick={() => {
                            setNotice('');
                            setSaveError('');
                        }}
                    >
                        ×
                    </button>
                </div>
            )}
            {dialog && dialog.kind !== 'delete' && (
                <CreateItemDialog
                    kind={dialog.kind}
                    doc={doc}
                    tableId={'tableId' in dialog ? dialog.tableId : undefined}
                    onClose={() => setDialog(null)}
                    onCreate={create}
                />
            )}
            {dialog?.kind === 'delete' && (
                <BuilderDialog
                    title="Удаление элемента"
                    onClose={() => setDialog(null)}
                >
                    <div className="builder-dialog__body">
                        <p>Удалить {deleteName}?</p>
                        <p className="builder-hint">
                            {selectedType
                                ? 'У использующих этот тип полей останется базовый тип.'
                                : selection.kind === 'relation'
                                  ? 'Поля и таблицы сохранятся.'
                                  : 'Связи с удаляемым элементом также будут удалены. ' +
                                    'Проверьте правила CEL и вычисляемые выражения.'}{' '}
                            Действие можно отменить.
                        </p>
                        <div className="builder-dialog__actions">
                            <button
                                className="builder-button builder-button--danger"
                                onClick={removeSelected}
                            >
                                Удалить
                            </button>
                            <button
                                className="builder-button"
                                onClick={() => setDialog(null)}
                            >
                                Отменить
                            </button>
                        </div>
                    </div>
                </BuilderDialog>
            )}
        </main>
    );
}
