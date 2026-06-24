import { useState, useEffect } from 'react';
import { Plus, Search, Check, ChevronUp, ChevronDown, Loader2 } from 'lucide-react';
import SubPage from '../components/SubPage';
import Modal from '../components/Modal';
import SwipeableItem from '../components/SwipeableItem';
import { detectPlatform, formatMembers } from '../data/mockData';
import { useSourcesStore } from '../store/useSourcesStore';

const platformLabels: Record<string, string> = {
  telegram: 'Telegram',
  whatsapp: 'WhatsApp',
  slack: 'Slack',
};

const platformColors: Record<string, string> = {
  telegram: '#229ED9',
  whatsapp: '#25D366',
  slack: '#E11D48',
};

interface ChatsPageContentProps {
  title: string;
  backTo: string;
  purpose: number; // 0 for Search, 1 for Outreach
}

export default function ChatsPageContent({ title, backTo, purpose }: ChatsPageContentProps) {
  const { sources: chats, isLoading, fetchSources, addSource, deleteSource, toggleSource } = useSourcesStore();
  const [searchQuery, setSearchQuery] = useState('');
  const [showAddChat, setShowAddChat] = useState(false);
  const [newChatLink, setNewChatLink] = useState('');
  const [collapsedPlatforms, setCollapsedPlatforms] = useState<Record<string, boolean>>({});

  useEffect(() => {
    fetchSources(purpose);
  }, [fetchSources, purpose]);

  const platforms = [...new Set(chats.map((c) => c.platform))];

  const togglePlatform = (p: string) => {
    setCollapsedPlatforms((prev) => ({ ...prev, [p]: !prev[p] }));
  };

  const handleToggleChat = (id: string, currentStatus: boolean) => {
    toggleSource(id, currentStatus);
  };

  const handleDeleteChat = (id: string) => {
    deleteSource(id);
  };

  const handleAddChat = async () => {
    if (!newChatLink.trim()) return;
    const platform = detectPlatform(newChatLink);
    const name = newChatLink.split('/').pop() || 'New Chat';
    
    try {
      await addSource(newChatLink, name, purpose, platform);
      setNewChatLink('');
      setShowAddChat(false);
    } catch (err: any) {
      if (err.message === 'DUPLICATE_CHAT') {
        alert('Этот чат уже добавлен в список.');
      } else {
        alert('Ошибка при добавлении чата.');
      }
    }
  };

  const filteredChats = chats.filter(
    (c) =>
      c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      c.username.toLowerCase().includes(searchQuery.toLowerCase())
  );


  return (
    <SubPage
      title={title}
      backTo={backTo}
      rightAction={
        <button
          onClick={() => setShowAddChat(true)}
          className="w-8 h-8 rounded-full flex items-center justify-center"
          style={{ backgroundColor: 'rgba(124,58,237,0.15)' }}
        >
          <Plus className="w-4 h-4" style={{ color: '#7C3AED' }} />
        </button>
      }
    >
      {/* ── Search ── */}
      <div className="px-4 py-3">
        <div
          className="flex items-center gap-2 px-4 py-3 rounded-2xl transition-all"
          style={{ 
            backgroundColor: 'rgba(255, 255, 255, 0.03)', 
            border: '1px solid rgba(255, 255, 255, 0.08)',
            backdropFilter: 'blur(12px)'
          }}
        >
          <Search className="w-4 h-4 shrink-0" style={{ color: '#94A3B8' }} />
          <input
            type="text"
            placeholder="Поиск чатов"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="flex-1 text-sm bg-transparent text-white outline-none placeholder:text-slate-500"
          />
        </div>
      </div>

      {/* ── Chat groups ── */}
      <div className="px-4 pb-24 relative">
        {isLoading && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-[#0B0E18]/50">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: '#7C3AED' }} />
          </div>
        )}
        {platforms.map((platform) => {
          const platformChats = filteredChats.filter((c) => c.platform === platform);
          if (platformChats.length === 0) return null;
          const isCollapsed = collapsedPlatforms[platform];

          return (
            <div key={platform} className="mb-4">
              {/* Platform header */}
              <button
                onClick={() => togglePlatform(platform)}
                className="flex items-center justify-between w-full py-2 mb-2"
              >
                <div className="flex items-center gap-2">
                  <div
                    className="w-7 h-7 rounded-full flex items-center justify-center"
                    style={{ backgroundColor: platformColors[platform] }}
                  >
                    {platform === 'telegram' ? (
                      <svg className="w-4 h-4 text-white" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
                        <path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.894 8.221l-1.97 9.28c-.145.658-.537.818-1.084.508l-3-2.21-1.446 1.394c-.14.18-.357.223-.548.223l.188-2.85 5.18-4.686c.223-.204-.054-.31-.346-.116l-6.405 4.04-2.777-.87c-.603-.188-.615-.604.126-.89l10.84-4.18c.5-.188.95.126.83.94l-.588 3.42z"/>
                      </svg>
                    ) : (
                      <span className="text-white text-[10px] font-bold">{platform.charAt(0).toUpperCase()}</span>
                    )}
                  </div>
                  <span className="text-[15px] font-bold text-white tracking-wide">{platformLabels[platform]}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span
                    className="text-xs font-semibold px-2 py-0.5 rounded-full"
                    style={{ backgroundColor: 'rgba(124,58,237,0.2)', color: '#8B5CF6' }}
                  >
                    {platformChats.length}
                  </span>
                  {isCollapsed ? (
                    <ChevronDown className="w-4 h-4" style={{ color: '#64748B' }} />
                  ) : (
                    <ChevronUp className="w-4 h-4" style={{ color: '#64748B' }} />
                  )}
                </div>
              </button>

              {/* Chat list */}
              {!isCollapsed && (
                <div className="flex flex-col gap-2">
                  {platformChats.map((chat) => (
                    <SwipeableItem key={chat.id} onDelete={() => handleDeleteChat(chat.id)}>
                      <div
                        onClick={() => handleToggleChat(chat.id, chat.checked)}
                        className="flex items-center gap-3 px-4 py-3 rounded-2xl cursor-pointer transition-all active:scale-[0.98]"
                        style={{ 
                          backgroundColor: 'rgba(255, 255, 255, 0.03)',
                          border: '1px solid rgba(255, 255, 255, 0.05)',
                          backdropFilter: 'blur(12px)',
                          boxShadow: chat.checked ? 'inset 0 0 0 1px rgba(124, 58, 237, 0.3)' : 'none'
                        }}
                      >
                        {/* Avatar */}
                        <div
                          className="w-10 h-10 rounded-full flex items-center justify-center shrink-0 shadow-lg overflow-hidden"
                          style={{ backgroundColor: chat.avatarColor }}
                        >
                          {chat.avatarUrl ? (
                            <img src={chat.avatarUrl} alt="" className="w-full h-full object-cover" />
                          ) : chat.platform === 'telegram' ? (
                            <svg className="w-5 h-5 text-white" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
                              <path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.894 8.221l-1.97 9.28c-.145.658-.537.818-1.084.508l-3-2.21-1.446 1.394c-.14.18-.357.223-.548.223l.188-2.85 5.18-4.686c.223-.204-.054-.31-.346-.116l-6.405 4.04-2.777-.87c-.603-.188-.615-.604.126-.89l10.84-4.18c.5-.188.95.126.83.94l-.588 3.42z"/>
                            </svg>
                          ) : (
                            <span className="text-white text-sm font-bold">{chat.name.charAt(0)}</span>
                          )}
                        </div>

                        {/* Info */}
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-bold text-white truncate">{chat.username}</p>
                          <p className="text-xs mt-0.5" style={{ color: '#94A3B8' }}>
                            {formatMembers(chat.members)} участников
                          </p>
                        </div>

                        {/* Checkbox */}
                        <div
                          className="w-6 h-6 rounded-lg flex items-center justify-center shrink-0 transition-all duration-300"
                          style={{
                            backgroundColor: chat.checked ? '#7C3AED' : 'rgba(255, 255, 255, 0.05)',
                            border: chat.checked ? 'none' : '1px solid rgba(255,255,255,0.15)',
                            boxShadow: chat.checked ? '0 0 12px rgba(124,58,237,0.5)' : 'none',
                          }}
                        >
                          {chat.checked && <Check className="w-4 h-4 text-white" strokeWidth={3} />}
                        </div>
                      </div>
                    </SwipeableItem>
                  ))}
                </div>
              )}
            </div>
          );
        })}


      </div>

      {/* ── Add chat modal ── */}
      <Modal isOpen={showAddChat} onClose={() => setShowAddChat(false)} title="Добавить чат">
        <div className="flex flex-col gap-4">
          <div>
            <label className="text-sm mb-1.5 block" style={{ color: '#94A3B8' }}>Ссылка на чат</label>
            <input
              type="text"
              value={newChatLink}
              onChange={(e) => setNewChatLink(e.target.value)}
              placeholder="https://t.me/your_chat_link"
              className="w-full px-4 py-3 rounded-xl text-sm text-white"
              style={{
                backgroundColor: '#0B0E18',
                border: '1px solid rgba(255,255,255,0.08)',
              }}
              onFocus={(e) => (e.currentTarget.style.borderColor = '#7C3AED')}
              onBlur={(e) => (e.currentTarget.style.borderColor = 'rgba(255,255,255,0.08)')}
            />
            <p className="text-xs mt-1.5" style={{ color: '#64748B' }}>
              Вставьте ссылку на Telegram-чат или имя пользователя
            </p>
          </div>
          <button
            onClick={handleAddChat}
            disabled={isLoading}
            className="w-full py-3 rounded-xl text-sm font-bold text-white transition-opacity active:opacity-80 flex justify-center items-center gap-2 disabled:opacity-50"
            style={{ backgroundColor: '#7C3AED' }}
          >
            {isLoading ? <Loader2 className="w-5 h-5 animate-spin" /> : 'ДОБАВИТЬ'}
          </button>
        </div>
      </Modal>
    </SubPage>
  );
}
