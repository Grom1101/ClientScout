import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface Account {
  id: string;
  email: string;
  telegramUserId: number | null;
  telegramName?: string | null;
  telegramAvatarBase64?: string | null;
  activeProfileId: string | null;
}

interface AuthState {
  token: string | null;
  account: Account | null;
  setAuth: (token: string, account: Account) => void;
  updateAccount: (account: Account) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      account: null,
      setAuth: (token, account) => set({ token, account }),
      updateAccount: (account) => set({ account }),
      logout: () => set({ token: null, account: null }),
    }),
    {
      name: 'auth-storage', // name of item in the storage (must be unique)
    }
  )
);
