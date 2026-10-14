import { Link } from 'react-router-dom';

export function NotFoundPage() {
    return (
        <div className="page">
            <main className="page__content">
                <div className="not-found">
                    <h1 className="not-found__title">404</h1>
                    <p className="not-found__text">Страница не найдена</p>
                    <Link to="/" className="btn btn--primary">
                        На главную
                    </Link>
                </div>
            </main>
        </div>
    );
}