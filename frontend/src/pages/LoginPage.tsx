import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Header } from '../components/Header';
import './LoginPage.css';

export function LoginPage() {
    const navigate = useNavigate();

    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState<string | null>(null);

    const handleSubmit = (e: FormEvent) => {
        e.preventDefault();
        setError(null);

        if (!email || !password) {
            setError('Заполните все поля');
            return;
        }
        
        navigate('/welcome');
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

                    <form className="login-form" onSubmit={handleSubmit}>
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