import { create } from 'zustand';
import { apiClient, HARDCODED_PROFILE_ID } from '../api/client';

export interface MessageTemplate {
  id: string;
  name: string;
  content: string;
  createdAt: string;
}

export interface Campaign {
  id: string;
  profileId: string;
  templateId: string;
  targetChatsJson: string;
  delayMinSec: number;
  delayMaxSec: number;
  status: number; // 0=Draft, 1=Running, 2=Paused, 3=Completed, 4=Failed
  sentCount: number;
  errorCount: number;
  createdAt: string;
}

interface OutreachState {
  templates: MessageTemplate[];
  activeCampaign: Campaign | null;
  isLoading: boolean;
  error: string | null;

  fetchTemplates: (profileId: string) => Promise<void>;
  saveTemplate: (profileId: string, content: string) => Promise<void>;
  
  fetchActiveCampaign: (profileId: string) => Promise<void>;
  startCampaign: (templateId: string, sourceIds: string[]) => Promise<void>;
  stopCampaign: () => Promise<void>;
}

export const useOutreachStore = create<OutreachState>((set, get) => ({
  templates: [],
  activeCampaign: null,
  isLoading: false,
  error: null,

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

  saveTemplate: async (profileId: string, content: string) => {
    set({ isLoading: true });
    try {
      const { templates } = get();
      if (templates.length > 0) {
        // Update existing (first one)
        const tpl = templates[0];
        const response = await apiClient.put<MessageTemplate>(`/outreach/templates/${tpl.id}`, {
          name: 'Основной шаблон',
          content
        });
        set({ templates: [response.data], isLoading: false });
      } else {
        // Create new
        const response = await apiClient.post<MessageTemplate>('/outreach/templates', {
          profileId,
          name: 'Основной шаблон',
          content
        });
        set({ templates: [response.data], isLoading: false });
      }
    } catch (err) {
      console.error('Failed to save template:', err);
      set({ isLoading: false, error: 'Failed to save template' });
    }
  },

  fetchActiveCampaign: async (profileId: string) => {
    try {
      const response = await apiClient.get<Campaign[]>(`/outreach/profiles/${profileId}/campaigns`);
      // Find the most recent active campaign (Running or Paused, usually)
      // If we just want to show the current state, maybe sort by createdAt desc
      if (response.data.length > 0) {
        const sorted = response.data.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        set({ activeCampaign: sorted[0] });
      } else {
        set({ activeCampaign: null });
      }
    } catch (err) {
      console.error('Failed to fetch campaigns:', err);
    }
  },

  startCampaign: async (templateId: string, sourceIds: string[]) => {
    set({ isLoading: true });
    try {
      const payload = {
        profileId: HARDCODED_PROFILE_ID,
        templateId,
        targetChatsJson: JSON.stringify(sourceIds),
        delayMinSec: 30,
        delayMaxSec: 90
      };

      // Create draft campaign
      const createResp = await apiClient.post<Campaign>('/outreach/campaigns', payload);
      const campaignId = createResp.data.id;

      // Start it
      const startResp = await apiClient.put<Campaign>(`/outreach/campaigns/${campaignId}/status`, null, {
        params: { status: 1 }
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
        params: { status: 2 } // 2 = Paused
      });
      set({ activeCampaign: response.data, isLoading: false });
    } catch (err) {
      console.error('Failed to stop campaign:', err);
      set({ isLoading: false, error: 'Failed to stop campaign' });
    }
  }
}));
