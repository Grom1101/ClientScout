import { useEffect } from 'react';
import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/useAuthStore';
import { authApi } from '../api/auth';

export default function AuthGuard() {
  const token = useAuthStore(state => state.token);
  const account = useAuthStore(state => state.account);
  const location = useLocation();

  useEffect(() => {
    if (token) {
      // Background check on mount to ensure the token is still valid (e.g. database wasn't wiped)
      // The interceptor in client.ts will handle 401s by logging out automatically
      authApi.getMe().catch(() => {});
    }
  }, [token]);

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  if (!account?.telegramUserId && location.pathname !== '/link-telegram') {
    return <Navigate to="/link-telegram" replace />;
  }

  return <Outlet />;
}
