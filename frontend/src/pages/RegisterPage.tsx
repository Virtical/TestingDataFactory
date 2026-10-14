import { useState, type SubmitEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Header } from '../components/Header';
import { useAuth } from '../context/AuthContext';
import { register, type ApiError } from '../api/auth';
import './RegisterPage.css';

export function RegisterPage() {
    const navigate = useNavigate();
    const { setUser } = useAuth();

    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [error, setError] = useState<string | null>(null);
    const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
    const [isSubmitting, setIsSubmitting] = useState(false);

    const handleSubmit = async (e: SubmitEvent) => {
        e.preventDefault();
        if (isSubmitting) return;
        setError(null);
        setFieldErrors({});

        if (password !== confirmPassword) {
            setFieldErrors({ confirmPassword: 'Пароли не совпадают' });
            return;
        }

        setIsSubmitting(true);
        
        try {
            const { user } = await register({
                firstName,
                lastName,
                email,
                password,
            });

            setUser(user);
            navigate('/dashboard', { replace: true });
        } catch (err) {
            const apiErr = err as ApiError;

            if (apiErr.code === 'VALIDATION_ERROR' && apiErr.fields) {
                setFieldErrors(apiErr.fields);
            } else if (apiErr.code === 'EMAIL_TAKEN') {
                setFieldErrors({ email: 'Пользователь с такой почтой уже есть' });
            } else if (apiErr.code === 'NETWORK_ERROR') {
                setError('Сервер недоступен, попробуйте позже');
            } else {
                setError(apiErr.message ?? 'Что-то пошло не так');
            }
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="page">
            <Header
                rightSlot={
                    <p className="register__login-hint">
                        Уже есть аккаунт?{' '}
                        <Link to="/login" className="register__login-link">
                            Войти
                        </Link>
                    </p>
                }
            />

            <main className="page__content">
                <div className="register-card">
                    <h1 className="register-card__title">Регистрация</h1>
                    <p className="register-card__subtitle">
                        Заполните данные, чтобы создать аккаунт
                    </p>

                    <form className="register-form" onSubmit={handleSubmit} noValidate>
                        <div className="register-form__row">
                            <div className="form-field">
                                <label className="form-field__label" htmlFor="firstName">
                                    Имя
                                </label>
                                <input
                                    id="firstName"
                                    type="text"
                                    className="form-field__input"
                                    placeholder="Иван"
                                    value={firstName}
                                    onChange={(e) => setFirstName(e.target.value)}
                                    disabled={isSubmitting}
                                    required
                                />
                                {fieldErrors.firstName && (
                                    <span className="form-field__error">{fieldErrors.firstName}</span>
                                )}
                            </div>

                            <div className="form-field">
                                <label className="form-field__label" htmlFor="lastName">
                                    Фамилия
                                </label>
                                <input
                                    id="lastName"
                                    type="text"
                                    className="form-field__input"
                                    placeholder="Иванов"
                                    value={lastName}
                                    onChange={(e) => setLastName(e.target.value)}
                                    disabled={isSubmitting}
                                    required
                                />
                                {fieldErrors.lastName && (
                                    <span className="form-field__error">{fieldErrors.lastName}</span>
                                )}
                            </div>
                        </div>

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
                                disabled={isSubmitting}
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
                                disabled={isSubmitting}
                                required
                            />
                            <span className="form-field__hint">
                Минимум 8 символов, буквы и цифры
              </span>
                            {fieldErrors.password && (
                                <span className="form-field__error">{fieldErrors.password}</span>
                            )}
                        </div>

                        <div className="form-field">
                            <label className="form-field__label" htmlFor="confirmPassword">
                                Подтвердите пароль
                            </label>
                            <input
                                id="confirmPassword"
                                type="password"
                                className="form-field__input"
                                placeholder="Повторите пароль"
                                value={confirmPassword}
                                onChange={(e) => setConfirmPassword(e.target.value)}
                                disabled={isSubmitting}
                                required
                            />
                            {fieldErrors.confirmPassword && (
                                <span className="form-field__error">
                  {fieldErrors.confirmPassword}
                </span>
                            )}
                        </div>

                        {error && <p className="register-form__error">{error}</p>}

                        <button type="submit" className="btn btn--primary register-form__submit" disabled={isSubmitting}>
                            {isSubmitting ? 'Регистрируем…' : 'Зарегистрироваться'}
                        </button>
                    </form>
                </div>
            </main>
        </div>
    );
}