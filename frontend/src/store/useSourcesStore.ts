import { create } from 'zustand';
import { apiClient, getActiveProfileId } from '../api/client';
import { mockChats, type ChatItem } from '../data/mockData';
import { useOutreachStore } from './useOutreachStore';
import { useSearchSettingsStore } from './useSearchSettingsStore';

export interface BackendSource {
  id: string;
  profileId: string;
  type: number;
  purpose: number;
  name: string;
  url: string;
  status: number;
  memberCount?: number;
  avatarUrl?: string;
  baseUrl?: string;
  topicId?: string;
  topicName?: string;
  isForumTopic?: boolean;
  lastError?: string;
}

export interface SourceTopic {
  id: string;
  name: string;
  canWrite?: boolean;
}

interface SourcesState {
  sources: ChatItem[];
  isLoading: boolean;
  error: string | null;
  fetchSources: (purpose: number) => Promise<void>;
  addSource: (url: string, name: string, purpose: number, platform: string) => Promise<void>;
  deleteSource: (id: string) => Promise<void>;
  toggleSource: (id: string, currentStatus: boolean) => Promise<void>;
  validateSource: (url: string, purpose?: number) => Promise<{ isValid?: boolean; errorCode?: string; isForum?: boolean; topics?: SourceTopic[] }>;
}

const mapTypeToPlatform = (type: number): ChatItem['platform'] => {
  switch (type) {
    case 0: return 'telegram';
    case 1: return 'kwork';
    default: return 'telegram';
  }
};

const mapPlatformToType = (platform: string): number => {
  if (platform === 'telegram') return 0;
  if (platform === 'kwork') return 1;
  if (platform === 'whatsapp') return 0;
  if (platform === 'slack') return 0;
  return 0;
};

const platformColors: Record<string, string> = {
  telegram: '#229ED9',
  kwork: '#FF7B00',
  whatsapp: '#25D366',
  slack: '#E11D48',
};

const getStableMemberCount = (url: string) => {
  const hash = Math.abs(url.split('').reduce((a, b) => ((a << 5) - a) + b.charCodeAt(0), 0));
  return (hash % 15000) + 500;
};

const getTelegramUserId = () => {
  const tg = (window as any).Telegram?.WebApp;
  return tg?.initDataUnsafe?.user?.id?.toString() || '1';
};

export const normalizeTelegramUrl = (url: string) => {
  const value = url.trim().replace(/^https?:\/\//i, '').replace(/^www\./i, '').replace(/\/$/, '');
  return value.toLowerCase();
};

export const cleanTelegramName = (value: string) => (
  value
    .replace(/^https?:\/\/(www\.)?t\.me\//i, '')
    .replace(/^@/, '')
    .replace(/\/\d+$/, '')
    .replace(/\/$/, '')
);

const getDisplayName = (source: BackendSource) => {
  if (source.isForumTopic && source.topicName) return source.topicName;
  if (source.name) return source.name;
  return cleanTelegramName(source.baseUrl || source.url);
};

const mapToChatItem = (source: BackendSource): ChatItem => {
  const platform = mapTypeToPlatform(source.type);
  return {
    id: source.id,
    platform,
    name: getDisplayName(source),
    username: source.url,
    members: source.memberCount ?? getStableMemberCount(source.url),
    avatarColor: platformColors[platform] || '#64748B',
    avatarUrl: source.avatarUrl,
    baseUrl: source.baseUrl,
    topicId: source.topicId,
    topicName: source.topicName,
    forumName: source.name,
    isForumTopic: source.isForumTopic,
    checked: source.status === 1,
    purpose: source.purpose,
    status: source.status,
    lastError: source.lastError,
  };
};

export const useSourcesStore = create<SourcesState>((set, get) => ({
  sources: [],
  isLoading: false,
  error: null,

  fetchSources: async (purpose: number) => {
    set({ isLoading: true, error: null });
    try {
      const response = await apiClient.get<BackendSource[]>('/sources', {
        params: { purpose }
      });

      set({
        sources: response.data.map(mapToChatItem),
        isLoading: false
      });
    } catch (err) {
      console.error('API fetch failed, falling back to mocks:', err);
      set({
        sources: mockChats,
        isLoading: false,
        error: 'Failed to fetch sources. Using mock data.'
      });
    }
  },

  validateSource: async (url: string, purpose = 1) => {
    try {
      const response = await apiClient.get('/sources/validate', {
        params: { url, purpose },
        headers: {
          'X-User-Id': getTelegramUserId()
        }
      });
      return response.data;
    } catch (err: any) {
      if (err.response?.data?.message) {
        throw new Error(err.response.data.message);
      }
      throw new Error('UNKNOWN_ERROR');
    }
  },

  addSource: async (url: string, name: string, purpose: number, platform: string) => {
    const { sources } = get();
    const normalizedNew = normalizeTelegramUrl(url);
    if (sources.some((s) => normalizeTelegramUrl(s.username) === normalizedNew)) {
      throw new Error('DUPLICATE_CHAT');
    }

    try {
      if (purpose === 1) {
        await useOutreachStore.getState().stopCampaignIfRunning();
      }
      const payload = {
        profileId: getActiveProfileId(),
        type: mapPlatformToType(platform),
        purpose,
        name,
        url,
      };

      const response = await apiClient.post<BackendSource>('/sources', payload, {
        headers: {
          'X-User-Id': getTelegramUserId()
        }
      });
      const newChat = mapToChatItem(response.data);

      set((state) => ({ sources: [...state.sources, newChat] }));
      await useSearchSettingsStore.getState().fetchSettings(getActiveProfileId());
    } catch (err: any) {
      console.error('Failed to add source via API:', err);
      throw new Error(err.response?.data?.message || 'ADD_SOURCE_FAILED');
    }
  },

  deleteSource: async (id: string) => {
    try {
      const source = get().sources.find(s => s.id === id);
      if (source && source.purpose === 1) {
        await useOutreachStore.getState().stopCampaignIfRunning();
      }
      await apiClient.delete(`/sources/${id}`);
      set((state) => ({ sources: state.sources.filter((s) => s.id !== id) }));
      await useSearchSettingsStore.getState().fetchSettings(getActiveProfileId());
    } catch (err) {
      console.error('Failed to delete source via API, updating locally:', err);
      set((state) => ({ sources: state.sources.filter((s) => s.id !== id) }));
    }
  },

  toggleSource: async (id: string, currentStatus: boolean) => {
    const newStatus = currentStatus ? 0 : 1;
    try {
      const source = get().sources.find(s => s.id === id);
      if (source && source.purpose === 1) {
        await useOutreachStore.getState().stopCampaignIfRunning();
      }
      set((state) => ({
        sources: state.sources.map((s) => s.id === id ? { ...s, checked: !currentStatus } : s)
      }));

      await apiClient.put(`/sources/${id}`, { status: newStatus });
      await useSearchSettingsStore.getState().fetchSettings(getActiveProfileId());
    } catch (err) {
      console.error('Failed to toggle source via API, reverting locally:', err);
      set((state) => ({
        sources: state.sources.map((s) => s.id === id ? { ...s, checked: currentStatus } : s)
      }));
    }
  },
}));
