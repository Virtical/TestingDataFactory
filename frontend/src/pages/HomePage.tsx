import { useRef, useState, type ChangeEvent, type SubmitEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Sidebar } from '../components/Sidebar';
import { Table, FileEarmarkCode, Server } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import { parseSpecification } from '../api/specifications';
import './HomePage.css';

export function HomePage() {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [description, setDescription] = useState('');
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [isReadingFile, setIsReadingFile] = useState(false);
    const [uploadError, setUploadError] = useState<string | null>(null);
    
    const initial = user ? user.firstName[0] : '';

    const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        event.target.value = '';
        if (!file) return;

        setUploadError(null);
        if (!/\.(ya?ml|json)$/i.test(file.name)) {
            setUploadError('Выберите файл спецификации в формате .yaml, .yml или .json.');
            return;
        }
        if (file.size > 5 * 1024 * 1024) {
            setUploadError('Файл слишком большой. Максимальный размер — 5 МБ.');
            return;
        }

        setIsReadingFile(true);
        try {
            const content = (await file.text()).replace(/^\uFEFF/, '');
            if (!content.trim()) {
                setUploadError('Файл пуст. Выберите файл с содержимым спецификации.');
                return;
            }
            navigate('/specification', { state: { fileName: file.name, content } });
        } catch {
            setUploadError('Не удалось прочитать файл. Попробуйте выбрать его ещё раз.');
        } finally {
            setIsReadingFile(false);
        }
    };

    const handleCreateDraft = (e: SubmitEvent) => {
        e.preventDefault();
        if (!description.trim() || isSubmitting) return;

        void submitDraft();
    }

    const submitDraft = async () => {
        setError(null);
        setIsSubmitting(true);

        try {
            const result = await parseSpecification(description);

            if (result.isSuccess) {
                navigate('/project/1'); // Когда бэкенд будет возвращать ID проекта, использовать его
            } else {
                const firstError = result.diagnostics[0]?.message || 'Не удалось разобрать спецификацию';
                setError(firstError);
            }
        } catch (err) {
            setError('Сервер недоступен или произошла ошибка. Попробуйте позже.');
        } finally {
            setIsSubmitting(false);
        }
    };
    
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

                        <form onSubmit={handleCreateDraft} className="create-spec__input-block">
                            <label>Опишите данные словами</label>
                            <textarea
                                placeholder="Например: интернет-магазин — клиенты, товары и заказы; у каждого клиента от 0 до 5 заказов, в заказе 1–4 позиции"
                                value={description}
                                onChange={(e) => setDescription(e.target.value)}
                                rows={4}
                                disabled={isSubmitting}
                            />

                            {error && (
                                <p className="create-spec__error">
                                    {error}
                                </p>
                            )}
                            
                            <div className="create-spec__input-actions">
                                <button className="btn btn--primary create-spec__draft-btn" disabled={isSubmitting || isReadingFile || !description.trim()}>
                                    {isSubmitting ? 'Анализируем...' : 'Создать черновик'}
                                </button>
                            </div>
                        </form>

                        <div className="create-spec__divider">
                            <span>или</span>
                        </div>

                        <input
                            ref={fileInputRef}
                            type="file"
                            accept=".yaml,.yml,.json"
                            aria-label="Файл спецификации"
                            hidden
                            onChange={handleFileChange}
                        />
                        {uploadError && <p className="create-spec__error" role="alert">{uploadError}</p>}

                        <div className="create-spec__cards">
                            <button
                                type="button"
                                className="spec-card spec-card--upload"
                                onClick={() => navigate('/builder')}
                                aria-label="Собрать в конструкторе"
                            >
                                <span className="spec-card__icon">
                                    <Table size={24} />
                                </span>
                                <span className="spec-card__title">Собрать в конструкторе</span>
                                <span className="spec-card__desc">
                                    Добавьте таблицы и поля, задайте типы, связи и ограничения.
                                </span>
                                <span className="spec-card__btn">Начать с пустой схемы</span>
                            </button>

                            <button
                                type="button"
                                className="spec-card spec-card--upload"
                                onClick={() => fileInputRef.current?.click()}
                                disabled={isReadingFile || isSubmitting}
                                aria-label="Загрузить спецификацию"
                                aria-busy={isReadingFile}
                            >
                                <span className="spec-card__icon">
                                    <FileEarmarkCode size={24} />
                                </span>
                                <span className="spec-card__title">{isReadingFile ? 'Открываем файл…' : 'Загрузить спецификацию'}</span>
                                <span className="spec-card__desc">Готовый файл спецификации - откроется в конструкторе для правки.</span>
                                <span className="spec-card__formats">.yaml, .json</span>
                            </button>

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
