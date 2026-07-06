import { useEffect, useState } from 'react';
import Modal from './Modal';
import { useSearchRuntimeStore, type KworkLoginFlowStatus } from '../store/useSearchRuntimeStore';
import { CheckCircle, AlertCircle, Loader2, Wifi, WifiOff } from 'lucide-react';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

// Modern Kwork SVG logo
const KworkSVG = () => (
  <svg viewBox="0 0 100 100" width="46" height="46" xmlns="http://www.w3.org/2000/svg">
    <polygon points="17,20 41,20 41,36 25,52 25,28 17,28" fill="#FF9900" />
    <polygon points="25,62 25,80 41,80 41,54 67,80 83,80 53,50 83,20 67,20" fill="#FFFFFF" />
  </svg>
);

function friendlyError(raw?: string | null): string | null {
  if (!raw) return null;
  const r = raw.toUpperCase();
  if (r.includes('KWORK_ACCESS_BLOCKED'))  return 'Kwork заблокировал доступ. Зайдите в Kwork через браузер, пройдите проверку и переподключите.';
  if (r.includes('NOT_AUTHORIZED'))        return 'Сессия устарела, переподключитесь.';
  if (r.includes('UNAUTHORIZED'))          return 'Нет доступа. Переподключитесь.';
  if (r.includes('TIMEOUT') || r.includes('TIMED OUT')) return 'Нет ответа от Kwork. Попробуйте позже.';
  if (r.includes('NETWORK') || r.includes('CONNECTION')) return 'Ошибка сети. Проверьте интернет.';
  if (r.includes('BOT') || r.includes('CAPTCHA') || r.includes('ANTI-BOT')) return 'Kwork запросил проверку. Войдите вручную и переподключите.';
  if (r.includes('SESSION'))               return 'Сессия недействительна, переподключитесь.';
  if (raw.length > 60) return 'Ошибка соединения, переподключитесь.';
  return raw;
}

const flowText = (flow?: KworkLoginFlowStatus | null): { text: string; isError: boolean; isDone: boolean } | null => {
  if (!flow) return null;
  if (flow.isCompleted) return { text: 'Kwork подключен', isError: false, isDone: true };
  if (flow.isFailed)    return { text: friendlyError(flow.error) || 'Не удалось подключить', isError: true, isDone: false };
  if (flow.status === 'opening_browser')    return { text: 'Открываю браузер...', isError: false, isDone: false };
  if (flow.status === 'waiting_for_login')  return { text: 'Войдите в Kwork в открывшемся браузере', isError: false, isDone: false };
  return { text: 'Подключаю...', isError: false, isDone: false };
};

export default function SearchExchangesModal({ isOpen, onClose }: Props) {
  const { exchanges, isLoading, fetchExchanges, startKworkLogin, disconnectKwork, fetchKworkLoginStatus } = useSearchRuntimeStore();
  const [flow, setFlow] = useState<KworkLoginFlowStatus | null>(null);
  const [isStarting, setIsStarting] = useState(false);
  const [isDisconnecting, setIsDisconnecting] = useState(false);

  useEffect(() => {
    if (isOpen) fetchExchanges();
  }, [isOpen, fetchExchanges]);

  useEffect(() => {
    if (!flow || flow.isCompleted || flow.isFailed) return;
    const timer = window.setInterval(async () => {
      const next = await fetchKworkLoginStatus(flow.flowId);
      setFlow(next);
    }, 2500);
    return () => window.clearInterval(timer);
  }, [flow, fetchKworkLoginStatus]);

  const kwork = exchanges.find(e => e.exchangeType === 0);
  const isConnected   = kwork?.status === 1;
  const needsReconnect = kwork?.status === 2;
  const isFlowPending  = !!flow && !flow.isCompleted && !flow.isFailed;
  const currentFlow    = flowText(flow);
  const errorText      = friendlyError(kwork?.lastError);

  const handleConnect = async () => {
    setIsStarting(true);
    try {
      const nextFlow = await startKworkLogin();
      setFlow(nextFlow);
    } finally {
      setIsStarting(false);
    }
  };

  const handleDisconnect = async () => {
    setIsDisconnecting(true);
    try {
      await disconnectKwork();
      setFlow(null);
    } finally {
      setIsDisconnecting(false);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Биржи">
      <div className="relative flex flex-col gap-4 pb-2" style={{ marginTop: '5px' }}>

        {/* ── Kwork ── */}
        <div className="rounded-xl border border-white/[0.08] bg-black/20" style={{ padding: '15px 20px' }}>
          {/* Шапка */}
          <div className="flex items-center gap-4">
            <div className="shrink-0"><KworkSVG /></div>

            <div className="flex-1 min-w-0">
              <p className="text-[17px] font-extrabold text-white leading-none" style={{ marginBottom: '4px' }}>Kwork</p>
              <div className="flex items-center gap-1.5" style={{ marginBottom: '4px' }}>
                {isLoading
                  ? <Loader2 className="h-4 w-4 animate-spin text-slate-500" />
                  : isConnected
                    ? <Wifi className="h-4 w-4" style={{ color: '#4CC2FF' }} />
                    : <WifiOff className="h-4 w-4" style={{ color: needsReconnect ? '#F59E0B' : '#64748B' }} />
                }
                <span 
                  className="text-[13px] font-bold"
                  style={{ color: isLoading ? '#64748B' : isConnected ? '#4CC2FF' : needsReconnect ? '#F59E0B' : '#64748B' }}
                >
                  {isLoading ? 'Проверка...' : isConnected ? 'Подключено' : needsReconnect ? 'Нужно переподключить' : 'Не подключено'}
                </span>
              </div>
              {kwork?.lastCheckedAt && !isLoading && (
                <p className="text-[11px] font-medium text-slate-500">
                  Последняя проверка {new Date(kwork.lastCheckedAt).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}
                </p>
              )}
            </div>

            {/* Индикатор */}
            <div 
              className="h-2 w-2 shrink-0 rounded-full"
              style={{
                backgroundColor: isConnected ? '#4CC2FF' : needsReconnect ? '#F59E0B' : '#1F2937',
                boxShadow: isConnected ? '0 0 8px rgba(76,194,255,0.6)' : needsReconnect ? '0 0 8px rgba(245,158,11,0.6)' : 'none',
              }} 
            />
          </div>

          {/* Ошибка с сервера (lastError) */}
          {errorText && !flow && (
            <div className="mb-3 mt-4 flex items-start gap-2 rounded-xl border border-red-500/15 bg-red-500/10 p-3">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-400" />
              <p className="text-xs font-semibold leading-relaxed text-red-400">{errorText}</p>
            </div>
          )}



          {/* Кнопки + флоу */}
          <div className="flex flex-col gap-3" style={{ marginTop: '15px' }}>
            <div className="grid grid-cols-2 gap-3">
              <button
                onClick={handleConnect}
                disabled={isStarting || isFlowPending}
                className="flex items-center justify-center gap-2 rounded-xl text-[13px] font-bold text-white transition-all hover:brightness-110 active:scale-[0.98] disabled:opacity-50"
                style={{ backgroundColor: needsReconnect ? '#F59E0B' : '#0EA5E9', height: '38px' }}
              >
                {(isStarting || isFlowPending) && (
                  <Loader2 className="h-4 w-4 animate-spin" />
                )}
                {kwork?.requiresReconnect ? 'Сброс' : 'Подключить'}
              </button>

              <button
                onClick={handleDisconnect}
                disabled={isDisconnecting || isFlowPending || !isConnected}
                className="flex items-center justify-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.04] text-[13px] font-bold text-white transition-all hover:bg-white/[0.08] active:scale-[0.98] disabled:opacity-50"
                style={{ height: '38px' }}
              >
                {isDisconnecting && (
                  <Loader2 className="h-4 w-4 animate-spin" />
                )}
                Отключить
              </button>
            </div>

            {/* Статус флоу */}
            {currentFlow && (
              <div 
                className="flex items-start gap-2 rounded-xl border p-3 mt-2"
                style={{
                  backgroundColor: currentFlow.isError ? 'rgba(239,68,68,0.08)' : currentFlow.isDone ? 'rgba(76,194,255,0.08)' : 'rgba(255,255,255,0.04)',
                  borderColor: currentFlow.isError ? 'rgba(239,68,68,0.15)' : currentFlow.isDone ? 'rgba(76,194,255,0.15)' : 'rgba(255,255,255,0.06)',
                }}
              >
                {currentFlow.isError
                  ? <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-400" />
                  : currentFlow.isDone
                    ? <CheckCircle className="mt-0.5 h-4 w-4 shrink-0 text-[#4CC2FF]" />
                    : <Loader2 className="mt-0.5 h-4 w-4 shrink-0 animate-spin text-slate-400" />
                }
                <p 
                  className="text-xs font-semibold leading-relaxed"
                  style={{ color: currentFlow.isError ? '#F87171' : currentFlow.isDone ? '#4CC2FF' : '#94A3B8' }}
                >
                  {currentFlow.text}
                </p>
              </div>
            )}
          </div>
        </div>

        {/* ── Upwork (скоро) ── */}
        <div className="flex items-center gap-4 rounded-xl border border-white/[0.05] bg-black/10 opacity-40" style={{ padding: '15px 20px' }}>
          <div className="flex shrink-0 items-center justify-center rounded-full bg-[#0D2B1E]" style={{ width: '46px', height: '46px' }}>
            <span className="font-black text-[#6EE7B7] text-lg">U</span>
          </div>
          <div>
            <p className="text-[15px] font-extrabold text-white">Upwork</p>
            <p className="mt-0.5 text-[11px] font-bold text-slate-500">Скоро</p>
          </div>
        </div>
      </div>
    </Modal>
  );
}
