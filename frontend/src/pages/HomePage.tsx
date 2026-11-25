import { useEffect, useRef, useState, type ChangeEvent, type SubmitEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Sidebar } from '../components/Sidebar';
import { Table, FileEarmarkCode, Server } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import { getSpecificationJob, startSpecificationJob, type SpecificationJob } from '../api/specifications';
import { type ApiError } from '../api/request';
import './HomePage.css';

const POLL_INTERVAL_MS = 2000;

type TrackedJob = {
    jobId: string;
    job?: SpecificationJob;
    error?: string;
    unavailable?: boolean;
};

const pendingLabels = {
    Queued: 'Задача в очереди',
    Running: 'Генерируем спецификацию…',
};

function isPending(job: SpecificationJob) {
    return job.status === 'Queued' || job.status === 'Running';
}

export function HomePage() {
    const { user } = useAuth();
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const jobId = searchParams.get('job') || null;
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [description, setDescription] = useState('');
    const [trackedJob, setTrackedJob] = useState<TrackedJob | null>(null);
    const submitRequest = useRef<AbortController | null>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [isReadingFile, setIsReadingFile] = useState(false);
    const [uploadError, setUploadError] = useState<string | null>(null);

    const initial = user ? user.firstName[0] : '';
    const currentJob = trackedJob?.jobId === jobId ? trackedJob : null;
    const job = currentJob?.job;
    const status = job?.status;
    const isGenerating = Boolean(jobId) && !currentJob?.unavailable && (!job || isPending(job));
    const isBusy = isSubmitting || isGenerating || isReadingFile;
    const jobError = currentJob?.error ?? (status === 'Failed'
        ? job?.errorMessage || 'Не удалось сгенерировать спецификацию'
        : null);

    useEffect(() => () => submitRequest.current?.abort(), []);

    useEffect(() => {
        if (!jobId) return;

        const controller = new AbortController();
        let timer: number | undefined;

        const refresh = async () => {
            try {
                const updatedJob = await getSpecificationJob(jobId, controller.signal);
                if (controller.signal.aborted) return;

                setTrackedJob({ jobId, job: updatedJob });
                if (!isPending(updatedJob)) return;
            } catch (err) {
                if (controller.signal.aborted) return;

                const { status, message } = err as ApiError;
                const unavailable = status === 401 || status === 403 || status === 404;
                setTrackedJob(previous => ({
                    jobId,
                    job: previous?.jobId === jobId ? previous.job : undefined,
                    error: unavailable
                        ? (status === 404 ? 'Черновик не найден' : message)
                        : 'Не удалось обновить статус. Повторяем запрос…',
                    unavailable,
                }));
                if (unavailable) return;
            }

            timer = window.setTimeout(() => void refresh(), POLL_INTERVAL_MS);
        };

        void refresh();
        return () => {
            controller.abort();
            window.clearTimeout(timer);
        };
    }, [jobId]);

    const handleFileChange = async (event: ChangeEvent<HTMLInputElement>) => {
        const file = event.target.files?.[0];
        event.target.value = '';
        if (!file || isBusy) return;

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
            navigate('/builder', { state: { fileName: file.name, content } });
        } catch {
            setUploadError('Не удалось прочитать файл. Попробуйте выбрать его ещё раз.');
        } finally {
            setIsReadingFile(false);
        }
    };

    const handleCreateDraft = async (e: SubmitEvent) => {
        e.preventDefault();
        const prompt = description.trim();
        if (!prompt || isBusy || submitRequest.current) return;

        const controller = new AbortController();
        submitRequest.current = controller;
        setError(null);
        setIsSubmitting(true);

        try {
            const createdJob = await startSpecificationJob(prompt, controller.signal);
            if (controller.signal.aborted) return;

            setTrackedJob({ jobId: createdJob.jobId, job: createdJob });
            setSearchParams(params => {
                const next = new URLSearchParams(params);
                next.set('job', createdJob.jobId);
                return next;
            }, { replace: true });
        } catch (err) {
            if (controller.signal.aborted) return;

            const apiError = err as ApiError;
            setError(apiError.fields?.prompt ?? apiError.message);
        } finally {
            submitRequest.current = null;
            if (!controller.signal.aborted) setIsSubmitting(false);
        }
    };

    return (
        <div className="layout layout--draft">
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
                            <label htmlFor="specification-prompt">Опишите данные словами</label>
                            <textarea
                                id="specification-prompt"
                                placeholder="Например: интернет-магазин — клиенты, товары и заказы; у каждого клиента от 0 до 5 заказов, в заказе 1–4 позиции"
                                value={description}
                                onChange={(e) => setDescription(e.target.value)}
                                rows={4}
                                disabled={isBusy}
                            />

                            {error && (
                                <p className="create-spec__error" role="alert">
                                    {error}
                                </p>
                            )}
                            
                            <div className="create-spec__input-actions">
                                <button type="submit" className="btn btn--primary create-spec__draft-btn" disabled={isBusy || !description.trim()}>
                                    {isSubmitting ? 'Запускаем…' : 'Создать черновик'}
                                </button>
                            </div>
                        </form>

                        <div role="status">
                            {isGenerating && (
                                <p className="create-spec__status">
                                    <span className="create-spec__spinner" aria-hidden="true" />
                                    {status === 'Queued' || status === 'Running' ? pendingLabels[status] : 'Проверяем статус…'}
                                </p>
                            )}
                            {status === 'Succeeded' && (
                                <span className="create-spec__announcement">Черновик спецификации готов</span>
                            )}
                        </div>
                        {jobError && <p className="create-spec__error" role="alert">{jobError}</p>}
                        {job?.status === 'Succeeded' && job.specificationYaml && (
                            <SpecificationDraft key={job.jobId} yaml={job.specificationYaml} />
                        )}

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
                                disabled={isBusy}
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

function SpecificationDraft({ yaml }: { yaml: string }) {
    const [copyFeedback, setCopyFeedback] = useState('');

    const copyYaml = async () => {
        try {
            await navigator.clipboard.writeText(yaml);
            setCopyFeedback('YAML скопирован');
        } catch {
            setCopyFeedback('Не удалось скопировать YAML');
        }
    };

    const downloadYaml = () => {
        const url = URL.createObjectURL(new Blob([yaml], { type: 'application/yaml' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = 'specification.yaml';
        link.click();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    };

    return (
        <section className="create-spec__result" aria-labelledby="specification-draft-title">
            <div className="create-spec__result-header">
                <h2 id="specification-draft-title">Черновик спецификации</h2>
                <div className="create-spec__result-actions">
                    <Link className="btn btn--outline" to="/builder" state={{ fileName: 'specification.yaml', content: yaml }}>
                        Открыть в редакторе
                    </Link>
                    <button type="button" className="btn btn--outline" onClick={() => void copyYaml()}>Скопировать</button>
                    <button type="button" className="btn btn--outline" onClick={downloadYaml}>Скачать YAML</button>
                </div>
            </div>
            <pre className="create-spec__yaml" tabIndex={0} aria-label="YAML спецификации"><code>{yaml}</code></pre>
            <div role="status">
                {copyFeedback && <p className="create-spec__copy-feedback">{copyFeedback}</p>}
            </div>
        </section>
    );
}
