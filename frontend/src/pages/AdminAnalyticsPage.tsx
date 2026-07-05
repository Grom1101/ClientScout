import React, { useEffect, useState } from 'react';
import { apiClient } from '../api/client';
import { useNavigate } from 'react-router-dom';

interface AdminStats {
  totalCalls: number;
  total429Errors: number;
  totalCostUsd: number;
  remainingBudgetUsd: number;
  providerStats: {
    providerName: string;
    calls: number;
    cost: number;
    inputTokens: number;
    outputTokens: number;
    errors429: number;
  }[];
  modelStats: {
    providerName: string;
    modelName: string;
    calls: number;
    cost: number;
    errors429: number;
  }[];
}

export default function AdminAnalyticsPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    apiClient.get<AdminStats>('/admin/stats')
      .then(res => setStats(res.data))
      .catch(err => {
        if (err.response?.status === 403) {
          setError('Доступ запрещен. Вы не являетесь администратором.');
        } else {
          setError('Не удалось загрузить статистику.');
        }
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="flex justify-center items-center h-full">
        <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-b-2 border-primary"></div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-4 pt-8 text-center text-red-500">
        <h2 className="text-xl font-bold mb-4">Ошибка</h2>
        <p>{error}</p>
        <button 
          onClick={() => navigate('/')}
          className="mt-6 px-4 py-2 bg-primary/20 text-primary rounded-lg"
        >
          На главную
        </button>
      </div>
    );
  }

  return (
    <div className="p-4 pb-24 overflow-y-auto w-full h-full text-sm">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-2xl font-bold">Панель управления</h1>
        <button onClick={() => navigate('/')} className="text-gray-400">Закрыть</button>
      </div>

      {stats && (
        <div className="space-y-6">
          <div className="grid grid-cols-2 gap-4">
            <div className="bg-[#2a2a2a] p-4 rounded-xl">
              <div className="text-gray-400 mb-1">Остаток бюджета</div>
              <div className="text-2xl font-bold text-green-400">${stats.remainingBudgetUsd.toFixed(4)}</div>
            </div>
            <div className="bg-[#2a2a2a] p-4 rounded-xl">
              <div className="text-gray-400 mb-1">Всего потрачено</div>
              <div className="text-2xl font-bold text-red-400">${stats.totalCostUsd.toFixed(4)}</div>
            </div>
            <div className="bg-[#2a2a2a] p-4 rounded-xl">
              <div className="text-gray-400 mb-1">Всего запросов</div>
              <div className="text-xl font-bold">{stats.totalCalls}</div>
            </div>
            <div className="bg-[#2a2a2a] p-4 rounded-xl">
              <div className="text-gray-400 mb-1">Ошибок 429</div>
              <div className="text-xl font-bold text-orange-400">{stats.total429Errors}</div>
            </div>
          </div>

          <div>
            <h2 className="text-lg font-bold mb-3">Провайдеры</h2>
            <div className="space-y-3">
              {stats.providerStats.map(p => (
                <div key={p.providerName} className="bg-[#2a2a2a] p-4 rounded-xl">
                  <div className="flex justify-between mb-2">
                    <span className="font-bold">{p.providerName}</span>
                    <span className="text-primary">${p.cost.toFixed(4)}</span>
                  </div>
                  <div className="flex justify-between text-gray-400 text-xs">
                    <span>Запросов: {p.calls}</span>
                    <span>Лимиты (429): {p.errors429}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div>
            <h2 className="text-lg font-bold mb-3">Модели</h2>
            <div className="space-y-3">
              {stats.modelStats.map(m => (
                <div key={`${m.providerName}-${m.modelName}`} className="bg-[#2a2a2a] p-3 rounded-xl border border-[#333]">
                  <div className="font-semibold text-sm mb-1">{m.modelName}</div>
                  <div className="flex justify-between text-gray-400 text-xs">
                    <span>Провайдер: {m.providerName}</span>
                    <span>${m.cost.toFixed(4)} ({m.calls} reqs)</span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
