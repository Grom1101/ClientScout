import { useLocation, useNavigate } from 'react-router-dom';
import { Home, Send, Search } from 'lucide-react';

const tabs = [
  { path: '/', label: 'Главная', icon: Home },
  { path: '/mailing', label: 'Рассылка', icon: Send },
  { path: '/search', label: 'Поиск', icon: Search },
];

export default function BottomNav() {
  const location = useLocation();
  const navigate = useNavigate();

  const getActiveTab = () => {
    if (location.pathname.startsWith('/mailing')) return '/mailing';
    if (location.pathname.startsWith('/search')) return '/search';
    return '/';
  };

  const activeTab = getActiveTab();

  return (
    <nav
      className="flex items-center justify-around py-2 pb-3 border-t"
      style={{ borderColor: 'rgba(255,255,255,0.06)', backgroundColor: '#0B0E18' }}
    >
      {tabs.map((tab) => {
        const isActive = activeTab === tab.path;
        const Icon = tab.icon;
        return (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            className="flex flex-col items-center gap-1 px-4 py-1 transition-colors"
          >
            <Icon
              className="w-5 h-5 transition-colors"
              style={{ color: isActive ? '#7C3AED' : '#64748B' }}
            />
            <span
              className="text-[11px] font-medium transition-colors"
              style={{ color: isActive ? '#7C3AED' : '#64748B' }}
            >
              {tab.label}
            </span>
          </button>
        );
      })}
    </nav>
  );
}
