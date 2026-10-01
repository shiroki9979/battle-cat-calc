import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useCatSearch } from '@/hooks/useCatSearch';
import type { CatSearchResult } from '@/types/api';
import { Badge } from '@/components/ui/badge';

interface CatSearchProps {
  seed: string;
  event: string;
  lang: string;
  count?: number;
  onSelect: (cat: CatSearchResult) => void;
  /** 已選為目標的位置 set，格式 "id-track" */
  selectedKeys: Set<string>;
}

export function CatSearch({ seed, event, lang, count, onSelect, selectedKeys }: CatSearchProps) {
  const [name, setName] = useState('');

  const { data: results, isLoading } = useCatSearch(
    seed && event && name.length >= 2
      ? { seed, event, name, lang, count }
      : null,
  );

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor="cat-search">Search Cat</Label>
      <Input
        id="cat-search"
        type="text"
        placeholder="Cat name (min 2 chars)"
        value={name}
        onChange={(e) => setName(e.target.value)}
      />

      {isLoading && (
        <p className="text-xs text-muted-foreground">Searching...</p>
      )}

      {results && results.length > 0 && (
        <ul className="max-h-48 overflow-auto rounded-lg border divide-y text-sm">
          {results.map((cat) => {
            const key = `${cat.id}-${cat.track}`;
            const isSelected = selectedKeys.has(key);
            return (
              <li key={key}>
                <button
                  type="button"
                  className="flex w-full items-center gap-2 px-3 py-1.5 text-left hover:bg-muted disabled:opacity-50"
                  disabled={isSelected}
                  onClick={() => onSelect(cat)}
                >
                  <span className="font-mono text-muted-foreground text-xs">
                    {cat.id}{cat.track}
                  </span>
                  <span className="flex-1">{cat.name}</span>
                  {cat.isDuplicateRare && (
                    <Badge variant="destructive" className="text-[10px]">Dup</Badge>
                  )}
                  {isSelected && (
                    <Badge variant="secondary" className="text-[10px]">Added</Badge>
                  )}
                </button>
              </li>
            );
          })}
        </ul>
      )}

      {results && results.length === 0 && name.length >= 2 && (
        <p className="text-xs text-muted-foreground">No results found</p>
      )}
    </div>
  );
}
