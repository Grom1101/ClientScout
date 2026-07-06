import { useEffect, useRef, useState } from 'react';
import { Bell, Bot, Clock, Loader2, Plus, X } from 'lucide-react';
import Modal from './Modal';
import Toggle from './Toggle';
import { getActiveProfileId } from '../api/client';
import {
  MAX_SEARCH_KEYWORDS,
  MAX_SEARCH_NEGATIVE_KEYWORDS,
  SEARCH_INTERVAL_OPTIONS,
  useSearchSettingsStore,
} from '../store/useSearchSettingsStore';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

const normalizeTerm = (value: string) => value.trim();

export default function SearchSettingsModal({ isOpen, onClose }: Props) {
  const { settings, isLoading, fetchSettings, saveSettings } = useSearchSettingsStore();
  const profileId = getActiveProfileId();

  const [draftNotifications, setDraftNotifications] = useState(true);
  const [draftInterval, setDraftInterval] = useState(30);
  const [draftKeywords, setDraftKeywords] = useState<string[]>([]);
  const [draftNegativeKeywords, setDraftNegativeKeywords] = useState<string[]>([]);
  const [keywordInput, setKeywordInput] = useState('');
  const [negativeInput, setNegativeInput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const hydratedRef = useRef(false);

  useEffect(() => {
    if (isOpen && profileId) {
      fetchSettings(profileId);
    }
  }, [fetchSettings, isOpen, profileId]);

  useEffect(() => {
    if (!isOpen) {
      hydratedRef.current = false;
      return;
    }

    if (!settings || hydratedRef.current) return;

    setDraftNotifications(settings.notificationsEnabled);
    setDraftInterval(settings.intervalMinutes);
    setDraftKeywords(settings.userKeywords);
    setDraftNegativeKeywords(settings.negativeKeywords);
    setKeywordInput('');
    setNegativeInput('');
    setError(null);
    hydratedRef.current = true;
  }, [settings, isOpen]);

  const keywordsLeft = MAX_SEARCH_KEYWORDS - draftKeywords.length;
  const negativeLeft = MAX_SEARCH_NEGATIVE_KEYWORDS - draftNegativeKeywords.length;

  const addTerm = (value: string, list: string[], setList: (terms: string[]) => void, max: number, label: string) => {
    const term = normalizeTerm(value);
    if (!term) return false;
    if (list.some((item) => item.toLowerCase() === term.toLowerCase())) {
      setError(`${label} уже добавлено.`);
      return false;
    }
    if (list.length >= max) {
      setError(`Лимит: ${max}.`);
      return false;
    }
    setList([...list, term]);
    setError(null);
    return true;
  };

  const handleAddKeyword = () => {
    if (addTerm(keywordInput, draftKeywords, setDraftKeywords, MAX_SEARCH_KEYWORDS, 'Ключевое слово')) {
      setKeywordInput('');
    }
  };

  const handleAddNegative = () => {
    if (addTerm(negativeInput, draftNegativeKeywords, setDraftNegativeKeywords, MAX_SEARCH_NEGATIVE_KEYWORDS, 'Стоп-слово')) {
      setNegativeInput('');
    }
  };

  const handleClose = () => {
    if (settings) {
      setDraftNotifications(settings.notificationsEnabled);
      setDraftInterval(settings.intervalMinutes);
      setDraftKeywords(settings.userKeywords);
      setDraftNegativeKeywords(settings.negativeKeywords);
    }
    setError(null);
    onClose();
  };

  const handleSave = async () => {
    if (!profileId) return;
    await saveSettings({
      profileId,
      isEnabled: settings?.isEnabled ?? false,
      notificationsEnabled: draftNotifications,
      intervalMinutes: draftInterval,
      userKeywords: draftKeywords,
      negativeKeywords: draftNegativeKeywords,
    });
    onClose();
  };

  const renderTerm = (term: string, onRemove: () => void, tone: 'purple' | 'red') => (
    <span
      key={term}
      className="inline-flex max-w-full items-center gap-1.5 rounded-lg text-xs font-bold"
      style={{
        backgroundColor: tone === 'purple' ? 'rgba(14, 165, 233,0.18)' : 'rgba(239,68,68,0.12)',
        color: tone === 'purple' ? '#9ECBFF' : '#FCA5A5',
        border: tone === 'purple' ? '1px solid rgba(76, 194, 255,0.22)' : '1px solid rgba(248,113,113,0.2)',
        padding: '3px 10px'
      }}
    >
      <span className="truncate">{term}</span>
      <button onClick={onRemove} className="shrink-0 rounded-full p-0.5 active:scale-90" type="button">
        <X className="h-3 w-3" />
      </button>
    </span>
  );

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Настройки поиска">
      <div className="relative flex flex-col gap-3 pb-2" style={{ marginTop: '5px' }}>
        {isLoading && (
          <div className="absolute inset-0 z-30 flex items-center justify-center rounded-2xl bg-[#1B1B1B]/50 backdrop-blur-sm">
            <Loader2 className="h-8 w-8 animate-spin" style={{ color: '#4CC2FF' }} />
          </div>
        )}

        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2 px-1">
            <Clock className="h-4 w-4" style={{ color: '#4CC2FF' }} />
            <h3 className="text-[15px] font-extrabold text-white">Периодичность поиска</h3>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {SEARCH_INTERVAL_OPTIONS.map((option) => (
              <button
                key={option}
                onClick={() => setDraftInterval(option)}
                className="h-11 rounded-xl text-[13px] font-black transition-all active:scale-95"
                style={{
                  backgroundColor: draftInterval === option ? 'rgba(14, 165, 233,0.24)' : 'rgba(255,255,255,0.04)',
                  color: draftInterval === option ? '#FFFFFF' : '#94A3B8',
                  border: draftInterval === option ? '1px solid rgba(76, 194, 255,0.45)' : '1px solid rgba(255,255,255,0.08)',
                }}
              >
                {option} мин
              </button>
            ))}
          </div>
        </div>

        <div className="flex items-center justify-between rounded-xl border border-white/[0.08] bg-black/20"
             style={{ padding: '14px 15px' }}>
          <div className="flex items-center gap-2.5">
            <Bell className="h-5 w-5" style={{ color: '#4CC2FF' }} />
            <span className="text-[15px] font-extrabold text-white">Уведомления</span>
          </div>
          <Toggle checked={draftNotifications} onChange={setDraftNotifications} />
        </div>

        <div className="flex flex-col gap-3">
          <div className="flex items-center justify-between px-1">
            <div className="flex items-center gap-2">
              <h3 className="text-[15px] font-extrabold text-white">Ключевые слова</h3>
              <span className="inline-flex items-center gap-1.5 rounded-full text-[10px] font-black uppercase" style={{ backgroundColor: 'rgba(14, 165, 233,0.16)', color: '#9ECBFF', padding: '3px 8px' }}>
                <Bot className="h-3 w-3" />
                AI
              </span>
            </div>
            <span className="text-xs" style={{ color: '#64748B' }}>{keywordsLeft} осталось</span>
          </div>
          <div className="flex gap-2">
            <input
              value={keywordInput}
              onChange={(e) => setKeywordInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  handleAddKeyword();
                }
              }}
              placeholder="React, сайт, Unity..."
              className="min-w-0 flex-1 rounded-xl bg-black/20 px-3 text-sm text-white outline-none placeholder:text-slate-500"
              style={{ border: '1px solid rgba(255,255,255,0.08)', paddingLeft: '16px', paddingRight: '16px' }}
            />
            <button disabled={!keywordInput.trim()} onClick={handleAddKeyword} className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-white transition-all disabled:opacity-50" style={{ backgroundColor: '#0EA5E9' }} type="button">
              <Plus className="h-5 w-5" />
            </button>
          </div>
          <div className="flex flex-wrap gap-2">
            {draftKeywords.map((term) => renderTerm(term, () => setDraftKeywords(draftKeywords.filter((item) => item !== term)), 'purple'))}
          </div>
        </div>

        <div className="flex flex-col gap-3">
          <div className="flex items-center justify-between px-1">
            <h3 className="text-[15px] font-extrabold text-white">Стоп-слова</h3>
            <span className="text-xs" style={{ color: '#64748B' }}>{negativeLeft} осталось</span>
          </div>
          <div className="flex gap-2">
            <input
              value={negativeInput}
              onChange={(e) => setNegativeInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  handleAddNegative();
                }
              }}
              placeholder="UE5, вакансии..."
              className="min-w-0 flex-1 rounded-xl bg-black/20 px-3 text-sm text-white outline-none placeholder:text-slate-500"
              style={{ border: '1px solid rgba(255,255,255,0.08)', paddingLeft: '16px', paddingRight: '16px' }}
            />
            <button disabled={!negativeInput.trim()} onClick={handleAddNegative} className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-white transition-all disabled:opacity-50" style={{ backgroundColor: '#0EA5E9' }} type="button">
              <Plus className="h-5 w-5" />
            </button>
          </div>
          <div className="flex flex-wrap gap-2">
            {draftNegativeKeywords.map((term) => renderTerm(term, () => setDraftNegativeKeywords(draftNegativeKeywords.filter((item) => item !== term)), 'red'))}
          </div>
        </div>

        {error && <p className="text-xs font-semibold text-red-400">{error}</p>}

        <div className="rounded-xl px-3 py-2.5 text-xs text-center leading-relaxed" style={{ backgroundColor: 'rgba(255,255,255,0.03)', color: '#7F8CA0', border: '1px solid rgba(255,255,255,0.06)' }}>
          Расширенный AI-словарь работает в фоновом режиме и автоматически обновляется при сохранении новых слов.
        </div>

        <button
          onClick={handleSave}
          className="flex h-[52px] w-full items-center justify-center rounded-[14px] text-[14px] font-black uppercase tracking-widest text-white transition-all active:scale-[0.98]"
          style={{ backgroundColor: '#0EA5E9', boxShadow: '0 4px 20px rgba(14,165,233,0.3)' }}
        >
          Сохранить
        </button>
      </div>
    </Modal>
  );
}
