import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { User, ChevronRight, Users, Send, LogOut, Plus } from 'lucide-react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, ResponsiveContainer, Tooltip } from 'recharts';
import { mockActivityData, mockMailingEntries } from '../data/mockData';
import { useAppStore } from '../store/useAppStore';
import Modal from '../components/Modal';

export default function HomePage() {
  const navigate = useNavigate();
  const { activeProfile, profiles, setActiveProfile, addProfile } = useAppStore();
  const [showProfile, setShowProfile] = useState(false);
  const [showNewProfile, setShowNewProfile] = useState(false);
  const [showProfileSwitch, setShowProfileSwitch] = useState(false);
  const [newProfileName, setNewProfileName] = useState('');

  const handleCreateProfile = () => {
    if (!newProfileName.trim()) return;
    addProfile(newProfileName.trim());
    setNewProfileName('');
    setShowNewProfile(false);
  };

  return (
    <div className="px-4 pt-4 pb-4">
      {/* ── Header ── */}
      <div className="flex items-center justify-between mb-5">
        <h1 className="text-xl font-bold text-white">Главная</h1>
        <button
          onClick={() => setShowProfile(true)}
          className="w-10 h-10 rounded-full flex items-center justify-center"
          style={{ backgroundColor: '#1C2038', border: '1px solid rgba(255,255,255,0.08)' }}
        >
          <User className="w-5 h-5" style={{ color: '#94A3B8' }} />
        </button>
      </div>

      {/* ── Greeting ── */}
      <div className="mb-5">
        <p className="text-lg font-semibold text-white">Привет 👋</p>
        <p className="text-sm" style={{ color: '#64748B' }}>Вот ваша статистика на сегодня</p>
      </div>

      {/* ── Stat cards ── */}
      <div className="grid grid-cols-2 gap-3 mb-5">
        <div className="stat-card-green rounded-2xl p-4">
          <p className="text-xs font-medium mb-2" style={{ color: '#10B981' }}>Лидов за сегодня</p>
          <div className="flex items-end justify-between">
            <span className="text-4xl font-bold text-white">128</span>
            <Users className="w-7 h-7" style={{ color: 'rgba(16,185,129,0.5)' }} />
          </div>
        </div>
        <div className="stat-card-purple rounded-2xl p-4">
          <p className="text-xs font-medium mb-2" style={{ color: '#8B5CF6' }}>Рассылок за сегодня</p>
          <div className="flex items-end justify-between">
            <span className="text-4xl font-bold text-white">24</span>
            <Send className="w-7 h-7" style={{ color: 'rgba(139,92,246,0.5)' }} />
          </div>
        </div>
      </div>

      {/* ── Activity chart ── */}
      <div
        className="rounded-2xl p-4 mb-5"
        style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
      >
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-base font-semibold text-white">Активность</h2>
          <span
            className="text-xs px-3 py-1 rounded-full"
            style={{ backgroundColor: '#1C2038', color: '#94A3B8' }}
          >
            Сегодня
          </span>
        </div>
        <ResponsiveContainer width="100%" height={160}>
          <LineChart data={mockActivityData}>
            <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.04)" />
            <XAxis
              dataKey="hour"
              tick={{ fill: '#64748B', fontSize: 10 }}
              axisLine={{ stroke: 'rgba(255,255,255,0.06)' }}
              tickLine={false}
            />
            <YAxis
              tick={{ fill: '#64748B', fontSize: 10 }}
              axisLine={false}
              tickLine={false}
              domain={[0, 100]}
            />
            <Tooltip
              contentStyle={{
                backgroundColor: '#1C2038',
                border: '1px solid rgba(255,255,255,0.1)',
                borderRadius: 8,
                color: '#F1F5F9',
                fontSize: 12,
              }}
            />
            <Line
              type="monotone"
              dataKey="leads"
              stroke="#10B981"
              strokeWidth={2}
              dot={false}
              name="Лиды"
            />
            <Line
              type="monotone"
              dataKey="mailings"
              stroke="#7C3AED"
              strokeWidth={2}
              dot={false}
              name="Рассылки"
            />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* ── Recent mailings ── */}
      <div
        className="rounded-2xl p-4"
        style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
      >
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold text-white">Последние рассылки</h2>
          <button onClick={() => navigate('/mailing')}>
            <ChevronRight className="w-5 h-5" style={{ color: '#64748B' }} />
          </button>
        </div>
        <div className="flex flex-col gap-3">
          {mockMailingEntries.map((entry) => (
            <div key={entry.id} className="flex items-center gap-3">
              <div
                className="w-9 h-9 rounded-full flex items-center justify-center shrink-0"
                style={{ backgroundColor: 'rgba(124,58,237,0.15)' }}
              >
                <Send className="w-4 h-4" style={{ color: '#7C3AED' }} />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-white truncate">{entry.chatName}</p>
                <p className="text-xs" style={{ color: '#64748B' }}>{entry.segment}</p>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <span className="text-xs" style={{ color: '#64748B' }}>{entry.time}</span>
                <span
                  className="text-xs font-semibold px-2 py-0.5 rounded-full"
                  style={{ backgroundColor: 'rgba(124,58,237,0.2)', color: '#8B5CF6' }}
                >
                  {entry.count}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* ═══════ MODALS ═══════ */}

      {/* ── Profile modal ── */}
      <Modal isOpen={showProfile} onClose={() => setShowProfile(false)} title="Профиль">
        <div className="flex items-center gap-3 mb-5">
          <div
            className="w-12 h-12 rounded-full flex items-center justify-center text-lg font-bold"
            style={{ backgroundColor: '#7C3AED', color: 'white' }}
          >
            {activeProfile.name.charAt(0)}
          </div>
          <div>
            <p className="font-semibold text-white">{activeProfile.name}</p>
            <p className="text-xs" style={{ color: '#64748B' }}>ID: {activeProfile.id}</p>
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <button
            onClick={() => { setShowProfile(false); setShowProfileSwitch(true); }}
            className="flex items-center gap-3 w-full py-3 px-2 rounded-xl transition-colors"
            style={{ color: '#94A3B8' }}
            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.04)')}
            onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
          >
            <User className="w-5 h-5" />
            <span className="flex-1 text-left text-sm">Сменить профиль</span>
            <ChevronRight className="w-4 h-4" />
          </button>

          <button
            onClick={() => { setShowProfile(false); setShowNewProfile(true); }}
            className="flex items-center gap-3 w-full py-3 px-2 rounded-xl transition-colors"
            style={{ color: '#94A3B8' }}
            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.04)')}
            onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
          >
            <Plus className="w-5 h-5" />
            <span className="flex-1 text-left text-sm">Создать новый профиль</span>
            <ChevronRight className="w-4 h-4" />
          </button>

          <button
            className="flex items-center gap-3 w-full py-3 px-2 rounded-xl mt-2 transition-colors"
            style={{ color: '#EF4444' }}
            onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'rgba(239,68,68,0.08)')}
            onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
          >
            <LogOut className="w-5 h-5" />
            <span className="text-sm">Выйти</span>
          </button>
        </div>
      </Modal>

      {/* ── New profile modal ── */}
      <Modal isOpen={showNewProfile} onClose={() => setShowNewProfile(false)} title="Новый профиль">
        <div className="flex flex-col gap-4">
          <div>
            <label className="text-sm mb-1.5 block" style={{ color: '#94A3B8' }}>Имя</label>
            <input
              type="text"
              value={newProfileName}
              onChange={(e) => setNewProfileName(e.target.value)}
              placeholder="Введите имя"
              className="w-full px-4 py-3 rounded-xl text-sm text-white"
              style={{
                backgroundColor: '#0B0E18',
                border: '1px solid rgba(255,255,255,0.08)',
              }}
              onFocus={(e) => (e.currentTarget.style.borderColor = '#7C3AED')}
              onBlur={(e) => (e.currentTarget.style.borderColor = 'rgba(255,255,255,0.08)')}
            />
          </div>
          <button
            onClick={handleCreateProfile}
            className="w-full py-3 rounded-xl text-sm font-semibold text-white transition-opacity active:opacity-80"
            style={{ backgroundColor: '#7C3AED' }}
          >
            Создать профиль
          </button>
        </div>
      </Modal>

      {/* ── Switch profile modal ── */}
      <Modal isOpen={showProfileSwitch} onClose={() => setShowProfileSwitch(false)} title="Сменить профиль">
        <div className="flex flex-col gap-2">
          {profiles.map((p) => (
            <button
              key={p.id}
              onClick={() => { setActiveProfile(p); setShowProfileSwitch(false); }}
              className="flex items-center gap-3 w-full py-3 px-3 rounded-xl text-left transition-colors"
              style={{
                backgroundColor: p.id === activeProfile.id ? 'rgba(124,58,237,0.15)' : 'transparent',
                border: p.id === activeProfile.id ? '1px solid rgba(124,58,237,0.3)' : '1px solid transparent',
              }}
            >
              <div
                className="w-9 h-9 rounded-full flex items-center justify-center text-sm font-bold"
                style={{ backgroundColor: '#7C3AED', color: 'white' }}
              >
                {p.name.charAt(0)}
              </div>
              <span className="text-sm text-white">{p.name}</span>
              {p.id === activeProfile.id && (
                <span className="ml-auto text-xs" style={{ color: '#10B981' }}>✓ Активен</span>
              )}
            </button>
          ))}
        </div>
      </Modal>
    </div>
  );
}
