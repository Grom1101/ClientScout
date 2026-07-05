import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, RefreshCw, AlertTriangle, DollarSign, Activity } from 'lucide-react';
import { apiClient } from '../api/client';
import { useAuthStore } from '../store/useAuthStore';

interface ModelStat {
  providerName: string;
  modelName: string;
  calls: number;
  cost: number;
  errors429: number;
}

interface ProviderStat {
  providerName: string;
  calls: number;
  cost: number;
  inputTokens: number;
  outputTokens: number;
  errors429: number;
}

interface AdminStats {
  totalCalls: number;
  total429Errors: number;
  totalCostUsd: number;
  remainingBudgetUsd: number;
  providerStats: ProviderStat[];
  modelStats: ModelStat[];
}

export default function AdminDashboard() {
  const navigate = useNavigate();
  const { account } = useAuthStore();
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Secure route check on frontend level
  useEffect(() => {
    if (account?.telegramUserId !== 1080953147) {
      navigate('/');
    }
  }, [account, navigate]);

  const fetchStats = async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await apiClient.get<AdminStats>('/admin/stats');
      setStats(res.data);
    } catch (err: any) {
      setError(err.response?.status === 403 ? 'Доступ запрещен' : 'Ошибка загрузки данных');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStats();
  }, []);

  if (account?.telegramUserId !== 1080953147) {
    return null; // Don't render anything while redirecting
  }

  return (
    <div className="flex flex-col h-full bg-[#0B0F19] text-white p-5 overflow-y-auto pb-24">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate('/')}
            className="w-10 h-10 rounded-full flex items-center justify-center bg-white/5 hover:bg-white/10 transition-colors"
          >
            <ArrowLeft className="w-5 h-5 text-slate-300" />
          </button>
          <div>
            <h1 className="text-2xl font-black tracking-tight text-white">Панель администратора</h1>
            <p className="text-sm font-semibold text-slate-400">Аналитика и расходы API</p>
          </div>
        </div>
        <button
          onClick={fetchStats}
          disabled={loading}
          className="w-10 h-10 rounded-full flex items-center justify-center bg-white/5 hover:bg-white/10 transition-colors disabled:opacity-50"
        >
          <RefreshCw className={`w-5 h-5 text-slate-300 ${loading ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {error ? (
        <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 font-medium">
          {error}
        </div>
      ) : stats ? (
        <div className="flex flex-col gap-6">
          {/* Main KPI Cards */}
          <div className="grid grid-cols-2 gap-4">
            <div className="p-5 rounded-2xl bg-white/5 border border-white/10 flex flex-col">
              <div className="flex items-center gap-2 text-emerald-400 mb-2">
                <DollarSign className="w-5 h-5" />
                <span className="text-sm font-bold uppercase tracking-widest">Доступный бюджет</span>
              </div>
              <span className="text-3xl font-black">${stats.remainingBudgetUsd.toFixed(2)}</span>
              <span className="text-xs font-semibold text-slate-400 mt-1">из $100.00</span>
            </div>

            <div className="p-5 rounded-2xl bg-white/5 border border-white/10 flex flex-col">
              <div className="flex items-center gap-2 text-sky-400 mb-2">
                <Activity className="w-5 h-5" />
                <span className="text-sm font-bold uppercase tracking-widest">Всего запросов</span>
              </div>
              <span className="text-3xl font-black">{stats.totalCalls}</span>
              <span className="text-xs font-semibold text-slate-400 mt-1">Потрачено: ${stats.totalCostUsd.toFixed(4)}</span>
            </div>
            
            <div className="col-span-2 p-5 rounded-2xl bg-red-500/10 border border-red-500/20 flex items-center justify-between">
              <div className="flex flex-col">
                <div className="flex items-center gap-2 text-red-400 mb-1">
                  <AlertTriangle className="w-5 h-5" />
                  <span className="text-sm font-bold uppercase tracking-widest">Ошибки 429 (Лимиты)</span>
                </div>
                <span className="text-xs font-medium text-red-400/80">Количество отказов провайдеров из-за лимитов</span>
              </div>
              <span className="text-3xl font-black text-red-400">{stats.total429Errors}</span>
            </div>
          </div>

          {/* Model Breakdown */}
          <div>
            <h2 className="text-lg font-bold mb-3 text-slate-200">Использование по моделям</h2>
            <div className="flex flex-col gap-3">
              {stats.modelStats.length === 0 ? (
                <div className="text-sm text-slate-400 italic">Нет данных</div>
              ) : (
                stats.modelStats.map((model, idx) => (
                  <div key={idx} className="p-4 rounded-xl bg-white/5 border border-white/5 flex items-center justify-between">
                    <div className="flex flex-col">
                      <span className="font-bold text-white">{model.modelName}</span>
                      <span className="text-xs font-semibold text-slate-400">{model.providerName}</span>
                    </div>
                    <div className="flex flex-col items-end">
                      <span className="font-black text-sky-400">${model.cost.toFixed(4)}</span>
                      <div className="flex gap-3 text-xs font-medium text-slate-400">
                        <span>{model.calls} запросов</span>
                        {model.errors429 > 0 && <span className="text-red-400">{model.errors429} ошибок</span>}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      ) : (
        <div className="flex-1 flex items-center justify-center">
          <RefreshCw className="w-8 h-8 text-slate-600 animate-spin" />
        </div>
      )}
    </div>
  );
}
