import { useState } from 'react';
import { ChevronRight } from 'lucide-react';
import Modal from './Modal';
import Toggle from './Toggle';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export default function SearchSettingsModal({ isOpen, onClose }: Props) {
  const [notificationsEnabled, setNotificationsEnabled] = useState(true);
  const [periodicity, setPeriodicity] = useState('10');
  const [showPeriodicityPicker, setShowPeriodicityPicker] = useState(false);

  const periodicityOptions = ['5', '10', '15', '30', '60'];

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Настройки поиска">
      <div className="flex flex-col gap-3">
        {/* Periodicity */}
        <div
          className="p-4 rounded-2xl"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <button
            onClick={() => setShowPeriodicityPicker(!showPeriodicityPicker)}
            className="flex items-center justify-between w-full"
          >
            <div>
              <p className="text-sm font-semibold text-white text-left">Периодичность поиска</p>
              <p className="text-xs text-left" style={{ color: '#64748B' }}>
                Каждые {periodicity} минут
              </p>
            </div>
            <ChevronRight
              className="w-5 h-5 transition-transform"
              style={{
                color: '#64748B',
                transform: showPeriodicityPicker ? 'rotate(90deg)' : 'none',
              }}
            />
          </button>

          {showPeriodicityPicker && (
            <div className="mt-3 flex gap-2 flex-wrap">
              {periodicityOptions.map((opt) => (
                <button
                  key={opt}
                  onClick={() => { setPeriodicity(opt); setShowPeriodicityPicker(false); }}
                  className="px-3 py-1.5 rounded-lg text-xs font-medium transition-colors"
                  style={{
                    backgroundColor: periodicity === opt ? '#7C3AED' : '#1C2038',
                    color: periodicity === opt ? 'white' : '#94A3B8',
                    border: `1px solid ${periodicity === opt ? '#7C3AED' : 'rgba(255,255,255,0.06)'}`,
                  }}
                >
                  {opt} мин
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Notifications */}
        <div
          className="flex items-center justify-between p-4 rounded-2xl"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <div>
            <p className="text-sm font-semibold text-white">Уведомления о новых заказах</p>
          </div>
          <Toggle checked={notificationsEnabled} onChange={setNotificationsEnabled} />
        </div>

        {/* Info */}
        <p
          className="text-xs text-center mt-4 px-4 leading-relaxed"
          style={{ color: '#64748B' }}
        >
          Настройте параметры поиска заказов по вашим предпочтениям
        </p>
      </div>
    </Modal>
  );
}
