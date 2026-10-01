import { useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import type { TrackResponse } from '@/types/api';

interface CatPickerProps {
  data: TrackResponse;
  targets: Set<string>;
  onToggle: (catName: string) => void;
}

/** 從軌道資料中提取不重複的貓咪名稱，依首次出現順序排列 */
function getUniqueCats(data: TrackResponse): string[] {
  const seen = new Set<string>();
  const result: string[] = [];
  for (const slot of [...data.trackA, ...data.trackB]) {
    for (const name of [slot.result, slot.guaranteed]) {
      if (name && !seen.has(name)) {
        seen.add(name);
        result.push(name);
      }
    }
  }
  return result;
}

export function CatPicker({ data, targets, onToggle }: CatPickerProps) {
  const uniqueCats = useMemo(() => getUniqueCats(data), [data]);
  const [filter, setFilter] = useState('');

  const filtered = useMemo(() => {
    if (!filter) return uniqueCats;
    const lower = filter.toLowerCase();
    return uniqueCats.filter((name) => name.toLowerCase().includes(lower));
  }, [uniqueCats, filter]);

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="cat-filter">Select Targets ({targets.size} selected)</Label>
      <Input
        id="cat-filter"
        type="text"
        placeholder="Filter cats..."
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
      />
      <div className="flex flex-wrap gap-1.5 max-h-48 overflow-auto rounded-lg border p-2">
        {filtered.map((name) => {
          const isSelected = targets.has(name);
          return (
            <Badge
              key={name}
              variant={isSelected ? 'default' : 'outline'}
              className="cursor-pointer select-none"
              onClick={() => onToggle(name)}
            >
              {name}
            </Badge>
          );
        })}
        {filtered.length === 0 && (
          <span className="text-xs text-muted-foreground">No matches</span>
        )}
      </div>
    </div>
  );
}
