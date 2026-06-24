import { useState } from 'react';
import Modal from './Modal';
import { mockExchanges, type Exchange } from '../data/mockData';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export default function SearchExchangesModal({ isOpen, onClose }: Props) {
  const [exchanges, setExchanges] = useState<Exchange[]>(mockExchanges);

  const toggleConnection = (id: string) => {
    setExchanges((prev) =>
      prev.map((ex) => (ex.id === id ? { ...ex, connected: !ex.connected } : ex))
    );
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Биржи и API ключи">
      <div className="flex flex-col gap-3">
        {exchanges.map((exchange) => (
          <div
            key={exchange.id}
            className="flex items-center gap-3 p-4 rounded-2xl"
            style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
          >
            {/* Icon */}
            <div
              className="w-10 h-10 rounded-xl flex items-center justify-center text-white text-sm font-bold shrink-0"
              style={{ backgroundColor: exchange.color }}
            >
              {exchange.initial}
            </div>

            {/* Info */}
            <div className="flex-1 min-w-0">
              <p className="text-sm font-semibold text-white">{exchange.name}</p>
              <p className="text-xs" style={{ color: exchange.connected ? '#10B981' : '#64748B' }}>
                {exchange.connected ? 'Подключено' : 'Не подключено'}
              </p>
              {exchange.connected && (
                <p className="text-xs" style={{ color: '#64748B' }}>API ключ сохранен</p>
              )}
            </div>

            {/* Connect button */}
            {!exchange.connected && (
              <button
                onClick={() => toggleConnection(exchange.id)}
                className="text-xs font-semibold px-4 py-2 rounded-lg transition-opacity active:opacity-80"
                style={{ backgroundColor: '#7C3AED', color: 'white' }}
              >
                Подключить
              </button>
            )}
          </div>
        ))}

        {/* Info note */}
        <p
          className="text-xs text-center mt-4 px-4 leading-relaxed"
          style={{ color: '#64748B' }}
        >
          Ваши API ключи хранятся в безопасности и не передаются третьим лицам
        </p>
      </div>
    </Modal>
  );
}
