import { X, ChevronLeft } from 'lucide-react';
import type { ReactNode } from 'react';

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  onBack?: () => void;
  title: string;
  children: ReactNode;
}

export default function Modal({ isOpen, onClose, onBack, title, children }: ModalProps) {
  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center py-6 animate-fade-in"
      style={{ paddingLeft: '37px', paddingRight: '37px', backgroundColor: 'rgba(0,0,0,0.62)', backdropFilter: 'blur(5px)' }}
      onClick={onClose}
    >
      <div
        className="w-full max-w-[360px] rounded-[24px] animate-scale-in"
        style={{
          background: 'linear-gradient(180deg, #132031 0%, #0F1825 100%)',
          border: '1px solid rgba(148,163,184,0.14)',
          boxShadow: '0 25px 50px rgba(0,0,0,0.5)',
          padding: '24px 30px 32px 30px'
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-5">
          <div className="flex items-center gap-3">
            {onBack && (
              <button
                onClick={onBack}
                className="w-9 h-9 flex items-center justify-center rounded-xl transition-colors"
                style={{ color: '#94A3B8', backgroundColor: 'rgba(255,255,255,0.05)' }}
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
            )}
            <h3 className="text-lg font-extrabold text-white">{title}</h3>
          </div>
          <button
            onClick={onClose}
            className="w-9 h-9 flex items-center justify-center rounded-xl transition-colors"
            style={{ color: '#64748B' }}
            onMouseEnter={(e) => (e.currentTarget.style.color = '#F1F5F9')}
            onMouseLeave={(e) => (e.currentTarget.style.color = '#64748B')}
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}
