import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi } from '../api/auth';
import { useAuthStore } from '../store/useAuthStore';
import { apiClient } from '../api/client';

export default function TelegramLinkPage() {
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [step, setStep] = useState<'PHONE' | 'CODE' | 'PASSWORD'>('PHONE');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  const account = useAuthStore(state => state.account);
  const updateAccount = useAuthStore(state => state.updateAccount);

  useEffect(() => {
    if (account?.telegramUserId) {
      navigate('/', { replace: true });
    }
  }, [account?.telegramUserId, navigate]);

  const refreshAccountAndOpenApp = async () => {
    const updatedAccount = await authApi.getMe();
    updateAccount(updatedAccount);
    navigate('/', { replace: true });
  };

  const handleSendPhone = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      await apiClient.post('/TelegramAuth/send-code', { phoneNumber: phone });
      setStep('CODE');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Не удалось отправить код');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSendCode = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const res = await apiClient.post('/TelegramAuth/verify-code', { phoneNumber: phone, code });
      if (res.data.requiresPassword) {
        setStep('PASSWORD');
      } else if (res.data.success) {
        await refreshAccountAndOpenApp();
      } else {
        setError('Не удалось подтвердить код');
      }
    } catch (err: any) {
      setError(err.response?.data?.message || 'Не удалось проверить код');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSendPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      const res = await apiClient.post('/TelegramAuth/verify-password', { password });
      if (res.data.success) {
        await refreshAccountAndOpenApp();
      } else {
        setError('Не удалось войти в Telegram');
      }
    } catch (err: any) {
      setError(err.response?.data?.message || 'Не удалось проверить пароль');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col items-center justify-center h-full p-6">
      <div className="w-full max-w-sm">
        <h1 className="text-2xl font-bold mb-6 text-center text-white">Привязка Telegram</h1>
        <p className="text-white/60 text-sm text-center mb-6">
          Для работы ClientScout нужно один раз привязать Telegram-аккаунт, от имени которого будут проверяться и добавляться чаты.
        </p>

        {step === 'PHONE' && (
          <form onSubmit={handleSendPhone} className="flex flex-col gap-4">
            <input
              type="text"
              placeholder="Номер телефона, например +1234567890"
              className="w-full p-3 bg-white/5 border border-white/10 rounded-md text-white outline-none focus:border-[#0078D4] focus:bg-white/[0.07]"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              required
            />
            {error && <div className="text-red-500 text-sm text-center">{error}</div>}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 bg-[#0078D4] hover:bg-[#0067B8] text-white font-medium rounded-md transition-colors disabled:opacity-50"
            >
              {isLoading ? 'Загрузка...' : 'Отправить код'}
            </button>
          </form>
        )}

        {step === 'CODE' && (
          <form onSubmit={handleSendCode} className="flex flex-col gap-4">
            <input
              type="text"
              placeholder="Код из Telegram"
              className="w-full p-3 bg-white/5 border border-white/10 rounded-md text-white outline-none focus:border-[#0078D4] focus:bg-white/[0.07]"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
            />
            {error && <div className="text-red-500 text-sm text-center">{error}</div>}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 bg-[#0078D4] hover:bg-[#0067B8] text-white font-medium rounded-md transition-colors disabled:opacity-50"
            >
              {isLoading ? 'Проверка...' : 'Привязать'}
            </button>
          </form>
        )}

        {step === 'PASSWORD' && (
          <form onSubmit={handleSendPassword} className="flex flex-col gap-4">
            <input
              type="password"
              placeholder="Облачный пароль Telegram"
              className="w-full p-3 bg-white/5 border border-white/10 rounded-md text-white outline-none focus:border-[#0078D4] focus:bg-white/[0.07]"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
            {error && <div className="text-red-500 text-sm text-center">{error}</div>}
            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-3 bg-[#0078D4] hover:bg-[#0067B8] text-white font-medium rounded-md transition-colors disabled:opacity-50"
            >
              {isLoading ? 'Проверка...' : 'Войти'}
            </button>
          </form>
        )}

        <button
          onClick={() => {
            useAuthStore.getState().logout();
            navigate('/login');
          }}
          className="mt-6 w-full py-3 text-white/50 hover:text-white text-sm font-medium transition-colors"
        >
          Выйти из аккаунта
        </button>
      </div>
    </div>
  );
}
