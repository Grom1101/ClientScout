import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Settings, ChevronRight, Play, Square, Loader2 } from 'lucide-react';
import MailingIntervalModal from '../components/MailingIntervalModal';
import MailingMessagesModal from '../components/MailingMessagesModal';
import { useOutreachStore } from '../store/useOutreachStore';
import { useSourcesStore } from '../store/useSourcesStore';
import { HARDCODED_PROFILE_ID } from '../api/client';

export default function MailingPage() {
  const navigate = useNavigate();
  const { activeCampaign, isLoading: isOutreachLoading, fetchActiveCampaign, fetchTemplates, startCampaign, stopCampaign, templates } = useOutreachStore();
  const { sources, fetchSources } = useSourcesStore();
  
  const [showInterval, setShowInterval] = useState(false);
  const [showMessages, setShowMessages] = useState(false);

  useEffect(() => {
    fetchActiveCampaign(HARDCODED_PROFILE_ID);
    fetchTemplates(HARDCODED_PROFILE_ID);
    fetchSources(1); // purpose=1 for Outreach
  }, [fetchActiveCampaign, fetchTemplates, fetchSources]);

  const selectedChats = sources.filter(s => s.checked);
  const isRunning = activeCampaign?.status === 1;

  const handleStart = async () => {
    if (templates.length === 0) {
      alert("Сначала создайте шаблон сообщения.");
      return;
    }
    if (selectedChats.length === 0) {
      alert("Выберите хотя бы один чат для рассылки.");
      return;
    }
    await startCampaign(templates[0].id, selectedChats.map(c => c.id));
  };

  return (
    <div className="px-4 pt-4 pb-4">
      {/* ── Header ── */}
      <div className="flex items-center justify-between mb-6 relative">
        {isOutreachLoading && (
          <div className="absolute -top-2 -left-2 z-10 bg-white/10 backdrop-blur-md p-1.5 rounded-full shadow-lg">
            <Loader2 className="w-5 h-5 animate-spin" style={{ color: '#A78BFA' }} />
          </div>
        )}
        <h1 className="text-2xl font-black text-transparent bg-clip-text bg-gradient-to-r from-white to-white/70 tracking-tight flex items-center gap-3">
          Рассылка
          {isRunning && (
            <span className="relative flex h-3 w-3">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-3 w-3 bg-emerald-500 shadow-[0_0_12px_#10B981]"></span>
            </span>
          )}
        </h1>
        <button
          onClick={() => setShowInterval(true)}
          className="w-11 h-11 rounded-2xl flex items-center justify-center cursor-pointer transition-all hover:bg-white/10 active:scale-95"
          style={{ 
            backgroundColor: 'rgba(255, 255, 255, 0.05)', 
            border: '1px solid rgba(255, 255, 255, 0.08)',
            backdropFilter: 'blur(12px)'
          }}
        >
          <Settings className="w-5 h-5" style={{ color: '#CBD5E1' }} />
        </button>
      </div>

      {/* ── Sections ── */}
      <div className="flex flex-col gap-3 mb-8">
        <button
          onClick={() => navigate('/mailing/chats')}
          className="flex items-center justify-between w-full p-4.5 rounded-2xl transition-all hover:bg-white/5 active:scale-[0.98]"
          style={{ 
            backgroundColor: 'rgba(255, 255, 255, 0.03)', 
            border: '1px solid rgba(255, 255, 255, 0.08)',
            backdropFilter: 'blur(12px)',
            boxShadow: '0 4px 24px -1px rgba(0,0,0,0.2)'
          }}
        >
          <div>
            <p className="text-[15px] font-bold text-white text-left tracking-wide">Чаты</p>
          </div>
          <div className="flex items-center gap-3">
            <span
              className="text-xs font-bold px-3 py-1 rounded-full shadow-[inset_0_1px_1px_rgba(255,255,255,0.2)]"
              style={{ backgroundColor: 'rgba(124,58,237,0.25)', color: '#A78BFA' }}
            >
              {selectedChats.length}
            </span>
            <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
          </div>
        </button>

        <button
          onClick={() => setShowMessages(true)}
          className="flex items-center justify-between w-full p-4.5 rounded-2xl transition-all hover:bg-white/5 active:scale-[0.98]"
          style={{ 
            backgroundColor: 'rgba(255, 255, 255, 0.03)', 
            border: '1px solid rgba(255, 255, 255, 0.08)',
            backdropFilter: 'blur(12px)',
            boxShadow: '0 4px 24px -1px rgba(0,0,0,0.2)'
          }}
        >
          <div>
            <p className="text-[15px] font-bold text-white text-left tracking-wide">Сообщения</p>
            <p className="text-[13px] text-left mt-0.5" style={{ color: '#94A3B8' }}>
              Текст сообщения + {templates[0]?.attachmentUrls?.length || 0} файл(ов)
            </p>
          </div>
          <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
        </button>
      </div>

      {/* ── Action buttons ── */}
      <div className="flex flex-col gap-3.5">
        <button
          onClick={handleStart}
          disabled={isRunning || isOutreachLoading}
          className="relative w-full py-4 rounded-2xl text-[15px] font-black text-white flex items-center justify-center gap-2 transition-all active:scale-[0.98] disabled:opacity-50 disabled:active:scale-100 overflow-hidden group"
          style={{ 
            background: 'linear-gradient(135deg, #059669 0%, #10B981 100%)',
            boxShadow: '0 8px 32px -8px rgba(16,185,129,0.5), inset 0 1px 1px rgba(255,255,255,0.2)'
          }}
        >
          <div className="absolute inset-0 bg-white/20 translate-y-full group-hover:translate-y-0 transition-transform duration-300 ease-out" />
          <Play className="w-5 h-5 relative z-10" fill="white" />
          <span className="relative z-10 tracking-wider">НАЧАТЬ РАССЫЛКУ</span>
        </button>

        <button
          onClick={() => stopCampaign()}
          disabled={!isRunning || isOutreachLoading}
          className="w-full py-4 rounded-2xl text-[15px] font-black text-white flex items-center justify-center gap-2 transition-all active:scale-[0.98] disabled:opacity-30 disabled:active:scale-100"
          style={{ 
            background: 'linear-gradient(135deg, #B91C1C 0%, #EF4444 100%)',
            boxShadow: isRunning ? '0 8px 32px -8px rgba(239,68,68,0.5), inset 0 1px 1px rgba(255,255,255,0.2)' : 'none'
          }}
        >
          <Square className="w-5 h-5" fill="white" />
          <span className="tracking-wider">СТОП</span>
        </button>
      </div>

      {/* ── Modals ── */}
      <MailingIntervalModal isOpen={showInterval} onClose={() => setShowInterval(false)} />
      <MailingMessagesModal isOpen={showMessages} onClose={() => setShowMessages(false)} />
    </div>
  );
}
