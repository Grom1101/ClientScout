import axios from 'axios';
import { useAppStore } from '../store/useAppStore';

const HARDCODED_PROFILE_ID = '00000000-0000-0000-0000-000000000001';
const FAKE_BEARER_TOKEN = 'fake-token-123';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5184/api',
});

apiClient.interceptors.request.use((config) => {
  const stateToken = useAppStore.getState().token;
  const token = stateToken || FAKE_BEARER_TOKEN;

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  // Inject hardcoded ProfileId if needed globally (optional, but good practice if Backend expects it)
  // config.headers['X-Profile-Id'] = HARDCODED_PROFILE_ID;

  return config;
});

export { HARDCODED_PROFILE_ID };
