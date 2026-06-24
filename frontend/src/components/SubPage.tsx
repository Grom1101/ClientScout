import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import type { ReactNode } from 'react';

interface SubPageProps {
  title: string;
  backTo: string;
  children: ReactNode;
  rightAction?: ReactNode;
}

export default function SubPage({ title, backTo, children, rightAction }: SubPageProps) {
  const navigate = useNavigate();

  return (
    <div className="flex flex-col h-full">
      <header
        className="flex items-center px-4 py-3 shrink-0"
        style={{ borderBottom: '1px solid rgba(255,255,255,0.06)' }}
      >
        <button
          onClick={() => navigate(backTo)}
          className="w-9 h-9 flex items-center justify-center rounded-full transition-colors"
          style={{ color: '#94A3B8' }}
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <h1 className="ml-2 text-lg font-semibold text-white flex-1">{title}</h1>
        {rightAction && <div className="ml-auto">{rightAction}</div>}
      </header>
      <div className="flex-1 overflow-y-auto">{children}</div>
    </div>
  );
}
