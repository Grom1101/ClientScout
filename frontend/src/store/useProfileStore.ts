import { create } from 'zustand';
import { apiClient } from '../api/client';

export interface ProfileSettings {
  notificationsEnabled: boolean;
  searchPeriodicityMinutes: number;
}

interface ProfileState {
  settings: ProfileSettings | null;
  isLoading: boolean;
  error: string | null;
  fetchSettings: (profileId: string) => Promise<void>;
  updateSettings: (profileId: string, newSettings: ProfileSettings) => Promise<void>;
}

export const useProfileStore = create<ProfileState>((set) => ({
  settings: null,
  isLoading: false,
  error: null,

  fetchSettings: async (profileId: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await apiClient.get<ProfileSettings>(`/profile/${profileId}`);
      set({ settings: response.data, isLoading: false });
    } catch (err) {
      console.error('Failed to fetch profile settings:', err);
      // Fallback
      set({
        settings: { notificationsEnabled: true, searchPeriodicityMinutes: 10 },
        isLoading: false,
        error: 'Failed to fetch settings',
      });
    }
  },

  updateSettings: async (profileId: string, newSettings: ProfileSettings) => {
    // Optimistic update
    set({ settings: newSettings });
    try {
      await apiClient.put(`/profile/${profileId}`, newSettings);
    } catch (err) {
      console.error('Failed to update profile settings:', err);
      // Not reverting for simplicity, but in prod we might
    }
  },
}));
