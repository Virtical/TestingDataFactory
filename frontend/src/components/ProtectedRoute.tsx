import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export function ProtectedRoute() {
    const { status, isAuthenticated } = useAuth();
    if (status === 'loading' || isAuthenticated) return null;
    if (status === 'guest') return <Navigate to="/login" replace />;
    return <Outlet />;
}

export function GuestRoute() {
    const { status, isAuthenticated } = useAuth();
    if (status === 'loading' || isAuthenticated) return null;
    if (status === 'authenticated') return <Navigate to="/dashboard" replace />;
    return <Outlet />;
}

export function RootRoute() {
    const { status, isAuthenticated } = useAuth();
    if (status === 'loading' || isAuthenticated) return null;
    if (status === 'authenticated') return <Navigate to="/dashboard" replace />;
    return <Outlet />;
}