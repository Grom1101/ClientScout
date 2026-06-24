import { create } from 'zustand';
import { apiClient, HARDCODED_PROFILE_ID } from '../api/client';
import { mockChats, type ChatItem } from '../data/mockData';

export interface BackendSource {
  id: string;
  profileId: string;
  type: number; // 0 - Telegram, 1 - Kwork, 2 - Upwork
  purpose: number; // 0 - SearchChat, 1 - OutreachChat
  name: string;
  url: string;
  status: number; // 0 = Pending/Выключен, 1 = Active/Включен
  memberCount?: number; // (To be provided by backend)
  avatarUrl?: string;   // (To be provided by backend)
}

interface SourcesState {
  sources: ChatItem[];
  isLoading: boolean;
  error: string | null;
  fetchSources: (purpose: number) => Promise<void>;
  addSource: (url: string, name: string, purpose: number, platform: string) => Promise<void>;
  deleteSource: (id: string) => Promise<void>;
  toggleSource: (id: string, currentStatus: boolean) => Promise<void>;
}

// Helpers to map BackendSource <-> ChatItem
const mapTypeToPlatform = (type: number): ChatItem['platform'] => {
  switch (type) {
    case 0: return 'telegram';
    // Mocks for others if needed
    default: return 'telegram';
  }
};

const mapPlatformToType = (platform: string): number => {
  if (platform === 'telegram') return 0;
  if (platform === 'whatsapp') return 0; // Backend only knows Telegram, Kwork, Upwork for now
  if (platform === 'slack') return 0;
  return 0;
};

const platformColors: Record<string, string> = {
  telegram: '#229ED9',
  whatsapp: '#25D366',
  slack: '#E11D48',
};

const getStableMemberCount = (url: string) => {
  const hash = Math.abs(url.split('').reduce((a, b) => ((a << 5) - a) + b.charCodeAt(0), 0));
  return (hash % 15000) + 500;
};

const mapToChatItem = (source: BackendSource): ChatItem => {
  const platform = mapTypeToPlatform(source.type);
  return {
    id: source.id,
    platform: platform,
    name: source.name,
    username: source.url,
    members: source.memberCount ?? getStableMemberCount(source.url),
    avatarColor: platformColors[platform] || '#64748B',
    avatarUrl: source.avatarUrl,
    checked: source.status === 1,
  };
};

export const useSourcesStore = create<SourcesState>((set, get) => ({
  sources: [],
  isLoading: false,
  error: null,

  fetchSources: async (purpose: number) => {
    set({ isLoading: true, error: null });
    try {
      // Assuming GET /api/sources?purpose={purpose} or similar
      // If the backend doesn't filter by purpose in GET, we might need to filter locally
      const response = await apiClient.get<BackendSource[]>('/sources', {
        params: { purpose }
      });
      
      set({ 
        sources: response.data.map(mapToChatItem),
        isLoading: false 
      });
    } catch (err) {
      console.error('API fetch failed, falling back to mocks:', err);
      // Fallback to mock data
      set({ 
        sources: mockChats, // In a real scenario we'd filter mockChats by purpose if needed
        isLoading: false, 
        error: 'Failed to fetch sources. Using mock data.' 
      });
    }
  },

  addSource: async (url: string, name: string, purpose: number, platform: string) => {
    // Check for duplicates locally first
    const { sources } = get();
    const normalizeUrl = (u: string) => u.toLowerCase().replace(/^https?:\/\//, '').replace(/\/$/, '');
    const normalizedNew = normalizeUrl(url);
    if (sources.some((s: ChatItem) => normalizeUrl(s.username) === normalizedNew)) {
      throw new Error('DUPLICATE_CHAT');
    }

    try {
      const payload = {
        profileId: HARDCODED_PROFILE_ID,
        type: mapPlatformToType(platform),
        purpose,
        name,
        url,
      };
      
      const response = await apiClient.post<BackendSource>('/sources', payload);
      const newChat = mapToChatItem(response.data);
      
      set((state) => ({ sources: [...state.sources, newChat] }));
    } catch (err: any) {
      console.error('Failed to add source via API, updating locally:', err);
      // Fallback to local optimistic update
      const newChat: ChatItem = {
        id: String(Date.now()),
        platform: platform as any,
        name,
        username: url,
        members: getStableMemberCount(url),
        avatarColor: platformColors[platform] || '#64748B',
        checked: true,
      };
      set((state) => ({ sources: [...state.sources, newChat] }));
    }
  },

  deleteSource: async (id: string) => {
    try {
      await apiClient.delete(`/sources/${id}`);
      set((state) => ({ sources: state.sources.filter((s) => s.id !== id) }));
    } catch (err) {
      console.error('Failed to delete source via API, updating locally:', err);
      set((state) => ({ sources: state.sources.filter((s) => s.id !== id) }));
    }
  },

  toggleSource: async (id: string, currentStatus: boolean) => {
    const newStatus = currentStatus ? 0 : 1;
    try {
      // Optimistic update
      set((state) => ({
        sources: state.sources.map((s) => s.id === id ? { ...s, checked: !currentStatus } : s)
      }));

      // Assuming PUT /api/sources/{id} with status payload
      await apiClient.put(`/sources/${id}`, { status: newStatus });
    } catch (err) {
      console.error('Failed to toggle source via API, reverting locally:', err);
      // Revert optimistic update
      set((state) => ({
        sources: state.sources.map((s) => s.id === id ? { ...s, checked: currentStatus } : s)
      }));
    }
  },
}));
