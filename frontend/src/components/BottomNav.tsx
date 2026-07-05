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
        borderColor: 'rgba(255,255,255,0.07)',
        backgroundColor: 'rgba(24, 24, 24, 0.94)',
        backdropFilter: 'blur(18px)',
        WebkitBackdropFilter: 'blur(18px)',
        boxShadow: '0 -16px 34px rgba(0,0,0,0.32)',
      }}
    >
      {tabs.map((tab) => {
        const isActive = activeTab === tab.path;
        const Icon = tab.icon;
        return (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            className="relative h-[58px] flex flex-col items-center justify-center gap-1 rounded-xl transition-all duration-200 active:scale-[0.97]"
            style={{
              backgroundColor: isActive ? 'rgba(76, 194, 255, 0.12)' : 'transparent',
              color: isActive ? '#4CC2FF' : '#9A9A9A',
            }}
          >
            {isActive && (
              <span
                className="absolute top-1 h-1 w-8 rounded-full"
                style={{ backgroundColor: '#4CC2FF', boxShadow: '0 0 10px rgba(76,194,255,0.6)' }}
              />
            )}
            <Icon className="w-6 h-6" strokeWidth={isActive ? 2.4 : 2} />
            <span className="text-[13px] font-semibold leading-none">{tab.label}</span>
          </button>
        );
      })}
    </nav>
  );
}
