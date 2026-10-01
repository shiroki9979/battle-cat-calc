import { Label } from '@/components/ui/label';

export const LANGUAGES = [
  { value: 'tw', label: 'BCTW' },
  { value: 'en', label: 'BCEN' },
  { value: 'jp', label: 'BCJP' },
  { value: 'kr', label: 'BCKR' },
] as const;

export type Lang = (typeof LANGUAGES)[number]['value'];

export const DEFAULT_LANG: Lang = 'tw';

interface LangSelectorProps {
  value: Lang;
  onChange: (value: Lang) => void;
}

export function LangSelector({ value, onChange }: LangSelectorProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="lang">Language</Label>
      <select
        id="lang"
        value={value}
        onChange={(e) => onChange(e.target.value as Lang)}
        className="h-9 w-28 rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs focus:outline-none focus:ring-1 focus:ring-ring"
      >
        {LANGUAGES.map((l) => (
          <option key={l.value} value={l.value}>
            {l.label}
          </option>
        ))}
      </select>
    </div>
  );
}
