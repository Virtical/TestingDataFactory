import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Header } from '../components/Header';
import './RegisterPage.css';

export function RegisterPage() {
    const navigate = useNavigate();

    const [firstName, setFirstName] = useState('');
    const [lastName, setLastName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [error, setError] = useState<string | null>(null);

    const handleSubmit = (e: FormEvent) => {
        e.preventDefault();
        setError(null);

        if (password !== confirmPassword) {
            setError('Пароли не совпадают');
            return;
        }
        if (password.length < 8) {
            setError('Пароль должен быть не короче 8 символов');
            return;
        }
        
        navigate('/welcome');
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

                    <form className="register-form" onSubmit={handleSubmit}>
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
                                    required
                                />
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
                                    required
                                />
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
                                required
                            />
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
                            <span className="form-field__hint">
                Минимум 8 символов, буквы и цифры
              </span>
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
                                required
                            />
                        </div>

                        {error && <p className="register-form__error">{error}</p>}

                        <button type="submit" className="btn btn--primary register-form__submit">
                            Зарегистрироваться
                        </button>
                    </form>
                </div>
            </main>
        </div>
    );
}