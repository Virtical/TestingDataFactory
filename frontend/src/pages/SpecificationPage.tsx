import { useLayoutEffect, useMemo, useRef, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Check2, ChevronDown, ExclamationCircle } from 'react-bootstrap-icons';
import { Sidebar } from '../components/Sidebar';
import { useAuth } from '../context/AuthContext';
import {
    isRecord, isSpecificationFile, readSpecification, specificationTables, specificationTitle,
    type SpecificationFile,
} from '../lib/specificationDocument';
import './SpecificationPage.css';

export function SpecificationPage() {
    const location = useLocation();
    if (!isSpecificationFile(location.state)) return <Navigate to="/dashboard" replace />;
    return <SpecificationEditor key={location.key} file={location.state} />;
}

function SpecificationEditor({ file }: { file: SpecificationFile }) {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [content, setContent] = useState(file.content);
    const [view, setView] = useState<'source' | 'builder'>('source');
    const [editError, setEditError] = useState<string | null>(null);
    const sourceRef = useRef<HTMLTextAreaElement>(null);

    useLayoutEffect(() => {
        const editor = sourceRef.current;
        if (!editor) return;

        const resize = () => {
            const { scrollTop, scrollLeft } = editor;
            editor.style.height = 'auto';
            editor.style.height = `${editor.scrollHeight + editor.offsetHeight - editor.clientHeight}px`;
            editor.scrollTop = scrollTop;
            editor.scrollLeft = scrollLeft;
        };

        resize();
        let width = editor.getBoundingClientRect().width;
        const observer = new ResizeObserver(() => {
            const nextWidth = editor.getBoundingClientRect().width;
            if (nextWidth === width) return;
            width = nextWidth;
            resize();
        });
        observer.observe(editor);
        return () => observer.disconnect();
    }, [content, view]);

    const parsed = useMemo(() => readSpecification({ fileName: file.fileName, content }), [file.fileName, content]);
    const { data, document, format, error } = parsed;
    const statusError = error ?? editError;
    const tables = specificationTables(data);
    const title = specificationTitle(file, data);
    const generation = isRecord(data?.generation) ? data.generation : {};
    const seed = generation.seed ?? data?.seed;
    const locale = typeof data?.locale === 'string' ? data.locale : '';
    const seedPath = Object.hasOwn(generation, 'seed') || !Object.hasOwn(data ?? {}, 'seed')
        ? ['generation', 'seed'] : ['seed'];

    const updateValue = (path: string[], value: string | number | undefined) => {
        if (!document) return;
        try {
            const updated = document.clone();
            if (value === undefined) updated.deleteIn(path);
            else updated.setIn(path, value);
            setContent(format === 'JSON' ? JSON.stringify(updated.toJS({ maxAliasCount: 100 }), null, 2) : updated.toString());
            setEditError(null);
        } catch {
            setEditError(`Не удалось изменить параметр. Проверьте его структуру во вкладке ${format}.`);
            setView('source');
        }
    };

    const continueToOverview = () => {
        if (statusError) return;
        navigate('/project/imported', { state: { fileName: file.fileName, content } });
    };

    return (
        <div className="layout specification-layout">
            <Sidebar showEmptyState={false} />
            <div className="layout__main">
                <header className="topbar specification-topbar">
                    <h1 className="topbar__title specification-topbar__title" title={title}>{title}</h1>
                    <ol className="specification-steps" aria-label="Этапы проекта">
                        <li className="specification-steps__item specification-steps__item--active" aria-current="step">
                            <span className="specification-steps__number">1</span>
                            <span>Спецификация</span>
                        </li>
                        <li className="specification-steps__item">
                            <span className="specification-steps__number">2</span>
                            <span>Данные, SQL, валидация</span>
                        </li>
                    </ol>
                    <div className="topbar__avatar" aria-label="Профиль">{user?.firstName[0] ?? ''}</div>
                </header>

                <main className="specification-content">
                    <section className="specification-workspace" aria-label="Редактор спецификации">
                        <div className="specification-toolbar">
                            <div className="specification-tabs" role="group" aria-label="Режим редактирования">
                                <button
                                    type="button"
                                    className={view === 'builder' ? 'specification-tabs__button is-active' : 'specification-tabs__button'}
                                    aria-pressed={view === 'builder'}
                                    disabled={Boolean(error)}
                                    onClick={() => setView('builder')}
                                >Конструктор</button>
                                <button
                                    type="button"
                                    className={view === 'source' ? 'specification-tabs__button is-active' : 'specification-tabs__button'}
                                    aria-pressed={view === 'source'}
                                    onClick={() => setView('source')}
                                >{format}</button>
                            </div>

                            <div className="specification-options">
                                <label className="specification-options__field">
                                    <span>Seed</span>
                                    <input
                                        type="number"
                                        step="1"
                                        aria-label="Seed"
                                        value={typeof seed === 'number' ? seed : ''}
                                        placeholder="42"
                                        disabled={Boolean(error)}
                                        onChange={(event) => {
                                            const value = event.target.value;
                                            if (!value) updateValue(seedPath, undefined);
                                            else if (Number.isSafeInteger(Number(value))) updateValue(seedPath, Number(value));
                                        }}
                                    />
                                </label>
                                <label className="specification-options__field">
                                    <span>Локаль</span>
                                    <span className="specification-options__select">
                                        <select aria-label="Локаль" value={locale} disabled={Boolean(error)} onChange={(event) => updateValue(['locale'], event.target.value || undefined)}>
                                            <option value="">Не задана</option>
                                            <option value="ru">Русский (ru)</option>
                                            <option value="en">English (en)</option>
                                            {locale && locale !== 'ru' && locale !== 'en' && <option value={locale}>{locale}</option>}
                                        </select>
                                        <ChevronDown size={12} aria-hidden="true" />
                                    </span>
                                </label>
                            </div>
                        </div>

                        {view === 'source' ? (
                            <textarea
                                ref={sourceRef}
                                className="specification-source"
                                aria-label={`Спецификация ${format}`}
                                aria-invalid={Boolean(error)}
                                aria-describedby="specification-status"
                                value={content}
                                rows={content.split('\n').length}
                                onChange={(event) => { setContent(event.target.value); setEditError(null); }}
                                spellCheck={false}
                                autoCapitalize="off"
                                autoCorrect="off"
                                wrap="off"
                            />
                        ) : (
                            <div className="specification-builder">
                                {tables.length === 0 && <p className="specification-builder__empty">В спецификации пока нет таблиц. Добавьте их во вкладке {format}.</p>}
                                {tables.map((table) => (
                                    <section className="specification-table" key={table.name}>
                                        <div className="specification-table__header">
                                            <h2>{table.name}</h2>
                                            {table.rows !== null && <span>{table.rows} строк</span>}
                                        </div>
                                        {table.fields.length === 0 ? <p>Поля пока не добавлены.</p> : (
                                            <div className="specification-table__fields">
                                                {table.fields.map(([name, field]) => (
                                                    <label className="specification-table__field" key={name}>
                                                        <span>{name}</span>
                                                        <input
                                                            aria-label={`Тип поля ${table.name}.${name}`}
                                                            value={isRecord(field) && typeof field.type === 'string' ? field.type : typeof field === 'string' ? field : ''}
                                                            onChange={(event) => updateValue(
                                                                isRecord(field) ? ['tables', table.name, 'fields', name, 'type'] : ['tables', table.name, 'fields', name],
                                                                event.target.value,
                                                            )}
                                                        />
                                                    </label>
                                                ))}
                                            </div>
                                        )}
                                    </section>
                                ))}
                            </div>
                        )}
                    </section>

                    <footer className="specification-footer">
                        <p id="specification-status" className={`specification-status${statusError ? ' specification-status--error' : ''}`} role="status">
                            {statusError ? <ExclamationCircle size={18} aria-hidden="true" /> : <Check2 size={20} aria-hidden="true" />}
                            <span>{statusError || 'Синтаксис спецификации корректен'}</span>
                        </p>
                        <button type="button" className="btn specification-ready" disabled={Boolean(statusError)} onClick={continueToOverview}>
                            Спецификация готова
                        </button>
                    </footer>
                </main>
            </div>
        </div>
    );
}
