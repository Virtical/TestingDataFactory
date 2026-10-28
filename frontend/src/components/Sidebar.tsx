import { useNavigate, Link } from 'react-router-dom';
import { Box, ArrowBarRight, PlusLg, Search } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import './Sidebar.css';

export function Sidebar() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();

    const displayName = user ? `${user.firstName} ${user.lastName}` : 'Пользователь';
    const displayEmail = user ? user.email : '';
    const initial = user ? user.firstName[0] : '';

    const handleLogout = () => {
        navigate('/', { replace: true });
        void logout();
    };

    return (
        <aside className="sidebar">
            <div className="sidebar__logo">
                <Box size={24} />
                <span className="sidebar__logo-text">DataForge</span>
            </div>

            <div className="sidebar__nav">
                <Link to="/dashboard" className="btn btn--primary sidebar__new-btn">
                    <PlusLg size={14} /> Новый проект
                </Link>
                <div className="sidebar__search">
                    <Search size={16} className="sidebar__search-icon" />
                    <input type="text" placeholder="Поиск" />
                </div>
            </div>

            <div className="sidebar__scroll-area">
                <p className="sidebar__empty-text">У вас пока нет проектов</p>
            </div>

            <div className="sidebar__footer">
                <div className="sidebar__user">
                    <div className="sidebar__user-avatar">{initial}</div>
                    <div className="sidebar__user-info">
                        <span className="sidebar__user-name">{displayName}</span>
                        <span className="sidebar__user-email">{displayEmail}</span>
                    </div>
                    <button
                        type="button"
                        className="sidebar__logout"
                        onClick={handleLogout}
                        aria-label="Выйти"
                        title="Выйти"
                    >
                        <ArrowBarRight size={16} />
                    </button>
                </div>
            </div>
        </aside>
    );
}