import { useState, type SubmitEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Header } from '../components/Header';
import { useAuth } from '../context/AuthContext';
import { login, type ApiError } from '../api/auth';
import './LoginPage.css';

export function LoginPage() {
    const navigate = useNavigate();

    const { setUser } = useAuth();
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

    const handleSubmit = async (e: SubmitEvent) => {
        e.preventDefault();
        setError(null);
        setFieldErrors({});

        try {
            const { user } = await login({ email, password });

            setUser(user);
            navigate('/dashboard', { replace: true });
        } catch (err) {
            const apiErr = err as ApiError;
            if (apiErr.code === 'VALIDATION_ERROR' && apiErr.fields) {
                setFieldErrors(apiErr.fields);
            } else if (apiErr.code === 'INVALID_CREDENTIALS') {
                setError('Неверная почта или пароль');
            } else if (apiErr.code === 'NETWORK_ERROR') {
                setError('Сервер недоступен, попробуйте позже');
            } else {
                setError(apiErr.message ?? 'Что-то пошло не так');
            }
        }
    };

    return (
        <div className="page">
            <Header
                rightSlot={
                    <p className="login__register-hint">
                        Нет аккаунта?{' '}
                        <Link to="/register" className="login__register-link">
                            Зарегистрироваться
                        </Link>
                    </p>
                }
            />

            <main className="page__content">
                <div className="login-card">
                    <h1 className="login-card__title">Вход в аккаунт</h1>
                    <p className="login-card__subtitle">
                        Войдите, чтобы продолжить работу с платформой
                    </p>

                    <form className="login-form" onSubmit={handleSubmit} noValidate>
                        <div className="form-field">
                            <label className="form-field__label" htmlFor="email">
                                Электронная почта
                            </label>
                            <input
                                id="email"
                                type="email"
                                className="form-field__input"
                                placeholder="example@gmail.com"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                required
                            />
                            {fieldErrors.email && (
                                <span className="form-field__error">{fieldErrors.email}</span>
                            )}
                        </div>

                        <div className="form-field">
                            <label className="form-field__label" htmlFor="password">
                                Пароль
                            </label>
                            <input
                                id="password"
                                type="password"
                                className="form-field__input"
                                placeholder="Введите пароль"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                            />
                            {fieldErrors.password && (
                                <span className="form-field__error">{fieldErrors.password}</span>
                            )}
                        </div>

                        {error && <p className="login-form__error">{error}</p>}

                        <button type="submit" className="btn btn--primary login-form__submit">
                            Войти
                        </button>
                    </form>
                </div>
            </main>
        </div>
    );
}