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
    <div className="flex h-full flex-col">
      <header
        className="flex shrink-0 items-center gap-3 px-6 py-5"
        style={{ borderBottom: '1px solid rgba(255,255,255,0.07)', backgroundColor: 'rgba(20, 20, 20, 0.80)' }}
      >
        <button
          onClick={() => navigate(backTo)}
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl transition-colors hover:bg-white/10"
          style={{ color: '#ADADAD' }}
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <h1 className="min-w-0 flex-1 truncate text-xl font-black text-white">{title}</h1>
        {rightAction && <div className="shrink-0">{rightAction}</div>}
      </header>
      <div className="flex-1 overflow-y-auto">{children}</div>
    </div>
  );
}
