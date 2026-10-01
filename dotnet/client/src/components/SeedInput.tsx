import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface SeedInputProps {
  value: string;
  onChange: (value: string) => void;
}

export function SeedInput({ value, onChange }: SeedInputProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="seed">Seed</Label>
      <Input
        id="seed"
        type="text"
        inputMode="numeric"
        placeholder="e.g. 2127986414"
        value={value}
        onChange={(e) => {
          // 僅允許數字輸入
          const v = e.target.value.replace(/\D/g, '');
          onChange(v);
        }}
      />
    </div>
  );
}
