import { useEffect, useState } from 'react';
import { Clock, CalendarDays } from 'lucide-react';
import Modal from './Modal';
import { useOutreachStore } from '../store/useOutreachStore';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

const PERIODICITY_OPTIONS = [
  { value: '5', label: '5 мин' },
  { value: '30', label: '30 мин' },
  { value: '60', label: '60 мин' },
];

export default function MailingIntervalModal({ isOpen, onClose }: Props) {
  const {
    periodicityMinutes,
    scheduleMode,
    scheduleStartTime,
    scheduleEndTime,
    setSchedule,
  } = useOutreachStore();

  const [draftTimeMode, setDraftTimeMode] = useState<'allday' | 'custom'>('allday');
  const [draftStartTime, setDraftStartTime] = useState('09:00');
  const [draftEndTime, setDraftEndTime] = useState('23:00');
  const [draftPeriodicity, setDraftPeriodicity] = useState('30');

  useEffect(() => {
    if (!isOpen) return;
    setDraftTimeMode(scheduleMode);
    setDraftStartTime(scheduleStartTime);
    setDraftEndTime(scheduleEndTime);
    // Snap to nearest option
    const raw = String(periodicityMinutes);
    const valid = PERIODICITY_OPTIONS.map(o => o.value);
    setDraftPeriodicity(valid.includes(raw) ? raw : '30');
  }, [isOpen, periodicityMinutes, scheduleMode, scheduleStartTime, scheduleEndTime]);

  const handleSave = async () => {
    await setSchedule({
      periodicityMinutes: Number(draftPeriodicity),
      scheduleMode: draftTimeMode,
      scheduleStartTime: draftStartTime,
      scheduleEndTime: draftEndTime,
    });
    onClose();
  };

  const RadioOption = ({ label, selected, onClick }: { label: string; selected: boolean; onClick: () => void }) => (
    <button
      onClick={onClick}
      className="flex w-full items-center justify-between rounded-xl h-[56px] transition-all"
      style={{
        paddingLeft: '15px',
        paddingRight: '15px',
        backgroundColor: selected ? 'rgba(0, 120, 212,0.1)' : 'rgba(0,0,0,0.15)',
        border: selected ? '1px solid rgba(0, 120, 212,0.4)' : '1px solid rgba(255,255,255,0.04)',
      }}
    >
      <span className="text-[14px] font-semibold" style={{ color: selected ? '#fff' : '#94A3B8' }}>
        {label}
      </span>
      <div
        className="flex h-5 w-5 items-center justify-center rounded-full transition-all"
        style={{
          border: selected ? '5px solid #0078D4' : '1px solid rgba(255,255,255,0.2)',
          backgroundColor: selected ? '#fff' : 'transparent',
          boxShadow: selected ? '0 0 10px rgba(0, 120, 212,0.5)' : 'none',
        }}
      />
    </button>
  );

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Настройки рассылки">
      <div className="flex flex-col gap-5 px-1 pb-2 pt-6">

        {/* Блок 1: Периодичность рассылки */}
        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2 px-1">
            <Clock className="h-4 w-4" style={{ color: '#4CC2FF' }} />
            <h3 className="text-[15px] font-extrabold text-white">Периодичность рассылки</h3>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {PERIODICITY_OPTIONS.map((option) => (
              <button
                key={option.value}
                onClick={() => setDraftPeriodicity(option.value)}
                className="h-11 rounded-xl text-[13px] font-black transition-all active:scale-95"
                style={{
                  backgroundColor: draftPeriodicity === option.value ? 'rgba(0, 120, 212,0.24)' : 'rgba(255,255,255,0.04)',
                  color: draftPeriodicity === option.value ? '#FFFFFF' : '#94A3B8',
                  border: draftPeriodicity === option.value ? '1px solid rgba(76, 194, 255,0.45)' : '1px solid rgba(255,255,255,0.08)',
                }}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>

        {/* Блок 2: Промежуток времени */}
        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-2 px-1">
            <CalendarDays className="h-4 w-4" style={{ color: '#4CC2FF' }} />
            <h3 className="text-[15px] font-extrabold text-white">Промежуток времени</h3>
          </div>
          <p className="px-1 text-[13px] leading-relaxed" style={{ color: '#64748B' }}>
            Сообщения будут отправляться по вашему локальному времени.
          </p>

          <div className="mt-1 flex flex-col gap-2">
            <RadioOption label="Круглосуточно" selected={draftTimeMode === 'allday'} onClick={() => setDraftTimeMode('allday')} />
            <RadioOption label="Задать диапазон" selected={draftTimeMode === 'custom'} onClick={() => setDraftTimeMode('custom')} />
          </div>

          {draftTimeMode === 'custom' && (
            <div className="mt-2 grid grid-cols-2 gap-3 animate-slide-up">
              <div className="flex flex-col rounded-xl py-3" style={{ paddingLeft: '15px', paddingRight: '15px', backgroundColor: 'rgba(0,0,0,0.2)', border: '1px solid rgba(255,255,255,0.06)' }}>
                <label className="mb-1.5 text-[11px] font-bold uppercase tracking-wider" style={{ color: '#64748B' }}>От</label>
                <input
                  type="time"
                  value={draftStartTime}
                  onChange={(e) => setDraftStartTime(e.target.value)}
                  className="bg-transparent text-[15px] font-bold text-white outline-none"
                  style={{ colorScheme: 'dark' }}
                />
              </div>
              <div className="flex flex-col rounded-xl py-3" style={{ paddingLeft: '15px', paddingRight: '15px', backgroundColor: 'rgba(0,0,0,0.2)', border: '1px solid rgba(255,255,255,0.06)' }}>
                <label className="mb-1.5 text-[11px] font-bold uppercase tracking-wider" style={{ color: '#64748B' }}>До</label>
                <input
                  type="time"
                  value={draftEndTime}
                  onChange={(e) => setDraftEndTime(e.target.value)}
                  className="bg-transparent text-[15px] font-bold text-white outline-none"
                  style={{ colorScheme: 'dark' }}
                />
              </div>
            </div>
          )}
        </div>

        {/* Кнопка сохранения */}
        <div className="mt-4">
          <button
            onClick={handleSave}
            className="flex h-[52px] w-full items-center justify-center rounded-[14px] text-[14px] font-black uppercase tracking-widest text-white transition-all active:scale-[0.98]"
            style={{ background: 'linear-gradient(135deg, #0078D4, #005A9E)', boxShadow: '0 8px 24px rgba(0, 120, 212,0.3)' }}
          >
            Сохранить
          </button>
        </div>

      </div>
    </Modal>
  );
}
