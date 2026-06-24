import { useState } from 'react';
import { ChevronDown } from 'lucide-react';
import Modal from './Modal';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export default function MailingIntervalModal({ isOpen, onClose }: Props) {
  const [timeMode, setTimeMode] = useState<'allday' | 'custom'>('allday');
  const [startTime, setStartTime] = useState('09:00');
  const [endTime, setEndTime] = useState('23:00');
  const [periodicity, setPeriodicity] = useState('30');
  const [showPeriodicity, setShowPeriodicity] = useState(false);

  const periodicityOptions = [
    { value: '5', label: 'Каждые 5 минут' },
    { value: '10', label: 'Каждые 10 минут' },
    { value: '15', label: 'Каждые 15 минут' },
    { value: '30', label: 'Каждые 30 минут' },
    { value: '60', label: 'Каждый час' },
    { value: '120', label: 'Каждые 2 часа' },
  ];

  const currentPeriodicityLabel = periodicityOptions.find((o) => o.value === periodicity)?.label || '';

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Интервал">
      <div className="flex flex-col gap-4">
        {/* ── Time period ── */}
        <div
          className="rounded-2xl p-4"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <h3 className="text-sm font-semibold text-white mb-1">Промежуток времени</h3>
          <p className="text-xs mb-4" style={{ color: '#64748B' }}>
            Укажите период времени, в который сообщения будут отправляться
          </p>

          {/* Radio: All day */}
          <label className="flex items-center gap-3 mb-3 cursor-pointer">
            <div
              className="w-5 h-5 rounded-full flex items-center justify-center"
              style={{
                border: `2px solid ${timeMode === 'allday' ? '#7C3AED' : 'rgba(255,255,255,0.2)'}`,
              }}
            >
              {timeMode === 'allday' && (
                <div className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: '#7C3AED' }} />
              )}
            </div>
            <span className="text-sm text-white">Круглосуточно</span>
            <input
              type="radio"
              checked={timeMode === 'allday'}
              onChange={() => setTimeMode('allday')}
              className="hidden"
            />
          </label>

          {/* Radio: Custom range */}
          <label className="flex items-center gap-3 mb-3 cursor-pointer">
            <div
              className="w-5 h-5 rounded-full flex items-center justify-center"
              style={{
                border: `2px solid ${timeMode === 'custom' ? '#7C3AED' : 'rgba(255,255,255,0.2)'}`,
              }}
            >
              {timeMode === 'custom' && (
                <div className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: '#7C3AED' }} />
              )}
            </div>
            <span className="text-sm text-white">
              Задать диапазон времени
            </span>
            <input
              type="radio"
              checked={timeMode === 'custom'}
              onChange={() => setTimeMode('custom')}
              className="hidden"
            />
          </label>

          {/* Time inputs */}
          {timeMode === 'custom' && (
            <div className="grid grid-cols-2 gap-3 mt-3 animate-slide-up">
              <div>
                <label className="text-xs mb-1 block" style={{ color: '#64748B' }}>Время начала</label>
                <input
                  type="time"
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                  className="w-full px-3 py-2.5 rounded-xl text-sm text-white"
                  style={{
                    backgroundColor: '#0B0E18',
                    border: '1px solid rgba(255,255,255,0.08)',
                    colorScheme: 'dark',
                  }}
                />
              </div>
              <div>
                <label className="text-xs mb-1 block" style={{ color: '#64748B' }}>Время окончания</label>
                <input
                  type="time"
                  value={endTime}
                  onChange={(e) => setEndTime(e.target.value)}
                  className="w-full px-3 py-2.5 rounded-xl text-sm text-white"
                  style={{
                    backgroundColor: '#0B0E18',
                    border: '1px solid rgba(255,255,255,0.08)',
                    colorScheme: 'dark',
                  }}
                />
              </div>
            </div>
          )}
        </div>

        {/* ── Periodicity ── */}
        <div
          className="rounded-2xl p-4"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <h3 className="text-sm font-semibold text-white mb-1">Периодичность</h3>
          <p className="text-xs mb-3" style={{ color: '#64748B' }}>Как часто отправлять сообщения</p>

          <button
            onClick={() => setShowPeriodicity(!showPeriodicity)}
            className="w-full flex items-center justify-between px-4 py-3 rounded-xl"
            style={{ backgroundColor: '#0B0E18', border: '1px solid rgba(255,255,255,0.08)' }}
          >
            <span className="text-sm text-white">{currentPeriodicityLabel}</span>
            <ChevronDown
              className="w-4 h-4 transition-transform"
              style={{
                color: '#64748B',
                transform: showPeriodicity ? 'rotate(180deg)' : 'none',
              }}
            />
          </button>

          {showPeriodicity && (
            <div className="mt-2 flex flex-col rounded-xl overflow-hidden animate-slide-up"
              style={{ backgroundColor: '#0B0E18', border: '1px solid rgba(255,255,255,0.06)' }}
            >
              {periodicityOptions.map((opt) => (
                <button
                  key={opt.value}
                  onClick={() => { setPeriodicity(opt.value); setShowPeriodicity(false); }}
                  className="w-full text-left px-4 py-3 text-sm transition-colors"
                  style={{
                    color: periodicity === opt.value ? '#7C3AED' : '#94A3B8',
                    backgroundColor: periodicity === opt.value ? 'rgba(124,58,237,0.08)' : 'transparent',
                    borderBottom: '1px solid rgba(255,255,255,0.04)',
                  }}
                >
                  {opt.label}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* ── Save button ── */}
        <button
          onClick={onClose}
          className="w-full py-3.5 rounded-2xl text-sm font-bold text-white transition-opacity active:opacity-80"
          style={{ backgroundColor: '#7C3AED' }}
        >
          СОХРАНИТЬ
        </button>
      </div>
    </Modal>
  );
}
