export type ApiError = {
    code: string;
    message: string;
    fields?: Record<string, string>;
    status?: number;
};

const NETWORK_ERROR: ApiError = {
    code: 'NETWORK_ERROR',
    message: 'Сервер недоступен, попробуйте позже',
};

const UNKNOWN_ERROR: ApiError = {
    code: 'UNKNOWN_ERROR',
    message: 'Что-то пошло не так',
};

type RequestOptions = {
    method?: 'GET' | 'POST';
    body?: unknown;
    signal?: AbortSignal;
};

export async function requestResponse(url: string, options: RequestOptions = {}): Promise<Response> {
    const { method = 'GET', body, signal } = options;
    let res: Response;

    try {
        res = await fetch(url, {
            method,
            headers: body ? { 'Content-Type': 'application/json' } : undefined,
            credentials: 'include',
            body: body ? JSON.stringify(body) : undefined,
            signal,
        });
    } catch {
        throw NETWORK_ERROR;
    }

    if (!res.ok) {
        const data = await res.json().catch(() => null);
        throw { ...(data?.error ?? UNKNOWN_ERROR), status: res.status } as ApiError;
    }
    return res;
}

export async function request<T>(url: string, options: RequestOptions = {}): Promise<T> {
    const res = await requestResponse(url, options);
    if (res.status === 204) return undefined as T;
    return await res.json().catch(() => null) as T;
}
