import { request } from './request';

export type { ApiError } from './request';

export type User = {
    firstName: string;
    lastName: string;
    email: string;
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
