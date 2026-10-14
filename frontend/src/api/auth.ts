export type User = {
    firstName: string;
    lastName: string;
    email: string;
};

export type ApiError = {
    code: string;
    message: string;
    fields?: Record<string, string>;
};

export type RegisterPayload = {
    firstName: string;
    lastName: string;
    email: string;
    password: string;
};

export type LoginPayload = {
    email: string;
    password: string;
};

const NETWORK_ERROR: ApiError = {
    code: 'NETWORK_ERROR',
    message: 'Сервер недоступен, попробуйте позже',
};

const UNKNOWN_ERROR: ApiError = {
    code: 'UNKNOWN_ERROR',
    message: 'Что-то пошло не так',
};

async function request<T>(url: string, body: unknown): Promise<T> {
    const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(body),
    }).catch(() => {
        throw NETWORK_ERROR;
    });

    const data = await res.json().catch(() => null);

    if (!res.ok) {
        throw (data?.error ?? UNKNOWN_ERROR) as ApiError;
    }

    return data as T;
}

export function register(payload: RegisterPayload): Promise<{ user: User }> {
    return request('/api/auth/register', payload);
}

export function login(payload: LoginPayload): Promise<{ user: User }> {
    return request('/api/auth/login', payload);
}