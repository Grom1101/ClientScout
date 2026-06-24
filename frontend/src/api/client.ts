import axios from 'axios';
import { useAppStore } from '../store/useAppStore';

export const apiClient = axios.create({
  baseURL: 'http://localhost:5184/api', // local testing, in prod use env
});

apiClient.interceptors.request.use((config) => {
  const token = useAppStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
