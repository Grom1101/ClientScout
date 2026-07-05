import axios from 'axios';
import { useAuthStore } from '../store/useAuthStore';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
});

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

    if (error.response?.status === 401 || isMissingCurrentAccount) {
      useAuthStore.getState().logout();
      if (!window.location.pathname.includes('/login')) {
        window.location.assign('/login');
      }
    }

    return Promise.reject(error);
  }
);

export const getActiveProfileId = () => {
  return useAuthStore.getState().account?.activeProfileId || "";
};
