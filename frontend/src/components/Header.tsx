import './Header.css';
import { Box } from 'react-bootstrap-icons';
import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';

type HeaderProps = {
    rightSlot?: ReactNode;
};

export function Header({ rightSlot }: HeaderProps) {
    return (
        <header className="header">
            <div className="header__inner">
                <Link to="/" className="header__logo">
                    <Box size={24} />
                    <span className="header__logo-text">DataForge</span>
                </Link>

                <div className="header__actions">
                    {rightSlot ?? (
                        <>
                            <Link to="/login" className="btn btn--outline">
                                Войти
                            </Link>
                            <Link to="/register" className="btn btn--primary">
                                Зарегистрироваться
                            </Link>
                        </>
                    )}
                </div>
            </div>
        </header>
    );
}
