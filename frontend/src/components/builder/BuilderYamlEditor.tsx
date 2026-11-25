import { Check2, ExclamationCircle } from 'react-bootstrap-icons';

type Props = {
    content: string;
    error: string | null;
    visualError: string | null;
    validationStatus: string;
    isValidated: boolean;
    onRetryValidation?: () => void;
    hasChanges: boolean;
    onChange: (value: string) => void;
    onReset: () => void;
};

export function BuilderYamlEditor({
    content, error, visualError, validationStatus, isValidated, onRetryValidation, hasChanges, onChange, onReset,
}: Props) {
    return (
        <section
            className="builder-yaml"
            aria-label="Редактор YAML"
        >
            <div className="builder-yaml__toolbar">
                <div>
                    <h2>Спецификация YAML</h2>
                    <p>Изменения применяются при сохранении или возвращении в конструктор.</p>
                </div>
                <button
                    type="button"
                    className="builder-button"
                    disabled={!hasChanges}
                    onClick={onReset}
                >
                    Отменить изменения YAML
                </button>
            </div>
            <textarea
                className="builder-yaml__source"
                aria-label="Спецификация YAML"
                aria-invalid={Boolean(error)}
                aria-describedby="builder-yaml-status"
                value={content}
                onChange={event => onChange(event.target.value)}
                spellCheck={false}
                autoCapitalize="off"
                autoCorrect="off"
                wrap="off"
            />
            <p
                id="builder-yaml-status"
                className={`builder-yaml__status${error ? ' builder-text-error' : ''}`}
                role="status"
            >
                {error ? <ExclamationCircle size={18} /> : isValidated ? <Check2 size={20} /> : null}
                <span>{error ?? validationStatus}</span>
                {onRetryValidation && (
                    <button className="builder-inline-link" onClick={onRetryValidation}>Повторить проверку</button>
                )}
            </p>
            {visualError && (
                <p className="builder-yaml__status builder-text-warning">
                    Схема недоступна в визуальном режиме. {visualError}
                </p>
            )}
        </section>
    );
}
