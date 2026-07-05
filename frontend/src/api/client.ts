import axios from 'axios';
import { useAuthStore } from '../store/useAuthStore';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
});

export const isPreviewMode = () => {
  if (typeof window === 'undefined') return false;

  const params = new URLSearchParams(window.location.search);
  if (params.get('preview') === '1') {
    window.localStorage.setItem('clientscout-preview', '1');
    return true;
  }

  return window.localStorage.getItem('clientscout-preview') === '1';
};

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const isMissingCurrentAccount =
      error.config?.url?.includes('/auth/me') &&
      error.response?.status === 404 &&
      error.response?.data?.message === 'Account not found.';

    if (!isPreviewMode() && (error.response?.status === 401 || isMissingCurrentAccount)) {
      useAuthStore.getState().logout();
      if (!window.location.pathname.includes('/login')) {
        window.location.assign('/login');
      }
    }

    return Promise.reject(error);
  }
);

export const getActiveProfileId = () => {
  if (isPreviewMode()) return 'preview-profile';
  return useAuthStore.getState().account?.activeProfileId || "";
};
