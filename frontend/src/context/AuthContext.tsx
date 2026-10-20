import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { type User, me, logout as apiLogout } from '../api/auth';

type AuthStatus = 'loading' | 'authenticated' | 'guest';

type AuthContextValue = {
    user: User | null;
    status: AuthStatus;
    setUser: (user: User | null) => void;
    logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUserState] = useState<User | null>(null);
    const [status, setStatus] = useState<AuthStatus>('loading');

    useEffect(() => {
        me()
            .then((data) => {
                setUserState(data.user);
                setStatus('authenticated');
            })
            .catch(() => {
                setStatus('guest');
            });
    }, []);

    const setUser = (next: User | null) => {
        setUserState(next);
        setStatus(next ? 'authenticated' : 'guest');
    };

    const logout = async () => {
        setUser(null);
        try {
            await apiLogout();
        } catch {
        }
    };

    return (
        <AuthContext.Provider
            value={{
                user,
                status,
                setUser,
                logout
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

export function useAuth() {
    const ctx = useContext(AuthContext);
    if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
    return ctx;
}