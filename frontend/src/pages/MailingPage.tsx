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
      <div className="flex items-center justify-between mb-5 relative">
        {isOutreachLoading && (
          <div className="absolute -top-2 -left-2 z-10 bg-[#0B0E18]/50 p-1 rounded-full">
            <Loader2 className="w-5 h-5 animate-spin" style={{ color: '#7C3AED' }} />
          </div>
        )}
        <h1 className="text-xl font-bold text-white flex items-center gap-2">
          Рассылка
          {isRunning && <span className="w-2 h-2 rounded-full bg-green-500 shadow-[0_0_8px_#10B981] animate-pulse" />}
        </h1>
        <button
          onClick={() => setShowInterval(true)}
          className="w-10 h-10 rounded-full flex items-center justify-center cursor-pointer"
          style={{ backgroundColor: '#1C2038', border: '1px solid rgba(255,255,255,0.08)' }}
        >
          <Settings className="w-5 h-5" style={{ color: '#94A3B8' }} />
        </button>
      </div>

      {/* ── Sections ── */}
      <div className="flex flex-col gap-3 mb-6">
        <button
          onClick={() => navigate('/mailing/chats')}
          className="flex items-center justify-between w-full p-4 rounded-2xl"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <div>
            <p className="text-sm font-semibold text-white text-left">Чаты</p>
          </div>
          <div className="flex items-center gap-2">
            <span
              className="text-xs font-semibold px-2 py-0.5 rounded-full"
              style={{ backgroundColor: 'rgba(124,58,237,0.2)', color: '#8B5CF6' }}
            >
              {selectedChats.length}
            </span>
            <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
          </div>
        </button>

        <button
          onClick={() => setShowMessages(true)}
          className="flex items-center justify-between w-full p-4 rounded-2xl"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <div>
            <p className="text-sm font-semibold text-white text-left">Сообщения</p>
            <p className="text-xs text-left" style={{ color: '#64748B' }}>Текст сообщения + 1 файл</p>
          </div>
          <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
        </button>
      </div>

      {/* ── Action buttons ── */}
      <div className="flex flex-col gap-3">
        <button
          onClick={handleStart}
          disabled={isRunning || isOutreachLoading}
          className="w-full py-3.5 rounded-2xl text-sm font-bold text-white flex items-center justify-center gap-2 transition-opacity active:opacity-80 disabled:opacity-50"
          style={{ backgroundColor: '#10B981' }}
        >
          <Play className="w-4 h-4" fill="white" />
          НАЧАТЬ РАССЫЛКУ
        </button>

        <button
          onClick={() => stopCampaign()}
          disabled={!isRunning || isOutreachLoading}
          className="w-full py-3.5 rounded-2xl text-sm font-bold text-white flex items-center justify-center gap-2 transition-opacity active:opacity-80 disabled:opacity-30"
          style={{ backgroundColor: '#EF4444' }}
        >
          <Square className="w-4 h-4" fill="white" />
          СТОП
        </button>
      </div>

      {/* ── Modals ── */}
      <MailingIntervalModal isOpen={showInterval} onClose={() => setShowInterval(false)} />
      <MailingMessagesModal isOpen={showMessages} onClose={() => setShowMessages(false)} />
    </div>
  );
}
