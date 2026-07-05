import { useEffect, useState } from 'react';
import Modal from './Modal';
import { useSearchRuntimeStore, type KworkLoginFlowStatus } from '../store/useSearchRuntimeStore';
import { CheckCircle, AlertCircle, Loader2, Wifi, WifiOff } from 'lucide-react';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

const KworkSVG = () => (
  <svg viewBox="0 0 34 34" width="34" height="34" xmlns="http://www.w3.org/2000/svg">
    <circle cx="17" cy="17" r="17" fill="#FF7B00" />
    <text
      x="17" y="23"
      textAnchor="middle"
      fill="white"
      fontSize="17"
      fontWeight="900"
      fontFamily="Arial Black, Arial, sans-serif"
      fontStyle="italic"
    >K</text>
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
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>

        {/* в”Ђв”Ђ Kwork в”Ђв”Ђ */}
        <div style={{
          borderRadius: 14,
          border: isConnected
            ? '1px solid rgba(16,185,129,0.2)'
            : needsReconnect
              ? '1px solid rgba(245,158,11,0.2)'
              : '1px solid rgba(255,255,255,0.07)',
          background: 'linear-gradient(160deg, rgba(20,30,48,0.98) 0%, rgba(12,18,32,0.98) 100%)',
          overflow: 'hidden',
        }}>

          {/* РЁР°РїРєР° */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px' }}>
            <div style={{ flexShrink: 0 }}><KworkSVG /></div>

            <div style={{ flex: 1, minWidth: 0 }}>
              <p style={{ color: 'white', fontSize: 14, fontWeight: 800, lineHeight: 1 }}>Kwork</p>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginTop: 4 }}>
                {isLoading
                  ? <Loader2 style={{ width: 11, height: 11, color: '#8A8A8A' }} className="animate-spin" />
                  : isConnected
                    ? <Wifi style={{ width: 11, height: 11, color: '#10B981' }} />
                    : <WifiOff style={{ width: 11, height: 11, color: needsReconnect ? '#F59E0B' : '#8A8A8A' }} />
                }
                <span style={{
                  fontSize: 11,
                  fontWeight: 600,
                  color: isLoading ? '#8A8A8A' : isConnected ? '#10B981' : needsReconnect ? '#F59E0B' : '#8A8A8A',
                }}>
                  {isLoading ? 'Проверка...' : isConnected ? 'Подключено' : needsReconnect ? 'Нужно переподключить' : 'Не подключено'}
                </span>
              </div>
              {kwork?.lastCheckedAt && !isLoading && (
                <p style={{ color: '#374151', fontSize: 10, marginTop: 2 }}>
                  Последняя проверка {new Date(kwork.lastCheckedAt).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}
                </p>
              )}
            </div>

            {/* РРЅРґРёРєР°С‚РѕСЂ */}
            <div style={{
              width: 7,
              height: 7,
              borderRadius: '50%',
              flexShrink: 0,
              backgroundColor: isConnected ? '#10B981' : needsReconnect ? '#F59E0B' : '#2A2A2A',
              boxShadow: isConnected ? '0 0 8px rgba(16,185,129,0.8)' : needsReconnect ? '0 0 8px rgba(245,158,11,0.6)' : 'none',
            }} />
          </div>

          {/* РћС€РёР±РєР° СЃ СЃРµСЂРІРµСЂР° (lastError) */}
          {errorText && !flow && (
            <div style={{
              marginLeft: 14, marginRight: 14, marginBottom: 8,
              borderRadius: 8,
              padding: '8px 10px',
              backgroundColor: 'rgba(239,68,68,0.08)',
              border: '1px solid rgba(239,68,68,0.15)',
              display: 'flex',
              alignItems: 'flex-start',
              gap: 7,
            }}>
              <AlertCircle style={{ width: 13, height: 13, color: '#F87171', flexShrink: 0, marginTop: 1 }} />
              <p style={{ fontSize: 12, lineHeight: 1.5, color: '#F87171' }}>{errorText}</p>
            </div>
          )}

          {/* Р Р°Р·РґРµР»РёС‚РµР»СЊ */}
          <div style={{ height: 1, backgroundColor: 'rgba(255,255,255,0.05)', marginLeft: 12, marginRight: 12 }} />

          {/* РљРЅРѕРїРєР° + С„Р»РѕСѓ */}
          <div style={{ padding: '8px 12px 10px', display: 'flex', flexDirection: 'column', gap: 6 }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              <button
                onClick={handleConnect}
                disabled={isStarting || isFlowPending}
                style={{
                  width: '100%',
                  borderRadius: 8,
                  paddingTop: 7,
                  paddingBottom: 7,
                  fontSize: 12,
                  fontWeight: 700,
                  color: 'white',
                  cursor: isStarting || isFlowPending ? 'default' : 'pointer',
                  opacity: isStarting || isFlowPending ? 0.6 : 1,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                  background: needsReconnect
                    ? 'linear-gradient(135deg, #D97706 0%, #B45309 100%)'
                    : 'linear-gradient(135deg, #0078D4 0%, #004A80 100%)',
                }}
              >
                {(isStarting || isFlowPending) && (
                  <Loader2 style={{ width: 12, height: 12 }} className="animate-spin" />
                )}
                {kwork?.requiresReconnect ? 'Переподключить' : 'Подключить'}
              </button>

              <button
                onClick={handleDisconnect}
                disabled={isDisconnecting || isFlowPending || !isConnected}
                style={{
                  width: '100%',
                  borderRadius: 8,
                  paddingTop: 7,
                  paddingBottom: 7,
                  fontSize: 12,
                  fontWeight: 700,
                  color: 'white',
                  cursor: isDisconnecting || isFlowPending || !isConnected ? 'default' : 'pointer',
                  opacity: isDisconnecting || isFlowPending || !isConnected ? 0.45 : 1,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: 6,
                  background: 'linear-gradient(135deg, #3A3A3A 0%, #2A2A2A 100%)',
                  border: '1px solid rgba(255,255,255,0.10)',
                }}
              >
                {isDisconnecting && (
                  <Loader2 style={{ width: 12, height: 12 }} className="animate-spin" />
                )}
                Отключить
              </button>
            </div>

            {/* РЎС‚Р°С‚СѓСЃ С„Р»РѕСѓ */}
            {currentFlow && (
              <div style={{
                borderRadius: 6,
                padding: '6px 8px',
                display: 'flex',
                alignItems: 'flex-start',
                gap: 6,
                backgroundColor: currentFlow.isError ? 'rgba(239,68,68,0.08)' : currentFlow.isDone ? 'rgba(16,185,129,0.08)' : 'rgba(255,255,255,0.04)',
                border: currentFlow.isError ? '1px solid rgba(239,68,68,0.15)' : currentFlow.isDone ? '1px solid rgba(16,185,129,0.15)' : '1px solid rgba(255,255,255,0.06)',
              }}>
                {currentFlow.isError
                  ? <AlertCircle style={{ width: 12, height: 12, color: '#F87171', flexShrink: 0, marginTop: 1 }} />
                  : currentFlow.isDone
                    ? <CheckCircle style={{ width: 12, height: 12, color: '#10B981', flexShrink: 0, marginTop: 1 }} />
                    : <Loader2 style={{ width: 12, height: 12, color: '#ADADAD', flexShrink: 0, marginTop: 1 }} className="animate-spin" />
                }
                <p style={{
                  fontSize: 11,
                  lineHeight: 1.4,
                  color: currentFlow.isError ? '#F87171' : currentFlow.isDone ? '#34D399' : '#ADADAD',
                }}>
                  {currentFlow.text}
                </p>
              </div>
            )}
          </div>
        </div>

        {/* в”Ђв”Ђ Upwork (СЃРєРѕСЂРѕ) в”Ђв”Ђ */}
        <div style={{
          borderRadius: 14,
          border: '1px solid rgba(255,255,255,0.05)',
          background: 'rgba(14,21,35,0.5)',
          padding: '8px 12px',
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          opacity: 0.4,
        }}>
          <div style={{
            width: 34,
            height: 34,
            borderRadius: '50%',
            backgroundColor: '#0D2B1E',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}>
            <span style={{ color: '#6EE7B7', fontSize: 15, fontWeight: 900, fontFamily: 'Arial Black, Arial, sans-serif' }}>U</span>
          </div>
          <div>
            <p style={{ color: 'white', fontSize: 13, fontWeight: 700 }}>Upwork</p>
            <p style={{ color: '#4B5563', fontSize: 11, marginTop: 1 }}>Скоро</p>
          </div>
        </div>

        {/* РџРѕРґСЃРєР°Р·РєР° */}
        <p style={{ color: '#374151', fontSize: 11, lineHeight: 1.6, textAlign: 'center' }}>
          Откроется браузер. Войдите в Kwork, приложение сохранит сессию.
        </p>
      </div>
    </Modal>
  );
}

