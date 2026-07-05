import { create } from 'zustand';
import { apiClient } from '../api/client';
import type { OrderItem } from '../data/mockData';

export interface BackendLead {
  id: string;
  profileId: string;
  sourceId: string;
  sourceName: string;
  sourceType: 'Telegram' | 'Kwork' | 'Upwork' | number;
  externalId: string;
  title: string | null;
  content: string;
  originalUrl: string;
  authorUrl: string | null;
  budget: number | null;
  status: number;
  matchedTerms: string[];
  score: number;
  aiConfidence: number | null;
  aiSummary: string | null;
  aiCategory: string | null;
  aiReason: string | null;
  aiStatus: number;
  foundAt: string;
  expiresAt: string;
}

interface LeadsState {
  leads: OrderItem[];
  history: OrderItem[];
  totalCount: number;
  isLoading: boolean;
  isHistoryLoading: boolean;
  error: string | null;
  fetchLeads: (profileId: string) => Promise<void>;
  fetchHistory: (profileId: string, limit?: number, offset?: number, aiFilter?: 'confirmed' | 'unverified') => Promise<void>;
  viewLead: (id: string) => Promise<void>;
  hideLead: (id: string) => Promise<void>;
}

const getSourceMap = (type: BackendLead['sourceType']): { source: OrderItem['source']; color: string } => {
  const value = typeof type === 'number'
    ? ['telegram', 'kwork', 'upwork'][type] ?? 'telegram'
    : String(type).toLowerCase();

  if (value === 'upwork') return { source: 'upwork', color: '#14A800' };
  if (value === 'kwork' || value === 'quark') return { source: 'kwork', color: '#FF6B00' };
  return { source: 'telegram', color: '#229ED9' };
};

const formatTimeAgo = (isoString: string): string => {
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = Math.max(0, now.getTime() - date.getTime());
  const diffMins = Math.floor(diffMs / 60000);

  if (diffMins < 60) return `${Math.max(1, diffMins)} \u043c\u0438\u043d. \u043d\u0430\u0437\u0430\u0434`;
  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours} \u0447. \u043d\u0430\u0437\u0430\u0434`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays} \u0434\u043d. \u043d\u0430\u0437\u0430\u0434`;
};

const formatDate = (isoString: string): string => {
  const date = new Date(isoString);
  return `${date.toLocaleDateString('ru-RU')} / ${date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}`;
};

const formatExpires = (isoString: string): string => {
  const expires = new Date(isoString);
  const now = new Date();
  const diffMs = Math.max(0, expires.getTime() - now.getTime());
  const diffHours = Math.ceil(diffMs / 3600000);

  if (diffHours <= 1) return `1 \u0447.`;
  if (diffHours <= 24) return `${diffHours} \u0447.`;
  return `${Math.ceil(diffHours / 24)} \u0434\u043d.`;
};

const normalizeText = (value: string | null | undefined): string => {
  return (value ?? '')
    .replace(/\s+/g, ' ')
    .trim();
};

const buildDescription = (lead: BackendLead): string => {
  const content = normalizeText(lead.content);
  const title = normalizeText(lead.title);
  const text = content || title || '\u041e\u043f\u0438\u0441\u0430\u043d\u0438\u0435 \u0437\u0430\u043a\u0430\u0437\u0430 \u043e\u0442\u0441\u0443\u0442\u0441\u0442\u0432\u0443\u0435\u0442';
  const maxLength = 180;

  if (text.length <= maxLength) {
    return text;
  }

  return `${text.slice(0, maxLength).trimEnd()}...`;
};

const mapToOrderItem = (lead: BackendLead): OrderItem => {
  const { source, color } = getSourceMap(lead.sourceType);
  const aiLabel = lead.aiStatus === 1
    ? '\u041f\u0440\u043e\u0432\u0435\u0440\u0435\u043d\u043e \u043d\u0435\u0439\u0440\u043e\u0441\u0435\u0442\u044c\u044e'
    : '\u041d\u0435 \u043f\u0440\u043e\u0432\u0435\u0440\u0435\u043d\u043e \u043d\u0435\u0439\u0440\u043e\u0441\u0435\u0442\u044c\u044e';

  return {
    id: lead.id,
    source,
    sourceColor: color,
    chatName: lead.sourceName,
    author: lead.authorUrl ? lead.authorUrl.split('/').filter(Boolean).pop() : undefined,
    title: lead.title || '\u041d\u043e\u0432\u044b\u0439 \u0437\u0430\u043a\u0430\u0437',
    description: buildDescription(lead),
    timeAgo: formatTimeAgo(lead.foundAt),
    date: formatDate(lead.foundAt),
    budget: lead.budget ? `${lead.budget} \u20bd` : undefined,
    link: lead.originalUrl,
    message: lead.content,
    status: lead.status,
    aiLabel,
    expiresIn: formatExpires(lead.expiresAt),
  };
};

export const useLeadsStore = create<LeadsState>((set, get) => ({
  leads: [],
  history: [],
  totalCount: 0,
  isLoading: false,
  isHistoryLoading: false,
  error: null,

  fetchLeads: async (profileId: string) => {
    set({ isLoading: true, error: null });
    try {
      const [leadsResponse, countResponse] = await Promise.all([
        apiClient.get<BackendLead[]>('/leads/recent', { params: { profileId } }),
        apiClient.get<{ count: number }>('/leads/count', { params: { profileId } }),
      ]);
      set({ leads: leadsResponse.data.map(mapToOrderItem), totalCount: countResponse.data.count, isLoading: false });
    } catch (err) {
      console.error('Failed to fetch recent leads:', err);
      set({ leads: [], isLoading: false, error: '\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u043b\u0438\u0434\u044b.' });
    }
  },

  fetchHistory: async (profileId: string, limit = 50, offset = 0, aiFilter) => {
    set({ isHistoryLoading: true, error: null });
    try {
      const response = await apiClient.get<BackendLead[]>('/leads', { params: { profileId, limit, offset, aiFilter } });
      set({ history: response.data.map(mapToOrderItem), isHistoryLoading: false });
    } catch (err) {
      console.error('Failed to fetch lead history:', err);
      set({ history: [], isHistoryLoading: false, error: '\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u0438\u0441\u0442\u043e\u0440\u0438\u044e.' });
    }
  },

  viewLead: async (id: string) => {
    set({
      leads: get().leads.map((lead) => lead.id === id ? { ...lead, status: 1 } : lead),
      history: get().history.map((lead) => lead.id === id ? { ...lead, status: 1 } : lead),
    });

    try {
      await apiClient.put(`/leads/${id}/viewed`);
    } catch (err) {
      console.error('Failed to mark lead as viewed:', err);
    }
  },

  hideLead: async (id: string) => {
    const prevLeads = get().leads;
    const prevHistory = get().history;
    set({
      leads: prevLeads.filter((lead) => lead.id !== id),
      history: prevHistory.filter((lead) => lead.id !== id),
    });

    try {
      await apiClient.delete(`/leads/${id}`);
    } catch (err) {
      console.error('Failed to hide lead via API:', err);
      set({ leads: prevLeads, history: prevHistory });
    }
  },
}));
