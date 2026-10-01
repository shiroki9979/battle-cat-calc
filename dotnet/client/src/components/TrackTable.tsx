import { useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import type { SlotDto, StepDto } from '@/types/api';
import { TrackSlotRow } from '@/components/TrackSlotRow';

const ROW_HEIGHT = 36;

interface TrackTableProps {
  track: string;
  slots: SlotDto[];
  highlightNames?: Set<string>;
  /** 路徑步驟 overlay：key = "id-track" → step index (1-based) */
  pathStepMap?: Map<string, number>;
  onSelectCat?: (catName: string) => void;
}

export function TrackTable({
  track,
  slots,
  highlightNames,
  pathStepMap,
  onSelectCat,
}: TrackTableProps) {
  const parentRef = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: slots.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 20,
  });

  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-semibold tracking-wide">Track {track}</h3>

      {/* Header */}
      <div className="rounded-lg border">
        <div
          role="row"
          className="grid grid-cols-[3rem_1fr_1fr_4rem] border-b bg-muted/50 px-2 text-xs font-medium text-muted-foreground"
        >
          <span className="py-2">#</span>
          <span className="py-2">Result</span>
          <span className="py-2">Guaranteed</span>
          <span className="py-2">Flag</span>
        </div>

        {/* Virtualized body */}
        <div
          ref={parentRef}
          className="overflow-auto"
          style={{ maxHeight: '70vh' }}
        >
          <div
            style={{
              height: `${virtualizer.getTotalSize()}px`,
              position: 'relative',
            }}
          >
            {virtualizer.getVirtualItems().map((virtualRow) => {
              const slot = slots[virtualRow.index];
              const key = `${slot.id}-${slot.track}`;
              return (
                <div
                  key={key}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: `${virtualRow.size}px`,
                    transform: `translateY(${virtualRow.start}px)`,
                  }}
                >
                  <TrackSlotRow
                    slot={slot}
                    isHighlighted={highlightNames?.has(slot.result)}
                    pathStep={pathStepMap?.get(key) ?? null}
                    onSelect={onSelectCat}
                  />
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}

/** 從模擬結果的 path 建立 pathStepMap */
export function buildPathStepMap(path: StepDto[]): Map<string, number> {
  const map = new Map<string, number>();
  path.forEach((step, i) => {
    const key = `${step.source.id}-${step.source.track}`;
    if (!map.has(key)) {
      map.set(key, i + 1);
    }
  });
  return map;
}
