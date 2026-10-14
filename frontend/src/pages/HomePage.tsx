import { Sidebar } from '../components/Sidebar';
import './HomePage.css';

export function HomePage() {
    return (
        <div className="layout">
            <Sidebar />
            <div className="layout__main">
                <header className="topbar">
                </header>
            </div>
        </div>
    );
}