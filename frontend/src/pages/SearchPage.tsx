import { useEffect, useState, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronRight, RefreshCw, Play, Square, Loader2 } from 'lucide-react';
import SearchSettingsModal from '../components/SearchSettingsModal';
import SearchExchangesModal from '../components/SearchExchangesModal';
import OrderDetailModal from '../components/OrderDetailModal';
import SwipeableItem from '../components/SwipeableItem';
import { useLeadsStore } from '../store/useLeadsStore';
import { getActiveProfileId } from '../api/client';
import { useSearchSettingsStore } from '../store/useSearchSettingsStore';
import { useSourcesStore } from '../store/useSourcesStore';
import { useSearchRuntimeStore } from '../store/useSearchRuntimeStore';

// Kwork logo — dark-theme mark: rounded square with the brand "k" glyph.
const KworkIcon = () => (
  <svg viewBox="0 0 44 44" width="44" height="44" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <defs>
      <linearGradient id="kworkBg" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#333333" />
        <stop offset="1" stopColor="#252525" />
      </linearGradient>
      <linearGradient id="kworkK" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stopColor="#FF8A1E" />
        <stop offset="1" stopColor="#FF6A00" />
      </linearGradient>
    </defs>
    <rect x="1" y="1" width="42" height="42" rx="12" fill="url(#kworkBg)" stroke="rgba(255,255,255,0.10)" />
    <path
      d="M15.5 12.5v19M15.5 22.2l9-9.7M15.9 21.7l9.6 9.8"
      stroke="url(#kworkK)"
      strokeWidth="3.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      fill="none"
    />
    <circle cx="30.5" cy="14.2" r="2.3" fill="#FF6A00" />
  </svg>
);

// Telegram logo — dark-theme mark: paper plane in a subtle dark disc.
const TelegramIcon = () => (
  <svg viewBox="0 0 44 44" width="44" height="44" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <defs>
      <linearGradient id="tgBg" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#333333" />
        <stop offset="1" stopColor="#252525" />
      </linearGradient>
      <linearGradient id="tgPlane" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stopColor="#4CC2FF" />
        <stop offset="1" stopColor="#2AABEE" />
      </linearGradient>
    </defs>
    <rect x="1" y="1" width="42" height="42" rx="12" fill="url(#tgBg)" stroke="rgba(255,255,255,0.10)" />
    <path
      d="M31.9 13.3 28.6 30c-.2 1-.9 1.3-1.8.8l-5-3.7-2.4 2.3c-.3.3-.5.5-1 .5l.4-5 9.2-8.3c.4-.4-.1-.6-.6-.2l-11.3 7.1-4.9-1.5c-1-.3-1-1 .2-1.5l19.1-7.4c.9-.3 1.6.2 1.3 1.6Z"
      fill="url(#tgPlane)"
    />
  </svg>
);

const openLeadLink = (link: string) => {
  const webApp = (window as any).Telegram?.WebApp;
  if (webApp && /^https:\/\/t\.me\//i.test(link) && typeof webApp.openTelegramLink === 'function') {
    webApp.openTelegramLink(link);
    return;
  }

  if (webApp && typeof webApp.openLink === 'function') {
    webApp.openLink(link);
    return;
  }

  window.open(link, '_blank', 'noopener,noreferrer');
};

const splitSourceTopic = (value?: string) => {
  if (!value) return null;
  const parts = value
    .split(/\s*(?:›|>|->|→|вЂє)\s*/)
    .map((part) => part.trim())
    .filter(Boolean);

  if (parts.length < 2) {
    return { source: value, topic: null };
  }

  return { source: parts[0], topic: parts.slice(1).join(' › ') };
};

const SourceLabel = ({ order }: { order: { chatName?: string; source: string; sourceColor: string } }) => {
  const fallback = order.source === 'telegram' ? 'Telegram-чат' : order.source === 'kwork' ? 'Kwork' : order.source;
  const label = splitSourceTopic(order.chatName || fallback);

  if (!label) return null;

  return (
    <span style={{ color: order.sourceColor, fontSize: 12, fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', display: 'inline-flex', alignItems: 'center', gap: 5, minWidth: 0 }}>
      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{label.source}</span>
      {label.topic && (
        <>
          <ChevronRight style={{ width: 14, height: 14, flexShrink: 0, strokeWidth: 3 }} />
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', color: '#60CDFF', fontWeight: 700 }}>{label.topic}</span>
        </>
      )}
    </span>
  );
};

export default function SearchPage() {
  const navigate = useNavigate();
  const { leads, totalCount, isLoading, fetchLeads, viewLead, hideLead } = useLeadsStore();
  const { settings, fetchSettings, setEnabled } = useSearchSettingsStore();
  const { sources: searchSources, fetchSources } = useSourcesStore();
  const { exchanges, fetchExchanges } = useSearchRuntimeStore();

  const [showSettings, setShowSettings] = useState(false);
  const [showExchanges, setShowExchanges] = useState(false);
  const [showOrder, setShowOrder] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [isToggling, setIsToggling] = useState(false);
  
  const previousNeedsAiExpansionRef = useRef<boolean | undefined>(settings?.needsAiExpansion);
  const [showAiToast, setShowAiToast] = useState(false);

  useEffect(() => {
    const currentNeedsAi = settings?.needsAiExpansion;
    if (previousNeedsAiExpansionRef.current === true && currentNeedsAi === false) {
      setShowAiToast(true);
      const t = setTimeout(() => setShowAiToast(false), 3000);
      return () => clearTimeout(t);
    }
    previousNeedsAiExpansionRef.current = currentNeedsAi;
  }, [settings?.needsAiExpansion]);

  useEffect(() => {
    const profileId = getActiveProfileId();
    fetchLeads(profileId);
    fetchSettings(profileId);
    fetchSources(0);
    fetchExchanges(profileId);

    const timer = window.setInterval(() => {
      fetchLeads(profileId);
      fetchSettings(profileId);
      fetchSources(0);
      fetchExchanges(profileId);
    }, 15000);

    return () => window.clearInterval(timer);
  }, [fetchLeads, fetchSettings, fetchSources, fetchExchanges]);

  const handleSearchToggle = async (enabled: boolean) => {
    if (isToggling) return;
    if (enabled && settings?.needsAiExpansion) {
      alert('Дождитесь завершения генерации AI-профиля перед запуском поиска.');
      return;
    }

    setIsToggling(true);
    if (enabled) {
      const hasKeywords = (settings?.userKeywords ?? []).length > 0;
      const hasSearchChat = searchSources.some((source) => source.checked);
      const hasExchange = exchanges.some((exchange) => exchange.isConnected && !exchange.requiresReconnect);

      if (!hasKeywords) {
        alert('\u0414\u043e\u0431\u0430\u0432\u044c\u0442\u0435 \u0445\u043e\u0442\u044f \u0431\u044b \u043e\u0434\u043d\u043e \u043a\u043b\u044e\u0447\u0435\u0432\u043e\u0435 \u0441\u043b\u043e\u0432\u043e \u0432 \u043d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0430\u0445 \u043f\u043e\u0438\u0441\u043a\u0430.');
        setShowSettings(true);
        setIsToggling(false);
        return;
      }

      if (!hasSearchChat && !hasExchange) {
        alert('\u0414\u043b\u044f \u0437\u0430\u043f\u0443\u0441\u043a\u0430 \u043f\u043e\u0438\u0441\u043a\u0430 \u0432\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0445\u043e\u0442\u044f \u0431\u044b \u043e\u0434\u0438\u043d \u0447\u0430\u0442 \u0438\u043b\u0438 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0438\u0442\u0435 \u0431\u0438\u0440\u0436\u0443.');
        setIsToggling(false);
        return;
      }
    }

    try {
      await setEnabled(getActiveProfileId(), enabled);
    } catch (err: any) {
      const message = err?.response?.data?.message;
      if (message === 'SEARCH_KEYWORDS_REQUIRED') {
        alert('\u0414\u043e\u0431\u0430\u0432\u044c\u0442\u0435 \u0445\u043e\u0442\u044f \u0431\u044b \u043e\u0434\u043d\u043e \u043a\u043b\u044e\u0447\u0435\u0432\u043e\u0435 \u0441\u043b\u043e\u0432\u043e \u0432 \u043d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0430\u0445 \u043f\u043e\u0438\u0441\u043a\u0430.');
        setShowSettings(true);
        return;
      }

      if (message === 'SEARCH_SOURCE_REQUIRED') {
        alert('\u0414\u043b\u044f \u0437\u0430\u043f\u0443\u0441\u043a\u0430 \u043f\u043e\u0438\u0441\u043a\u0430 \u0432\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0445\u043e\u0442\u044f \u0431\u044b \u043e\u0434\u0438\u043d \u0447\u0430\u0442 \u0438\u043b\u0438 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0438\u0442\u0435 \u0431\u0438\u0440\u0436\u0443.');
        return;
      }

      throw err;
    } finally {
      setIsToggling(false);
    }
  };

  const openOrder = async (id: string, link?: string) => {
    await viewLead(id);
    if (link) {
      openLeadLink(link);
      return;
    }

    setSelectedOrderId(id);
    setShowOrder(true);
  };

  const actionWidth = 'min(380px, calc(100% - 64px))';
  const isSearching = settings?.isEnabled ?? false;
  const selectedSearchChats = searchSources.filter((source) => source.checked);
  const settingsLabel = settings
    ? `${settings.intervalMinutes} мин · ${settings.userKeywords.length} ключ. · ${settings.negativeKeywords.length} стоп · ${settings.notificationsEnabled ? 'уведомления вкл.' : 'уведомления выкл.'}`
    : 'Интервал, ключевые слова, уведомления';

  return (
    <div className="min-h-full w-full px-5 pt-5 pb-6">
      <div className="relative mx-auto flex flex-col items-center" style={{ width: '100%', marginBottom: 8, paddingTop: 8 }}>
        <h1 className="text-[22px] font-black leading-tight text-white">Поиск заказов</h1>

        {isSearching && (
          <div className="flex items-center justify-center gap-2.5" style={{ marginTop: 8 }}>
            <span className="h-2.5 w-2.5 rounded-full bg-emerald-400 shadow-[0_0_12px_rgba(16,185,129,0.9)]" />
            <span className="text-[14px] font-black uppercase tracking-wide" style={{ color: '#34D399' }}>
              запущен
            </span>
          </div>
        )}

        {settings?.needsAiExpansion && (
          <div className="flex items-center justify-center gap-2 px-3 py-1.5 rounded-full bg-sky-500/10 border border-sky-500/20" style={{ marginTop: 8 }}>
            <Loader2 className="h-3.5 w-3.5 animate-spin text-sky-400" />
            <span className="text-xs font-semibold text-sky-400">Генерация AI-профиля...</span>
          </div>
        )}
      </div>

      {showAiToast && (
        <div className="absolute top-2 left-1/2 z-50 flex -translate-x-1/2 items-center justify-center gap-2 rounded-full px-4 py-2 shadow-[0_4px_12px_rgba(16,185,129,0.4)]" style={{ backgroundColor: '#10B981', color: '#fff' }}>
          <span className="text-sm font-bold">✨ Теневой AI-профиль создан</span>
        </div>
      )}

      <div className="mx-auto flex w-full flex-col gap-4">
        <button
          onClick={() => navigate('/search/chats')}
          className="mail-card mail-card-pressed flex min-h-[74px] w-full items-center justify-between py-4 text-left transition-all active:scale-[0.99]"
          style={{ borderRadius: 12, paddingLeft: '15px', paddingRight: '15px' }}
        >
          <div className="min-w-0">
            <p className="text-[16px] font-extrabold text-white">Чаты</p>
            <p className="mt-1 text-[13px] font-medium" style={{ color: '#9A9A9A' }}>
              Выбрано чатов для отправки, read-only чаты разрешены
            </p>
          </div>
          <div className="flex items-center gap-3">
            <span
              className="min-w-8 rounded-lg px-2.5 py-1 text-center text-sm font-black"
              style={{ backgroundColor: 'rgba(0, 120, 212,0.18)', color: '#60CDFF' }}
            >
              {selectedSearchChats.length}
            </span>
            <ChevronRight className="h-6 w-6" style={{ color: '#8A8A8A' }} />
          </div>
        </button>

        <button
          onClick={() => setShowSettings(true)}
          className="mail-card flex min-h-[74px] w-full items-center justify-between py-4 text-left transition-all active:scale-[0.99]"
          style={{ borderRadius: 12, paddingLeft: '15px', paddingRight: '15px' }}
        >
          <div className="min-w-0">
            <p className="text-[16px] font-extrabold text-white">Настройки поиска</p>
            <p className="mt-1 truncate text-[13px] font-medium" style={{ color: '#9A9A9A' }}>
              {settingsLabel}
            </p>
          </div>
          <ChevronRight className="h-6 w-6 shrink-0" style={{ color: '#8A8A8A' }} />
        </button>

        <button
          onClick={() => setShowExchanges(true)}
          className="mail-card flex min-h-[74px] w-full items-center justify-between py-4 text-left transition-all active:scale-[0.99]"
          style={{ borderRadius: 12, paddingLeft: '15px', paddingRight: '15px' }}
        >
          <div className="min-w-0">
            <p className="text-[16px] font-extrabold text-white">Биржи</p>
            <p className="mt-1 truncate text-[13px] font-medium" style={{ color: '#9A9A9A' }}>
              Kwork и другие источники позже
            </p>
          </div>
          <ChevronRight className="h-6 w-6 shrink-0" style={{ color: '#8A8A8A' }} />
        </button>
      </div>

      <div className="flex w-full flex-col items-center gap-4" style={{ marginTop: 24, marginBottom: 24 }}>
        {!isSearching ? (
          <button
            onClick={() => handleSearchToggle(true)}
            disabled={isToggling || settings?.needsAiExpansion}
            className="fluent-accent-btn relative flex h-[60px] items-center justify-center gap-3 overflow-hidden rounded-[14px] text-[14px] font-bold uppercase tracking-wide text-white disabled:opacity-50"
            style={{
              width: actionWidth,
              background: 'linear-gradient(180deg, #2FCB88 0%, #16A968 100%)',
              boxShadow: '0 10px 26px rgba(22,169,104,0.34), inset 0 0 0 1px rgba(255,255,255,0.12)',
            }}
          >
            <span className="relative z-10 flex h-7 w-7 items-center justify-center rounded-full bg-white/20">
              {isToggling ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" fill="white" />}
            </span>
            <span className="relative z-10">Запустить поиск</span>
          </button>
        ) : (
          <button
            onClick={() => handleSearchToggle(false)}
            disabled={isToggling}
            className="fluent-accent-btn relative flex h-[60px] items-center justify-center gap-3 overflow-hidden rounded-[14px] text-[14px] font-bold uppercase tracking-wide text-white disabled:opacity-35"
            style={{
              width: actionWidth,
              background: 'linear-gradient(180deg, #F04A44 0%, #C42B26 100%)',
              boxShadow: '0 10px 26px rgba(196,43,38,0.30), inset 0 0 0 1px rgba(255,255,255,0.12)',
            }}
          >
            <span className="relative z-10 flex h-7 w-7 items-center justify-center rounded-full bg-white/20">
              <Square className="h-3.5 w-3.5" fill="white" />
            </span>
            <span className="relative z-10">Остановить поиск</span>
          </button>
        )}
      </div>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10, marginTop: 4, paddingLeft: 12, paddingRight: 12 }}>
        <p style={{ fontSize: 13, fontWeight: 700, color: '#FFFFFF' }}>
          Найденные заказы:
        </p>
        <button
          onClick={() => fetchLeads(getActiveProfileId())}
          disabled={isLoading}
          style={{ display: 'flex', alignItems: 'center', gap: 5, color: '#4CC2FF', fontSize: 12, fontWeight: 600, opacity: isLoading ? 0.5 : 1 }}
        >
          <RefreshCw style={{ width: 13, height: 13 }} className={isLoading ? 'animate-spin' : ''} />
          Обновить
        </button>
      </div>

      <div className="relative flex flex-col gap-3">
        {!isLoading && leads.length === 0 && (
          <div className="rounded-2xl p-4 text-sm" style={{ backgroundColor: '#2B2B2B', color: '#9A9A9A', border: '1px solid rgba(255,255,255,0.06)' }}>
            Пока нет найденных заказов.
          </div>
        )}

        {leads.map((order) => (
          <SwipeableItem key={order.id} onDelete={() => hideLead(order.id)}>
            <div
              style={{
                backgroundColor: '#2B2B2B',
                border: '1px solid rgba(255,255,255,0.06)',
                borderRadius: 16,
                paddingTop: 12,
                paddingBottom: 12,
                paddingLeft: 12,
                paddingRight: 12,
              }}
            >
              {/* Р’РµСЂС��РЅСЏСЏ СЃС‚СЂРѕРєР°: РёРєРѕРЅРєР° + РєРѕРЅС‚РµРЅС‚ */}
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>

                {/* РРєРѕРЅРєР° РёСЃС‚РѕС‡РЅРёРєР° */}
                <div
                  style={{
                    width: 44,
                    height: 44,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    flexShrink: 0,
                    marginTop: 2,
                  }}
                >
                  {order.source === 'kwork' ? <KworkIcon /> : <TelegramIcon />}
                </div>

                {/* РўРµРєСЃС‚РѕРІС‹Р№ Р±Р»РѕРє */}
                <div style={{ flex: 1, minWidth: 0 }}>

                  {/* РСЃС‚РѕС‡РЅРёРє + РІСЂРµРјСЏ */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginBottom: 5 }}>
                    <SourceLabel order={order} />
                    <span style={{ color: '#8A8A8A', fontSize: 12, flexShrink: 0 }}>{order.timeAgo}</span>
                  </div>

                  {/* Р—Р°РіРѕР»РѕРІРѕРє + Р±РµР№РґР¶ NEW */}
                  <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: 6, marginBottom: 6 }}>
                    <p style={{ color: 'white', fontSize: 14, fontWeight: 700, lineHeight: 1.3, margin: 0 }}>{order.title}</p>
                    {order.status === 0 && (
                      <span style={{
                        color: '#10B981',
                        backgroundColor: 'rgba(16,185,129,0.12)',
                        borderRadius: 20,
                        paddingTop: 2,
                        paddingBottom: 2,
                        paddingLeft: 8,
                        paddingRight: 8,
                        fontSize: 9,
                        fontWeight: 800,
                        textTransform: 'uppercase',
                        letterSpacing: '0.05em',
                      }}>
                        New
                      </span>
                    )}
                  </div>

                  {/* РћРїРёСЃР°РЅРёРµ */}
                  <p style={{
                    color: '#ADADAD',
                    fontSize: 13,
                    lineHeight: '18px',
                    marginBottom: 6,
                    display: '-webkit-box',
                    WebkitLineClamp: 2,
                    WebkitBoxOrient: 'vertical',
                    overflow: 'hidden',
                    wordBreak: 'break-word',
                  }}>
                    {order.description}
                  </p>

                  {/* AI РјРµС‚РєР° + СЃСЂРѕРє + РєРЅРѕРїРєР° РґРµР№СЃС‚РІРёСЏ РІ РѕРґРЅРѕР№ СЃС‚СЂРѕРєРµ */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginTop: 4 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: '#8A8A8A' }}>
                      {order.aiLabel && <span>{order.aiLabel}</span>}
                      {order.expiresIn && <span style={{ color: '#EF4444', fontWeight: 700 }}>-{order.expiresIn}</span>}
                    </div>
                    <button
                      onClick={() => openOrder(order.id, order.link)}
                      style={{
                        backgroundColor: '#0078D4',
                        color: 'white',
                        borderRadius: 10,
                        paddingTop: 6,
                        paddingBottom: 6,
                        paddingLeft: 16,
                        paddingRight: 16,
                        fontSize: 12,
                        fontWeight: 700,
                        cursor: 'pointer',
                        flexShrink: 0,
                      }}
                    >
                      {order.source === 'telegram' ? 'Написать' : 'Открыть'}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </SwipeableItem>
        ))}
      </div>

      {totalCount > leads.length && (
        <button
          onClick={() => navigate('/search/leads')}
          className="mail-card flex min-h-[64px] w-full items-center justify-between text-left transition-all active:scale-[0.99]"
          style={{
            borderRadius: 12,
            paddingLeft: 15,
            paddingRight: 15,
            marginTop: 12,
            marginBottom: 12,
            border: '1px solid rgba(0, 120, 212,0.28)',
            background: 'linear-gradient(180deg, rgba(40,40,40,0.97), rgba(28,28,28,0.97))',
          }}
        >
          <span style={{ color: '#9ECBFF', fontSize: 15, fontWeight: 700 }}>Показать все</span>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ color: '#4CC2FF', fontSize: 13, fontWeight: 600 }}>{totalCount} заказов</span>
            <ChevronRight style={{ color: '#4CC2FF', width: 20, height: 20 }} />
          </div>
        </button>
      )}

      <SearchSettingsModal isOpen={showSettings} onClose={() => setShowSettings(false)} />
      <SearchExchangesModal isOpen={showExchanges} onClose={() => setShowExchanges(false)} />
      <OrderDetailModal isOpen={showOrder} onClose={() => setShowOrder(false)} orderId={selectedOrderId} />
    </div>
  );
}
