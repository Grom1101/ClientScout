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
        className="w-full max-w-[360px] rounded-[24px] animate-scale-in flex flex-col"
        style={{
          background: 'linear-gradient(180deg, #2B2B2B 0%, #202020 100%)',
          border: '1px solid rgba(76, 194, 255,0.16)',
          boxShadow: '0 25px 60px rgba(5,8,18,0.6)',
          padding: '20px 24px 24px 24px',
          maxHeight: 'calc(100vh - 48px)'
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex shrink-0 items-center justify-between mb-5">
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
            className="w-9 h-9 flex items-center justify-center rounded-xl text-[#64748B] hover:text-[#0078D4] hover:bg-[#0078D4]/10 active:text-[#005A9E] active:scale-95 transition-all"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto min-h-0 -mx-4 px-4">
          {children}
        </div>
      </div>
    </div>
  );
}
