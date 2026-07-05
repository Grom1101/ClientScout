import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Check, ChevronRight, LogOut, Pencil, Plus, Send, Trash2, User, Users, X, BookOpen, Shield } from 'lucide-react';
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

  const [showRecommendations, setShowRecommendations] = useState(false);
  const [showProfile, setShowProfile] = useState(false);
  const [profileView, setProfileView] = useState<'main' | 'list' | 'new'>('main');
  const [newProfileName, setNewProfileName] = useState('');
  const [editingProfileId, setEditingProfileId] = useState<string | null>(null);
  const [editingProfileName, setEditingProfileName] = useState('');
  
  const [chartPeriod, setChartPeriod] = useState<StatsPeriod>('today');
  const [combinedChartData, setCombinedChartData] = useState<OutreachActivityPoint[]>([]);
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
        const res = await apiClient.get(`/outreach/profiles/${profileId}/stats?period=${chartPeriod}&timezoneOffsetMinutes=${tz}`);
        setCombinedChartData(res.data.activity || []);
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
  }, [activeProfile?.id, fetchStats, fetchHistory, chartPeriod]);

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

  
  return (
                            <div 
      className="relative flex flex-col" 
      style={{ 
        padding: '20px 20px 84px 20px',
        boxSizing: 'border-box', 
        height: '100%', 
        width: '100%',
        overflow: 'hidden'
      }}
    >
      {/* Header Area */}
      <div className="flex items-center justify-between relative z-10 shrink-0" style={{ marginBottom: '20px' }}>
        <div className="flex items-center gap-4">
          <button
            onClick={() => {
              setProfileView('main');
              setShowProfile(true);
            }}
            className="rounded-full flex items-center justify-center overflow-hidden shrink-0 transition-transform active:scale-95 ring-2 ring-[#4CC2FF]/60 shadow-[0_0_20px_rgba(76,194,255,0.4)]"
            style={{ width: '52px', height: '52px', backgroundColor: '#1E293B' }}
          >
            {account?.telegramAvatarBase64 ? (
              <img src={account.telegramAvatarBase64} alt="Avatar" className="w-full h-full object-cover" />
            ) : (
              <div className="text-white text-xl font-bold">{account?.telegramName?.charAt(0) || 'U'}</div>
            )}
          </button>
          <div className="flex flex-col">
            <h1 className="font-black text-white tracking-tight leading-none" style={{ fontSize: '24px', marginBottom: '6px' }}>
              Привет, {account?.telegramName || 'Пользователь'}! 👋
            </h1>
            <div className="flex items-center gap-2 mt-1">
              <div className="w-2.5 h-2.5 rounded-full bg-[#4CC2FF] shadow-[0_0_8px_rgba(76,194,255,0.8)]"></div>
              <span className="text-[14px] font-bold text-slate-400">
                {activeProfile?.name || 'Профиль не выбран'}
              </span>
            </div>
          </div>
        </div>
        {account?.telegramUserId === 1080953147 && (
          <button
            onClick={() => navigate('/admin')}
            className="absolute top-0 right-0 w-10 h-10 rounded-full flex items-center justify-center bg-white/5 hover:bg-white/10 transition-colors"
          >
            <Shield className="w-5 h-5 text-indigo-400" />
          </button>
        )}
      </div>

      <div className="flex flex-col relative z-10 flex-1 min-h-0" style={{ gap: '16px' }}>
        {/* Quick Actions */}
        <div className="grid grid-cols-2 gap-3 shrink-0">
          <button 
            onClick={() => navigate('/search')}
            className="flex items-center justify-center gap-2 rounded-[20px] transition-all active:scale-[0.98]"
            style={{ height: '52px', backgroundColor: '#10B981', boxShadow: '0 4px 20px rgba(16,185,129,0.2)' }}
          >
            <Users className="w-5 h-5 text-white" />
            <span className="text-[16px] font-bold text-white">Искать</span>
          </button>
          <button 
            onClick={() => navigate('/mailing')}
            className="flex items-center justify-center gap-2 rounded-[20px] transition-all active:scale-[0.98]"
            style={{ height: '52px', backgroundColor: '#0EA5E9', boxShadow: '0 4px 20px rgba(14,165,233,0.2)' }}
          >
            <Send className="w-5 h-5 text-white" />
            <span className="text-[16px] font-bold text-white">Рассылка</span>
          </button>
        </div>

        {/* Combined Stats Banner */}
        <div className="flex items-center justify-between relative overflow-hidden rounded-[20px] shrink-0" 
               style={{ 
                 padding: '16px 20px',
                 background: 'rgba(255,255,255,0.03)',
                 backdropFilter: 'blur(20px)',
                 border: '1px solid rgba(255,255,255,0.05)',
                 boxSizing: 'border-box'
               }}>
            <div className="flex flex-col">
              <span className="text-[11px] font-extrabold uppercase tracking-widest text-slate-400 mb-1">Найдено (24ч)</span>
              <div className="flex items-center gap-2">
                <span className="leading-none font-black text-emerald-400 tracking-tight" style={{ fontSize: '32px' }}>{stats?.leadsToday ?? 0}</span>
              </div>
            </div>
            <div className="w-[1px] h-10 bg-white/10 mx-3"></div>
            <div className="flex flex-col items-end">
              <span className="text-[11px] font-extrabold uppercase tracking-widest text-slate-400 mb-1">Отправлено (24ч)</span>
              <div className="flex items-center gap-2">
                <span className="leading-none font-black text-sky-400 tracking-tight" style={{ fontSize: '32px' }}>{stats?.sentToday ?? 0}</span>
              </div>
            </div>
        </div>

        {/* Combined Chart */}
        <div className="flex flex-col relative overflow-hidden rounded-[24px] flex-1 min-h-[160px]" 
             style={{ 
               paddingTop: '12px',
               paddingBottom: '16px',
               background: 'rgba(255,255,255,0.02)',
               border: '1px solid rgba(255,255,255,0.05)',
               boxSizing: 'border-box'
             }}>
          <div className="flex items-center justify-between shrink-0" style={{ paddingLeft: '24px', paddingRight: '24px', marginBottom: '10px' }}>
            <span className="text-[17px] text-white font-black tracking-wide">Эффективность</span>
            <div className="flex items-center rounded-[8px] bg-black/40 border border-white/[0.08]" style={{ padding: '2px', gap: '2px' }}>
              {(['today', 'month'] as StatsPeriod[]).map((period) => {
                const isActive = chartPeriod === period;
                return (
                  <button
                    key={period}
                    onClick={() => setChartPeriod(period)}
                    className="inline-flex items-center justify-center rounded-[6px] text-[9px] font-black uppercase tracking-widest transition-all"
                    style={{
                      padding: '4px 10px',
                      backgroundColor: isActive ? 'rgba(0, 120, 212, 0.25)' : 'transparent',
                      color: isActive ? '#9ECBFF' : '#64748B',
                      border: isActive ? '1px solid rgba(76, 194, 255, 0.3)' : '1px solid transparent',
                      boxShadow: isActive ? '0 2px 10px rgba(0, 120, 212, 0.2)' : 'none'
                    }}
                  >
                    {period === 'today' ? 'Сегодня' : 'Месяц'}
                  </button>
                );
              })}
            </div>
          </div>
          <div className="w-full flex-1 min-h-0">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={chartPeriod === 'today' ? combinedChartData.slice(3) : combinedChartData} margin={{ top: 15, right: 60, left: 60, bottom: 5 }}>
                <defs>
                  <linearGradient id="colorLeads" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#10B981" stopOpacity={0.4}/>
                    <stop offset="100%" stopColor="#10B981" stopOpacity={0}/>
                  </linearGradient>
                  <linearGradient id="colorSent" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#0EA5E9" stopOpacity={0.4}/>
                    <stop offset="100%" stopColor="#0EA5E9" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <XAxis 
                  dataKey="label" 
                  tick={{ fill: '#64748B', fontSize: 11, fontWeight: 700 }} 
                  axisLine={false} 
                  tickLine={false} 
                  dy={12} 
                  ticks={chartPeriod === 'today' ? ['03:00', '07:00', '11:00', '15:00', '19:00', '23:00'] : undefined}
                  interval={chartPeriod === 'today' ? 0 : "preserveStartEnd"}
                />
                <Tooltip
                  cursor={{ stroke: 'rgba(255,255,255,0.1)', strokeWidth: 1, strokeDasharray: '4 4' }}
                  contentStyle={{ backgroundColor: 'rgba(15, 23, 42, 0.95)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 12, color: '#fff', fontSize: 13, boxShadow: '0 10px 25px rgba(0,0,0,0.5)' }}
                  labelStyle={{ color: '#94A3B8', marginBottom: 4, fontWeight: 600 }}
                />
                <Area type="monotone" dataKey="leads" stroke="#10B981" strokeWidth={3} fillOpacity={1} fill="url(#colorLeads)" name="Найдено" />
                <Area type="monotone" dataKey="sent" stroke="#0EA5E9" strokeWidth={3} fillOpacity={1} fill="url(#colorSent)" name="Отправлено" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Recommendations Button */}
        <button
          onClick={() => setShowRecommendations(true)}
          className="flex items-center justify-between w-full rounded-[20px] transition-all active:scale-[0.98] shrink-0"
          style={{ 
            padding: '20px',
            backgroundColor: 'rgba(0, 120, 212, 0.1)',
            border: '1px solid rgba(0, 120, 212, 0.2)',
            backdropFilter: 'blur(12px)',
          }}
        >
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full flex items-center justify-center shrink-0" style={{ backgroundColor: 'rgba(0, 120, 212, 0.2)' }}>
              <BookOpen className="w-5 h-5" style={{ color: '#60CDFF' }} />
            </div>
            <div className="flex flex-col text-left">
              <span className="text-[16px] font-bold text-white mb-0.5">Рекомендации</span>
              <span className="text-[12px] font-medium" style={{ color: '#60CDFF', opacity: 0.7 }}>Как пользоваться приложением</span>
            </div>
          </div>
          <ChevronRight className="w-5 h-5" style={{ color: '#60CDFF', opacity: 0.5 }} />
        </button>
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
        <div className="relative w-full" style={{ marginTop: '5px', paddingTop: '5px' }}>
          {/* Main View - Always in DOM to dictate exact modal height */}
          <div 
            className={`transition-all duration-300 ease-[cubic-bezier(0.23,1,0.32,1)] ${profileView === 'main' ? 'opacity-100 translate-x-0' : 'opacity-0 -translate-x-8 pointer-events-none'}`}
          >
            {/* Avatar block */}
            <div className="flex items-center gap-5 px-1" style={{ marginBottom: '15px' }}>
              <div className="shrink-0 rounded-full flex items-center justify-center text-xl font-bold overflow-hidden shadow-[0_4px_20px_rgba(0, 120, 212,0.3)]" style={{ width: '64px', height: '64px', minWidth: '64px', minHeight: '64px', backgroundColor: '#0078D4', color: 'white' }}>
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
                  backgroundColor: 'rgba(76, 194, 255, 0.05)',
                  border: '1px solid rgba(76, 194, 255, 0.15)',
                  backdropFilter: 'blur(12px)',
                }}
              >
                <div className="flex items-center gap-3">
                  <LogOut className="w-5 h-5" style={{ color: '#4CC2FF' }} />
                  <span className="text-[15px] font-medium" style={{ color: '#4CC2FF' }}>Выйти из аккаунта</span>
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
                      backgroundColor: isCurrent ? 'rgba(0, 120, 212,0.15)' : 'rgba(255, 255, 255, 0.03)',
                      border: isCurrent ? '1px solid rgba(0, 120, 212,0.3)' : '1px solid rgba(255, 255, 255, 0.08)',
                      backdropFilter: 'blur(12px)'
                    }}
                  >
                    <div className="w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold shrink-0 shadow-sm" style={{ backgroundColor: p.color || '#0078D4', color: 'white' }}>
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
                style={{ height: '56px', backgroundColor: '#0078D4', boxShadow: '0 4px 15px rgba(0, 120, 212,0.3)' }}
              >
                Создать профиль
              </button>
            </div>
          </div>
      </Modal>
    </div>
  );
}
