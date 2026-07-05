import { create } from 'zustand';
import { apiClient, getActiveProfileId } from '../api/client';
import { useSearchSettingsStore } from './useSearchSettingsStore';

export interface SearchRuntimeStatus {
  isEnabled: boolean;
  intervalMinutes: number;
  notificationsEnabled: boolean;
  botConnected: boolean;
  lastCheckedAt: string | null;
  nextRunAt: string | null;
}

export interface ExchangeConnection {
  id: string;
  profileId: string;
  exchangeType: number;
  status: number;
  isConnected: boolean;
  requiresReconnect: boolean;
  lastCheckedAt: string | null;
  lastError: string | null;
  updatedAt: string;
}

export interface KworkLoginFlowStatus {
  flowId: string;
  status: string;
  isCompleted: boolean;
  isFailed: boolean;
  error: string | null;
}

interface SearchRuntimeState {
  status: SearchRuntimeStatus | null;
  exchanges: ExchangeConnection[];
  isLoading: boolean;
  fetchStatus: (profileId?: string) => Promise<void>;
  fetchExchanges: (profileId?: string) => Promise<void>;
  startKworkLogin: (profileId?: string) => Promise<KworkLoginFlowStatus>;
  disconnectKwork: (profileId?: string) => Promise<void>;
  fetchKworkLoginStatus: (flowId: string) => Promise<KworkLoginFlowStatus>;
}

export const useSearchRuntimeStore = create<SearchRuntimeState>((set, get) => ({
  status: null,
  exchanges: [],
  isLoading: false,

  fetchStatus: async (profileId = getActiveProfileId()) => {
    if (!profileId) return;
    const response = await apiClient.get<SearchRuntimeStatus>('/search/status', { params: { profileId } });
    set({ status: response.data });
  },

  fetchExchanges: async (profileId = getActiveProfileId()) => {
    if (!profileId) return;
    set({ isLoading: true });
    try {
      const response = await apiClient.get<ExchangeConnection[]>('/search/exchanges', { params: { profileId } });
      set({ exchanges: response.data, isLoading: false });
    } catch (error) {
      console.error('Failed to fetch exchanges:', error);
      set({ isLoading: false });
    }
  },

  startKworkLogin: async (profileId = getActiveProfileId()) => {
    const response = await apiClient.post<KworkLoginFlowStatus>('/search/exchanges/login/start', {
      profileId,
      exchangeType: 0,
    });
    return response.data;
  },

  disconnectKwork: async (profileId = getActiveProfileId()) => {
    if (!profileId) return;
    await apiClient.post('/search/exchanges/disconnect', {
      profileId,
      exchangeType: 0,
    });
    await get().fetchExchanges(profileId);
    await get().fetchStatus(profileId);
    await useSearchSettingsStore.getState().fetchSettings(profileId);
  },

  fetchKworkLoginStatus: async (flowId: string) => {
    const response = await apiClient.get<KworkLoginFlowStatus>(`/search/exchanges/login/${flowId}`);
    if (response.data.isCompleted) {
      await get().fetchExchanges();
      await get().fetchStatus();
      await useSearchSettingsStore.getState().fetchSettings(getActiveProfileId());
    }
    return response.data;
  },
}));
