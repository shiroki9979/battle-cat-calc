import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { X } from 'lucide-react';

interface TargetListProps {
  targets: string[];
  onRemove: (name: string) => void;
  onClear: () => void;
}

export function TargetList({ targets, onRemove, onClear }: TargetListProps) {
  if (targets.length === 0) return null;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium">
          Targets ({targets.length})
        </span>
        <Button variant="ghost" size="xs" onClick={onClear}>
          Clear all
        </Button>
      </div>
      <div className="flex flex-wrap gap-1.5">
        {targets.map((name) => (
          <Badge key={name} variant="secondary" className="gap-1 pr-1">
            {name}
            <button
              type="button"
              className="rounded-full p-0.5 hover:bg-muted-foreground/20"
              onClick={() => onRemove(name)}
            >
              <X className="size-3" />
            </button>
          </Badge>
        ))}
      </div>
    </div>
  );
}
