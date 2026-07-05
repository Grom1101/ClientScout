import { create } from 'zustand';
import { apiClient, getActiveProfileId } from '../api/client';

export const MAX_MESSAGE_LENGTH = 1024;
export const MAX_ATTACHMENT_COUNT = 1;
export const MAX_ATTACHMENT_SIZE = 20 * 1024 * 1024;
export const ALLOWED_ATTACHMENT_EXTENSIONS = ['png', 'jpg', 'jpeg', 'pdf', 'doc', 'docx', 'txt'];

export interface MessageTemplate {
  id: string;
  name: string;
  content: string;
  attachmentUrls?: string[];
  createdAt: string;
}

export interface Campaign {
  id: string;
  profileId: string;
  templateId: string;
  targetChatsJson: string;
  delayMinSec: number;
  delayMaxSec: number;
  status: number;
  periodicityMinutes: number;
  scheduleMode: 'allday' | 'custom';
  scheduleStartTime?: string | null;
  scheduleEndTime?: string | null;
  timezoneOffsetMinutes: number;
  sentCount: number;
  errorCount: number;
  createdAt: string;
}

export interface OutreachActivityPoint {
  label: string;
  sent: number;
  errors: number;
  leads: number;
}

export interface RecentOutreachLog {
  id: string;
  chatName: string;
  profileName: string;
  messagePreview: string;
  matchedKeyword?: string | null;
  status: number;
  errorMessage?: string | null;
  sentAt: string;
}

export interface OutreachStats {
  sentToday: number;
  leadsToday: number;
  activity: OutreachActivityPoint[];
  recentLogs: RecentOutreachLog[];
}

interface OutreachState {
  templates: MessageTemplate[];
  activeCampaign: Campaign | null;
  stats: OutreachStats | null;
  isLoading: boolean;
  error: string | null;
  periodicityMinutes: number;
  scheduleMode: 'allday' | 'custom';
  scheduleStartTime: string;
  scheduleEndTime: string;
  setSchedule: (settings: { periodicityMinutes: number; scheduleMode: 'allday' | 'custom'; scheduleStartTime: string; scheduleEndTime: string }) => Promise<void>;
  fetchTemplates: (profileId: string) => Promise<void>;
  saveTemplate: (profileId: string, content: string, attachmentUrls?: string[]) => Promise<void>;
  uploadFile: (file: File) => Promise<string>;
  fetchActiveCampaign: (profileId: string) => Promise<void>;
  fetchStats: (profileId: string, period: 'today' | 'month') => Promise<void>;
  isStatsLoading: boolean;
  startCampaign: (templateId: string, sourceIds: string[]) => Promise<void>;
  stopCampaign: () => Promise<void>;
  stopCampaignIfRunning: () => Promise<void>;
}

const getFileExtension = (fileName: string) => fileName.split('.').pop()?.toLowerCase() || '';

export const useOutreachStore = create<OutreachState>((set, get) => ({
  templates: [],
  activeCampaign: null,
  stats: null,
  isLoading: false,
  isStatsLoading: false,
  error: null,
  periodicityMinutes: 30,
  scheduleMode: 'allday',
  scheduleStartTime: '09:00',
  scheduleEndTime: '23:00',

  setSchedule: async (settings) => {
    await get().stopCampaignIfRunning();
    set({ ...settings, periodicityMinutes: Math.max(5, settings.periodicityMinutes) });
  },

  fetchTemplates: async (profileId: string) => {
    set({ isLoading: true, error: null });
    try {
      const response = await apiClient.get<MessageTemplate[]>(`/outreach/profiles/${profileId}/templates`);
      set({ templates: response.data, isLoading: false });
    } catch (err) {
      console.error('Failed to fetch templates:', err);
      set({ isLoading: false, error: 'Failed to fetch templates' });
    }
  },

  saveTemplate: async (profileId: string, content: string, attachmentUrls: string[] = []) => {
    set({ isLoading: true });
    try {
      await get().stopCampaignIfRunning();
      const safeContent = content.slice(0, MAX_MESSAGE_LENGTH);
      const safeAttachments = attachmentUrls.slice(0, MAX_ATTACHMENT_COUNT);
      const { templates } = get();

      if (templates.length > 0) {
        const tpl = templates[0];
        const response = await apiClient.put<MessageTemplate>(`/outreach/templates/${tpl.id}`, {
          name: 'Основной шаблон',
          content: safeContent,
          attachmentUrls: safeAttachments,
        });
        set({ templates: [response.data], isLoading: false });
      } else {
        const response = await apiClient.post<MessageTemplate>('/outreach/templates', {
          profileId,
          name: 'Основной шаблон',
          content: safeContent,
          attachmentUrls: safeAttachments,
        });
        set({ templates: [response.data], isLoading: false });
      }
    } catch (err) {
      console.error('Failed to save template:', err);
      set({ isLoading: false, error: 'Failed to save template' });
    }
  },

  uploadFile: async (file: File): Promise<string> => {
    try {
      const extension = getFileExtension(file.name);
      if (!ALLOWED_ATTACHMENT_EXTENSIONS.includes(extension)) {
        throw new Error('Можно прикрепить только PNG, JPG, JPEG, PDF, DOC, DOCX или TXT.');
      }
      if (file.size > MAX_ATTACHMENT_SIZE) {
        throw new Error('Файл не должен превышать 20 MB.');
      }

      const formData = new FormData();
      formData.append('file', file);
      const response = await apiClient.post<{ url?: string; Url?: string }>('/file/upload', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      const uploadedUrl = response.data.url || response.data.Url;
      if (!uploadedUrl) {
        throw new Error('Сервер не вернул ссылку на файл.');
      }
      return uploadedUrl;
    } catch (err: any) {
      console.error('Failed to upload file:', err);
      if (err.response) {
        throw new Error(err.response.data?.message || err.response.data || 'Ошибка сервера при загрузке.');
      }
      throw err;
    }
  },

  fetchActiveCampaign: async (profileId: string) => {
    try {
      const response = await apiClient.get<Campaign[]>(`/outreach/profiles/${profileId}/campaigns`);
      if (response.data.length > 0) {
        const sorted = response.data.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        const activeCampaign = sorted[0];
        set({
          activeCampaign,
          periodicityMinutes: activeCampaign.periodicityMinutes,
          scheduleMode: activeCampaign.scheduleMode || 'allday',
          scheduleStartTime: activeCampaign.scheduleStartTime || '09:00',
          scheduleEndTime: activeCampaign.scheduleEndTime || '23:00',
        });
      } else {
        set({ activeCampaign: null });
      }
    } catch (err) {
      console.error('Failed to fetch campaigns:', err);
    }
  },

  fetchStats: async (profileId: string, period: 'today' | 'month') => {
    set({ isStatsLoading: true });
    try {
      const tzOffset = -new Date().getTimezoneOffset(); // UTC+3 → +180
      const response = await apiClient.get<OutreachStats>(`/outreach/profiles/${profileId}/stats`, {
        params: { period, timezoneOffsetMinutes: tzOffset },
      });
      set({ stats: response.data, isStatsLoading: false });
    } catch (err) {
      console.error('Failed to fetch outreach stats:', err);
      set({ isStatsLoading: false });
    }
  },

  startCampaign: async (templateId: string, sourceIds: string[]) => {
    set({ isLoading: true });
    try {
      const payload = {
        profileId: getActiveProfileId(),
        templateId,
        targetChatsJson: JSON.stringify(sourceIds),
        delayMinSec: 1,
        delayMaxSec: 3,
        periodicityMinutes: Math.max(5, get().periodicityMinutes),
        scheduleMode: get().scheduleMode,
        scheduleStartTime: get().scheduleStartTime,
        scheduleEndTime: get().scheduleEndTime,
        timezoneOffsetMinutes: new Date().getTimezoneOffset(),
      };

      const createResp = await apiClient.post<Campaign>('/outreach/campaigns', payload);
      const startResp = await apiClient.put<Campaign>(`/outreach/campaigns/${createResp.data.id}/status`, null, {
        params: { status: 1 },
      });

      set({ activeCampaign: startResp.data, isLoading: false });
    } catch (err) {
      console.error('Failed to start campaign:', err);
      set({ isLoading: false, error: 'Failed to start campaign' });
    }
  },

  stopCampaign: async () => {
    const { activeCampaign } = get();
    if (!activeCampaign) return;

    set({ isLoading: true });
    try {
      const response = await apiClient.put<Campaign>(`/outreach/campaigns/${activeCampaign.id}/status`, null, {
        params: { status: 2 },
      });
      set({ activeCampaign: response.data, isLoading: false });
    } catch (err) {
      console.error('Failed to stop campaign:', err);
      set({ isLoading: false, error: 'Failed to stop campaign' });
    }
  },

  stopCampaignIfRunning: async () => {
    const { activeCampaign, stopCampaign } = get();
    if (activeCampaign?.status === 1) {
      await stopCampaign();
    }
  },
}));
