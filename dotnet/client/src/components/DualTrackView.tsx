import { useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import type { TrackResponse, StepDto } from '@/types/api';
import { TrackSlotRow } from '@/components/TrackSlotRow';
import { RARITY_LABEL, RARITY_ORDER } from '@/lib/rarity';

const ROW_HEIGHT = 36;

interface DualTrackViewProps {
  data: TrackResponse;
  highlightNames?: Set<string>;
  /** key = "id-track" → step index (1-based) */
  pathStepMap?: Map<string, number>;
  onSelectCat?: (catName: string) => void;
}

export function DualTrackView({ data, highlightNames, pathStepMap, onSelectCat }: DualTrackViewProps) {
  const parentRef = useRef<HTMLDivElement>(null);
  const rowCount = Math.max(data.trackA.length, data.trackB.length);

  const virtualizer = useVirtualizer({
    count: rowCount,
    getScrollElement: () => parentRef.current,
    estimateSize: () => ROW_HEIGHT,
    overscan: 20,
  });

  return (
    <div className="rounded-lg border">
      {/* 稀有度色彩圖例 */}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b px-3 py-2 text-xs">
        {RARITY_ORDER.map((r) => (
          <span key={r} data-rarity={r}>
            {RARITY_LABEL[r]}
          </span>
        ))}
      </div>
      {/* 共用滾動容器（header 與 content 同在滾動區內，避免 scrollbar 寬度偏移） */}
      <div
        ref={parentRef}
        className="overflow-auto"
        style={{ maxHeight: '70vh' }}
      >
        {/* Sticky header */}
        <div className="sticky top-0 z-10 grid grid-cols-2 border-b bg-muted">
          <div className="border-r">
            <div className="px-2 py-2 text-sm font-semibold tracking-wide">Track A</div>
            <div
              role="row"
              className="grid grid-cols-[4.5rem_1fr_1fr_5rem] px-2 text-xs font-medium text-muted-foreground"
            >
              <span className="pb-1">#</span>
              <span className="pb-1">Result</span>
              <span className="pb-1">Guaranteed</span>
              <span className="pb-1">Flag</span>
            </div>
          </div>
          <div>
            <div className="px-2 py-2 text-sm font-semibold tracking-wide">Track B</div>
            <div
              role="row"
              className="grid grid-cols-[4.5rem_1fr_1fr_5rem] px-2 text-xs font-medium text-muted-foreground"
            >
              <span className="pb-1">#</span>
              <span className="pb-1">Result</span>
              <span className="pb-1">Guaranteed</span>
              <span className="pb-1">Flag</span>
            </div>
          </div>
        </div>
        <div
          style={{
            height: `${virtualizer.getTotalSize()}px`,
            position: 'relative',
          }}
        >
          {virtualizer.getVirtualItems().map((virtualRow) => {
            const slotA = data.trackA[virtualRow.index];
            const slotB = data.trackB[virtualRow.index];
            const keyA = slotA ? `${slotA.id}-${slotA.track}` : undefined;
            const keyB = slotB ? `${slotB.id}-${slotB.track}` : undefined;
            return (
              <div
                key={virtualRow.index}
                className="grid grid-cols-2"
                style={{
                  position: 'absolute',
                  top: 0,
                  left: 0,
                  width: '100%',
                  height: `${virtualRow.size}px`,
                  transform: `translateY(${virtualRow.start}px)`,
                }}
              >
                <div className="border-r">
                  {slotA && (
                    <TrackSlotRow
                      slot={slotA}
                      isHighlighted={highlightNames?.has(slotA.result)}
                      pathStep={keyA ? (pathStepMap?.get(keyA) ?? null) : null}
                      onSelect={onSelectCat}
                    />
                  )}
                </div>
                <div>
                  {slotB && (
                    <TrackSlotRow
                      slot={slotB}
                      isHighlighted={highlightNames?.has(slotB.result)}
                      pathStep={keyB ? (pathStepMap?.get(keyB) ?? null) : null}
                      onSelect={onSelectCat}
                    />
                  )}
                </div>
              </div>
            );
          })}
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
