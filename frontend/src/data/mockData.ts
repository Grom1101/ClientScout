/* ─── Interfaces ─── */

export interface ChatItem {
  id: string;
  platform: 'telegram' | 'whatsapp' | 'slack';
  name: string;
  username: string;
  members: number;
  avatarColor: string;
  checked: boolean;
}

export interface OrderItem {
  id: string;
  source: 'telegram' | 'upwork' | 'quark';
  sourceColor: string;
  chatName?: string;
  author?: string;
  title: string;
  description: string;
  timeAgo: string;
  budget?: string;
  date?: string;
  message?: string;
  link?: string;
}

export interface Exchange {
  id: string;
  name: string;
  color: string;
  initial: string;
  connected: boolean;
}

export interface MailingEntry {
  id: string;
  chatName: string;
  segment: string;
  time: string;
  count: number;
}

export interface Profile {
  id: string;
  name: string;
}

/* ─── Mock Data ─── */

export const mockProfiles: Profile[] = [
  { id: '1', name: 'Frontend Russian' },
  { id: '2', name: 'Frontend English' },
];

export const mockChats: ChatItem[] = [
  { id: '1', platform: 'telegram', name: 'Design Jobs', username: '@design_jobs', members: 12340, avatarColor: '#7C3AED', checked: true },
  { id: '2', platform: 'telegram', name: 'Startup Founders', username: '@startup_founders', members: 8785, avatarColor: '#10B981', checked: true },
  { id: '3', platform: 'telegram', name: 'Freelance RU', username: '@freelance_ru', members: 15210, avatarColor: '#3B82F6', checked: true },
  { id: '4', platform: 'telegram', name: 'Remote Work', username: '@remote_work', members: 10876, avatarColor: '#F59E0B', checked: false },
  { id: '5', platform: 'telegram', name: 'Marketing Ideas', username: '@marketing_ideas', members: 9321, avatarColor: '#EC4899', checked: true },
  { id: '6', platform: 'telegram', name: 'Content Jobs', username: '@content_jobs', members: 5103, avatarColor: '#06B6D4', checked: false },
  { id: '7', platform: 'whatsapp', name: 'Freelance Work', username: 'Freelance Work', members: 1240, avatarColor: '#22C55E', checked: true },
  { id: '8', platform: 'whatsapp', name: 'Content Jobs', username: 'Content Jobs', members: 890, avatarColor: '#22C55E', checked: false },
  { id: '9', platform: 'slack', name: '#job-posts', username: '#job-posts', members: 847, avatarColor: '#E11D48', checked: true },
];

export const mockOrders: OrderItem[] = [
  {
    id: '1',
    source: 'telegram',
    sourceColor: '#229ED9',
    chatName: '@design_jobs',
    author: '@design_dev',
    title: 'Ищу контент-разработчика для IT проекта',
    description: 'Нужен специалист для разработки стратегии контента и ведения соцсетей.',
    timeAgo: '5 мин. назад',
    date: '12.05.2024 / 14:30',
    message: 'Привет! Мы ищем частного контент-менеджера для нашего IT проекта. Нужно создать стратегию, вести соцсети и писать статьи. Опыт в IT обязателен.',
  },
  {
    id: '2',
    source: 'upwork',
    sourceColor: '#14A800',
    title: 'Content Developer for SaaS Startup',
    description: 'We are looking for an experienced content developer to join our remote team...',
    timeAgo: '15 мин. назад',
    budget: '$600 — $1200',
    date: '12.05.2024',
    link: 'https://upwork.com/jobs/...',
  },
  {
    id: '3',
    source: 'quark',
    sourceColor: '#FF6B00',
    title: 'Разработка контента для онлайн-курса',
    description: 'Требуется контент-менеджер для написания контента в EdTech и SaaS.',
    timeAgo: '32 мин. назад',
    budget: '25 000 — 40 000 ₽',
    date: '12.05.2024',
    link: 'https://kwork.ru/projects/...',
  },
];

export const mockExchanges: Exchange[] = [
  { id: '1', name: 'Upwork', color: '#14A800', initial: 'Up', connected: true },
  { id: '2', name: 'Quark', color: '#3B82F6', initial: 'Q', connected: true },
  { id: '3', name: 'Freelancehunt', color: '#64748B', initial: 'Fh', connected: false },
  { id: '4', name: 'Fiverr', color: '#1DBF73', initial: 'Fi', connected: false },
  { id: '5', name: 'Toptal', color: '#204ECF', initial: 'T', connected: false },
];

export const mockMailingEntries: MailingEntry[] = [
  { id: '1', chatName: 'Дизайн студии', segment: 'Сегмент: дизайн', time: '10:30', count: 18 },
  { id: '2', chatName: 'Маркетинг агентства', segment: 'Сегмент: маркетинг', time: 'Вчера', count: 25 },
  { id: '3', chatName: 'IT компания', segment: 'Сегмент: IT', time: '2 дн. назад', count: 12 },
];

export const mockActivityData = [
  { hour: '00:00', leads: 5, mailings: 3 },
  { hour: '03:00', leads: 10, mailings: 6 },
  { hour: '06:00', leads: 18, mailings: 10 },
  { hour: '09:00', leads: 32, mailings: 20 },
  { hour: '12:00', leads: 52, mailings: 35 },
  { hour: '15:00', leads: 68, mailings: 48 },
  { hour: '18:00', leads: 82, mailings: 60 },
  { hour: '21:00', leads: 92, mailings: 70 },
  { hour: '23:59', leads: 100, mailings: 78 },
];

/** Determines platform from a chat link URL */
export function detectPlatform(url: string): ChatItem['platform'] {
  if (url.includes('t.me') || url.includes('telegram')) return 'telegram';
  if (url.includes('wa.me') || url.includes('whatsapp') || url.includes('chat.whatsapp')) return 'whatsapp';
  return 'slack';
}

/** Format large numbers (12340 → "12 340") */
export function formatMembers(n: number): string {
  return n.toLocaleString('ru-RU');
}
