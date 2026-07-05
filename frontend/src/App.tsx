import { useEffect } from 'react';
import AnimatedRoutes from './components/AnimatedRoutes';
import { useLocation } from 'react-router-dom';
import BottomNav from './components/BottomNav';

export default function App() {
  const location = useLocation();

  useEffect(() => {
    // Notify Telegram that the Web App is ready
    const tg = (window as any).Telegram?.WebApp;
    if (tg) {
      tg.ready();
      // Optionally expand the webview to full height
      tg.expand();
      
      // Set CSS variables based on Telegram's theme
      document.documentElement.style.setProperty('--tg-theme-bg-color', tg.backgroundColor || '#202020');
      document.documentElement.style.setProperty('--tg-theme-text-color', tg.textColor || '#FFFFFF');
    }
  }, []);

  const hideNav = ['/login', '/register', '/link-telegram'].includes(location.pathname);

  return (
    <div className="app-bg flex flex-col h-screen w-full" style={{ color: 'var(--tg-theme-text-color, #ffffff)' }}>
      <AnimatedRoutes />
      {!hideNav && <BottomNav />}
    </div>
  );
}
