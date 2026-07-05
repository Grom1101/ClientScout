import { create } from 'zustand';
import { apiClient } from '../api/client';
import { authApi } from '../api/auth';
import { useAuthStore } from './useAuthStore';

export interface Profile {
  id: string;
  name: string;
  color?: string;
  isActive?: boolean;
  isDefault?: boolean;
}

interface AppState {
  activeProfile: Profile | null;
  profiles: Profile[];
  token: string | null;
  fetchProfiles: () => Promise<void>;
  setActiveProfile: (profile: Profile) => Promise<void>;
  addProfile: (name: string) => Promise<void>;
  renameProfile: (profile: Profile, name: string) => Promise<void>;
  deleteProfile: (profile: Profile) => Promise<void>;
}

export const useAppStore = create<AppState>((set, get) => ({
  activeProfile: null,
  profiles: [],
  token: null,

  fetchProfiles: async () => {
    const response = await apiClient.get<Profile[]>('/profiles');
    const profiles = response.data;
    const account = useAuthStore.getState().account;
    const activeProfile =
      profiles.find(profile => profile.id === account?.activeProfileId) ??
      profiles.find(profile => profile.isDefault) ??
      profiles[0] ??
      null;

    set({ profiles, activeProfile });
  },

  setActiveProfile: async (profile) => {
    await apiClient.put(`/profiles/${profile.id}/activate`);
    const updatedAccount = await authApi.getMe();
    useAuthStore.getState().updateAccount(updatedAccount);

    set((state) => ({
      activeProfile: profile,
      profiles: state.profiles.map(item => ({
        ...item,
        isDefault: item.id === profile.id,
      })),
    }));
  },

  addProfile: async (name) => {
    const response = await apiClient.post<Profile>('/profiles', {
      name,
      color: '#0078D4',
      keywords: null,
      negativeKeywords: null,
      minBudget: null,
      languageFilter: null,
    });

    set({ profiles: [...get().profiles, response.data] });
  },

  renameProfile: async (profile, name) => {
    const response = await apiClient.put<Profile>(`/profiles/${profile.id}`, {
      name,
      color: profile.color ?? '#0078D4',
      isActive: profile.isActive ?? true,
      keywords: null,
      negativeKeywords: null,
      minBudget: null,
      languageFilter: null,
    });

    set((state) => ({
      activeProfile: state.activeProfile?.id === profile.id ? response.data : state.activeProfile,
      profiles: state.profiles.map(item => item.id === profile.id ? response.data : item),
    }));
  },

  deleteProfile: async (profile) => {
    await apiClient.delete(`/profiles/${profile.id}`);
    const updatedAccount = await authApi.getMe();
    useAuthStore.getState().updateAccount(updatedAccount);

    set((state) => {
      const profiles = state.profiles.filter(item => item.id !== profile.id);
      const activeProfile =
        profiles.find(item => item.id === updatedAccount.activeProfileId) ??
        profiles.find(item => item.isDefault) ??
        profiles[0] ??
        null;

      return { profiles, activeProfile };
    });
  },
}));
