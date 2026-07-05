import { useLocation, useNavigate } from 'react-router-dom';
import { Home, Search, Send } from 'lucide-react';

const tabs = [
  { path: '/', label: 'Главная', icon: Home },
  { path: '/mailing', label: 'Рассылка', icon: Send },
  { path: '/search', label: 'Поиск', icon: Search },
];

export default function BottomNav() {
  const location = useLocation();
  const navigate = useNavigate();

  const activeTab = location.pathname.startsWith('/mailing')
    ? '/mailing'
    : location.pathname.startsWith('/search')
      ? '/search'
      : '/';

  return (
    <nav
      className="grid grid-cols-3 gap-2 px-5 pt-3 pb-[calc(14px+env(safe-area-inset-bottom))] border-t"
      style={{
        borderColor: 'rgba(148,163,184,0.10)',
        backgroundColor: 'rgba(7, 13, 22, 0.96)',
        boxShadow: '0 -16px 34px rgba(0,0,0,0.28)',
      }}
    >
      {tabs.map((tab) => {
        const isActive = activeTab === tab.path;
        const Icon = tab.icon;
        return (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            className="h-[58px] flex flex-col items-center justify-center gap-1 rounded-xl transition-all active:scale-[0.98]"
            style={{
              backgroundColor: isActive ? 'rgba(124,58,237,0.13)' : 'transparent',
              color: isActive ? '#8B5CF6' : '#7A8798',
            }}
          >
            <Icon className="w-7 h-7" strokeWidth={isActive ? 2.4 : 2} />
            <span className="text-[13px] font-semibold leading-none">{tab.label}</span>
          </button>
        );
      })}
    </nav>
  );
}
