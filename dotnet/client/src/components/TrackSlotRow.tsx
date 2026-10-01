import type { SlotDto } from '@/types/api';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import type { MouseEvent } from 'react';

interface TrackSlotRowProps {
  slot: SlotDto;
  isHighlighted?: boolean;
  /** 路徑中的步數（1-based），null 表示不在路徑中 */
  pathStep?: number | null;
  onSelect?: (catName: string) => void;
}

export function TrackSlotRow({ slot, isHighlighted, pathStep, onSelect }: TrackSlotRowProps) {
  const guaranteedPosition = slot.isDuplicateRare ? null : slot.nextPos;
  const showGuaranteed = !!slot.guaranteed && (guaranteedPosition !== null || slot.guaranteed !== slot.result);

  const selectName = (event: MouseEvent<HTMLButtonElement>, name: string) => {
    event.stopPropagation();
    onSelect?.(name);
  };

  return (
    <div
      role="row"
      className={cn(
        'grid h-full w-full grid-cols-[4.5rem_1fr_1fr_5rem] items-center border-b px-2 text-sm',
        slot.isDuplicateRare && 'bg-destructive/10',
        isHighlighted && 'bg-primary/10 font-medium',
        pathStep != null && 'ring-2 ring-inset ring-primary/40',
        onSelect && 'cursor-pointer hover:bg-muted',
      )}
      onClick={onSelect ? () => onSelect(slot.result) : undefined}
    >
      <span className="font-mono text-muted-foreground py-1.5">
        {pathStep != null && (
          <span className="mr-1 inline-flex size-4 items-center justify-center rounded-full bg-primary text-[10px] text-primary-foreground">
            {pathStep}
          </span>
        )}
        {slot.id}{slot.track}
      </span>
      <span className="truncate py-1.5" data-rarity={slot.rarity}>
        <button type="button" className="max-w-full truncate text-left" disabled={!onSelect}
          onClick={(event) => selectName(event, slot.result)}>{slot.result}</button>
      </span>
      <span
        className={cn('truncate py-1.5', !slot.guaranteedRarity && 'text-muted-foreground')}
        data-rarity={slot.guaranteedRarity}
      >
        {showGuaranteed && (
          <button type="button" className="max-w-full truncate text-left" disabled={!onSelect}
            onClick={(event) => selectName(event, slot.guaranteed)}>
            {slot.guaranteed}
            {guaranteedPosition && (
              <span className="ml-1 text-xs opacity-70">
                → {guaranteedPosition.id}{guaranteedPosition.track}
              </span>
            )}
          </button>
        )}
      </span>
      <span className="py-1.5">
        {slot.isDuplicateRare && (
          <Badge variant="destructive" className="text-[10px]">
            {slot.nextPos
              ? `→ ${slot.nextPos.id}${slot.nextPos.track}`
              : 'Dup'}
          </Badge>
        )}
      </span>
    </div>
  );
}
