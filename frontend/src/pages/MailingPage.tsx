import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronRight, Loader2, Play, Square, Send, Check, X, RefreshCw } from 'lucide-react';
import MailingIntervalModal from '../components/MailingIntervalModal';
import MailingMessagesModal from '../components/MailingMessagesModal';
import { useOutreachStore } from '../store/useOutreachStore';
import { useSourcesStore } from '../store/useSourcesStore';
import { getActiveProfileId } from '../api/client';

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

const ChatNameLabel = ({ value }: { value: string }) => {
  const label = splitSourceTopic(value);
  if (!label) {
    return <p className="text-[15px] font-semibold text-slate-200 truncate mb-0.5">{value}</p>;
  }

  return (
    <p className="text-[15px] font-semibold truncate mb-0.5 inline-flex max-w-full items-center gap-1.5 text-slate-200">
      <span className="truncate">{label.source}</span>
      {label.topic && (
        <>
          <ChevronRight className="h-3.5 w-3.5 shrink-0 text-slate-500" strokeWidth={3} />
          <span className="truncate font-bold" style={{ color: '#60CDFF' }}>{label.topic}</span>
        </>
      )}
    </p>
  );
};

export default function MailingPage() {
  const navigate = useNavigate();
  const {
    activeCampaign,
    isLoading: isOutreachLoading,
    isStatsLoading,
    fetchActiveCampaign,
    fetchTemplates,
    startCampaign,
    stopCampaign,
    templates,
    periodicityMinutes,
    scheduleMode,
    scheduleStartTime,
    scheduleEndTime,
    stats,
    fetchStats
  } = useOutreachStore();
  const { sources, fetchSources } = useSourcesStore();

  const [showInterval, setShowInterval] = useState(false);
  const [showMessages, setShowMessages] = useState(false);

  useEffect(() => {
    const profileId = getActiveProfileId();
    fetchActiveCampaign(profileId);
    fetchTemplates(profileId);
    fetchSources(1);
    fetchStats(profileId, 'today');
    const timer = window.setInterval(() => fetchStats(profileId, 'today'), 15000);
    return () => window.clearInterval(timer);
  }, [fetchActiveCampaign, fetchTemplates, fetchSources, fetchStats]);

  const selectedChats = sources.filter((source) => source.checked);
  const isRunning = activeCampaign?.status === 1;
  const attachmentCount = templates[0]?.attachmentUrls?.length || 0;
  const messagePreviewText = templates[0]?.content
    ? templates[0].content.trim().slice(0, 40) + (templates[0].content.length > 40 ? '...' : '')
    : 'Текст сообщения';
  const intervalLabel = scheduleMode === 'custom'
    ? `${scheduleStartTime}-${scheduleEndTime}, каждые ${periodicityMinutes} мин`
    : `Круглосуточно, каждые ${periodicityMinutes} мин`;

  const handleStart = async () => {
    if (templates.length === 0) {
      alert('Сначала создайте шаблон сообщения.');
      return;
    }
    if (selectedChats.length === 0) {
      alert('Выберите хотя бы один чат для рассылки.');
      return;
    }
    await startCampaign(templates[0].id, selectedChats.map((chat) => chat.id));
  };

  const cardWidth = '100%';
  const actionWidth = 'min(380px, calc(100% - 64px))';

  return (
    <div className="min-h-full w-full px-5 pt-5 pb-6">
      <div className="relative mx-auto flex flex-col items-center" style={{ width: '100%', marginBottom: 8, paddingTop: 8 }}>
        {isOutreachLoading && (
          <div className="absolute left-0 top-1 z-10 rounded-full bg-white/10 p-1.5 shadow-lg backdrop-blur-md">
            <Loader2 className="h-5 w-5 animate-spin" style={{ color: '#4CC2FF' }} />
          </div>
        )}

        <h1 className="text-[22px] font-black leading-tight text-white">Рассылка</h1>
        
        {isRunning && (
          <div className="flex items-center justify-center gap-2.5" style={{ marginTop: '8px' }}>
            <span className="h-2.5 w-2.5 rounded-full bg-emerald-400 shadow-[0_0_12px_rgba(16,185,129,0.9)]" />
            <span className="text-[14px] font-black uppercase tracking-wide" style={{ color: '#34D399' }}>запущена</span>
          </div>
        )}
      </div>

      <div className="mx-auto flex flex-col gap-4" style={{ width: cardWidth }}>
        <button
          onClick={() => navigate('/mailing/chats')}
          className="mail-card mail-card-pressed flex min-h-[74px] w-full items-center justify-between py-4 text-left transition-all active:scale-[0.99]"
          style={{ borderRadius: 12, paddingLeft: '15px', paddingRight: '15px' }}
        >
          <div className="min-w-0">
            <p className="text-[16px] font-extrabold text-white">Чаты</p>
            <p className="mt-1 text-[13px] font-medium" style={{ color: '#7F8CA0' }}>
              Выбрано чатов для отправки
            </p>
          </div>
          <div className="flex items-center gap-3">
            <span
              className="min-w-8 rounded-lg px-2.5 py-1 text-center text-sm font-black"
              style={{ backgroundColor: 'rgba(0, 120, 212,0.18)', color: '#60CDFF' }}
            >
              {selectedChats.length}
            </span>
            <ChevronRight className="h-6 w-6" style={{ color: '#708096' }} />
          </div>
        </button>

        <button
          onClick={() => setShowMessages(true)}
          className="mail-card flex min-h-[74px] w-full items-center justify-between py-4 text-left transition-all active:scale-[0.99]"
          style={{ borderRadius: 12, paddingLeft: '15px', paddingRight: '15px' }}
        >
          <div className="min-w-0">
            <p className="text-[16px] font-extrabold text-white">Сообщения</p>
            <p className="mt-1 truncate text-[13px] font-medium" style={{ color: '#7F8CA0' }}>
              {messagePreviewText}{attachmentCount > 0 ? ` + ${attachmentCount} файл(ов)` : ''}
            </p>
          </div>
          <ChevronRight className="h-6 w-6 shrink-0" style={{ color: '#708096' }} />
        </button>

        <button
          onClick={() => setShowInterval(true)}
          className="mail-card flex min-h-[74px] w-full items-center justify-between py-4 text-left transition-all active:scale-[0.99]"
          style={{ borderRadius: 12, paddingLeft: '15px', paddingRight: '15px' }}
        >
          <div className="min-w-0">
            <p className="text-[16px] font-extrabold text-white">Настройки рассылки</p>
            <p className="mt-1 truncate text-[13px] font-medium" style={{ color: '#7F8CA0' }}>
              {intervalLabel}
            </p>
          </div>
          <ChevronRight className="h-6 w-6 shrink-0" style={{ color: '#708096' }} />
        </button>
      </div>

      <div className="flex w-full flex-col items-center gap-4" style={{ marginTop: 24 }}>
        {!isRunning ? (
          <button
            onClick={handleStart}
            disabled={isOutreachLoading}
            className="relative flex h-[58px] items-center justify-center gap-3 overflow-hidden rounded-[10px] text-[14px] font-black uppercase tracking-wide text-white transition-all disabled:opacity-50"
            style={{
              width: actionWidth,
              background: 'linear-gradient(135deg, #2CCB86 0%, #22B873 100%)',
              boxShadow: '0 14px 32px rgba(34,184,115,0.28)',
            }}
          >
            <Play className="relative z-10 h-5 w-5" fill="white" />
            <span className="relative z-10">Начать рассылку</span>
          </button>
        ) : (
          <button
            onClick={() => stopCampaign()}
            disabled={isOutreachLoading}
            className="flex h-[58px] items-center justify-center gap-3 rounded-[10px] text-[14px] font-black uppercase tracking-wide text-white transition-all disabled:opacity-35"
            style={{
              width: actionWidth,
              background: 'linear-gradient(135deg, #EF232A 0%, #D51F26 100%)',
              boxShadow: '0 14px 32px rgba(239,35,42,0.24)',
            }}
          >
            <Square className="h-5 w-5" fill="white" />
            <span>Остановить рассылку</span>
          </button>
        )}
      </div>

      <div className="mx-auto flex flex-col gap-3" style={{ width: cardWidth, marginTop: 20 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 2, paddingLeft: 12, paddingRight: 12 }}>
          <p style={{ fontSize: 13, fontWeight: 700, color: '#FFFFFF' }}>Последние рассылки:</p>
          <button
            onClick={() => fetchStats(getActiveProfileId(), 'today')}
            disabled={isStatsLoading}
            style={{ display: 'flex', alignItems: 'center', gap: 5, color: '#4CC2FF', fontSize: 12, fontWeight: 600, opacity: isStatsLoading ? 0.5 : 1 }}
          >
            <RefreshCw style={{ width: 13, height: 13 }} className={isStatsLoading ? 'animate-spin' : ''} />
            Обновить
          </button>
        </div>
        
        {(!stats?.recentLogs || stats.recentLogs.length === 0) && (
          <div className="flex flex-col items-center justify-center py-6 text-center rounded-2xl" style={{ backgroundColor: '#2B2B2B', border: '1px solid rgba(255,255,255,0.06)' }}>
            <div className="w-12 h-12 rounded-full mb-3 flex items-center justify-center" style={{ backgroundColor: 'rgba(255,255,255,0.03)' }}>
              <Send className="w-5 h-5 text-slate-600" />
            </div>
            <p className="text-[14px]" style={{ color: '#64748B' }}>Пока нет отправленных сообщений</p>
          </div>
        )}

        {stats?.recentLogs && stats.recentLogs.length > 0 && (
          <div className="flex flex-col gap-4 rounded-2xl py-5" style={{ backgroundColor: '#2B2B2B', border: '1px solid rgba(255,255,255,0.06)' }}>
            {stats.recentLogs.map((entry, index) => (
              <div key={entry.id} className="flex items-center gap-4 group" style={{ 
                paddingLeft: 20, 
                paddingRight: 20,
                paddingTop: index === 0 ? 20 : 0,
                paddingBottom: index === stats.recentLogs.length - 1 ? 20 : 0
              }}>
                <div className="w-11 h-11 rounded-2xl flex items-center justify-center shrink-0 transition-transform group-active:scale-95 shadow-sm" style={{ backgroundColor: entry.status === 0 ? 'rgba(0, 120, 212, 0.12)' : 'rgba(239, 68, 68, 0.1)', border: entry.status === 0 ? '1px solid rgba(0, 120, 212, 0.18)' : '1px solid rgba(239, 68, 68, 0.15)' }}>
                  <Send className="w-5 h-5" style={{ color: entry.status === 0 ? '#60CDFF' : '#F87171' }} />
                </div>
                <div className="flex-1 min-w-0">
                  <ChatNameLabel value={entry.chatName} />
                  <div className="flex items-center gap-1.5 text-[13px] truncate" style={{ color: '#94A3B8' }}>
                    <span className="truncate max-w-[100px]">{entry.messagePreview}</span>
                    <span className="w-1 h-1 rounded-full bg-slate-600 shrink-0"></span>
                    <span className="truncate" style={{ color: entry.errorMessage ? '#F87171' : '#94A3B8' }}>
                      {entry.errorMessage || entry.profileName}
                    </span>
                    {entry.matchedKeyword && (
                      <>
                        <span className="text-slate-600 shrink-0">→</span>
                        <span className="font-semibold px-2 py-0.5 rounded-md bg-sky-500/10 text-sky-300 shrink-0 border border-sky-500/20">{entry.matchedKeyword}</span>
                      </>
                    )}
                  </div>
                </div>
                <div className="text-[12px] font-medium shrink-0 flex flex-col items-end gap-1">
                  <span style={{ color: '#64748B' }}>{new Date(entry.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                  {entry.status === 0 && <Check className="w-3.5 h-3.5 text-emerald-500" />}
                  {entry.status !== 0 && <X className="w-3.5 h-3.5 text-red-500" />}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <MailingIntervalModal isOpen={showInterval} onClose={() => setShowInterval(false)} />
      <MailingMessagesModal isOpen={showMessages} onClose={() => setShowMessages(false)} />
    </div>
  );
}
