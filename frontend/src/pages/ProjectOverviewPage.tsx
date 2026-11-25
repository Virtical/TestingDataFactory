import { useMemo, useState } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import { Sidebar } from '../components/Sidebar';
import { Server, CodeSlash, ShieldCheck, Check2 } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import { startGeneration } from '../api/specifications';
import { isRecord, isSpecificationFile, readSpecification, specificationTables, specificationTitle } from '../lib/specificationDocument';
import './ProjectOverviewPage.css';

type RunRecord = {
    id: string;
    date: string;
    action: string;
    params: string;
    version: string;
    status: 'InProgress' | 'Completed';
};

export function ProjectOverviewPage() {
    const { user } = useAuth();
    const { id: projectId } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const file = isSpecificationFile(location.state) ? location.state : null;
    const parsed = useMemo(() => file ? readSpecification(file) : null, [file]);
    const data = parsed?.data;
    const tables = specificationTables(data ?? null);
    const generation = isRecord(data?.generation) ? data.generation : {};
    const seed = generation.seed ?? data?.seed;
    const version = typeof data?.version === 'number' || typeof data?.version === 'string' ? data.version : 1;
    const details = file ? [
        `Таблиц: ${tables.length}`,
        `полей: ${tables.reduce((count, table) => count + table.fields.length, 0)}`,
        ...(typeof seed === 'number' ? [`seed ${seed}`] : []),
        ...(typeof data?.locale === 'string' ? [`локаль ${data.locale}`] : []),
    ].join(' · ') : '1 таблица · 1 поле · seed 42 · локаль ru';
    const initial = user ? user.firstName[0] : 'A';

    const [runs, setRuns] = useState<RunRecord[]>([]);

    const downloadSpecification = () => {
        if (!file) return;
        const url = URL.createObjectURL(new Blob([file.content], { type: parsed?.format === 'JSON' ? 'application/json' : 'application/yaml' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = file.fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    };

    const handleActionClick = async (actionType: 'data' | 'sql' | 'validate', title: string, paramsStr: string) => {
        try {
            const response = await startGeneration(projectId || 1, actionType, {});

            const newRun: RunRecord = {
                id: response.runId,
                date: 'Только что',
                action: title,
                params: paramsStr,
                version: `v${version}`,
                status: 'InProgress',
            };

            setRuns(prev => [newRun, ...prev]);

            // Имитация завершения задачи через 3 секунды
            setTimeout(() => {
                setRuns(prev => prev.map(run =>
                    run.id === response.runId ? { ...run, status: 'Completed' } : run
                ));
            }, 3000);

        } catch (error) {
            alert('Ошибка при запуске генерации');
        }
    };

    return (
        <div className="layout">
            <Sidebar showEmptyState={!file} />
            <div className="layout__main">
                <header className="topbar">
                    <span className="topbar__title">{file ? specificationTitle(file, data ?? null) : 'Интернет-магазин'}</span>

                    <div className="stepper">
                        <div className="stepper__step stepper__step--done">
                            <span className="stepper__circle">
                                <Check2 size={14} strokeWidth={1} />
                            </span>
                            <span className="stepper__label">Спецификация</span>
                        </div>
                        <div className="stepper__line"></div>
                        <div className="stepper__step stepper__step--active">
                            <span className="stepper__circle">2</span>
                            <span className="stepper__label">Данные, SQL, валидация</span>
                        </div>
                    </div>

                    <div className="topbar__right">
                        <div className="topbar__avatar">{initial}</div>
                    </div>
                </header>

                <main className="layout__content overview-content">
                    <div className="overview-container">

                        <div className="spec-info-card">
                            <div className="spec-info-card__header">
                                <div>
                                    <span className="spec-info-card__eyebrow">спецификация</span>
                                    <h2 className="spec-info-card__title">Версия {version}</h2>
                                    <p className="spec-info-card__details">{details}</p>
                                </div>
                                <div className="spec-info-card__actions">
                                    <button className="btn btn--outline" onClick={() => file && navigate('/specification', { state: file })}>Изменить спецификацию</button>
                                    <button className="btn btn--outline" onClick={downloadSpecification}>Скачать {parsed?.format ?? 'YAML'}</button>
                                </div>
                            </div>

                            <div className="spec-info-card__tags">
                                {file ? tables.map((table) => (
                                    <span className="tag" key={table.name}>{table.name}{table.rows !== null ? ` · ${table.rows} строк` : ''}</span>
                                )) : <span className="tag">table_1 · 100 строк</span>}
                            </div>
                        </div>

                        <h3 className="section-title">Что сделать по спецификации</h3>

                        <div className="action-cards">
                            <div className="action-card" onClick={() => handleActionClick('data', 'Данные', 'JSON · обычные данные')}>
                                <div className="action-card__top">
                                    <div className="action-card__icon"><Server size={24} /></div>
                                    <div className="action-card__text">
                                        <h4 className="action-card__title">Сгенерировать данные</h4>
                                        <p className="action-card__desc">Датасет по всем таблицам спецификации</p>
                                    </div>
                                </div>
                                <span className="action-card__formats">JSON · CSV · SQLite</span>
                            </div>

                            <div className="action-card" onClick={() => handleActionClick('sql', 'SQL-скрипт', 'PostgreSQL')}>
                                <div className="action-card__top">
                                    <div className="action-card__icon"><CodeSlash size={24} /></div>
                                    <div className="action-card__text">
                                        <h4 className="action-card__title">Сгенерировать SQL-скрипт</h4>
                                        <p className="action-card__desc">Скрипт для заполнения базы данных</p>
                                    </div>
                                </div>
                                <span className="action-card__formats">PostgreSQL · MySQL · SQLite</span>
                            </div>

                            <div className="action-card" onClick={() => handleActionClick('validate', 'Валидация', 'orders.csv')}>
                                <div className="action-card__top">
                                    <div className="action-card__icon"><ShieldCheck size={24} /></div>
                                    <div className="action-card__text">
                                        <h4 className="action-card__title">Проверить данные</h4>
                                        <p className="action-card__desc">Сверим готовые данные со спецификацией</p>
                                    </div>
                                </div>
                                <span className="action-card__formats">.csv · .json · .db</span>
                            </div>
                        </div>

                        <div className="runs-card">
                            <h3 className="runs-card__title">Запуски</h3>
                            <table className="runs-table">
                                <thead>
                                <tr>
                                    <th>Когда</th>
                                    <th>Действие</th>
                                    <th>Параметры</th>
                                    <th>Спецификация</th>
                                    <th>Статус</th>
                                    <th></th>
                                </tr>
                                </thead>
                                <tbody>
                                {runs.length === 0 ? (
                                    <tr>
                                        <td colSpan={6} className="runs-table__empty-cell">
                                            Запусков пока не было
                                        </td>
                                    </tr>
                                ) : (
                                    runs.map(run => (
                                        <tr key={run.id}>
                                            <td>{run.date}</td>
                                            <td>{run.action}</td>
                                            <td>{run.params}</td>
                                            <td>{run.version}</td>
                                            <td>
                                                {run.status === 'InProgress' ? (
                                                    <span className="badge badge--warning">В процессе...</span>
                                                ) : (
                                                    <span className="badge badge--success">
                                                            <Check2 size={12}/> Готово
                                                        </span>
                                                )}
                                            </td>
                                            <td className="text-right">
                                                {run.status === 'Completed' && (
                                                    <a href="#" className="link" onClick={(e) => { e.preventDefault(); alert('Скачивание файла'); }}>Скачать</a>
                                                )}
                                            </td>
                                        </tr>
                                    ))
                                )}
                                </tbody>
                            </table>
                        </div>

                    </div>
                </main>
            </div>
        </div>
    );
}
