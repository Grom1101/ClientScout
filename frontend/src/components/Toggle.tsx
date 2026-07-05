interface ToggleProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
}

export default function Toggle({ checked, onChange }: ToggleProps) {
  return (
    <button
      onClick={() => onChange(!checked)}
      className="relative w-12 h-7 rounded-full transition-colors duration-200 shrink-0"
      style={{ backgroundColor: checked ? '#10B981' : 'rgba(255,255,255,0.16)' }}
    >
      <div
        className="absolute top-0.5 w-6 h-6 rounded-full bg-white shadow-md transition-transform duration-200"
        style={{ transform: `translateX(${checked ? '22px' : '2px'})` }}
      />
    </button>
  );
}
