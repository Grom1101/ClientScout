import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Check, ChevronRight, LogOut, Pencil, Plus, Send, Trash2, User, Users, X } from 'lucide-react';
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import Modal from '../components/Modal';
import { useAppStore, type Profile } from '../store/useAppStore';
import { useAuthStore } from '../store/useAuthStore';
import { useOutreachStore, type OutreachActivityPoint } from '../store/useOutreachStore';
import { useLeadsStore } from '../store/useLeadsStore';
import { getActiveProfileId, apiClient } from '../api/client';

type StatsPeriod = 'today' | 'month';

export default function HomePage() {
  const navigate = useNavigate();
  const { activeProfile, profiles, fetchProfiles, setActiveProfile, addProfile, renameProfile, deleteProfile } = useAppStore();
  const { account, logout } = useAuthStore();
  const { stats, fetchStats } = useOutreachStore();
  const { fetchHistory } = useLeadsStore();

  const [showProfile, setShowProfile] = useState(false);
  const [profileView, setProfileView] = useState<'main' | 'list' | 'new'>('main');
  const [newProfileName, setNewProfileName] = useState('');
  const [editingProfileId, setEditingProfileId] = useState<string | null>(null);
  const [editingProfileName, setEditingProfileName] = useState('');
  
  const [leadsPeriod, setLeadsPeriod] = useState<StatsPeriod>('today');
  const [sentPeriod, setSentPeriod] = useState<StatsPeriod>('today');
  const [leadsChartData, setLeadsChartData] = useState<OutreachActivityPoint[]>([]);
  const [sentChartData, setSentChartData] = useState<OutreachActivityPoint[]>([]);

  useEffect(() => {
    fetchProfiles().catch((error) => console.error('Failed to load profiles', error));
  }, [fetchProfiles]);

  useEffect(() => {
    const profileId = activeProfile?.id || getActiveProfileId();
    if (!profileId) return;

    fetchStats(profileId, 'today');
    fetchHistory(profileId, 5, 0, 'confirmed');

    const fetchLocalStats = async () => {
      try {
        const tz = -new Date().getTimezoneOffset();
        const [resLeads, resSent] = await Promise.all([
          apiClient.get(`/outreach/profiles/${profileId}/stats?period=${leadsPeriod}&timezoneOffsetMinutes=${tz}`),
          apiClient.get(`/outreach/profiles/${profileId}/stats?period=${sentPeriod}&timezoneOffsetMinutes=${tz}`)
        ]);
        setLeadsChartData(resLeads.data.activity || []);
        setSentChartData(resSent.data.activity || []);
      } catch (e) {
        console.error('Failed to fetch chart stats', e);
      }
    };
    
    fetchLocalStats();
    
    const timer = window.setInterval(() => {
        fetchStats(profileId, 'today');
        fetchHistory(profileId, 5, 0, 'confirmed');
        fetchLocalStats();
    }, 15000);
    return () => window.clearInterval(timer);
  }, [activeProfile?.id, fetchStats, fetchHistory, leadsPeriod, sentPeriod]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const handleCreateProfile = async () => {
    if (!newProfileName.trim()) return;
    await addProfile(newProfileName.trim());
    setNewProfileName('');
    setProfileView('list');
  };

  const startRenameProfile = (profile: Profile) => {
    if (profile.id !== activeProfile?.id) return;
    setEditingProfileId(profile.id);
    setEditingProfileName(profile.name);
  };

  const saveRenameProfile = async () => {
    const profile = profiles.find((p) => p.id === editingProfileId);
    const name = editingProfileName.trim();
    if (!profile || !name) return;
    await renameProfile(profile, name);
    setEditingProfileId(null);
    setEditingProfileName('');
  };

  const handleSwitchProfile = async (profile: Profile) => {
    if (profile.id === activeProfile?.id) return;
    const confirmed = window.confirm(`Перейти на профиль "${profile.name}"?`);
    if (!confirmed) return;
    await setActiveProfile(profile);
    setShowProfile(false);
    setTimeout(() => setProfileView('main'), 300);
  };

  const handleDeleteProfile = async (profile: Profile) => {
    if (profile.id === activeProfile?.id) return;
    const confirmed = window.confirm(`Удалить профиль "${profile.name}"?`);
    if (!confirmed) return;
    await deleteProfile(profile);
  };

  const maxLeadsDomain = Math.max(5, ...leadsChartData.map((item) => item.leads));
  const maxSentDomain = Math.max(5, ...sentChartData.map((item) => item.sent));

  return (
    <div className="min-h-full w-full px-5 pt-5 pb-24">
      <div className="relative mx-auto flex flex-col items-center" style={{ width: '100%', marginBottom: 8, paddingTop: 8 }}>
        {/* Header */}
        <div className="flex w-full items-center justify-between mb-4 mt-2" style={{ paddingLeft: '15px', paddingRight: '15px' }}>
          <h1 className="text-[22px] font-black leading-tight text-white">Главная страница</h1>
          <button
            onClick={() => {
              setProfileView('main');
              setShowProfile(true);
            }}
            className="rounded-full flex items-center justify-center overflow-hidden shrink-0 transition-transform active:scale-95"
            style={{ 
              width: '42px', 
              height: '42px', 
              backgroundColor: '#1C2038', 
              border: '1px solid rgba(255,255,255,0.08)',
              boxShadow: '0 4px 12px rgba(0,0,0,0.1)'
            }}
          >
            {account?.telegramAvatarBase64 ? (
              <img src={account.telegramAvatarBase64} alt="Avatar" className="w-full h-full object-cover" />
            ) : (
              <div className="text-white text-sm font-bold">{account?.telegramName?.charAt(0) || 'U'}</div>
            )}
          </button>
        </div>
      </div>

      <div className="mx-auto flex w-full flex-col gap-4">
        {/* Welcome & Profile */}
        <div className="mail-card flex flex-col gap-1 w-full text-left transition-all" style={{ borderRadius: 12, padding: '15px' }}>
          <p className="text-[20px] font-extrabold text-white truncate">
            Привет, {account?.telegramName || 'Пользователь'}! 👋
          </p>
          <div className="flex items-center gap-2 mt-1">
            <span className="text-[13px] font-medium" style={{ color: '#7F8CA0' }}>Активный профиль:</span>
            <div className="flex items-center gap-1.5 px-2 py-0.5 rounded-md" style={{ backgroundColor: 'rgba(99,102,241,0.1)' }}>
              <div className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: '#10B981' }}></div>
              <span className="text-[12px] font-bold" style={{ color: '#C7D2FE' }}>{activeProfile?.name || 'Не выбран'}</span>
            </div>
          </div>
        </div>

        {/* Stat Cards */}
        <div className="grid grid-cols-2 gap-4 w-full">
          <div className="mail-card relative overflow-hidden group" style={{ borderRadius: 12, padding: '15px' }}>
            <div className="absolute -top-10 -right-10 w-32 h-32 rounded-full blur-[30px] transition-opacity" style={{ backgroundColor: 'rgba(16, 185, 129, 0.4)' }}></div>
            <p className="text-[11px] font-bold mb-1 relative z-10 tracking-wider uppercase" style={{ color: '#10B981' }}>Заказов сегодня</p>
            <div className="flex items-end justify-between relative z-10">
              <span className="text-3xl font-extrabold text-white tracking-tight">{stats?.leadsToday ?? 0}</span>
              <div className="p-1.5 rounded-xl bg-emerald-500/10">
                <Users className="w-5 h-5 text-emerald-500" />
              </div>
            </div>
          </div>
          <div className="mail-card relative overflow-hidden group" style={{ borderRadius: 12, padding: '15px' }}>
            <div className="absolute -top-10 -right-10 w-32 h-32 rounded-full blur-[30px] transition-opacity" style={{ backgroundColor: 'rgba(129, 140, 248, 0.4)' }}></div>
            <p className="text-[11px] font-bold mb-1 relative z-10 tracking-wider uppercase" style={{ color: '#818CF8' }}>Рассылок сегодня</p>
            <div className="flex items-end justify-between relative z-10">
              <span className="text-3xl font-extrabold text-white tracking-tight">{stats?.sentToday ?? 0}</span>
              <div className="p-1.5 rounded-xl bg-indigo-500/10">
                <Send className="w-5 h-5 text-indigo-500" />
              </div>
            </div>
          </div>
        </div>

        {/* Chart 1: Заказы */}
        <div className="mail-card relative overflow-hidden w-full" style={{ borderRadius: 12, padding: '15px' }}>
          <div className="absolute top-0 left-0 right-0 h-1/2 bg-gradient-to-b from-white/[0.02] to-transparent pointer-events-none"></div>
          <div className="flex items-center justify-between mb-4 relative z-10" style={{ paddingLeft: '15px', paddingRight: '15px' }}>
            <div className="flex items-center gap-2">
              <span className="text-[15px] text-white font-extrabold">График заказов</span>
            </div>
            <div className="flex rounded-full p-1 w-[150px]" style={{ backgroundColor: 'rgba(0,0,0,0.25)', border: '1px solid rgba(255,255,255,0.06)' }}>
              {(['today', 'month'] as StatsPeriod[]).map((period) => (
                <button
                  key={period}
                  onClick={() => setLeadsPeriod(period)}
                  className="flex-1 text-[10px] text-center uppercase tracking-wider py-1.5 rounded-full transition-all font-extrabold"
                  style={{
                    backgroundColor: leadsPeriod === period ? 'rgba(16, 185, 129, 0.25)' : 'transparent',
                    color: leadsPeriod === period ? '#10B981' : '#64748B',
                  }}
                >
                  {period === 'today' ? 'Сегодня' : 'Месяц'}
                </button>
              ))}
            </div>
          </div>
          <div className="relative z-10 w-full" style={{ marginLeft: '-15px' }}>
            <ResponsiveContainer width="100%" height={140}>
              <AreaChart data={leadsChartData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorLeadsOnly" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#10B981" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#10B981" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.03)" />
                <XAxis 
                  dataKey="label" 
                  tick={{ fill: '#64748B', fontSize: 11 }} 
                  axisLine={false} 
                  tickLine={false} 
                  dy={10} 
                  ticks={leadsPeriod === 'today' ? ['00:00', '06:00', '12:00', '18:00', '23:00'] : undefined}
                  minTickGap={leadsPeriod === 'today' ? undefined : 20}
                />
                <YAxis tick={{ fill: '#64748B', fontSize: 11 }} axisLine={false} tickLine={false} domain={[0, maxLeadsDomain]} allowDecimals={false} dx={-10} width={40} />
                <Tooltip
                  contentStyle={{ backgroundColor: 'rgba(28, 32, 56, 0.9)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 12, color: '#F1F5F9', fontSize: 13, backdropFilter: 'blur(8px)' }}
                  itemStyle={{ color: '#fff', fontWeight: 600 }}
                />
                <Area 
                  type="monotone" 
                  dataKey="leads" 
                  stroke="#10B981" 
                  strokeWidth={2} 
                  fillOpacity={1} 
                  fill="url(#colorLeadsOnly)" 
                  name="Найдено" 
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Chart 2: Рассылки */}
        <div className="mail-card relative overflow-hidden w-full" style={{ borderRadius: 12, padding: '15px' }}>
          <div className="absolute top-0 left-0 right-0 h-1/2 bg-gradient-to-b from-white/[0.02] to-transparent pointer-events-none"></div>
          <div className="flex items-center justify-between mb-4 relative z-10" style={{ paddingLeft: '15px', paddingRight: '15px' }}>
            <div className="flex items-center gap-2">
              <span className="text-[15px] text-white font-extrabold">График рассылок</span>
            </div>
            <div className="flex rounded-full p-1 w-[150px]" style={{ backgroundColor: 'rgba(0,0,0,0.25)', border: '1px solid rgba(255,255,255,0.06)' }}>
              {(['today', 'month'] as StatsPeriod[]).map((period) => (
                <button
                  key={period}
                  onClick={() => setSentPeriod(period)}
                  className="flex-1 text-[10px] text-center uppercase tracking-wider py-1.5 rounded-full transition-all font-extrabold"
                  style={{
                    backgroundColor: sentPeriod === period ? 'rgba(129, 140, 248, 0.25)' : 'transparent',
                    color: sentPeriod === period ? '#818CF8' : '#64748B',
                  }}
                >
                  {period === 'today' ? 'Сегодня' : 'Месяц'}
                </button>
              ))}
            </div>
          </div>
          <div className="relative z-10 w-full" style={{ marginLeft: '-15px' }}>
            <ResponsiveContainer width="100%" height={140}>
              <AreaChart data={sentChartData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorSentOnly" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#818CF8" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#818CF8" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.03)" />
                <XAxis 
                  dataKey="label" 
                  tick={{ fill: '#64748B', fontSize: 11 }} 
                  axisLine={false} 
                  tickLine={false} 
                  dy={10} 
                  ticks={sentPeriod === 'today' ? ['00:00', '06:00', '12:00', '18:00', '23:00'] : undefined}
                  minTickGap={sentPeriod === 'today' ? undefined : 20}
                />
                <YAxis tick={{ fill: '#64748B', fontSize: 11 }} axisLine={false} tickLine={false} domain={[0, maxSentDomain]} allowDecimals={false} dx={-10} width={40} />
                <Tooltip
                  contentStyle={{ backgroundColor: 'rgba(28, 32, 56, 0.9)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 12, color: '#F1F5F9', fontSize: 13, backdropFilter: 'blur(8px)' }}
                  itemStyle={{ color: '#fff', fontWeight: 600 }}
                />
                <Area 
                  type="monotone" 
                  dataKey="sent" 
                  stroke="#818CF8" 
                  strokeWidth={2} 
                  fillOpacity={1} 
                  fill="url(#colorSentOnly)" 
                  name="Отправлено" 
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>

      <Modal 
        isOpen={showProfile} 
        onClose={() => {
          setShowProfile(false);
          setTimeout(() => setProfileView('main'), 300);
        }}
        onBack={profileView !== 'main' ? () => setProfileView('main') : undefined}
        title={profileView === 'main' ? 'Аккаунт' : profileView === 'list' ? 'Профили' : 'Новый профиль'}
      >
        <div className="relative w-full">
          {/* Main View - Always in DOM to dictate exact modal height */}
          <div 
            className={`transition-all duration-300 ease-[cubic-bezier(0.23,1,0.32,1)] ${profileView === 'main' ? 'opacity-100 translate-x-0' : 'opacity-0 -translate-x-8 pointer-events-none'}`}
          >
            {/* Avatar block */}
            <div className="flex items-center gap-5 px-1" style={{ marginBottom: '15px' }}>
              <div className="shrink-0 rounded-full flex items-center justify-center text-xl font-bold overflow-hidden shadow-[0_4px_20px_rgba(99,102,241,0.3)]" style={{ width: '64px', height: '64px', minWidth: '64px', minHeight: '64px', backgroundColor: '#6366F1', color: 'white' }}>
                {account?.telegramAvatarBase64 ? (
                  <img src={account.telegramAvatarBase64} alt="Avatar" className="w-full h-full object-cover" />
                ) : (
                  (account?.telegramName || account?.email || '?').charAt(0).toUpperCase()
                )}
              </div>
              <div className="min-w-0">
                <p className="font-bold text-white text-[19px] truncate">{account?.telegramName || account?.email}</p>
                <p className="text-sm truncate mt-0.5" style={{ color: '#94A3B8' }}>ID: {account?.id}</p>
              </div>
            </div>

            {/* Buttons */}
            <div className="flex flex-col gap-3">
              <button
                onClick={() => setProfileView('list')}
                className="flex items-center justify-between w-full rounded-2xl transition-all active:scale-[0.98]"
                style={{ 
                  height: '56px',
                  paddingLeft: '15px',
                  paddingRight: '15px',
                  backgroundColor: 'rgba(255, 255, 255, 0.03)',
                  border: '1px solid rgba(255, 255, 255, 0.08)',
                  backdropFilter: 'blur(12px)',
                }}
              >
                <div className="flex items-center gap-3">
                  <User className="w-5 h-5" style={{ color: '#94A3B8' }} />
                  <span className="text-[15px] font-medium text-white">Профили</span>
                </div>
                <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
              </button>

              <button
                onClick={() => { setShowProfile(false); handleLogout(); }}
                className="flex items-center justify-between w-full rounded-2xl transition-all active:scale-[0.98]"
                style={{ 
                  height: '56px',
                  paddingLeft: '15px',
                  paddingRight: '15px',
                  backgroundColor: 'rgba(239, 68, 68, 0.05)',
                  border: '1px solid rgba(239, 68, 68, 0.15)',
                  backdropFilter: 'blur(12px)',
                }}
              >
                <div className="flex items-center gap-3">
                  <LogOut className="w-5 h-5" style={{ color: '#EF4444' }} />
                  <span className="text-[15px] font-medium" style={{ color: '#EF4444' }}>Выйти из аккаунта</span>
                </div>
              </button>
            </div>
          </div>

          {/* List View */}
          <div 
            className={`absolute inset-0 flex flex-col gap-3 overflow-y-auto transition-all duration-300 ease-[cubic-bezier(0.23,1,0.32,1)] ${profileView === 'list' ? 'opacity-100 translate-x-0' : profileView === 'main' ? 'opacity-0 translate-x-8 pointer-events-none' : 'opacity-0 -translate-x-8 pointer-events-none'}`} 
            style={{ paddingRight: '4px', marginRight: '-4px', paddingTop: '20px' }}
          >
            {profiles.map((p) => {
                const isCurrent = p.id === activeProfile?.id;
                const isEditing = editingProfileId === p.id;
                return (
                  <div
                    key={p.id}
                    className="flex items-center gap-3 w-full rounded-2xl text-left transition-colors shrink-0"
                    style={{
                      height: '56px',
                      paddingLeft: '15px',
                      paddingRight: '15px',
                      backgroundColor: isCurrent ? 'rgba(99,102,241,0.15)' : 'rgba(255, 255, 255, 0.03)',
                      border: isCurrent ? '1px solid rgba(99,102,241,0.3)' : '1px solid rgba(255, 255, 255, 0.08)',
                      backdropFilter: 'blur(12px)'
                    }}
                  >
                    <div className="w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold shrink-0 shadow-sm" style={{ backgroundColor: p.color || '#6366F1', color: 'white' }}>
                      {p.name.charAt(0).toUpperCase()}
                    </div>

                    {isEditing ? (
                      <>
                        <input
                          value={editingProfileName}
                          onChange={(e) => setEditingProfileName(e.target.value)}
                          className="flex-1 min-w-0 bg-transparent text-[15px] font-medium text-white outline-none"
                          autoFocus
                        />
                        <button onClick={saveRenameProfile} className="w-8 h-8 flex items-center justify-center rounded-xl bg-emerald-500/15 text-emerald-400">
                          <Check className="w-4 h-4" />
                        </button>
                        <button onClick={() => setEditingProfileId(null)} className="w-8 h-8 flex items-center justify-center rounded-xl bg-white/5 text-slate-400">
                          <X className="w-4 h-4" />
                        </button>
                      </>
                    ) : (
                      <>
                        <div className="flex-1 min-w-0 flex items-center justify-between">
                          <span className="text-[15px] font-medium text-white truncate block">{p.name}</span>
                          {isCurrent && <span className="text-[11px] font-semibold px-2 py-0.5 rounded-md" style={{ color: '#10B981', backgroundColor: 'rgba(16,185,129,0.1)' }}>Активен</span>}
                        </div>
                        {isCurrent ? (
                          <button onClick={() => startRenameProfile(p)} className="w-8 h-8 flex items-center justify-center rounded-xl transition-colors hover:bg-white/10 text-slate-300">
                            <Pencil className="w-4 h-4" />
                          </button>
                        ) : (
                          <>
                            <button onClick={() => handleSwitchProfile(p)} className="px-3 h-8 rounded-xl text-xs font-semibold bg-white/10 hover:bg-white/15 transition-colors text-white">
                              Перейти
                            </button>
                            <button onClick={() => handleDeleteProfile(p)} className="w-8 h-8 flex items-center justify-center rounded-xl transition-colors bg-red-500/10 hover:bg-red-500/20 text-red-400">
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </>
                        )}
                      </>
                    )}
                  </div>
                );
              })}

              {profiles.length < 5 && (
                <button
                  onClick={() => setProfileView('new')}
                  className="flex items-center gap-3 w-full rounded-2xl text-left transition-all active:scale-[0.98] mt-1 shrink-0"
                  style={{ 
                    height: '56px',
                    paddingLeft: '15px',
                    paddingRight: '15px',
                    backgroundColor: 'transparent',
                    border: '1px dashed rgba(255, 255, 255, 0.2)',
                  }}
                >
                  <div className="w-8 h-8 rounded-full flex items-center justify-center" style={{ backgroundColor: 'rgba(255,255,255,0.05)' }}>
                    <Plus className="w-4 h-4 text-slate-300" />
                  </div>
                  <span className="text-[15px] font-medium text-slate-300">Создать новый профиль</span>
                </button>
              )}
          </div>

          {/* New Profile View */}
          <div 
            className={`absolute inset-0 flex flex-col gap-4 transition-all duration-300 ease-[cubic-bezier(0.23,1,0.32,1)] ${profileView === 'new' ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-8 pointer-events-none'}`} 
            style={{ paddingTop: '10px' }}
          >
            <div>
                <label className="text-[13px] font-medium mb-1.5 block px-1" style={{ color: '#94A3B8' }}>Имя профиля</label>
                <input
                  type="text"
                  value={newProfileName}
                  onChange={(e) => setNewProfileName(e.target.value)}
                  placeholder="Введите имя..."
                  className="w-full rounded-2xl text-[15px] text-white transition-colors"
                  style={{ 
                    height: '56px',
                    paddingLeft: '15px',
                    paddingRight: '15px',
                    backgroundColor: 'rgba(255,255,255,0.03)', 
                    border: '1px solid rgba(255,255,255,0.08)',
                    outline: 'none'
                  }}
                />
              </div>
              <button 
                onClick={handleCreateProfile} 
                className="w-full rounded-2xl text-[15px] font-semibold text-white transition-all active:scale-[0.98]" 
                style={{ height: '56px', backgroundColor: '#6366F1', boxShadow: '0 4px 15px rgba(99,102,241,0.3)' }}
              >
                Создать профиль
              </button>
            </div>
          </div>
      </Modal>
    </div>
  );
}
