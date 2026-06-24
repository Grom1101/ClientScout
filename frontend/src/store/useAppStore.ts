import { create } from 'zustand';
import { mockProfiles, type Profile } from '../data/mockData';

interface AppState {
  activeProfile: Profile;
  profiles: Profile[];
  token: string | null;
  setActiveProfile: (profile: Profile) => void;
  addProfile: (name: string) => void;
}

export const useAppStore = create<AppState>((set) => ({
  activeProfile: mockProfiles[0],
  profiles: mockProfiles,
  token: null,
  setActiveProfile: (profile) => set({ activeProfile: profile }),
  addProfile: (name) =>
    set((state) => {
      const newProfile: Profile = { id: String(Date.now()), name };
      return { profiles: [...state.profiles, newProfile] };
    }),
}));
