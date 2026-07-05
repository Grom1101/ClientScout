import { useEffect, useState } from 'react';
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
    successfulCalls: number;
    cost: number;
    inputTokens: number;
    outputTokens: number;
    errors429: number;
  }[];
}

const DEFAULT_PRICING: Record<string, { in: number, out: number }> = {
  'BluesMinds:gpt-4o-mini': { in: 0.30, out: 0.18 },
  'BluesMinds:mimo-v2.5': { in: 0.10, out: 0.28 }
};

function formatTokens(num: number): string {
  if (num >= 1000000) return (num / 1000000).toFixed(2) + 'M';
  if (num >= 1000) return (num / 1000).toFixed(1) + 'k';
  return num.toString();
}

const FIXED_MODELS = [
  { providerName: 'BluesMinds', modelName: 'gpt-4o-mini' },
  { providerName: 'BluesMinds', modelName: 'mimo-v2.5' },
  { providerName: 'Groq', modelName: 'llama3-8b-8192' },
  { providerName: 'Groq', modelName: 'llama3-70b-8192' },
  { providerName: 'Groq', modelName: 'llama-3.1-8b-instant' },
  { providerName: 'Groq', modelName: 'llama-3.3-70b-versatile' },
  { providerName: 'OpenRouter', modelName: 'google/gemini-1.5-flash' },
  { providerName: 'OpenRouter', modelName: 'meta-llama/llama-3.1-8b-instruct:free' },
  { providerName: 'OpenRouter', modelName: 'qwen/qwen-2.5-72b-instruct:free' },
  { providerName: 'OpenRouter', modelName: 'nvidia/llama-3.1-nemotron-70b-instruct:free' },
  { providerName: 'OpenRouter', modelName: 'google/gemini-2.0-flash-lite-preview-02-05:free' },
];

export default function AdminAnalyticsPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<'current' | 'forecast'>('current');
  const navigate = useNavigate();

  // Forecast State
  const [mau, setMau] = useState<number>(1000);
  const [reqsPerUser, setReqsPerUser] = useState<number>(5);
  const [selectedModel, setSelectedModel] = useState<string>('BluesMinds:gpt-4o-mini');
  const [avgInputTokens, setAvgInputTokens] = useState<number>(500);
  const [avgOutputTokens, setAvgOutputTokens] = useState<number>(300);
  const [budget, setBudget] = useState<number>(100);

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

  const renderCurrent = () => {
    if (!stats) return null;
    return (
      <div className="space-y-6">
        <div className="grid grid-cols-2 gap-4">
          <div className="bg-[#2a2a2a] p-4 rounded-xl border border-white/5 shadow-sm">
            <div className="text-gray-400 mb-1 text-xs">Остаток бюджета</div>
            <div className="text-xl font-bold text-green-400">${stats.remainingBudgetUsd.toFixed(4)}</div>
          </div>
          <div className="bg-[#2a2a2a] p-4 rounded-xl border border-white/5 shadow-sm">
            <div className="text-gray-400 mb-1 text-xs">Потрачено</div>
            <div className="text-xl font-bold text-red-400">${stats.totalCostUsd.toFixed(4)}</div>
          </div>
          <div className="bg-[#2a2a2a] p-4 rounded-xl border border-white/5 shadow-sm">
            <div className="text-gray-400 mb-1 text-xs">Всего запросов</div>
            <div className="text-lg font-bold">{stats.totalCalls}</div>
          </div>
          <div className="bg-[#2a2a2a] p-4 rounded-xl border border-white/5 shadow-sm">
            <div className="text-gray-400 mb-1 text-xs">Ошибок 429</div>
            <div className="text-lg font-bold text-orange-400">{stats.total429Errors}</div>
          </div>
        </div>

        <div>
          <h2 className="text-lg font-bold mb-3 flex items-center gap-2">
            <span className="w-1.5 h-6 bg-primary rounded-full"></span>
            Провайдеры
          </h2>
          <div className="space-y-3">
            {stats.providerStats.length === 0 && <div className="text-gray-500 italic">Нет данных</div>}
            {stats.providerStats.map(p => (
              <div key={p.providerName} className="bg-[#2a2a2a] p-4 rounded-xl border border-white/5 shadow-sm">
                <div className="flex justify-between items-center mb-3">
                  <span className="font-bold text-base">{p.providerName}</span>
                  <span className="text-primary font-mono bg-primary/10 px-2 py-0.5 rounded">${p.cost.toFixed(4)}</span>
                </div>
                <div className="grid grid-cols-2 gap-y-2 text-xs text-gray-400">
                  <div className="flex flex-col">
                    <span className="text-gray-500">Запросы</span>
                    <span className="text-white">{p.calls}</span>
                  </div>
                  <div className="flex flex-col">
                    <span className="text-gray-500">Лимиты (429)</span>
                    <span className="text-orange-400">{p.errors429}</span>
                  </div>
                  <div className="flex flex-col">
                    <span className="text-gray-500">Input Токены</span>
                    <span className="text-white">{formatTokens(p.inputTokens)}</span>
                  </div>
                  <div className="flex flex-col">
                    <span className="text-gray-500">Output Токены</span>
                    <span className="text-white">{formatTokens(p.outputTokens)}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div>
          <h2 className="text-lg font-bold mb-3 flex items-center gap-2">
            <span className="w-1.5 h-6 bg-blue-500 rounded-full"></span>
            Детализация по Моделям
          </h2>
          <div className="space-y-3">
            {(() => {
              const mergedStats = [...FIXED_MODELS];
              
              // Add any models that are in stats but not in FIXED_MODELS
              stats.modelStats.forEach(stat => {
                if (!mergedStats.some(m => m.providerName === stat.providerName && m.modelName === stat.modelName)) {
                  mergedStats.push({ providerName: stat.providerName, modelName: stat.modelName });
                }
              });

              return mergedStats.map(m => {
                const stat = stats.modelStats.find(s => s.providerName === m.providerName && s.modelName === m.modelName) || {
                  calls: 0,
                  successfulCalls: 0,
                  cost: 0,
                  inputTokens: 0,
                  outputTokens: 0,
                  errors429: 0
                };

                return (
                  <div key={`${m.providerName}-${m.modelName}`} className="bg-[#2a2a2a] p-3 rounded-xl border border-white/5 shadow-sm">
                    <div className="flex justify-between items-start mb-2">
                      <div>
                        <div className="font-semibold text-sm">{m.modelName}</div>
                        <div className="text-xs text-gray-500">{m.providerName}</div>
                      </div>
                      <div className="text-right">
                        <div className="text-primary font-mono text-sm">${stat.cost.toFixed(4)}</div>
                        <div className="text-xs text-gray-400">Успешно: <span className="text-white font-medium">{stat.successfulCalls}</span> / {stat.calls} reqs</div>
                      </div>
                    </div>
                    <div className="flex gap-4 text-xs mt-2 pt-2 border-t border-white/5">
                      <span className="text-gray-400">In: <span className="text-white">{formatTokens(stat.inputTokens)}</span></span>
                      <span className="text-gray-400">Out: <span className="text-white">{formatTokens(stat.outputTokens)}</span></span>
                      {stat.errors429 > 0 && <span className="text-orange-400 ml-auto">Err: {stat.errors429}</span>}
                    </div>
                  </div>
                );
              });
            })()}
          </div>
        </div>
      </div>
    );
  };

  const renderForecast = () => {
    // Calculators
    const calcCost = (users: number) => {
      const totalReqs = users * reqsPerUser * 30; // 30 days in a month
      const totalInTokens = totalReqs * avgInputTokens;
      const totalOutTokens = totalReqs * avgOutputTokens;
      const pricing = DEFAULT_PRICING[selectedModel] || { in: 0, out: 0 };
      
      const costIn = (totalInTokens / 1000000) * pricing.in;
      const costOut = (totalOutTokens / 1000000) * pricing.out;
      return costIn + costOut;
    };

    const monthlyCost = calcCost(mau);
    const isOverBudget = monthlyCost > budget;

    const scales = [500, 1000, 5000, 10000];

    return (
      <div className="space-y-6">
        <div className="bg-[#2a2a2a] p-4 rounded-xl border border-white/5 shadow-sm space-y-4">
          <h2 className="font-bold text-lg border-b border-white/10 pb-2">Параметры прогноза</h2>
          
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs text-gray-400 mb-1">Кол-во юзеров (MAU)</label>
              <input 
                type="number" 
                value={mau} 
                onChange={e => setMau(Number(e.target.value))}
                className="w-full bg-[#1a1a1a] border border-white/10 rounded-lg px-3 py-2 text-sm text-white"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-400 mb-1">Бюджет ($/мес)</label>
              <input 
                type="number" 
                value={budget} 
                onChange={e => setBudget(Number(e.target.value))}
                className="w-full bg-[#1a1a1a] border border-white/10 rounded-lg px-3 py-2 text-sm text-white"
              />
            </div>
            <div className="col-span-2">
              <label className="block text-xs text-gray-400 mb-1">Запросов на юзера в день</label>
              <input 
                type="number" 
                value={reqsPerUser} 
                onChange={e => setReqsPerUser(Number(e.target.value))}
                className="w-full bg-[#1a1a1a] border border-white/10 rounded-lg px-3 py-2 text-sm text-white"
              />
            </div>
            
            <div className="col-span-2">
              <label className="block text-xs text-gray-400 mb-1">Модель</label>
              <select 
                value={selectedModel}
                onChange={e => setSelectedModel(e.target.value)}
                className="w-full bg-[#1a1a1a] border border-white/10 rounded-lg px-3 py-2 text-sm text-white"
              >
                {Object.keys(DEFAULT_PRICING).map(k => (
                  <option key={k} value={k}>{k.split(':')[1]} ({k.split(':')[0]})</option>
                ))}
              </select>
            </div>
            
            <div>
              <label className="block text-xs text-gray-400 mb-1">Сред. Input токенов</label>
              <input 
                type="number" 
                value={avgInputTokens} 
                onChange={e => setAvgInputTokens(Number(e.target.value))}
                className="w-full bg-[#1a1a1a] border border-white/10 rounded-lg px-3 py-2 text-sm text-white"
              />
            </div>
            <div>
              <label className="block text-xs text-gray-400 mb-1">Сред. Output токенов</label>
              <input 
                type="number" 
                value={avgOutputTokens} 
                onChange={e => setAvgOutputTokens(Number(e.target.value))}
                className="w-full bg-[#1a1a1a] border border-white/10 rounded-lg px-3 py-2 text-sm text-white"
              />
            </div>
          </div>
        </div>

        <div className={`p-5 rounded-xl border shadow-sm ${isOverBudget ? 'bg-red-500/10 border-red-500/30' : 'bg-green-500/10 border-green-500/30'}`}>
          <div className="text-sm text-center mb-2">Прогноз расходов (для {mau} юзеров):</div>
          <div className={`text-3xl font-bold text-center ${isOverBudget ? 'text-red-400' : 'text-green-400'}`}>
            ${monthlyCost.toFixed(2)} / мес
          </div>
          <div className="text-center mt-3 text-xs text-gray-400">
            {isOverBudget 
              ? `Превышает бюджет на $${(monthlyCost - budget).toFixed(2)}` 
              : `Остаток бюджета: $${(budget - monthlyCost).toFixed(2)}`}
          </div>
          <div className="text-center mt-2 text-xs text-gray-500">
            Итого запросов: {(mau * reqsPerUser * 30).toLocaleString('ru')} в месяц
          </div>
        </div>

        <div>
          <h2 className="text-lg font-bold mb-3">Масштабирование (Scale)</h2>
          <div className="space-y-2">
            {scales.map(scaleUsers => {
              const cost = calcCost(scaleUsers);
              const over = cost > budget;
              return (
                <div key={scaleUsers} className="flex justify-between items-center bg-[#2a2a2a] p-3 rounded-lg border border-white/5">
                  <span className="font-semibold">{scaleUsers} юзеров</span>
                  <span className={`font-mono font-bold ${over ? 'text-red-400' : 'text-green-400'}`}>
                    ${cost.toFixed(2)}
                  </span>
                </div>
              );
            })}
          </div>
        </div>

      </div>
    );
  };

  return (
    <div className="p-4 pb-24 overflow-y-auto w-full h-full text-sm">
      <div className="flex justify-between items-center mb-4">
        <h1 className="text-2xl font-bold">Аналитика</h1>
        <button onClick={() => navigate('/')} className="text-gray-400 text-xs bg-white/5 px-3 py-1.5 rounded-lg">Закрыть</button>
      </div>

      <div className="flex bg-[#1a1a1a] rounded-lg p-1 mb-6 border border-white/5">
        <button 
          className={`flex-1 py-2 text-center rounded-md font-medium transition-colors ${activeTab === 'current' ? 'bg-[#333] text-white shadow-sm' : 'text-gray-400 hover:text-white'}`}
          onClick={() => setActiveTab('current')}
        >
          Сейчас
        </button>
        <button 
          className={`flex-1 py-2 text-center rounded-md font-medium transition-colors ${activeTab === 'forecast' ? 'bg-[#333] text-white shadow-sm' : 'text-gray-400 hover:text-white'}`}
          onClick={() => setActiveTab('forecast')}
        >
          Будущее
        </button>
      </div>

      {activeTab === 'current' ? renderCurrent() : renderForecast()}
    </div>
  );
}
