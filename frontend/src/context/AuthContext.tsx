import { createContext, useContext, useState, type ReactNode } from 'react';
import type { User } from '../api/auth';

type AuthContextValue = {
    user: User | null;
    isAuthenticated: boolean;
    setUser: (user: User | null) => void;
    logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

const USER_KEY = 'auth_user';

function readStoredUser(): User | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
    const [user, setUserState] = useState<User | null>(readStoredUser);

    const setUser = (next: User | null) => {
        setUserState(next);
        if (next) {
            localStorage.setItem(USER_KEY, JSON.stringify(next));
        } else {
            localStorage.removeItem(USER_KEY);
        }
    };

    return (
        <AuthContext.Provider
            value={{
                user,
                isAuthenticated: Boolean(user),
                setUser,
                logout: () => setUser(null),
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