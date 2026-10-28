import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { ProtectedRoute, GuestRoute, RootRoute } from './components/ProtectedRoute';
import { LandingPage } from './pages/LandingPage';
import { HomePage } from './pages/HomePage';
import { RegisterPage } from './pages/RegisterPage';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage } from './pages/NotFoundPage';
import { ProjectOverviewPage } from './pages/ProjectOverviewPage';

function App() {
    return (
        <AuthProvider>
            <BrowserRouter useTransitions={false}>
                <Routes>
                    <Route element={<RootRoute />}>
                        <Route path="/" element={<LandingPage />} />
                    </Route>
                    <Route element={<ProtectedRoute />}>
                        <Route path="/dashboard" element={<HomePage />} />
                        <Route path="/project/:id" element={<ProjectOverviewPage />} />
                    </Route>
                    <Route element={<GuestRoute />}>
                        <Route path="/login" element={<LoginPage />} />
                        <Route path="/register" element={<RegisterPage />} />
                    </Route>
                    <Route path="*" element={<NotFoundPage />} />
                </Routes>
            </BrowserRouter>
        </AuthProvider>
    );
}

export default App
