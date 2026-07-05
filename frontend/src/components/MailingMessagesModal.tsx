import { useEffect, useRef, useState } from 'react';
import { FileText, Loader2, Paperclip, X } from 'lucide-react';
import Modal from './Modal';
import {
  ALLOWED_ATTACHMENT_EXTENSIONS,
  MAX_ATTACHMENT_COUNT,
  MAX_MESSAGE_LENGTH,
  useOutreachStore,
} from '../store/useOutreachStore';
import { getActiveProfileId } from '../api/client';

interface Props {
  isOpen: boolean;
  onClose: () => void;
}

export default function MailingMessagesModal({ isOpen, onClose }: Props) {
  const { templates, isLoading, fetchTemplates, saveTemplate, uploadFile } = useOutreachStore();

  const [message, setMessage] = useState('');
  const [attachmentUrls, setAttachmentUrls] = useState<string[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const hydratedRef = useRef(false);

  useEffect(() => {
    if (isOpen) {
      fetchTemplates(getActiveProfileId());
    }
  }, [isOpen, fetchTemplates]);

  useEffect(() => {
    if (!isOpen) {
      hydratedRef.current = false;
      return;
    }

    if (hydratedRef.current) return;

    if (templates.length > 0) {
      setMessage(templates[0].content.slice(0, MAX_MESSAGE_LENGTH));
      setAttachmentUrls((templates[0].attachmentUrls || []).slice(0, MAX_ATTACHMENT_COUNT));
    } else {
      setMessage('');
      setAttachmentUrls([]);
    }
    hydratedRef.current = true;
  }, [templates, isOpen]);

  const handleSave = async () => {
    await saveTemplate(getActiveProfileId(), message, attachmentUrls);
    onClose();
  };

  const removeFile = (url: string) => {
    setAttachmentUrls((prev) => prev.filter((u) => u !== url));
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (attachmentUrls.length >= MAX_ATTACHMENT_COUNT) {
      alert('Можно прикрепить только один файл.');
      e.target.value = '';
      return;
    }

    setIsUploading(true);
    try {
      const url = await uploadFile(file);
      setAttachmentUrls([url]);
    } catch (err: any) {
      alert(err.message || 'Ошибка при загрузке файла');
    } finally {
      setIsUploading(false);
      e.target.value = '';
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Сообщения">
      <div className="flex flex-col gap-4 relative">
        {isLoading && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-[#0A0D17]/50">
            <Loader2 className="w-8 h-8 animate-spin" style={{ color: '#818CF8' }} />
          </div>
        )}

        <div
          className="rounded-2xl flex-1 shadow-[0_4px_24px_-1px_rgba(0,0,0,0.2)]"
          style={{
            paddingTop: '10px',
            paddingLeft: '15px',
            paddingRight: '15px',
            paddingBottom: '10px',
            backgroundColor: 'rgba(255, 255, 255, 0.03)',
            border: '1px solid rgba(255, 255, 255, 0.08)',
            backdropFilter: 'blur(12px)',
          }}
        >
          <textarea
            value={message}
            onChange={(e) => setMessage(e.target.value.slice(0, MAX_MESSAGE_LENGTH))}
            placeholder="Введите текст сообщения..."
            maxLength={MAX_MESSAGE_LENGTH}
            className="w-full h-48 bg-transparent text-[15px] text-white resize-none leading-relaxed placeholder:text-white/30"
            style={{ outline: 'none' }}
          />
          <p className="text-[13px] mt-0 font-medium" style={{ color: '#94A3B8' }}>
            Текст отправляется как подпись к файлу, если файл прикреплен.
          </p>
        </div>

        <div className="flex justify-between items-center">
          <span className="text-xs" style={{ color: '#64748B' }}>
            Файл {attachmentUrls.length} / {MAX_ATTACHMENT_COUNT}
          </span>
          <span className="text-xs" style={{ color: '#64748B' }}>
            {message.length} / {MAX_MESSAGE_LENGTH}
          </span>
        </div>

        {attachmentUrls.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold text-white mb-3">Прикрепленный файл</h3>
            {attachmentUrls.map((url) => {
              const fileName = url.split('/').pop() || 'file';
              return (
                <div
                  key={url}
                  className="flex items-center gap-3 py-3 rounded-xl shadow-[0_2px_12px_rgba(0,0,0,0.1)] transition-all hover:bg-white/5"
                  style={{
                    paddingLeft: '15px',
                    paddingRight: '15px',
                    backgroundColor: 'rgba(255, 255, 255, 0.03)',
                    border: '1px solid rgba(255, 255, 255, 0.08)',
                    backdropFilter: 'blur(8px)',
                  }}
                >
                  <div className="w-10 h-10 rounded-lg flex items-center justify-center" style={{ backgroundColor: 'rgba(99,102,241,0.15)' }}>
                    <FileText className="w-5 h-5 shrink-0" style={{ color: '#A5B4FC' }} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-semibold text-white truncate">{fileName}</p>
                    <p className="text-xs mt-0.5" style={{ color: '#94A3B8' }}>Загружено</p>
                  </div>
                  <button
                    onClick={() => removeFile(url)}
                    className="w-8 h-8 flex items-center justify-center rounded-full shrink-0 transition-all hover:bg-red-500/20 hover:text-red-400 active:scale-95"
                    style={{ color: '#64748B' }}
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>
              );
            })}
          </div>
        )}

        <label
          className="w-full h-[56px] rounded-xl text-[15px] font-bold flex items-center justify-center gap-2 cursor-pointer transition-all hover:bg-white/5 active:scale-[0.98]"
          style={{
            border: '1px dashed rgba(99,102,241,0.5)',
            backgroundColor: 'rgba(99,102,241,0.08)',
            color: '#C7D2FE',
          }}
        >
          {isUploading ? (
            <Loader2 className="w-5 h-5 animate-spin text-indigo-400" />
          ) : (
            <Paperclip className="w-5 h-5" />
          )}
          {isUploading ? 'Загрузка...' : `Прикрепить файл (${attachmentUrls.length}/${MAX_ATTACHMENT_COUNT})`}
          <input
            type="file"
            className="hidden"
            accept={ALLOWED_ATTACHMENT_EXTENSIONS.map((ext) => `.${ext}`).join(',')}
            onChange={handleFileUpload}
            disabled={isUploading || attachmentUrls.length >= MAX_ATTACHMENT_COUNT}
          />
        </label>

        <button
          onClick={handleSave}
          disabled={isLoading || isUploading}
          className="w-full h-[56px] rounded-xl text-[14px] font-black tracking-wide text-white transition-all hover:brightness-110 active:scale-[0.98] disabled:opacity-50"
          style={{ 
            background: 'linear-gradient(135deg, #6366F1 0%, #4F46E5 100%)',
            boxShadow: '0 8px 24px rgba(99,102,241,0.3)',
          }}
        >
          СОХРАНИТЬ
        </button>
      </div>
    </Modal>
  );
}
