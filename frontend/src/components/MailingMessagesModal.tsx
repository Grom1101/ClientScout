import { useEffect, useState } from 'react';
import { Paperclip, X, FileText, Loader2 } from 'lucide-react';
import Modal from './Modal';
import { useOutreachStore } from '../store/useOutreachStore';
import { HARDCODED_PROFILE_ID } from '../api/client';



interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export default function MailingMessagesModal({ isOpen, onClose }: Props) {
  const { templates, isLoading, fetchTemplates, saveTemplate, uploadFile } = useOutreachStore();
  
  const [message, setMessage] = useState('');
  const [attachmentUrls, setAttachmentUrls] = useState<string[]>([]);
  const [isUploading, setIsUploading] = useState(false);

  useEffect(() => {
    if (isOpen) {
      fetchTemplates(HARDCODED_PROFILE_ID);
    }
  }, [isOpen, fetchTemplates]);

  useEffect(() => {
    if (templates.length > 0) {
      setMessage(templates[0].content);
      setAttachmentUrls(templates[0].attachmentUrls || []);
    } else {
      setMessage('');
      setAttachmentUrls([]);
    }
  }, [templates]);

  const handleClose = () => {
    const currentTplContent = templates.length > 0 ? templates[0].content : '';
    const currentTplAttachments = templates.length > 0 ? (templates[0].attachmentUrls || []) : [];
    
    // Check if changed
    const attachmentsChanged = JSON.stringify(attachmentUrls) !== JSON.stringify(currentTplAttachments);
    
    if (message !== currentTplContent || attachmentsChanged) {
      saveTemplate(HARDCODED_PROFILE_ID, message, attachmentUrls);
    }
    onClose();
  };

  const removeFile = (url: string) => {
    setAttachmentUrls((prev) => prev.filter((u) => u !== url));
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    try {
      const url = await uploadFile(file);
      setAttachmentUrls((prev) => [...prev, url]);
    } catch (err) {
      alert("Ошибка при загрузке файла");
    } finally {
      setIsUploading(false);
      // Reset input value
      e.target.value = '';
    }
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
        {attachmentUrls.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold text-white mb-3">Прикреплённые файлы</h3>
            <div className="flex flex-col gap-2">
              {attachmentUrls.map((url) => {
                const fileName = url.split('/').pop() || 'file';
                return (
                  <div
                    key={url}
                    className="flex items-center gap-3 px-3 py-2.5 rounded-xl"
                    style={{ backgroundColor: '#141828', border: '1px solid rgba(255,255,255,0.06)' }}
                  >
                    <FileText className="w-5 h-5 shrink-0" style={{ color: '#7C3AED' }} />
                    <div className="flex-1 min-w-0">
                      <p className="text-sm text-white truncate">{fileName}</p>
                      <p className="text-xs" style={{ color: '#64748B' }}>Загружено</p>
                    </div>
                    <button
                      onClick={() => removeFile(url)}
                      className="w-6 h-6 flex items-center justify-center rounded-full shrink-0 transition-colors hover:bg-white/10"
                      style={{ color: '#64748B' }}
                    >
                      <X className="w-4 h-4" />
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* ── Attach file button ── */}
        <label
          className="w-full py-3 rounded-2xl text-sm font-medium flex items-center justify-center gap-2 cursor-pointer transition-colors hover:bg-white/5"
          style={{ border: '1px dashed rgba(255,255,255,0.15)', color: '#94A3B8' }}
        >
          {isUploading ? (
            <Loader2 className="w-4 h-4 animate-spin" />
          ) : (
            <Paperclip className="w-4 h-4" />
          )}
          {isUploading ? 'Загрузка...' : 'Прикрепить файл'}
          <input type="file" className="hidden" onChange={handleFileUpload} disabled={isUploading} />
        </label>
      </div>
    </Modal>
  );
}
