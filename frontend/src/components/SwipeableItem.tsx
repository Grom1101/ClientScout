import { useRef, useState, useCallback } from 'react';
import { Trash2 } from 'lucide-react';
import type { ReactNode } from 'react';

interface SwipeableItemProps {
  onDelete: () => void;
  children: ReactNode;
}

export default function SwipeableItem({ onDelete, children }: SwipeableItemProps) {
  const [offset, setOffset] = useState(0);
  const [swiped, setSwiped] = useState(false);
  const startX = useRef(0);
  const isDragging = useRef(false);

  /* ── Touch events ── */
  const handleTouchStart = useCallback((e: React.TouchEvent) => {
    startX.current = e.touches[0].clientX;
    isDragging.current = true;
  }, []);

  const handleTouchMove = useCallback((e: React.TouchEvent) => {
    if (!isDragging.current) return;
    const diff = startX.current - e.touches[0].clientX;
    if (diff > 0) {
      setOffset(Math.min(diff, 80));
    } else if (swiped) {
      setOffset(Math.max(80 + diff, 0));
    } else {
      setOffset(0);
    }
  }, [swiped]);

  const handleTouchEnd = useCallback(() => {
    isDragging.current = false;
    if (offset > 40) {
      setOffset(80);
      setSwiped(true);
    } else {
      setOffset(0);
      setSwiped(false);
    }
  }, [offset]);

  /* ── Mouse events (desktop testing) ── */
  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    startX.current = e.clientX;
    isDragging.current = true;

    const handleMouseMove = (ev: MouseEvent) => {
      if (!isDragging.current) return;
      const diff = startX.current - ev.clientX;
      if (diff > 0) {
        setOffset(Math.min(diff, 80));
      } else {
        setOffset(Math.max(0, swiped ? 80 + diff : 0));
      }
    };

    const handleMouseUp = () => {
      isDragging.current = false;
      setOffset((prev) => {
        if (prev > 40) {
          setSwiped(true);
          return 80;
        }
        setSwiped(false);
        return 0;
      });
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mouseup', handleMouseUp);
    };

    window.addEventListener('mousemove', handleMouseMove);
    window.addEventListener('mouseup', handleMouseUp);
  }, [swiped]);

  return (
    <div className="relative overflow-hidden rounded-xl select-none">
      {/* Delete button behind the item */}
      <button
        onClick={onDelete}
        className="absolute inset-y-0 right-0 w-20 flex items-center justify-center transition-colors hover:bg-red-600 active:bg-red-700"
        style={{ backgroundColor: '#EF4444' }}
      >
        <Trash2 className="w-5 h-5 text-white" />
      </button>

      {/* Item content — slides to reveal delete */}
      <div
        style={{
          transform: `translateX(-${offset}px)`,
          transition: isDragging.current ? 'none' : 'transform 0.3s ease',
          backgroundColor: '#141828',
        }}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
        onMouseDown={handleMouseDown}
      >
        {children}
      </div>
    </div>
  );
}
