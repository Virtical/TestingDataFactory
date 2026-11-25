import { useEffect, useMemo, useRef, useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { Server, CodeSlash, ShieldCheck, Check2 } from 'react-bootstrap-icons';
import { Sidebar } from '../components/Sidebar';
import { useAuth } from '../context/AuthContext';
import { generateDataset, type DatasetDownload, type DatasetFormat } from '../api/datasets';
import { type ApiError } from '../api/request';
import {
    isRecord, isSpecificationFile, readSpecification, specificationTables, specificationTitle,
    type SpecificationFile,
} from '../lib/specificationDocument';
import './ProjectOverviewPage.css';

type RunRecord = {
    id: number;
    date: string;
    format: DatasetFormat;
    status: 'InProgress' | 'Completed' | 'Failed';
    result?: DatasetDownload;
    error?: ApiError;
};

function downloadFile(blob: Blob, name: string) {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    link.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function ProjectOverviewPage() {
    const location = useLocation();
    if (!isSpecificationFile(location.state)) return <Navigate to="/dashboard" replace />;
    return <DatasetOverview key={location.key} file={location.state} />;
}

function DatasetOverview({ file }: { file: SpecificationFile }) {
    const { user } = useAuth();
    const navigate = useNavigate();
    const parsed = useMemo(() => readSpecification(file), [file]);
    const data = parsed.data;
    const tables = specificationTables(data);
    const generation = isRecord(data?.generation) ? data.generation : {};
    const output = isRecord(generation.output) ? generation.output : {};
    const seed = generation.seed ?? data?.seed;
    const version = typeof data?.version === 'number' || typeof data?.version === 'string' ? data.version : 1;
    const details = [
        'Таблиц: ' + tables.length,
        'полей: ' + tables.reduce((count, table) => count + table.fields.length, 0),
        ...(typeof seed === 'number' ? ['seed ' + seed] : []),
        ...(typeof data?.locale === 'string' ? ['локаль ' + data.locale] : []),
    ].join(' · ');

    const [format, setFormat] = useState<DatasetFormat>(() => Object.hasOwn(output, 'json') ? 'json' : 'csv');
    const [runs, setRuns] = useState<RunRecord[]>([]);
    const nextRunId = useRef(0);
    const activeRequest = useRef<AbortController | null>(null);
    const isGenerating = runs[0]?.status === 'InProgress';
    const latestError = runs[0]?.error;

    useEffect(() => () => activeRequest.current?.abort(), []);

    const startGeneration = async () => {
        if (activeRequest.current || parsed.error) return;

        const controller = new AbortController();
        activeRequest.current = controller;
        const run: RunRecord = {
            id: ++nextRunId.current,
            date: new Date().toLocaleString('ru-RU', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }),
            format,
            status: 'InProgress',
        };
        setRuns(previous => [run, ...previous]);

        try {
            const result = await generateDataset(file.content, format, controller.signal);
            if (!controller.signal.aborted)
                setRuns(previous => previous.map(item => item.id === run.id ? { ...item, status: 'Completed', result } : item));
        } catch (err) {
            if (!controller.signal.aborted) {
                const error = err as ApiError;
                setRuns(previous => previous.map(item => item.id === run.id ? { ...item, status: 'Failed', error } : item));
            }
        } finally {
            activeRequest.current = null;
        }
    };

    return (
        <div className="layout layout--dataset">
            <Sidebar showEmptyState={false} />
            <div className="layout__main">
                <header className="topbar">
                    <span className="topbar__title">{specificationTitle(file, data)}</span>
                    <div className="stepper">
                        <div className="stepper__step stepper__step--done">
                            <span className="stepper__circle"><Check2 size={14} strokeWidth={1} /></span>
                            <span className="stepper__label">Спецификация</span>
                        </div>
                        <div className="stepper__line" />
                        <div className="stepper__step stepper__step--active">
                            <span className="stepper__circle">2</span>
                            <span className="stepper__label">Данные, SQL, валидация</span>
                        </div>
                    </div>
                    <div className="topbar__right">
                        <div className="topbar__avatar">{user?.firstName[0] ?? 'A'}</div>
                    </div>
                </header>

                <main className="layout__content overview-content">
                    <div className="overview-container">
                        <section className="spec-info-card" aria-label="Спецификация">
                            <div className="spec-info-card__header">
                                <div>
                                    <h2 className="spec-info-card__title">Версия {version}</h2>
                                    <p className="spec-info-card__details">{details}</p>
                                </div>
                                <div className="spec-info-card__actions">
                                    <button type="button" className="btn btn--outline" onClick={() => navigate('/specification', { state: file })}>
                                        Изменить спецификацию
                                    </button>
                                    <button type="button" className="btn btn--outline" onClick={() => downloadFile(new Blob([file.content], {
                                        type: parsed.format === 'JSON' ? 'application/json' : 'application/yaml',
                                    }), file.fileName)}>
                                        Скачать {parsed.format}
                                    </button>
                                </div>
                            </div>
                            <div className="spec-info-card__tags">
                                {tables.map(table => (
                                    <span className="tag" key={table.name}>
                                        {table.name}{table.rows !== null ? ' · ' + table.rows + ' строк' : ''}
                                    </span>
                                ))}
                            </div>
                        </section>

                        <h3 className="section-title">Что сделать по спецификации</h3>
                        <div className="action-cards">
                            <div className="action-card action-card--data">
                                <button
                                    type="button"
                                    className="action-card__trigger"
                                    aria-label="Сгенерировать данные"
                                    disabled={isGenerating || Boolean(parsed.error)}
                                    onClick={() => void startGeneration()}
                                >
                                    <span className="action-card__top">
                                        <span className="action-card__icon"><Server size={24} aria-hidden="true" /></span>
                                        <span className="action-card__text">
                                            <span className="action-card__title">{isGenerating ? 'Генерируем данные…' : 'Сгенерировать данные'}</span>
                                            <span className="action-card__desc">Датасет по всем таблицам спецификации</span>
                                        </span>
                                    </span>
                                </button>
                                <div className="action-card__format">
                                    <label htmlFor="dataset-format">Формат</label>
                                    <select id="dataset-format" value={format} disabled={isGenerating} onChange={event => setFormat(event.target.value as DatasetFormat)}>
                                        <option value="csv">CSV</option>
                                        <option value="json">JSON</option>
                                    </select>
                                </div>
                            </div>

                            <div className="action-card">
                                <div className="action-card__top">
                                    <div className="action-card__icon"><CodeSlash size={24} aria-hidden="true" /></div>
                                    <div className="action-card__text">
                                        <h4 className="action-card__title">Сгенерировать SQL-скрипт</h4>
                                        <p className="action-card__desc">Скрипт для заполнения базы данных</p>
                                    </div>
                                </div>
                                <span className="action-card__formats">Пока недоступно</span>
                            </div>

                            <div className="action-card">
                                <div className="action-card__top">
                                    <div className="action-card__icon"><ShieldCheck size={24} aria-hidden="true" /></div>
                                    <div className="action-card__text">
                                        <h4 className="action-card__title">Проверить данные</h4>
                                        <p className="action-card__desc">Сверим готовые данные со спецификацией</p>
                                    </div>
                                </div>
                                <span className="action-card__formats">Пока недоступно</span>
                            </div>
                        </div>

                        {(parsed.error || latestError) && (
                            <div className="dataset-error" role="alert">
                                <p>{parsed.error || latestError?.message || 'Не удалось сгенерировать данные'}</p>
                                {latestError?.fields && (
                                    <ul>{Object.entries(latestError.fields).map(([path, message]) => (
                                        <li key={path}><code>{path}</code> — {message}</li>
                                    ))}</ul>
                                )}
                            </div>
                        )}

                        <section className="runs-card" aria-labelledby="dataset-runs-title">
                            <h3 id="dataset-runs-title" className="runs-card__title">Запуски</h3>
                            <div className="runs-table-scroll" tabIndex={0} aria-label="Таблица запусков">
                                <table className="runs-table">
                                    <thead>
                                        <tr>
                                            <th scope="col">Когда</th>
                                            <th scope="col">Действие</th>
                                            <th scope="col">Параметры</th>
                                            <th scope="col">Спецификация</th>
                                            <th scope="col">Статус</th>
                                            <th scope="col" aria-label="Файл" />
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {runs.length === 0 ? (
                                            <tr>
                                                <td colSpan={6} className="runs-table__empty-cell">Запусков пока не было</td>
                                            </tr>
                                        ) : runs.map(run => (
                                            <tr key={run.id}>
                                                <td>{run.date}</td>
                                                <td>Данные</td>
                                                <td>{run.format.toUpperCase()}{run.result?.seed ? ' · seed ' + run.result.seed : ''}</td>
                                                <td>v{version}</td>
                                                <td aria-live="polite">
                                                    {run.status === 'InProgress' && <span className="badge badge--warning">В процессе…</span>}
                                                    {run.status === 'Completed' && <span className="badge badge--success">Готово</span>}
                                                    {run.status === 'Failed' && <span className="badge badge--danger">Ошибка</span>}
                                                </td>
                                                <td className="text-right">
                                                    {run.result && <button type="button" className="link" onClick={() => {
                                                        if (run.result) downloadFile(run.result.blob, run.result.fileName);
                                                    }}>Скачать</button>}
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </section>
                    </div>
                </main>
            </div>
        </div>
    );
}
