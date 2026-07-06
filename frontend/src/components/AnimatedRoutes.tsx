import { lazy, Suspense, useEffect, useRef } from 'react';
import { Routes, Route, useLocation } from 'react-router-dom';
import { AnimatePresence, motion } from 'framer-motion';

import AuthGuard from './AuthGuard';

const HomePage = lazy(() => import('../pages/HomePage'));
const SearchPage = lazy(() => import('../pages/SearchPage'));
const SearchChatsPage = lazy(() => import('../pages/SearchChatsPage'));
const SearchLeadHistoryPage = lazy(() => import('../pages/SearchLeadHistoryPage'));
const MailingPage = lazy(() => import('../pages/MailingPage'));
const MailingChatsPage = lazy(() => import('../pages/MailingChatsPage'));
const LoginPage = lazy(() => import('../pages/LoginPage'));
const RegisterPage = lazy(() => import('../pages/RegisterPage'));
const TelegramLinkPage = lazy(() => import('../pages/TelegramLinkPage'));
const AdminAnalyticsPage = lazy(() => import('../pages/AdminAnalyticsPage'));

const variants = {
  initial: (direction: number) => ({
    x: direction > 0 ? '100%' : direction < 0 ? '-100%' : '0%',
  }),
  animate: {
    x: '0%',
  },
  exit: (direction: number) => ({
    x: direction > 0 ? '-100%' : direction < 0 ? '100%' : '0%',
  }),
};

export default function AnimatedRoutes() {
  const location = useLocation();
  const prevPathRef = useRef(location.pathname);

  const getTabIndex = (path: string) => {
    if (path.startsWith('/mailing')) return 1;
    if (path.startsWith('/search')) return 2;
    if (path === '/') return 0;
    return -1;
  };

  let direction = 0;
  const currentIdx = getTabIndex(location.pathname);
  const lastIdx = getTabIndex(prevPathRef.current);

  if (currentIdx !== -1 && lastIdx !== -1 && currentIdx !== lastIdx) {
    direction = currentIdx > lastIdx ? 1 : -1;
  }

  useEffect(() => {
    prevPathRef.current = location.pathname;
  }, [location.pathname]);

  return (
    <div className="relative flex-1 w-full h-full overflow-hidden">
      <AnimatePresence initial={false} custom={direction}>
        <motion.div
          key={location.pathname}
          custom={direction}
          variants={variants}
          initial="initial"
          animate="animate"
          exit="exit"
          transition={{ type: "spring", stiffness: 260, damping: 30 }}
          className="absolute inset-0 w-full h-full overflow-y-auto overflow-x-hidden bg-[#202020]"
        >
          <Suspense fallback={null}>
            <Routes location={location}>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/link-telegram" element={<TelegramLinkPage />} />

              <Route element={<AuthGuard />}>
                <Route path="/" element={<HomePage />} />
                <Route path="/search" element={<SearchPage />} />
                <Route path="/search/chats" element={<SearchChatsPage />} />
                <Route path="/search/leads" element={<SearchLeadHistoryPage />} />
                <Route path="/mailing" element={<MailingPage />} />
                <Route path="/mailing/chats" element={<MailingChatsPage />} />
                <Route path="/admin" element={<AdminAnalyticsPage />} />
              </Route>
            </Routes>
          </Suspense>
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
