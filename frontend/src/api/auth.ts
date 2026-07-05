import { apiClient } from './client';

export interface AuthResponse {
  token: string;
  account: {
    id: string;
    email: string;
    telegramUserId: number | null;
    telegramName?: string | null;
    telegramAvatarBase64?: string | null;
    activeProfileId: string | null;
  };
}

interface AuthResultDto {
  token: string;
  accountId: string;
  email: string;
  isTelegramLinked: boolean;
  activeProfileId: string | null;
}

const toAuthResponse = async (data: AuthResultDto): Promise<AuthResponse> => {
  useTemporaryToken(data.token);

  try {
    const account = await authApi.getMe();
    return { token: data.token, account };
  } catch {
    return {
      token: data.token,
      account: {
        id: data.accountId,
        email: data.email,
        telegramUserId: null,
        activeProfileId: data.activeProfileId,
      },
    };
  }
};

const useTemporaryToken = (token: string) => {
  apiClient.defaults.headers.common.Authorization = `Bearer ${token}`;
};

export const authApi = {
  login: async (email: string, password: string, rememberMe = false): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResultDto>('/auth/login', { email, password, rememberMe });
    return toAuthResponse(response.data);
  },
  
  register: async (email: string, password: string): Promise<AuthResponse> => {
    const response = await apiClient.post<AuthResultDto>('/auth/register', { email, password });
    return toAuthResponse(response.data);
  },

  getMe: async (): Promise<AuthResponse['account']> => {
    const response = await apiClient.get<AuthResponse['account']>('/auth/me');
    return response.data;
  },

  linkTelegram: async (phone: string, phoneCodeHash: string, code: string): Promise<void> => {
    await apiClient.post('/auth/link-telegram', { phone, phoneCodeHash, code });
  }
};
