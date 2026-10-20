import { useNavigate } from 'react-router-dom';
import { Box, ArrowBarRight } from 'react-bootstrap-icons';
import { useAuth } from '../context/AuthContext';
import './Sidebar.css';

export function Sidebar() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();

    const displayName = user ? `${user.firstName} ${user.lastName}` : 'Пользователь';

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

            <div className="sidebar__user">
                <div className="sidebar__user-avatar" />
                <span className="sidebar__user-name">{displayName}</span>
                <button
                    type="button"
                    className="sidebar__logout"
                    onClick={handleLogout}
                    aria-label="Выйти"
                    title="Выйти"
                >
                    <ArrowBarRight size={24} />
                </button>
            </div>
        </aside>
    );
}