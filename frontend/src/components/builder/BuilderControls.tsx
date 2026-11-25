import { useId, type ReactNode } from 'react';
import { ChevronDown } from 'react-bootstrap-icons';

export function Control({
    label,
    children,
    stacked = false,
}: {
    label: string;
    children: ReactNode;
    stacked?: boolean;
}) {
    return (
        <label className={`builder-control${stacked ? ' builder-control--stacked' : ''}`}>
            <span>{label}</span>
            {children}
        </label>
    );
}

export function Select({
    value,
    onChange,
    children,
    label,
}: {
    value: string;
    onChange: (value: string) => void;
    children: ReactNode;
    label?: string;
}) {
    return (
        <span className="builder-select">
            <select
                value={value}
                onChange={event => onChange(event.target.value)}
                aria-label={label}
            >
                {children}
            </select>
            <ChevronDown
                size={12}
                aria-hidden="true"
            />
        </span>
    );
}

export function NumberInput({
    value,
    onChange,
    min,
    max,
    step = 'any',
    placeholder,
    label,
}: {
    value: number | undefined;
    onChange: (value: number | undefined) => void;
    min?: number;
    max?: number;
    step?: number | 'any';
    placeholder?: string;
    label?: string;
}) {
    return (
        <input
            type="number"
            value={value ?? ''}
            min={min}
            max={max}
            step={step}
            placeholder={placeholder}
            aria-label={label}
            onChange={event =>
                onChange(event.target.value === '' ? undefined : event.target.valueAsNumber)
            }
        />
    );
}

export function Checkbox({
    checked,
    onChange,
    children,
}: {
    checked: boolean;
    onChange: (checked: boolean) => void;
    children: ReactNode;
}) {
    return (
        <label className="builder-checkbox">
            <input
                type="checkbox"
                checked={checked}
                onChange={event => onChange(event.target.checked)}
            />
            <span>{children}</span>
        </label>
    );
}

export function Tabs({
    tabs,
    active,
    onChange,
    label,
}: {
    tabs: string[];
    active: string;
    onChange: (tab: string) => void;
    label: string;
}) {
    const id = useId();
    return (
        <div
            className="builder-tabs"
            role="tablist"
            aria-label={label}
        >
            {tabs.map(tab => (
                <button
                    type="button"
                    role="tab"
                    key={tab}
                    id={`${id}-${tab}`}
                    aria-selected={active === tab}
                    className={active === tab ? 'is-active' : ''}
                    onClick={() => onChange(tab)}
                >
                    {tab}
                </button>
            ))}
        </div>
    );
}
