import { useState, useMemo, useRef, useEffect } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useEvents } from '@/hooks/useEvents';
import type { EventDto } from '@/types/api';
import type { Lang } from '@/components/LangSelector';

interface EventSelectorProps {
  value: string;
  onChange: (value: string) => void;
  lang: Lang;
}

interface GroupedEvents {
  group: string;
  items: EventDto[];
}

function groupEvents(events: EventDto[]): GroupedEvents[] {
  const map = new Map<string, EventDto[]>();
  for (const e of events) {
    const list = map.get(e.group) ?? [];
    list.push(e);
    map.set(e.group, list);
  }
  return Array.from(map, ([group, items]) => ({ group, items }));
}

export function EventSelector({ value, onChange, lang }: EventSelectorProps) {
  const { data: events, isLoading, isError } = useEvents(lang);
  const [filter, setFilter] = useState('');
  const selectRef = useRef<HTMLSelectElement>(null);

  // 篩選事件：以關鍵字過濾 label 或 value
  const filtered = useMemo(() => {
    if (!events) return [];
    if (!filter.trim()) return events;
    const lower = filter.toLowerCase();
    return events.filter(
      (e) =>
        e.label.toLowerCase().includes(lower) ||
        e.value.toLowerCase().includes(lower),
    );
  }, [events, filter]);

  const grouped = useMemo(() => groupEvents(filtered), [filtered]);

  // 當選到的值不在篩選結果中時，自動選第一個
  useEffect(() => {
    if (filtered.length > 0 && !filtered.some((e) => e.value === value)) {
      onChange(filtered[0].value);
    }
  }, [filtered, value, onChange]);

  if (isLoading) {
    return (
      <div className="flex flex-col gap-1.5">
        <Label>Event</Label>
        <span className="text-sm text-muted-foreground">Loading events...</span>
      </div>
    );
  }

  if (isError || !events) {
    return (
      <div className="flex flex-col gap-1.5">
        <Label>Event</Label>
        <span className="text-sm text-destructive">Failed to load events</span>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor="event-filter">Event</Label>
      <Input
        id="event-filter"
        type="text"
        placeholder="Filter events..."
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        className="h-8 text-sm"
      />
      <select
        ref={selectRef}
        id="event"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="h-9 w-full min-w-[320px] rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs focus:outline-none focus:ring-1 focus:ring-ring"
      >
        {filtered.length === 0 && (
          <option value="" disabled>
            No matching events
          </option>
        )}
        {grouped.map((g) => (
          <optgroup key={g.group} label={g.group}>
            {g.items.map((e) => (
              <option key={e.value} value={e.value}>
                {e.label}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
      <span className="text-xs text-muted-foreground">
        {filtered.length} of {events.length} events
      </span>
    </div>
  );
}
