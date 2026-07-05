import { create } from 'zustand';
import { apiClient } from '../api/client';

export const SEARCH_INTERVAL_OPTIONS = [5, 30, 60] as const;
export const MAX_SEARCH_KEYWORDS = 20;
export const MAX_SEARCH_NEGATIVE_KEYWORDS = 10;

export interface SearchSettings {
  id: string;
  profileId: string;
  isEnabled: boolean;
  notificationsEnabled: boolean;
  intervalMinutes: number;
  userKeywords: string[];
  negativeKeywords: string[];
  needsAiExpansion: boolean;
  lastAiExpandedAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateSearchSettingsPayload {
  profileId: string;
  isEnabled: boolean;
  notificationsEnabled: boolean;
  intervalMinutes: number;
  userKeywords: string[];
  negativeKeywords: string[];
}

interface SearchSettingsState {
  settings: SearchSettings | null;
  isLoading: boolean;
  error: string | null;
  fetchSettings: (profileId: string) => Promise<void>;
  saveSettings: (payload: UpdateSearchSettingsPayload) => Promise<void>;
  setEnabled: (profileId: string, isEnabled: boolean) => Promise<void>;
}

const defaultSettings = (profileId: string): SearchSettings => ({
  id: '',
  profileId,
  isEnabled: false,
  notificationsEnabled: true,
  intervalMinutes: 30,
  userKeywords: [],
  negativeKeywords: [],
  needsAiExpansion: false,
  lastAiExpandedAt: null,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
});

const normalizeTerms = (terms: string[], max: number) => (
  Array.from(new Set(terms.map((term) => term.trim()).filter(Boolean))).slice(0, max)
);

export const useSearchSettingsStore = create<SearchSettingsState>((set, get) => ({
  settings: null,
  isLoading: false,
  error: null,

  fetchSettings: async (profileId: string) => {
    if (!profileId) return;
    set({ isLoading: true, error: null });
    try {
      const response = await apiClient.get<SearchSettings>('/search/settings', {
        params: { profileId },
      });
      set({ settings: response.data, isLoading: false });
    } catch (err) {
      console.error('Failed to fetch search settings:', err);
      set({ settings: defaultSettings(profileId), isLoading: false, error: 'Failed to fetch search settings' });
    }
  },

  saveSettings: async (payload) => {
    const safePayload = {
      ...payload,
      userKeywords: normalizeTerms(payload.userKeywords, MAX_SEARCH_KEYWORDS),
      negativeKeywords: normalizeTerms(payload.negativeKeywords, MAX_SEARCH_NEGATIVE_KEYWORDS),
    };

    set({ isLoading: true, error: null });
    try {
      const response = await apiClient.put<SearchSettings>('/search/settings', safePayload);
      set({ settings: response.data, isLoading: false });
    } catch (err: any) {
      console.error('Failed to save search settings:', err);
      set({ isLoading: false, error: err.response?.data?.message || 'Failed to save search settings' });
      throw err;
    }
  },

  setEnabled: async (profileId, isEnabled) => {
    const current = get().settings ?? defaultSettings(profileId);
    await get().saveSettings({
      profileId,
      isEnabled,
      notificationsEnabled: current.notificationsEnabled,
      intervalMinutes: current.intervalMinutes,
      userKeywords: current.userKeywords,
      negativeKeywords: current.negativeKeywords,
    });
  },
}));
