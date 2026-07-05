import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { authApi } from '../api/auth';
import { useAuthStore } from '../store/useAuthStore';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();
  const setAuth = useAuthStore(state => state.setAuth);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const res = await authApi.login(email, password, rememberMe);
      setAuth(res.token, res.account);
      navigate(res.account.telegramUserId ? '/' : '/link-telegram');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Не удалось войти');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col items-center justify-center h-full p-6">
      <div className="w-full max-w-sm">
        <h1 className="text-2xl font-bold mb-6 text-center text-white">Вход в ClientScout</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <input
            type="email"
            placeholder="Email"
            className="w-full p-3.5 bg-white/5 border border-white/10 rounded-2xl text-white outline-none transition-colors focus:border-indigo-500 focus:bg-white/[0.07]"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
          <input
            type="password"
            placeholder="Пароль"
            className="w-full p-3.5 bg-white/5 border border-white/10 rounded-2xl text-white outline-none transition-colors focus:border-indigo-500 focus:bg-white/[0.07]"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          <label className="flex items-center gap-2 text-sm text-white/70">
            <input
              type="checkbox"
              checked={rememberMe}
              onChange={(e) => setRememberMe(e.target.checked)}
              className="h-4 w-4 rounded border-white/20 bg-white/5 accent-indigo-600"
            />
            Запомнить меня на 30 дней
          </label>
          {error && <div className="text-red-500 text-sm text-center">{error}</div>}
          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-3.5 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-2xl transition-all active:scale-[0.99] shadow-lg shadow-indigo-600/25 disabled:opacity-50"
          >
            {isLoading ? 'Загрузка...' : 'Войти'}
          </button>
        </form>
        <p className="mt-6 text-center text-white/50 text-sm">
          Нет аккаунта? <Link to="/register" className="text-indigo-400 font-semibold hover:underline">Зарегистрироваться</Link>
        </p>
      </div>
    </div>
  );
}
