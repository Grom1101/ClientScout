import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, CheckCircle, Loader2, Wifi, WifiOff } from 'lucide-react';
import Modal from './Modal';
import { useSearchRuntimeStore, type ExchangeConnection, type KworkLoginFlowStatus } from '../store/useSearchRuntimeStore';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

const EXCHANGE_STATUS = {
  notConnected: 0,
  connected: 1,
  requiresReconnect: 2,
} as const;

const EXCHANGE_TYPES = {
  kwork: 0,
  upwork: 1,
  fiverr: 2,
  freelancer: 3,
} as const;

const FUTURE_EXCHANGES = [
  { exchangeType: EXCHANGE_TYPES.upwork, providerKey: 'upwork', displayName: 'Upwork' },
  { exchangeType: EXCHANGE_TYPES.fiverr, providerKey: 'fiverr', displayName: 'Fiverr' },
  { exchangeType: EXCHANGE_TYPES.freelancer, providerKey: 'freelancer', displayName: 'Freelancer' },
];

const KworkSVG = () => (
  <svg viewBox="0 0 100 100" width="46" height="46" xmlns="http://www.w3.org/2000/svg">
    <polygon points="17,20 41,20 41,36 25,52 25,28 17,28" fill="#FF9900" />
    <polygon points="25,62 25,80 41,80 41,54 67,80 83,80 53,50 83,20 67,20" fill="#FFFFFF" />
  </svg>
);

function friendlyError(raw?: string | null): string | null {
  if (!raw) return null;
  const r = raw.toUpperCase();
  if (r.includes('KWORK_ACCESS_BLOCKED')) return 'Kwork заблокировал доступ. Зайдите в Kwork через браузер, пройдите проверку и переподключите.';
  if (r.includes('NOT_AUTHORIZED')) return 'Сессия устарела, переподключитесь.';
  if (r.includes('UNAUTHORIZED')) return 'Нет доступа. Переподключитесь.';
  if (r.includes('TIMEOUT') || r.includes('TIMED OUT')) return 'Нет ответа от биржи. Попробуйте позже.';
  if (r.includes('NETWORK') || r.includes('CONNECTION')) return 'Ошибка сети. Проверьте интернет.';
  if (r.includes('BOT') || r.includes('CAPTCHA') || r.includes('ANTI-BOT')) return 'Биржа запросила проверку. Войдите вручную и переподключите.';
  if (r.includes('SESSION')) return 'Сессия недействительна, переподключитесь.';
  if (raw.length > 60) return 'Ошибка соединения, переподключитесь.';
  return raw;
}

const flowText = (flow?: KworkLoginFlowStatus | null): { text: string; isError: boolean; isDone: boolean } | null => {
  if (!flow) return null;
  if (flow.isCompleted) return { text: 'Биржа подключена', isError: false, isDone: true };
  if (flow.isFailed) return { text: friendlyError(flow.error) || 'Не удалось подключить биржу', isError: true, isDone: false };
  if (flow.status === 'opening_browser') return { text: 'Открываю браузер...', isError: false, isDone: false };
  if (flow.status === 'waiting_for_login') return { text: 'Войдите в аккаунт биржи в открывшемся браузере', isError: false, isDone: false };
  return { text: 'Подключаю...', isError: false, isDone: false };
};

function getFallbackExchange(type: number) {
  return FUTURE_EXCHANGES.find(exchange => exchange.exchangeType === type);
}

function getExchangeIcon(exchange: Pick<ExchangeConnection, 'exchangeType' | 'displayName'>, enabled: boolean) {
  if (exchange.exchangeType === EXCHANGE_TYPES.kwork) {
    return <KworkSVG />;
  }

  return (
    <div
      className="flex shrink-0 items-center justify-center rounded-full"
      style={{ width: '46px', height: '46px', backgroundColor: enabled ? '#0D2B1E' : '#1F2937' }}
    >
      <span className="text-lg font-black" style={{ color: enabled ? '#6EE7B7' : '#64748B' }}>
        {exchange.displayName.charAt(0).toUpperCase()}
      </span>
    </div>
  );
}

export default function SearchExchangesModal({ isOpen, onClose }: Props) {
  const {
    exchanges,
    isLoading,
    fetchExchanges,
    startExchangeLogin,
    disconnectExchange,
    fetchKworkLoginStatus,
  } = useSearchRuntimeStore();
  const [flow, setFlow] = useState<KworkLoginFlowStatus | null>(null);
  const [flowExchangeType, setFlowExchangeType] = useState<number | null>(null);
  const [busyExchangeType, setBusyExchangeType] = useState<number | null>(null);

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

  const exchangeCards = useMemo(() => {
    const byType = new Map<number, ExchangeConnection>();
    exchanges.forEach(exchange => byType.set(exchange.exchangeType, exchange));

    const placeholders = FUTURE_EXCHANGES
      .filter(exchange => !byType.has(exchange.exchangeType))
      .map(exchange => ({
        id: '',
        profileId: '',
        exchangeType: exchange.exchangeType,
        providerKey: exchange.providerKey,
        displayName: exchange.displayName,
        status: EXCHANGE_STATUS.notConnected,
        isConnected: false,
        requiresReconnect: false,
        supportsBrowserLogin: false,
        supportsManualSession: false,
        isAvailable: false,
        lastCheckedAt: null,
        lastError: null,
        updatedAt: '',
      }));

    return [...exchanges, ...placeholders].sort((a, b) => a.exchangeType - b.exchangeType);
  }, [exchanges]);

  const currentFlow = flowText(flow);
  const isFlowPending = !!flow && !flow.isCompleted && !flow.isFailed;

  const handleConnect = async (exchange: ExchangeConnection) => {
    if (!exchange.supportsBrowserLogin || !exchange.isAvailable) return;
    setBusyExchangeType(exchange.exchangeType);
    setFlowExchangeType(exchange.exchangeType);
    try {
      const nextFlow = await startExchangeLogin(exchange.exchangeType);
      setFlow(nextFlow);
    } finally {
      setBusyExchangeType(null);
    }
  };

  const handleDisconnect = async (exchange: ExchangeConnection) => {
    setBusyExchangeType(exchange.exchangeType);
    try {
      await disconnectExchange(exchange.exchangeType);
      if (flowExchangeType === exchange.exchangeType) {
        setFlow(null);
        setFlowExchangeType(null);
      }
    } finally {
      setBusyExchangeType(null);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Биржи">
      <div className="relative flex flex-col gap-4 pb-2" style={{ marginTop: '5px' }}>
        {exchangeCards.map((exchange) => {
          const fallback = getFallbackExchange(exchange.exchangeType);
          const isAvailable = exchange.isAvailable && exchange.supportsBrowserLogin;
          const isConnected = exchange.status === EXCHANGE_STATUS.connected;
          const needsReconnect = exchange.status === EXCHANGE_STATUS.requiresReconnect;
          const isBusy = busyExchangeType === exchange.exchangeType || (isFlowPending && flowExchangeType === exchange.exchangeType);
          const errorText = friendlyError(exchange.lastError);
          const isPlaceholder = !!fallback && !exchange.isAvailable;

          return (
            <div
              key={exchange.exchangeType}
              className="rounded-xl border bg-black/20"
              style={{
                padding: '15px 20px',
                borderColor: isPlaceholder ? 'rgba(255,255,255,0.05)' : 'rgba(255,255,255,0.08)',
                opacity: isPlaceholder ? 0.45 : 1,
              }}
            >
              <div className="flex items-center gap-4">
                <div className="shrink-0">{getExchangeIcon(exchange, !isPlaceholder)}</div>

                <div className="min-w-0 flex-1">
                  <p className="text-[17px] font-extrabold leading-none text-white" style={{ marginBottom: '4px' }}>
                    {exchange.displayName || fallback?.displayName || 'Exchange'}
                  </p>
                  <div className="flex items-center gap-1.5" style={{ marginBottom: '4px' }}>
                    {isLoading ? (
                      <Loader2 className="h-4 w-4 animate-spin text-slate-500" />
                    ) : isConnected ? (
                      <Wifi className="h-4 w-4" style={{ color: '#4CC2FF' }} />
                    ) : (
                      <WifiOff className="h-4 w-4" style={{ color: needsReconnect ? '#F59E0B' : '#64748B' }} />
                    )}
                    <span
                      className="text-[13px] font-bold"
                      style={{ color: isLoading ? '#64748B' : isConnected ? '#4CC2FF' : needsReconnect ? '#F59E0B' : '#64748B' }}
                    >
                      {isPlaceholder ? 'Скоро' : isLoading ? 'Проверка...' : isConnected ? 'Подключено' : needsReconnect ? 'Нужно переподключить' : 'Не подключено'}
                    </span>
                  </div>
                  {exchange.lastCheckedAt && !isLoading && (
                    <p className="text-[11px] font-medium text-slate-500">
                      Последняя проверка {new Date(exchange.lastCheckedAt).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}
                    </p>
                  )}
                </div>

                <div
                  className="h-2 w-2 shrink-0 rounded-full"
                  style={{
                    backgroundColor: isConnected ? '#4CC2FF' : needsReconnect ? '#F59E0B' : '#1F2937',
                    boxShadow: isConnected ? '0 0 8px rgba(76,194,255,0.6)' : needsReconnect ? '0 0 8px rgba(245,158,11,0.6)' : 'none',
                  }}
                />
              </div>

              {errorText && !flow && (
                <div className="mb-3 mt-4 flex items-start gap-2 rounded-xl border border-red-500/15 bg-red-500/10 p-3">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-400" />
                  <p className="text-xs font-semibold leading-relaxed text-red-400">{errorText}</p>
                </div>
              )}

              <div className="flex flex-col gap-3" style={{ marginTop: '15px' }}>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    onClick={() => handleConnect(exchange)}
                    disabled={!isAvailable || isBusy}
                    className="flex items-center justify-center gap-2 rounded-xl text-[13px] font-bold text-white transition-all hover:brightness-110 active:scale-[0.98] disabled:opacity-50"
                    style={{ backgroundColor: needsReconnect ? '#F59E0B' : '#0EA5E9', height: '38px' }}
                  >
                    {isBusy && <Loader2 className="h-4 w-4 animate-spin" />}
                    {needsReconnect ? 'Сброс' : 'Подключить'}
                  </button>

                  <button
                    onClick={() => handleDisconnect(exchange)}
                    disabled={busyExchangeType === exchange.exchangeType || isFlowPending || !isConnected}
                    className="flex items-center justify-center gap-2 rounded-xl border border-white/[0.08] bg-white/[0.04] text-[13px] font-bold text-white transition-all hover:bg-white/[0.08] active:scale-[0.98] disabled:opacity-50"
                    style={{ height: '38px' }}
                  >
                    {busyExchangeType === exchange.exchangeType && <Loader2 className="h-4 w-4 animate-spin" />}
                    Отключить
                  </button>
                </div>

                {currentFlow && flowExchangeType === exchange.exchangeType && (
                  <div
                    className="mt-2 flex items-start gap-2 rounded-xl border p-3"
                    style={{
                      backgroundColor: currentFlow.isError ? 'rgba(239,68,68,0.08)' : currentFlow.isDone ? 'rgba(76,194,255,0.08)' : 'rgba(255,255,255,0.04)',
                      borderColor: currentFlow.isError ? 'rgba(239,68,68,0.15)' : currentFlow.isDone ? 'rgba(76,194,255,0.15)' : 'rgba(255,255,255,0.06)',
                    }}
                  >
                    {currentFlow.isError ? (
                      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-red-400" />
                    ) : currentFlow.isDone ? (
                      <CheckCircle className="mt-0.5 h-4 w-4 shrink-0 text-[#4CC2FF]" />
                    ) : (
                      <Loader2 className="mt-0.5 h-4 w-4 shrink-0 animate-spin text-slate-400" />
                    )}
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
          );
        })}
      </div>
    </Modal>
  );
}
