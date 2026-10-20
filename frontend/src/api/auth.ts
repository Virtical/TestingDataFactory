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

async function request<T>(url: string, options: { method?: 'GET' | 'POST'; body?: unknown } = {}): Promise<T> {
    const { method = 'GET', body } = options;

    let res: Response;

    try {
        res = await fetch(url, {
            method,
            headers: body ? { 'Content-Type': 'application/json' } : undefined,
            credentials: 'include',
            body: body ? JSON.stringify(body) : undefined,
        });
    } catch {
        throw NETWORK_ERROR;
    }
    
    if (res.status === 204) {
        return undefined as T;
    }

    const data = await res.json().catch(() => null);

    if (!res.ok) {
        throw (data?.error ?? UNKNOWN_ERROR) as ApiError;
    }

    return data as T;
}

export function register(payload: RegisterPayload): Promise<{ user: User }> {
    return request('/api/auth/register', { method: 'POST', body: payload });
}

export function login(payload: LoginPayload): Promise<{ user: User }> {
    return request('/api/auth/login', { method: 'POST', body: payload });
}

export function me(): Promise<{ user: User }> {
    return request('/api/auth/me', { method: 'GET' });
}

export function logout(): Promise<void> {
    return request<void>('/api/auth/logout', { method: 'POST' });
}