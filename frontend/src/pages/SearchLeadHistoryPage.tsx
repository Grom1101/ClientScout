import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import { getActiveProfileId } from '../api/client';
import { useLeadsStore } from '../store/useLeadsStore';
import SwipeableItem from '../components/SwipeableItem';
import SubPage from '../components/SubPage';

// Modern Kwork SVG logo
const KworkIcon = () => (
  <svg viewBox="0 0 100 100" width="44" height="44" xmlns="http://www.w3.org/2000/svg">
    <polygon points="17,20 41,20 41,36 25,52 25,28 17,28" fill="#FF9900" />
    <polygon points="25,62 25,80 41,80 41,54 67,80 83,80 53,50 83,20 67,20" fill="#FFFFFF" />
  </svg>
);

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

export default function SearchLeadHistoryPage() {
  const navigate = useNavigate();
  const { history, isHistoryLoading, fetchHistory, viewLead, hideLead } = useLeadsStore();
  const [page, setPage] = useState(0);
  const [aiFilter, setAiFilter] = useState<'confirmed' | 'unverified'>('confirmed');
  const pageSize = 30;

  useEffect(() => {
    fetchHistory(getActiveProfileId(), pageSize, page * pageSize, aiFilter);
  }, [fetchHistory, page, aiFilter]);

  const switchFilter = (value: 'confirmed' | 'unverified') => {
    setAiFilter(value);
    setPage(0);
  };

  const openLead = async (id: string, link?: string) => {
    await viewLead(id);
    if (link) {
      openLeadLink(link);
    }
  };

  return (
    <SubPage title="История заказов" backTo="/search" hideDivider={true}>
      <div className="px-5 pt-4 pb-6">

      {/* Р¤РёР»СЊС‚СЂ AI вЂ” РІ СЃС‚РёР»Рµ mail-card */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 16, paddingLeft: 12, paddingRight: 12 }}>
        <button
          onClick={() => switchFilter('confirmed')}
          className="mail-card"
          style={{
            flex: 1,
            borderRadius: 12,
            paddingTop: 11,
            paddingBottom: 11,
            paddingLeft: 12,
            paddingRight: 12,
            fontSize: 13,
            fontWeight: 700,
            cursor: 'pointer',
            color: aiFilter === 'confirmed' ? '#F8FAFC' : '#64748B',
            border: aiFilter === 'confirmed'
              ? '1px solid rgba(14, 165, 233,0.5)'
              : '1px solid rgba(255,255,255,0.06)',
            backgroundColor: aiFilter === 'confirmed' ? '#1E293B' : 'transparent',
          }}
        >
          Проверено нейросетью
        </button>
        <button
          onClick={() => switchFilter('unverified')}
          className="mail-card"
          style={{
            flex: 1,
            borderRadius: 12,
            paddingTop: 11,
            paddingBottom: 11,
            paddingLeft: 12,
            paddingRight: 12,
            fontSize: 13,
            fontWeight: 700,
            cursor: 'pointer',
            color: aiFilter === 'unverified' ? '#F8FAFC' : '#64748B',
            border: aiFilter === 'unverified'
              ? '1px solid rgba(14, 165, 233,0.5)'
              : '1px solid rgba(255,255,255,0.06)',
            backgroundColor: aiFilter === 'unverified' ? '#1E293B' : 'transparent',
          }}
        >
          Не проверено нейросетью
        </button>
      </div>

      {/* Р—Р°РіСЂСѓР·РєР° */}
      {isHistoryLoading && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '40px 0' }}>
          <Loader2 style={{ width: 32, height: 32, color: '#0EA5E9' }} className="animate-spin" />
        </div>
      )}

      {/* РџСѓСЃС‚Рѕ */}
      {!isHistoryLoading && history.length === 0 && (
        <div style={{ backgroundColor: '#2B2B2B', border: '1px solid rgba(255,255,255,0.06)', borderRadius: 16, padding: 16, color: '#7F8CA0', fontSize: 14 }}>
          История пока пустая.
        </div>
      )}

      {/* РЎРїРёСЃРѕРє РєР°СЂС‚РѕС‡РµРє */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {history.map((order) => (
          <SwipeableItem key={order.id} onDelete={() => hideLead(order.id)} className="overflow-hidden rounded-[16px]">
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
              {/* РРєРѕРЅРєР° + РєРѕРЅС‚РµРЅС‚ */}
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>

                {/* РРєРѕРЅРєР° РёСЃС‚РѕС‡РЅРёРєР° */}
                <div
                  style={{
                    width: 44,
                    height: 44,
                    borderRadius: '50%',
                    backgroundColor: order.source === 'kwork' ? 'transparent' : `${order.sourceColor}18`,
                    color: order.sourceColor,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    flexShrink: 0,
                    overflow: 'hidden',
                    marginTop: 2,
                  }}
                >
                  {order.source === 'kwork' ? (
                    <KworkIcon />
                  ) : (
                    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <line x1="22" y1="2" x2="11" y2="13" />
                      <polygon points="22 2 15 22 11 13 2 9 22 2" />
                    </svg>
                  )}
                </div>

                {/* РўРµРєСЃС‚РѕРІС‹Р№ Р±Р»РѕРє */}
                <div style={{ flex: 1, minWidth: 0 }}>
                  {/* РСЃС‚РѕС‡РЅРёРє + РІСЂРµРјСЏ */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginBottom: 5 }}>
                    <SourceLabel order={order} />
                    <span style={{ color: '#64748B', fontSize: 12, flexShrink: 0 }}>{order.timeAgo}</span>
                  </div>

                  {/* Р—Р°РіРѕР»РѕРІРѕРє + NEW */}
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
                      }}>New</span>
                    )}
                  </div>

                  {/* РћРїРёСЃР°РЅРёРµ */}
                  <p style={{
                    color: '#94A3B8',
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

                  {/* РќРёР¶РЅСЏСЏ СЃС‚СЂРѕРєР°: РјРµС‚РєРё + РєРЅРѕРїРєР° */}
                  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, marginTop: 4 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, color: '#64748B' }}>
                      {order.aiLabel && <span>{order.aiLabel}</span>}
                      {order.expiresIn && <span style={{ color: '#EF4444', fontWeight: 700 }}>-{order.expiresIn}</span>}
                    </div>
                    <button
                      onClick={() => openLead(order.id, order.link)}
                      style={{
                        backgroundColor: '#0EA5E9',
                        color: 'white',
                        borderRadius: 8,
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

      {/* РџР°РіРёРЅР°С†РёСЏ */}
      {(page > 0 || history.length >= pageSize) && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, marginTop: 16, paddingLeft: 12, paddingRight: 12, paddingBottom: 12 }}>
          <button
            onClick={() => setPage(v => Math.max(0, v - 1))}
            disabled={page === 0 || isHistoryLoading}
            className="mail-card"
            style={{
              flex: 1,
              borderRadius: 12,
              paddingTop: 12,
              paddingBottom: 12,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              fontSize: 13,
              fontWeight: 700,
              color: page === 0 ? '#374151' : '#60CDFF',
              cursor: page === 0 ? 'default' : 'pointer',
              opacity: page === 0 || isHistoryLoading ? 0.4 : 1,
              border: '1px solid rgba(255,255,255,0.06)',
            }}
          >
            <ChevronLeft style={{ width: 16, height: 16 }} />
            Назад
          </button>

          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            minWidth: 44,
            height: 44,
            borderRadius: 12,
            backgroundColor: 'rgba(14, 165, 233,0.12)',
            border: '1px solid rgba(14, 165, 233,0.2)',
          }}>
            <span style={{ color: '#60CDFF', fontSize: 14, fontWeight: 800 }}>{page + 1}</span>
          </div>

          <button
            onClick={() => setPage(v => v + 1)}
            disabled={history.length < pageSize || isHistoryLoading}
            className="mail-card"
            style={{
              flex: 1,
              borderRadius: 12,
              paddingTop: 12,
              paddingBottom: 12,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 6,
              fontSize: 13,
              fontWeight: 700,
              color: history.length < pageSize ? '#374151' : '#60CDFF',
              cursor: history.length < pageSize ? 'default' : 'pointer',
              opacity: history.length < pageSize || isHistoryLoading ? 0.4 : 1,
              border: '1px solid rgba(255,255,255,0.06)',
            }}
          >
            Далее
            <ChevronRight style={{ width: 16, height: 16 }} />
          </button>
        </div>
      )}
      </div>
    </SubPage>
  );
}
