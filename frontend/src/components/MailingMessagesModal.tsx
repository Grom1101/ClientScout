import { useEffect, useState } from 'react';
import { Paperclip, X, FileText, Loader2 } from 'lucide-react';
import Modal from './Modal';
import { useOutreachStore } from '../store/useOutreachStore';
import { HARDCODED_PROFILE_ID } from '../api/client';

interface AttachedFile {
  id: string;
  name: string;
  size: string;
}

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export default function MailingMessagesModal({ isOpen, onClose }: Props) {
  const { templates, isLoading, fetchTemplates, saveTemplate } = useOutreachStore();
  
  const [message, setMessage] = useState('');
  const [files, setFiles] = useState<AttachedFile[]>([
    { id: '1', name: 'image_banner.jpg', size: '2.4 MB' },
  ]);

  useEffect(() => {
    if (isOpen) {
      fetchTemplates(HARDCODED_PROFILE_ID);
    }
  }, [isOpen, fetchTemplates]);

  useEffect(() => {
    if (templates.length > 0) {
      setMessage(templates[0].content);
    } else {
      setMessage('');
    }
  }, [templates]);

  const handleClose = () => {
    const currentTplContent = templates.length > 0 ? templates[0].content : '';
    if (message !== currentTplContent) {
      saveTemplate(HARDCODED_PROFILE_ID, message);
    }
    onClose();
  };

  const removeFile = (id: string) => {
    setFiles((prev) => prev.filter((f) => f.id !== id));
  };

  const addMockFile = () => {
    const mockNames = ['presentation.pdf', 'portfolio.zip', 'cover_letter.docx', 'logo.png'];
    const mockSizes = ['1.2 MB', '3.8 MB', '256 KB', '480 KB'];
    const idx = files.length % mockNames.length;
    setFiles((prev) => [
      ...prev,
      { id: String(Date.now()), name: mockNames[idx], size: mockSizes[idx] },
    ]);
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Сообщения">
      <div className="flex flex-col gap-4 relative">
        {isLoading && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-[#0B0E18]/50">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: '#7C3AED' }} />
          </div>
        )}
        {/* ── Text area ── */}
        <div
          className="rounded-2xl p-4 flex-1"
          style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
        >
          <textarea
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            placeholder="Введите текст сообщения..."
            maxLength={4096}
            className="w-full h-48 bg-transparent text-sm text-white resize-none leading-relaxed"
            style={{ outline: 'none' }}
          />
          <p className="text-xs mt-1" style={{ color: '#64748B' }}>
            (можно использовать переменные)
          </p>
        </div>

        {/* ── Character count ── */}
        <div className="flex justify-end">
          <span className="text-xs" style={{ color: '#64748B' }}>
            {message.length} / 4096
          </span>
        </div>

        {/* ── Attached files ── */}
        {files.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold text-white mb-3">Прикреплённые файлы</h3>
            <div className="flex flex-col gap-2">
              {files.map((file) => (
                <div
                  key={file.id}
                  className="flex items-center gap-3 px-3 py-2.5 rounded-xl"
                  style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
                >
                  <FileText className="w-5 h-5 shrink-0" style={{ color: '#7C3AED' }} />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm text-white truncate">{file.name}</p>
                    <p className="text-xs" style={{ color: '#64748B' }}>{file.size}</p>
                  </div>
                  <button
                    onClick={() => removeFile(file.id)}
                    className="w-6 h-6 flex items-center justify-center rounded-full shrink-0"
                    style={{ color: '#64748B' }}
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* ── Attach file button ── */}
        <button
          onClick={addMockFile}
          className="w-full py-3 rounded-2xl text-sm font-medium flex items-center justify-center gap-2"
          style={{ border: '1px dashed rgba(255,255,255,0.15)', color: '#94A3B8' }}
        >
          <Paperclip className="w-4 h-4" />
          Прикрепить файл
        </button>
      </div>
    </Modal>
  );
}
