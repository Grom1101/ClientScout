import { ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import type { ReactNode } from 'react';

interface SubPageProps {
  title: string;
  backTo: string;
  children: ReactNode;
  rightAction?: ReactNode;
  hideDivider?: boolean;
}

export default function SubPage({ title, backTo, children, rightAction, hideDivider }: SubPageProps) {
  const navigate = useNavigate();

  return (
    <div className="flex h-full flex-col">
      <header
        className="flex shrink-0 items-center gap-3"
        style={{ borderBottom: hideDivider ? 'none' : '1px solid rgba(148,163,184,0.10)', backgroundColor: 'transparent', paddingLeft: '10px', paddingRight: '10px', paddingTop: '13px', paddingBottom: '11px' }}
      >
        <button
          onClick={() => navigate(backTo)}
          className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl text-[#94A3B8] hover:text-[#0078D4] hover:bg-[#0078D4]/10 active:text-[#005A9E] active:scale-95 transition-all"
        >
          <ArrowLeft className="h-6 w-6" />
        </button>
        <h1 className="min-w-0 flex-1 truncate text-xl font-black text-white">{title}</h1>
        {rightAction && <div className="shrink-0">{rightAction}</div>}
      </header>
      <div className="flex-1 overflow-y-auto">{children}</div>
    </div>
  );
}
