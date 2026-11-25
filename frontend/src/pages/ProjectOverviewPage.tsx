import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { Sidebar } from '../components/Sidebar';
import { Server, CodeSlash, ShieldCheck, Check2 } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import { startGeneration } from '../api/specifications';
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
    const initial = user ? user.firstName[0] : 'A';

    const [runs, setRuns] = useState<RunRecord[]>([]);

    const handleActionClick = async (actionType: 'data' | 'sql' | 'validate', title: string, paramsStr: string) => {
        try {
            const response = await startGeneration(projectId || 1, actionType, {});

            const newRun: RunRecord = {
                id: response.runId,
                date: 'Только что',
                action: title,
                params: paramsStr,
                version: 'v1',
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
            <Sidebar />
            <div className="layout__main">
                <header className="topbar">
                    <span className="topbar__title">Интернет-магазин</span>

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
                                    <h2 className="spec-info-card__title">Версия 1</h2>
                                    <p className="spec-info-card__details">1 таблица · 1 поле · seed 42 · локаль ru</p>
                                </div>
                                <div className="spec-info-card__actions">
                                    <button className="btn btn--outline">Изменить спецификацию</button>
                                    <button className="btn btn--outline">Скачать YAML</button>
                                </div>
                            </div>

                            <div className="spec-info-card__tags">
                                <span className="tag">table_1 · 100 строк</span>
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