import { useState } from 'react';
import { Sidebar } from '../components/Sidebar';
import { Table, FileEarmarkCode, Server } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import './HomePage.css';

export function HomePage() {
    const { user } = useAuth();
    const [description, setDescription] = useState('');
    
    const initial = user ? user.firstName[0] : '';

    return (
        <div className="layout">
            <Sidebar />
            <div className="layout__main">
                <header className="topbar">
                    <span className="topbar__title">Новый проект</span>
                    <div className="topbar__right">
                        <div className="topbar__avatar">{initial}</div>
                    </div>
                </header>

                <main className="layout__content layout__content--center">
                    <div className="create-spec">
                        <h1 className="create-spec__title">Какие данные нужны?</h1>
                        <p className="create-spec__subtitle">
                            Начните со спецификации — по ней генерируются данные и SQL-скрипты и проверяются готовые данные
                        </p>

                        <div className="create-spec__input-block">
                            <label>Опишите данные словами</label>
                            <textarea
                                placeholder="Например: интернет-магазин — клиенты, товары и заказы; у каждого клиента от 0 до 5 заказов, в заказе 1–4 позиции"
                                value={description}
                                onChange={(e) => setDescription(e.target.value)}
                                rows={4}
                            />
                            <div className="create-spec__input-actions">
                                <button className="btn btn--primary create-spec__draft-btn">
                                    Создать черновик
                                </button>
                            </div>
                        </div>

                        <div className="create-spec__divider">
                            <span>или</span>
                        </div>

                        <div className="create-spec__cards">
                            <div className="spec-card">
                                <div className="spec-card__icon">
                                    <Table size={24} />
                                </div>
                                <h3 className="spec-card__title">Собрать в конструкторе</h3>
                                <p className="spec-card__desc">Добавьте таблицы и поля, задайте типы, связи и ограничения.</p>
                                <button className="spec-card__btn">Начать с пустой схемы</button>
                            </div>

                            <div className="spec-card">
                                <div className="spec-card__icon">
                                    <FileEarmarkCode size={24} />
                                </div>
                                <h3 className="spec-card__title">Загрузить спецификацию</h3>
                                <p className="spec-card__desc">Готовый файл спецификации - откроется в конструкторе для правки.</p>
                                <div className="spec-card__formats">.yaml, .json</div>
                            </div>

                            <div className="spec-card">
                                <div className="spec-card__icon">
                                    <Server size={24} />
                                </div>
                                <h3 className="spec-card__title">Загрузить пример данных</h3>
                                <p className="spec-card__desc">Определим таблицы, типы и связи по данным. Подойдет и для валидации.</p>
                                <div className="spec-card__formats">.csv, .json, .db, .sql</div>
                            </div>
                        </div>
                    </div>
                </main>
            </div>
        </div>
    );
}