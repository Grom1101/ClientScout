import { useState } from 'react';
import { apiClient } from '../api/client';

type AuthStep = 'phone' | 'code' | 'password';

export function TelegramAuthModal({
  isOpen,
  onClose,
  onSuccess
}: {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}) {
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [step, setStep] = useState<AuthStep>('phone');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const getUserId = () => {
    const tg = (window as any).Telegram?.WebApp;
    return tg?.initDataUnsafe?.user?.id?.toString() || '1';
  };

  const handleSendCode = async () => {
    if (!phone.trim()) return;
    setLoading(true);
    setError('');

    try {
      await apiClient.post('/TelegramAuth/send-code', {
        userId: getUserId(),
        phoneNumber: phone.trim()
      });
      setStep('code');
    } catch (err: any) {
      setError(err.response?.data?.message || 'Не удалось отправить код.');
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyCode = async () => {
    if (!code.trim()) return;
    setLoading(true);
    setError('');

    try {
      const response = await apiClient.post('/TelegramAuth/verify-code', {
        userId: getUserId(),
        phoneNumber: phone.trim(),
        code: code.trim()
      });

      if (response.data?.requiresPassword) {
        setStep('password');
        return;
      }

      if (response.data?.success === false) {
        setError('Telegram запросил дополнительный шаг авторизации.');
        return;
      }

      onSuccess();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Неверный код.');
    } finally {
      setLoading(false);
    }
  };

  const handleVerifyPassword = async () => {
    if (!password) return;
    setLoading(true);
    setError('');

    try {
      const response = await apiClient.post('/TelegramAuth/verify-password', {
        userId: getUserId(),
        password
      });

      if (response.data?.success === false) {
        setError('Не удалось завершить авторизацию.');
        return;
      }

      setPassword('');
      onSuccess();
    } catch (err: any) {
      setError(err.response?.data?.message || 'Неверный облачный пароль.');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setError('');
    setCode('');
    setPassword('');
    setStep('phone');
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-sm rounded-2xl overflow-hidden shadow-2xl border border-indigo-400/15" style={{ background: 'linear-gradient(180deg, #1A1E36 0%, #10142A 100%)' }}>
        <div className="p-6">
          <h2 className="text-xl font-bold text-white mb-2">Авторизация Telegram</h2>
          <p className="text-sm text-gray-400 mb-6">
            Подключите ваш Telegram-аккаунт, чтобы проверять чаты и отправлять рассылки только туда, где аккаунт состоит и может писать.
          </p>

          {error && (
            <div className="mb-4 p-3 bg-red-500/10 text-red-400 rounded-xl text-sm border border-red-500/20">
              {error}
            </div>
          )}

          {step === 'phone' && (
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-2">Номер телефона</label>
              <input
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="+375291234567"
                className="w-full bg-white/5 text-white rounded-xl px-4 py-3 outline-none focus:ring-2 focus:ring-[#6366F1] transition-all border border-white/10"
              />
              <div className="flex gap-3 mt-6">
                <button className="flex-1 py-3 rounded-xl text-sm font-bold text-white bg-white/10 hover:bg-white/20" onClick={handleClose}>
                  Отмена
                </button>
                <button
                  className="flex-1 py-3 rounded-xl text-sm font-bold text-white bg-[#6366F1] hover:bg-[#4F46E5] disabled:opacity-50"
                  onClick={handleSendCode}
                  disabled={!phone.trim() || loading}
                >
                  {loading ? 'Отправка...' : 'Получить код'}
                </button>
              </div>
            </div>
          )}

          {step === 'code' && (
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-2">Код из Telegram</label>
              <input
                type="text"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="12345"
                className="w-full bg-white/5 text-white rounded-xl px-4 py-3 outline-none focus:ring-2 focus:ring-[#6366F1] transition-all border border-white/10"
              />
              <div className="flex gap-3 mt-6">
                <button className="flex-1 py-3 rounded-xl text-sm font-bold text-white bg-white/10 hover:bg-white/20" onClick={() => setStep('phone')}>
                  Назад
                </button>
                <button
                  className="flex-1 py-3 rounded-xl text-sm font-bold text-white bg-[#6366F1] hover:bg-[#4F46E5] disabled:opacity-50"
                  onClick={handleVerifyCode}
                  disabled={!code.trim() || loading}
                >
                  {loading ? 'Проверка...' : 'Войти'}
                </button>
              </div>
            </div>
          )}

          {step === 'password' && (
            <div>
              <label className="block text-sm font-medium text-gray-400 mb-2">Облачный пароль Telegram</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Введите пароль 2FA"
                className="w-full bg-white/5 text-white rounded-xl px-4 py-3 outline-none focus:ring-2 focus:ring-[#6366F1] transition-all border border-white/10"
              />
              <div className="flex gap-3 mt-6">
                <button className="flex-1 py-3 rounded-xl text-sm font-bold text-white bg-white/10 hover:bg-white/20" onClick={() => setStep('code')}>
                  Назад
                </button>
                <button
                  className="flex-1 py-3 rounded-xl text-sm font-bold text-white bg-[#6366F1] hover:bg-[#4F46E5] disabled:opacity-50"
                  onClick={handleVerifyPassword}
                  disabled={!password || loading}
                >
                  {loading ? 'Проверка...' : 'Завершить'}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
