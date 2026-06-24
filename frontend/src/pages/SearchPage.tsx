import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Settings, ChevronRight, RefreshCw, Send, ExternalLink } from 'lucide-react';
import { mockOrders } from '../data/mockData';
import SearchSettingsModal from '../components/SearchSettingsModal';
import SearchExchangesModal from '../components/SearchExchangesModal';
import OrderDetailModal from '../components/OrderDetailModal';

const sourceIcons: Record<string, string> = {
  telegram: '✈️',
  upwork: '🟢',
  quark: '🟠',
};

export default function SearchPage() {
  const navigate = useNavigate();
  const [isSearching, setIsSearching] = useState(false);

  const [showSettings, setShowSettings] = useState(false);
  const [showExchanges, setShowExchanges] = useState(false);
  const [showOrder, setShowOrder] = useState(false);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);

  const handleSearch = () => {
    setIsSearching(true);
    setTimeout(() => setIsSearching(false), 2000);
  };

  const openOrder = (id: string) => {
    setSelectedOrderId(id);
    setShowOrder(true);
  };

  return (
    <div className="px-4 pt-4 pb-4">
      {/* ── Header ── */}
      <div className="flex items-center justify-between mb-5">
        <h1 className="text-xl font-bold text-white">Поиск заказов</h1>
        <button
          onClick={() => setShowSettings(true)}
          className="w-10 h-10 rounded-full flex items-center justify-center"
          style={{ backgroundColor: '#1C2038', border: '1px solid rgba(255,255,255,0.08)' }}
        >
          <Settings className="w-5 h-5" style={{ color: '#94A3B8' }} />
        </button>
      </div>

      {/* ── Source sections ── */}
      <div className="flex flex-col gap-3 mb-5">
        <button
          onClick={() => navigate('/search/chats')}
          className="flex items-center justify-between w-full p-4 rounded-2xl"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <div>
            <p className="text-sm font-semibold text-white text-left">Чаты</p>
            <p className="text-xs text-left" style={{ color: '#64748B' }}>Telegram, WhatsApp и др.</p>
          </div>
          <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
        </button>

        <button
          onClick={() => setShowExchanges(true)}
          className="flex items-center justify-between w-full p-4 rounded-2xl"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <div>
            <p className="text-sm font-semibold text-white text-left">Биржи</p>
            <p className="text-xs text-left" style={{ color: '#64748B' }}>Upwork, Quark и др.</p>
          </div>
          <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
        </button>
      </div>

      {/* ── Search button ── */}
      <button
        onClick={handleSearch}
        disabled={isSearching}
        className="w-full py-3.5 rounded-2xl text-sm font-bold text-white mb-4 transition-opacity active:opacity-80 disabled:opacity-50"
        style={{ backgroundColor: '#10B981' }}
      >
        {isSearching ? '⏳ Поиск...' : '▶ Запустить поиск'}
      </button>

      {/* ── Search info ── */}
      <p className="text-xs mb-4" style={{ color: '#64748B' }}>
        Последний поиск: 12.05.2024, 14:30
      </p>

      {/* ── Found orders ── */}
      <div className="flex items-center justify-between mb-3">
        <p className="text-sm font-semibold text-white">
          Найденных заказов: <span style={{ color: '#10B981' }}>24</span>
        </p>
        <button className="flex items-center gap-1 text-xs" style={{ color: '#7C3AED' }}>
          <RefreshCw className="w-3.5 h-3.5" />
          <span>Обновить</span>
        </button>
      </div>

      <div className="flex flex-col gap-3">
        {mockOrders.map((order) => (
          <div
            key={order.id}
            className="w-full text-left p-4 rounded-2xl transition-colors cursor-default"
            style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
          >
            <div className="flex items-start gap-3">
              {/* Source indicator */}
              <div
                className="w-9 h-9 rounded-full flex items-center justify-center text-base shrink-0 mt-0.5"
                style={{ backgroundColor: `${order.sourceColor}20` }}
              >
                {sourceIcons[order.source]}
              </div>

              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between mb-1">
                  <span className="text-xs capitalize" style={{ color: order.sourceColor }}>
                    {order.source === 'telegram' ? 'Telegram-чат' : order.source.charAt(0).toUpperCase() + order.source.slice(1)}
                  </span>
                  <span className="text-xs" style={{ color: '#64748B' }}>{order.timeAgo}</span>
                </div>
                <p className="text-sm font-medium text-white mb-1 leading-snug">{order.title}</p>
                <p className="text-xs leading-relaxed mb-2" style={{ color: '#94A3B8' }}>
                  {order.description}
                </p>
                {order.budget && (
                  <p className="text-xs font-semibold" style={{ color: '#10B981' }}>{order.budget}</p>
                )}
              </div>
            </div>

            {/* Action button */}
            <div className="flex justify-end mt-2">
              <button
                onClick={() => openOrder(order.id)}
                className="flex items-center gap-1 text-xs font-semibold px-3 py-1.5 rounded-lg transition-opacity active:opacity-80"
                style={{ backgroundColor: '#7C3AED', color: 'white' }}
              >
                {order.source === 'telegram' ? (
                  <><Send className="w-3 h-3" /> Написать</>
                ) : (
                  <><ExternalLink className="w-3 h-3" /> Открыть</>
                )}
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* ── Modals ── */}
      <SearchSettingsModal isOpen={showSettings} onClose={() => setShowSettings(false)} />
      <SearchExchangesModal isOpen={showExchanges} onClose={() => setShowExchanges(false)} />
      <OrderDetailModal isOpen={showOrder} onClose={() => setShowOrder(false)} orderId={selectedOrderId} />
    </div>
  );
}
