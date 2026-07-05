import { useEffect, useMemo, useState } from 'react';
import { AlertCircle, Check, ChevronDown, ChevronUp, Link, Loader2, Lock, Plus, Search } from 'lucide-react';
import Modal from '../components/Modal';
import SubPage from '../components/SubPage';
import SwipeableItem from '../components/SwipeableItem';
import { TelegramAuthModal } from '../components/TelegramAuthModal';
import { authApi } from '../api/auth';
import { detectPlatform, formatMembers, type ChatItem } from '../data/mockData';
import { cleanTelegramName, normalizeTelegramUrl, type SourceTopic, useSourcesStore } from '../store/useSourcesStore';
import { useAuthStore } from '../store/useAuthStore';

const platformLabels: Record<string, string> = {
  telegram: 'Telegram',
  kwork: 'Kwork',
  whatsapp: 'WhatsApp',
  slack: 'Slack',
};

const platformColors: Record<string, string> = {
  telegram: '#229ED9',
  kwork: '#FF7B00',
  whatsapp: '#25D366',
  slack: '#E11D48',
};

interface ChatsPageContentProps {
  title: string;
  backTo: string;
  purpose: number;
}

type ForumTopicState = SourceTopic & { checked: boolean; alreadyAdded: boolean };

type ChatListEntry =
  | { kind: 'chat'; chat: ChatItem }
  | { kind: 'forum'; key: string; platform: ChatItem['platform']; name: string; username: string; members: number; avatarColor: string; avatarUrl?: string; checked: boolean; topics: ChatItem[] };

const getTopicUrl = (baseUrl: string, topicId: string) => `${baseUrl.replace(/\/$/, '')}/${topicId}`;

const getChatTitle = (chat: ChatItem) => {
  if (chat.isForumTopic) {
    const forumName = chat.forumName || chat.name;
    const topicName = chat.topicName || chat.name;
    return forumName && topicName && forumName !== topicName ? `${forumName} › ${topicName}` : topicName;
  }
  return chat.name || cleanTelegramName(chat.username);
};

const getSourceErrorText = (error?: string) => {
  if (!error) return 'Чат недоступен. Проверьте доступ и включите его заново.';
  const normalized = error.toUpperCase();
  if (
    normalized.includes('NOT_A_MEMBER') ||
    normalized.includes('USER_NOT_PARTICIPANT') ||
    normalized.includes('CHANNEL_PRIVATE') ||
    normalized.includes('CHAT_WRITE_FORBIDDEN') ||
    normalized.includes('CHAT_ADMIN_REQUIRED')
  ) {
    return 'Вы вышли из чата или чат стал недоступен.';
  }
  if (normalized.includes('NOT_AUTHORIZED') || normalized.includes('AUTH_KEY')) {
    return 'Telegram-аккаунт нужно подключить заново.';
  }
  if (normalized.includes('NOT_FOUND') || normalized.includes('INVALID TELEGRAM URL')) {
    return 'Чат удален или ссылка больше не работает.';
  }
  return 'Ошибка проверки чата. Нажмите, чтобы включить заново.';
};

const TelegramIcon = ({ className = 'h-4 w-4' }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
    <path d="M21.9 4.2 18.7 19.3c-.24 1.07-.88 1.33-1.78.83l-4.92-3.63-2.37 2.28c-.26.26-.48.48-.98.48l.35-5.02 9.14-8.26c.4-.35-.09-.55-.61-.2L6.23 12.9 1.36 11.38c-1.06-.33-1.08-1.06.22-1.57L20.6 2.48c.88-.33 1.65.2 1.3 1.72Z" />
  </svg>
);

const PlatformIcon = ({ platform }: { platform: ChatItem['platform'] }) => {
  if (platform === 'telegram') {
    return <TelegramIcon className="h-4 w-4 text-white" />;
  }
  if (platform === 'kwork') {
    return <span className="text-[13px] font-black italic text-white">K</span>;
  }
  return <span className="text-[10px] font-black text-white">{platform.charAt(0).toUpperCase()}</span>;
};

const buildEntries = (chats: ChatItem[]): ChatListEntry[] => {
  const forumGroups = new Map<string, ChatItem[]>();
  const entries: ChatListEntry[] = [];

  for (const chat of chats) {
    if (chat.isForumTopic && chat.baseUrl) {
      const key = normalizeTelegramUrl(chat.baseUrl);
      forumGroups.set(key, [...(forumGroups.get(key) || []), chat]);
      continue;
    }
    entries.push({ kind: 'chat', chat });
  }

  for (const [key, topics] of forumGroups) {
    const first = topics[0];
    entries.push({
      kind: 'forum',
      key,
      platform: first.platform,
      name: first.forumName || cleanTelegramName(first.baseUrl || first.username),
      username: first.baseUrl || first.username,
      members: first.members,
      avatarColor: first.avatarColor,
      avatarUrl: first.avatarUrl,
      checked: topics.some((topic) => topic.checked),
      topics,
    });
  }

  return entries;
};

export default function ChatsPageContent({ title, backTo, purpose }: ChatsPageContentProps) {
  const { sources: chats, isLoading, fetchSources, addSource, deleteSource, toggleSource, validateSource } = useSourcesStore();
  const [searchQuery, setSearchQuery] = useState('');
  const [showAddChat, setShowAddChat] = useState(false);
  const [showAuthModal, setShowAuthModal] = useState(false);
  const [newChatLink, setNewChatLink] = useState('');
  const [collapsedPlatforms, setCollapsedPlatforms] = useState<Record<string, boolean>>({});
  const [expandedForums, setExpandedForums] = useState<Record<string, boolean>>({});
  const [addError, setAddError] = useState<string | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const [forumTopics, setForumTopics] = useState<ForumTopicState[] | null>(null);

  useEffect(() => {
    fetchSources(purpose);
  }, [fetchSources, purpose]);

  const filteredChats = useMemo(() => {
    const q = searchQuery.toLowerCase();
    return chats.filter((chat) => getChatTitle(chat).toLowerCase().includes(q) || chat.username.toLowerCase().includes(q));
  }, [chats, searchQuery]);

  const entries = useMemo(() => buildEntries(filteredChats), [filteredChats]);
  const platforms = [...new Set(entries.map((entry) => entry.kind === 'chat' ? entry.chat.platform : entry.platform))];

  const closeAddModal = () => {
    setShowAddChat(false);
    setNewChatLink('');
    setAddError(null);
    setForumTopics(null);
  };

  const translateAddError = (message: string) => {
    switch (message) {
      case 'NOT_AUTHORIZED':
        setShowAuthModal(true);
        setShowAddChat(false);
        authApi.getMe().then((updated) => useAuthStore.getState().updateAccount(updated)).catch(() => {});
        return;
      case 'NOT_A_MEMBER':
        setAddError('Сначала вступите в этот чат с вашего Telegram-аккаунта.');
        return;
      case 'READ_ONLY':
        setAddError('В этот чат или тему нельзя писать.');
        return;
      case 'NOT_FOUND':
        setAddError('Чат по этой ссылке не найден.');
        return;
      case 'DUPLICATE_CHAT':
        setAddError('Этот чат или тема уже добавлены.');
        return;
      default:
        setAddError('Ошибка при добавлении чата.');
    }
  };

  const handleAddChat = async () => {
    setAddError(null);
    if (!newChatLink.trim()) return;

    if (forumTopics) {
      const selected = forumTopics.filter((topic) => topic.checked && (purpose === 0 || topic.canWrite !== false) && !topic.alreadyAdded);
      if (selected.length === 0) {
        setAddError('Выберите хотя бы одну доступную тему.');
        return;
      }

      setIsAdding(true);
      try {
        const platform = detectPlatform(newChatLink);
        for (const topic of selected) {
          await addSource(getTopicUrl(newChatLink, topic.id), topic.name, purpose, platform);
        }
        closeAddModal();
      } catch (err: any) {
        translateAddError(err.message);
      } finally {
        setIsAdding(false);
      }
      return;
    }

    const normalizedNew = normalizeTelegramUrl(newChatLink);
    if (chats.some((chat) => normalizeTelegramUrl(chat.username) === normalizedNew)) {
      setAddError('Этот чат уже добавлен.');
      return;
    }

    setIsAdding(true);
    try {
      const valRes = await validateSource(newChatLink, purpose);
      if (!valRes.isValid) throw new Error(valRes.errorCode || 'UNKNOWN_ERROR');

      if (valRes.isForum && valRes.topics) {
        setForumTopics(valRes.topics.map((topic) => {
          const topicUrl = getTopicUrl(newChatLink, topic.id);
          const alreadyAdded = chats.some((chat) => normalizeTelegramUrl(chat.username) === normalizeTelegramUrl(topicUrl));
          return { ...topic, checked: false, alreadyAdded };
        }));
        return;
      }

      const platform = detectPlatform(newChatLink);
      await addSource(newChatLink, cleanTelegramName(newChatLink), purpose, platform);
      closeAddModal();
    } catch (err: any) {
      translateAddError(err.message);
    } finally {
      setIsAdding(false);
    }
  };

  const renderAvatar = (entry: { platform: ChatItem['platform']; avatarColor: string; avatarUrl?: string; name: string }) => (
    <div
      className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full shadow-[0_0_0_1px_rgba(255,255,255,0.08)]"
      style={{ backgroundColor: entry.avatarColor }}
    >
      {entry.avatarUrl ? (
        <img
          src={entry.avatarUrl}
          alt=""
          className="h-full w-full object-cover"
          onError={(event) => {
            event.currentTarget.style.display = 'none';
          }}
        />
      ) : entry.platform === 'telegram' ? (
        <TelegramIcon className="h-5 w-5 text-white" />
      ) : (
        <span className="text-sm font-black text-white">{entry.name.charAt(0).toUpperCase()}</span>
      )}
    </div>
  );

  const renderCheck = (checked: boolean) => (
    <div
      className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md transition-all"
      style={{
        backgroundColor: checked ? 'rgba(0, 120, 212,0.6)' : 'rgba(0, 120, 212,0.08)',
        border: '1px solid rgba(76, 194, 255,0.75)',
        boxShadow: checked ? '0 0 16px rgba(0, 120, 212,0.45)' : 'none',
      }}
    >
      {checked && <Check className="h-4 w-4 text-white" strokeWidth={3} />}
    </div>
  );

  return (
    <SubPage
      title={title}
      backTo={backTo}
      rightAction={
        <button
          onClick={() => setShowAddChat(true)}
          className="flex h-9 w-9 items-center justify-center rounded-full transition-transform hover:scale-105"
          style={{ background: 'linear-gradient(135deg, #0078D4, #005A9E)', marginRight: '14px' }}
        >
          <Plus className="h-5 w-5 text-white" />
        </button>
      }
    >
      <div className="w-full pb-5" style={{ paddingTop: '15px', paddingLeft: '10px', paddingRight: '10px' }}>
        <div className="mail-card flex min-h-[54px] items-center gap-3 rounded-xl px-5 py-3">
          <Search className="h-5 w-5 shrink-0" style={{ color: '#7F8CA0', marginLeft: '15px' }} />
          <input
            type="text"
            placeholder="Поиск чатов"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="min-w-0 flex-1 bg-transparent text-[15px] text-white outline-none placeholder:text-slate-500"
          />
        </div>
        <div style={{ height: 15 }} />

        <div className="relative pb-24">
          {isLoading && (
            <div className="absolute inset-0 z-10 flex items-center justify-center bg-[#07111c]/50">
              <Loader2 className="h-8 w-8 animate-spin" style={{ color: '#4CC2FF' }} />
            </div>
          )}

          {platforms.map((platform) => {
            const platformEntries = entries.filter((entry) => entry.kind === 'chat' ? entry.chat.platform === platform : entry.platform === platform);
            const isCollapsed = collapsedPlatforms[platform];

            return (
              <div key={platform} className="mail-card mb-4 overflow-hidden rounded-xl">
                <button
                  onClick={() => setCollapsedPlatforms((prev) => ({ ...prev, [platform]: !prev[platform] }))}
                  className="flex min-h-[54px] w-full items-center justify-between py-3"
                  style={{ backgroundColor: 'rgba(255,255,255,0.025)', paddingLeft: 20, paddingRight: 20 }}
                >
                  <div className="flex min-w-0 items-center gap-3">
                    <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full" style={{ backgroundColor: platformColors[platform] }}>
                      <PlatformIcon platform={platform as ChatItem['platform']} />
                    </div>
                    <span className="truncate text-[15px] font-extrabold text-white">{platformLabels[platform]}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="flex min-w-[32px] h-[32px] items-center justify-center rounded-lg px-2 text-[15px] font-black" style={{ backgroundColor: 'rgba(0, 120, 212,0.18)', color: '#60CDFF' }}>
                      {platformEntries.length}
                    </span>
                    {isCollapsed ? <ChevronDown className="h-4 w-4 text-slate-500" /> : <ChevronUp className="h-4 w-4 text-slate-500" />}
                  </div>
                </button>

                {!isCollapsed && (
                  <div className="flex flex-col">
                    {platformEntries.map((entry) => {
                      if (entry.kind === 'forum') {
                        const expanded = expandedForums[entry.key] ?? true;
                        return (
                          <div key={entry.key} className="border-t border-white/[0.04]">
                            <button
                              onClick={() => setExpandedForums((prev) => ({ ...prev, [entry.key]: !expanded }))}
                              className="flex min-h-[64px] w-full items-center gap-4 py-4 text-left"
                              style={{ paddingLeft: 20, paddingRight: 20 }}
                            >
                              {renderAvatar(entry)}
                              <div className="min-w-0 flex-1">
                                <p className="truncate text-sm font-bold text-white">{entry.name}</p>
                                <p className="mt-0.5 text-xs" style={{ color: '#7F8CA0' }}>{entry.topics.length} тем</p>
                              </div>
                              {expanded ? <ChevronUp className="h-4 w-4 text-slate-500" /> : <ChevronDown className="h-4 w-4 text-slate-500" />}
                            </button>

                            {expanded && (
                              <div className="flex flex-col bg-black/10 px-2 pb-2">
                                {entry.topics.map((topic) => {
                                  const topicHasError = topic.status === 2 || !!topic.lastError;
                                  return (
                                  <SwipeableItem key={topic.id} onDelete={() => deleteSource(topic.id)}>
                                    <button
                                      onClick={() => toggleSource(topic.id, topic.checked)}
                                      className="flex min-h-[62px] w-full items-center gap-4 rounded-lg py-3 text-left transition-all active:scale-[0.99]"
                                      style={{
                                        paddingLeft: 20,
                                        paddingRight: 20,
                                        backgroundColor: topicHasError ? 'rgba(239,68,68,0.08)' : undefined,
                                        borderColor: topicHasError ? 'rgba(239,68,68,0.24)' : undefined,
                                      }}
                                    >
                                      {renderAvatar({ ...topic, name: getChatTitle(topic) })}
                                      <div className="min-w-0 flex-1">
                                        <p className="truncate text-[15px] font-extrabold text-white">{getChatTitle(topic)}</p>
                                        <p className="text-[13px] font-medium" style={{ color: topicHasError ? '#F87171' : '#8EA5C7' }}>
                                          {topicHasError ? getSourceErrorText(topic.lastError) : 'тема'}
                                        </p>
                                      </div>
                                      {renderCheck(topic.checked)}
                                    </button>
                                  </SwipeableItem>
                                  );
                                })}
                              </div>
                            )}
                          </div>
                        );
                      }

                      const chat = entry.chat;
                      const hasError = chat.status === 2 || !!chat.lastError;
                      return (
                        <SwipeableItem key={chat.id} onDelete={() => deleteSource(chat.id)}>
                          <button
                            onClick={() => toggleSource(chat.id, chat.checked)}
                            className="flex min-h-[68px] w-full items-center gap-4 border-t border-white/[0.04] py-4 text-left transition-all active:scale-[0.99]"
                            style={{
                              paddingLeft: 20,
                              paddingRight: 20,
                              backgroundColor: hasError ? 'rgba(239,68,68,0.08)' : undefined,
                              borderColor: hasError ? 'rgba(239,68,68,0.24)' : undefined,
                            }}
                          >
                            {renderAvatar({ ...chat, name: getChatTitle(chat) })}
                            <div className="min-w-0 flex-1">
                              <p className="truncate text-[15px] font-extrabold text-white">{getChatTitle(chat)}</p>
                              <p className="mt-0.5 text-[13px] font-medium" style={{ color: hasError ? '#F87171' : '#8EA5C7' }}>
                                {hasError ? getSourceErrorText(chat.lastError) : `${formatMembers(chat.members)} участников`}
                              </p>
                            </div>
                            {renderCheck(chat.checked)}
                          </button>
                        </SwipeableItem>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>

      <Modal isOpen={showAddChat} onClose={closeAddModal} title="Добавить чат">
        <div className="flex flex-col" style={{ gap: '5px' }}>
          {addError && (
            <div className="flex items-start gap-2 rounded-xl border border-red-500/20 bg-red-500/10 p-3 text-sm text-red-400" style={{ marginTop: '5px' }}>
              <AlertCircle className="h-5 w-5 shrink-0" />
              <span>{addError}</span>
            </div>
          )}

          {!forumTopics ? (
            <div className="flex flex-col" style={{ marginTop: addError ? '0' : '5px' }}>
              <div className="relative group">
                <div 
                  className="pointer-events-none absolute inset-y-0 left-0 flex items-center transition-colors group-focus-within:text-[#0078D4]"
                  style={{ paddingLeft: '15px' }}
                >
                  <Link className="h-5 w-5 text-slate-400 group-focus-within:text-[#0078D4] transition-colors" />
                </div>
                <input
                  type="text"
                  value={newChatLink}
                  onChange={(e) => {
                    setNewChatLink(e.target.value);
                    setAddError(null);
                  }}
                  placeholder="https://t.me/your_chat_link"
                  className="w-full rounded-2xl pr-4 text-[15px] text-white transition-all placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-[#0078D4]/40 focus:border-[#0078D4]/40"
                  style={{ 
                    height: '46px',
                    paddingLeft: '43px',
                    backgroundColor: '#101A26',
                    border: addError ? '1px solid rgba(239,68,68,0.55)' : '1px solid rgba(148,163,184,0.12)',
                    boxShadow: 'inset 0 2px 4px rgba(0,0,0,0.2)' 
                  }}
                />
              </div>
            </div>
          ) : (
            <div style={{ marginBottom: '6px' }}>
              <label className="block text-sm font-semibold text-slate-200" style={{ marginBottom: '12px' }}>Выберите темы:</label>
              <div className="flex max-h-60 flex-col gap-2 overflow-y-auto pr-1">
                {forumTopics.map((topic, idx) => {
                  const disabled = (purpose !== 0 && topic.canWrite === false) || topic.alreadyAdded;
                  const showWriteLock = purpose !== 0 && topic.canWrite === false;
                  return (
                    <label
                      key={topic.id}
                      className="flex cursor-pointer items-center gap-3 rounded-xl transition-all"
                      style={{ padding: '14px 14px', backgroundColor: disabled ? 'rgba(255,255,255,0.02)' : 'rgba(255,255,255,0.04)', opacity: disabled ? 0.55 : 1 }}
                    >
                      <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md transition-all" style={{ backgroundColor: topic.checked ? 'rgba(0, 120, 212,0.6)' : 'rgba(0, 120, 212,0.08)', border: '1px solid rgba(76, 194, 255,0.75)', boxShadow: topic.checked ? '0 0 16px rgba(0, 120, 212,0.45)' : 'none' }}>
                        {topic.checked && <Check className="h-4 w-4 text-white" strokeWidth={3} />}
                        {!topic.checked && showWriteLock && <Lock className="h-4 w-4 text-slate-400" />}
                      </div>
                      <div className="min-w-0 flex-1 flex flex-col justify-center">
                        <span className="block truncate text-sm text-white leading-tight">{topic.name}</span>
                        {topic.alreadyAdded && <span className="block text-xs text-slate-500 leading-tight">уже добавлена</span>}
                        {showWriteLock && <span className="block text-xs text-slate-500 leading-tight">писать нельзя</span>}
                      </div>
                      <input
                        type="checkbox"
                        className="hidden"
                        checked={topic.checked}
                        disabled={disabled}
                        onChange={(e) => {
                          const newTopics = [...forumTopics];
                          newTopics[idx].checked = e.target.checked;
                          setForumTopics(newTopics);
                          setAddError(null);
                        }}
                      />
                    </label>
                  );
                })}
              </div>
            </div>
          )}

          <button
            onClick={handleAddChat}
            disabled={isAdding || (!newChatLink.trim() && !forumTopics)}
            className="flex h-[54px] w-full items-center justify-center gap-2 rounded-2xl text-[15px] font-black uppercase tracking-wide text-white transition-all hover:brightness-110 active:scale-[0.98] disabled:opacity-50"
            style={{ 
              marginTop: '5px',
              background: 'linear-gradient(135deg, #0078D4, #005A9E)',
              boxShadow: '0 8px 24px -6px rgba(0, 120, 212, 0.4)'
            }}
          >
            {isAdding ? <Loader2 className="h-5 w-5 animate-spin" /> : forumTopics ? 'Сохранить' : 'Добавить'}
          </button>
        </div>
      </Modal>

      <TelegramAuthModal
        isOpen={showAuthModal}
        onClose={() => setShowAuthModal(false)}
        onSuccess={async () => {
          try {
            const me = await authApi.getMe();
            useAuthStore.getState().updateAccount(me as any);
          } catch (e) {
            console.error('Failed to refresh user info', e);
          }
          setShowAuthModal(false);
          setShowAddChat(true);
        }}
      />
    </SubPage>
  );
}
