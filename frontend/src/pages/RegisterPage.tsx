import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { authApi } from '../api/auth';
import { useAuthStore } from '../store/useAuthStore';

export default function RegisterPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();
  const setAuth = useAuthStore(state => state.setAuth);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);
    try {
      const res = await authApi.register(email, password);
      setAuth(res.token, res.account);
      navigate('/link-telegram');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Registration failed');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col items-center justify-center h-full p-6">
      <div className="w-full max-w-sm">
        <h1 className="text-2xl font-bold mb-6 text-center text-white">Регистрация</h1>
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <input
            type="email"
            placeholder="Email"
            className="w-full p-3.5 bg-white/5 border border-white/10 rounded-md text-white outline-none transition-colors focus:border-[#0078D4] focus:bg-white/[0.07]"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
          <input
            type="password"
            placeholder="Пароль"
            className="w-full p-3.5 bg-white/5 border border-white/10 rounded-md text-white outline-none transition-colors focus:border-[#0078D4] focus:bg-white/[0.07]"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          {error && <div className="text-red-500 text-sm text-center">{error}</div>}
          <button
            type="submit"
            disabled={isLoading}
            className="w-full py-3.5 bg-[#0078D4] hover:bg-[#0067B8] text-white font-semibold rounded-md transition-all active:scale-[0.99] shadow-lg shadow-[#0078D4]/30 disabled:opacity-50"
          >
            {isLoading ? 'Создать аккаунт' : 'Зарегистрироваться'}
          </button>
        </form>
        <p className="mt-6 text-center text-white/50 text-sm">
          Уже есть аккаунт? <Link to="/login" className="text-[#4CC2FF] font-semibold hover:underline">Войти</Link>
        </p>
      </div>
    </div>
  );
}
