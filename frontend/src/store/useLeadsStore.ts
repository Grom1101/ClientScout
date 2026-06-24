import { create } from 'zustand';
import { apiClient } from '../api/client';
import { mockOrders, type OrderItem } from '../data/mockData';

export interface BackendLead {
  id: string;
  profileId: string;
  sourceId: string;
  sourceName: string;
  sourceType: string; // "Telegram", "Upwork", "Kwork"
  externalId: string;
  title: string | null;
  content: string;
  originalUrl: string;
  authorUrl: string | null;
  budget: number | null;
  status: number; // 0 = New, 1 = Viewed, 2 = Responded, 3 = Hidden
  matchedKeywords: string[];
  foundAt: string; // ISO string
}

interface LeadsState {
  leads: OrderItem[];
  isLoading: boolean;
  error: string | null;
  fetchLeads: (profileId: string) => Promise<void>;
  viewLead: (id: string) => Promise<void>;
  hideLead: (id: string) => Promise<void>;
}

// Helpers
const getSourceMap = (type: string): { source: OrderItem['source'], color: string } => {
  const t = type.toLowerCase();
  if (t === 'upwork') return { source: 'upwork', color: '#14A800' };
  if (t === 'kwork' || t === 'quark') return { source: 'quark', color: '#FF6B00' };
  return { source: 'telegram', color: '#229ED9' }; // default Telegram
};

const formatTimeAgo = (isoString: string): string => {
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);

  if (diffMins < 60) return `${Math.max(1, diffMins)} мин. назад`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours} ч. назад`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays} дн. назад`;
};

const formatDate = (isoString: string): string => {
  const date = new Date(isoString);
  return date.toLocaleDateString('ru-RU') + ' / ' + date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
};

const mapToOrderItem = (lead: BackendLead): OrderItem => {
  const { source, color } = getSourceMap(lead.sourceType);
  return {
    id: lead.id,
    source,
    sourceColor: color,
    chatName: lead.sourceName,
    author: lead.authorUrl ? lead.authorUrl.split('/').pop() : undefined,
    title: lead.title || 'Новый заказ',
    description: lead.content,
    timeAgo: formatTimeAgo(lead.foundAt),
    date: formatDate(lead.foundAt),
    budget: lead.budget ? `${lead.budget} ₽` : undefined, // Assuming ruble or based on source
    link: lead.originalUrl,
    message: lead.content, // Used in detailed view
  };
};

export const useLeadsStore = create<LeadsState>((set, get) => ({
  leads: [],
  isLoading: false,
  error: null,

  fetchLeads: async (profileId: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await apiClient.get<BackendLead[]>(`/lead/profile/${profileId}`);
      // Filter out hidden leads if they come from backend just in case
      const activeLeads = response.data.filter(l => l.status !== 3);
      set({
        leads: activeLeads.map(mapToOrderItem),
        isLoading: false,
      });
    } catch (err) {
      console.error('Failed to fetch leads via API, falling back to mocks:', err);
      set({
        leads: mockOrders,
        isLoading: false,
        error: 'Failed to fetch leads.',
      });
    }
  },

  viewLead: async (id: string) => {
    try {
      // Optimistic update - if we had unread states, we'd clear it here.
      // Since mock data doesn't explicitly store 'isNew' yet, we just fire the API.
      await apiClient.post(`/lead/${id}/view`);
    } catch (err) {
      console.error('Failed to mark lead as viewed:', err);
    }
  },

  hideLead: async (id: string) => {
    // Optimistic update: remove lead from UI immediately
    const prevLeads = get().leads;
    set({ leads: prevLeads.filter(l => l.id !== id) });

    try {
      await apiClient.post(`/lead/${id}/hide`);
    } catch (err) {
      console.error('Failed to hide lead via API:', err);
      // Revert if failed
      set({ leads: prevLeads });
    }
  },
}));
