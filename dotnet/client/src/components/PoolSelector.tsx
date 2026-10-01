import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

interface PoolSelectorProps {
  value: string;
  onChange: (value: string) => void;
}

export function PoolSelector({ value, onChange }: PoolSelectorProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="event">Event ID</Label>
      <Input
        id="event"
        type="text"
        inputMode="numeric"
        placeholder="e.g. 1019"
        value={value}
        onChange={(e) => {
          const v = e.target.value.replace(/\D/g, '');
          onChange(v);
        }}
      />
    </div>
  );
}
