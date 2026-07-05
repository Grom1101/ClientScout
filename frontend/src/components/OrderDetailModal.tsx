import { Send, ExternalLink } from 'lucide-react';
import Modal from './Modal';
import { useLeadsStore } from '../store/useLeadsStore';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  orderId: string | null;
}

export default function OrderDetailModal({ isOpen, onClose, orderId }: Props) {
  const { leads } = useLeadsStore();
  const order = leads.find((o) => o.id === orderId);

  if (!order) {
    return (
      <Modal isOpen={isOpen} onClose={onClose} title="Заказ">
        <div className="py-8 text-center" style={{ color: '#8A8A8A' }}>
          Заказ не найден
        </div>
      </Modal>
    );
  }

  const isTelegram = order.source === 'telegram';

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={isTelegram ? 'Заказ из Telegram' : 'Заказ с биржи'}>
      <div className="flex flex-col">
        {/* ── Source badge ── */}
        <div className="flex items-center gap-2 mb-4">
          <div
            className="w-8 h-8 rounded-full flex items-center justify-center text-white text-xs font-bold"
            style={{ backgroundColor: order.sourceColor }}
          >
            {order.source.charAt(0).toUpperCase()}
          </div>
          <span className="text-sm font-medium" style={{ color: order.sourceColor }}>
            {order.source === 'telegram' ? 'Telegram-чат' : order.source.charAt(0).toUpperCase() + order.source.slice(1)}
          </span>
          <span className="text-xs" style={{ color: '#8A8A8A' }}>{order.timeAgo}</span>
        </div>

        {/* ── Title ── */}
        <h2 className="text-lg font-bold text-white mb-4 leading-snug">{order.title}</h2>

        {/* ── Info section ── */}
        <div
          className="rounded-2xl p-4 mb-4 flex flex-col gap-3"
          style={{ backgroundColor: '#2B2B2B', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          {isTelegram && (
            <>
              <div className="flex justify-between">
                <span className="text-xs" style={{ color: '#8A8A8A' }}>Чат</span>
                <span className="text-xs text-white">{order.chatName}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-xs" style={{ color: '#8A8A8A' }}>Автор</span>
                <span className="text-xs text-white">{order.author}</span>
              </div>
            </>
          )}
          {!isTelegram && (
            <>
              <div className="flex justify-between">
                <span className="text-xs" style={{ color: '#8A8A8A' }}>Платформа</span>
                <span className="text-xs text-white">{order.source.charAt(0).toUpperCase() + order.source.slice(1)}</span>
              </div>
              {order.budget && (
                <div className="flex justify-between">
                  <span className="text-xs" style={{ color: '#8A8A8A' }}>Бюджет</span>
                  <span className="text-xs font-semibold" style={{ color: '#10B981' }}>{order.budget}</span>
                </div>
              )}
            </>
          )}
          <div className="flex justify-between">
            <span className="text-xs" style={{ color: '#8A8A8A' }}>Дата</span>
            <span className="text-xs text-white">{order.date}</span>
          </div>
        </div>

        {/* ── Message / Description ── */}
        <div
          className="rounded-2xl p-4 mb-6 max-h-60 overflow-y-auto"
          style={{ backgroundColor: '#2B2B2B', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <h3 className="text-sm font-semibold text-white mb-2">
            {isTelegram ? 'Сообщение' : 'Описание'}
          </h3>
          <p className="text-sm leading-relaxed" style={{ color: '#ADADAD' }}>
            {order.message || order.description}
          </p>
        </div>

        {/* ── Action button ── */}
        <button
          onClick={onClose}
          className="w-full py-3.5 rounded-2xl text-sm font-bold text-white flex items-center justify-center gap-2 transition-opacity active:opacity-80"
          style={{ backgroundColor: '#0078D4' }}
        >
          {isTelegram ? (
            <><Send className="w-4 h-4" /> Написать в чат</>
          ) : (
            <><ExternalLink className="w-4 h-4" /> Открыть в браузере</>
          )}
        </button>
      </div>
    </Modal>
  );
}
