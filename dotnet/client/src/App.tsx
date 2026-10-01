import { useState, useCallback, useMemo, useEffect } from 'react';
import { useSearchParams } from 'react-router';
import { SeedInput } from '@/components/SeedInput';
import { EventSelector } from '@/components/EventSelector';
import { LangSelector, DEFAULT_LANG, LANGUAGES } from '@/components/LangSelector';
import type { Lang } from '@/components/LangSelector';
import { DualTrackView } from '@/components/DualTrackView';
import { CatPicker } from '@/components/CatPicker';
import { TargetList } from '@/components/TargetList';
import { ResultPanel } from '@/components/ResultPanel';
import { useTrackQuery } from '@/hooks/useTrackQuery';
import { useSimulate } from '@/hooks/useSimulate';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Search, Play } from 'lucide-react';
import { buildPathStepMap } from '@/components/DualTrackView';

const VALID_LANGS = new Set(LANGUAGES.map((l) => l.value));

function isValidLang(v: string | null): v is Lang {
  return v !== null && VALID_LANGS.has(v as Lang);
}

function App() {
  const [searchParams, setSearchParams] = useSearchParams();

  // 從 URL 讀取 lang / event / seed / count，值不合法則使用預設值
  const langParam = searchParams.get('lang');
  const lang: Lang = isValidLang(langParam) ? langParam : DEFAULT_LANG;
  const event = searchParams.get('event') ?? '';
  const seed = searchParams.get('seed') ?? '';
  const countParam = searchParams.get('count');
  const count = countParam && /^\d+$/.test(countParam) ? countParam : '100';

  const setLang = useCallback(
    (newLang: Lang) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set('lang', newLang);
        return next;
      }, { replace: true });
    },
    [setSearchParams],
  );

  const setEvent = useCallback(
    (newEvent: string) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (newEvent) {
          next.set('event', newEvent);
        } else {
          next.delete('event');
        }
        return next;
      }, { replace: true });
    },
    [setSearchParams],
  );

  const setSeed = useCallback(
    (newSeed: string) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (newSeed) {
          next.set('seed', newSeed);
        } else {
          next.delete('seed');
        }
        return next;
      }, { replace: true });
    },
    [setSearchParams],
  );

  const setCount = useCallback(
    (newCount: string) => {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (newCount && newCount !== '100') {
          next.set('count', newCount);
        } else {
          next.delete('count');
        }
        return next;
      }, { replace: true });
    },
    [setSearchParams],
  );

  const [targets, setTargets] = useState<string[]>([]);

  // 僅在使用者送出後才觸發查詢，避免每次按鍵都發請求
  const [submittedParams, setSubmittedParams] = useState<{
    seed: string;
    event: string;
    lang: string;
    count: number;
  } | null>(null);

  const { data, isLoading, isError, error } = useTrackQuery(
    submittedParams
      ? { seed: submittedParams.seed, event: submittedParams.event, lang: submittedParams.lang, count: submittedParams.count }
      : null,
  );

  const handleSubmit = useCallback(() => {
    if (seed && event) {
      setSubmittedParams({ seed, event, lang, count: Number(count) });
      setTargets([]);
    }
  }, [seed, event, lang, count]);

  // 點選切換目標貓咪（加入或移除）
  const handleToggleCat = useCallback((catName: string) => {
    setTargets((prev) =>
      prev.includes(catName)
        ? prev.filter((t) => t !== catName)
        : [...prev, catName],
    );
  }, []);

  const handleRemoveTarget = useCallback((name: string) => {
    setTargets((prev) => prev.filter((t) => t !== name));
  }, []);

  const handleClearTargets = useCallback(() => {
    setTargets([]);
  }, []);

  // 高亮目標貓咪名稱
  const highlightNames = useMemo(() => new Set(targets), [targets]);

  // --- Simulate ---
  const simulate = useSimulate();
  const { reset } = simulate;

  useEffect(() => {
    // Detach the previous mutation so late responses cannot restore stale paths.
    reset();
  }, [submittedParams, targets, reset]);

  // 路徑步驟 overlay map
  const pathStepMap = useMemo(
    () => (simulate.data ? buildPathStepMap(simulate.data.path) : undefined),
    [simulate.data],
  );

  const handleSimulate = useCallback(() => {
    if (!submittedParams || targets.length === 0) return;
    simulate.mutate({
      seed: submittedParams.seed,
      pools: [{ eventId: submittedParams.event, lang: submittedParams.lang, count: submittedParams.count }],
      startPos: { id: 1, track: 'A' },
      targetCats: targets,
    });
  }, [submittedParams, targets, simulate]);

  return (
    <div className="mx-auto max-w-7xl px-4 py-6 space-y-6">
      {/* Header */}
      <header>
        <h1 className="text-xl font-bold tracking-tight">
          BattleCatCalc Tracker
        </h1>
        <p className="text-sm text-muted-foreground">
          Seed tracking &amp; gacha path optimizer
        </p>
      </header>

      {/* Controls */}
      <Card>
        <CardContent className="flex flex-col gap-5 pt-4">
          <div className="flex flex-wrap items-end gap-x-6 gap-y-4">
            <div className="w-full sm:w-[240px]">
              <SeedInput value={seed} onChange={setSeed} />
            </div>
            <LangSelector value={lang} onChange={setLang} />
            <div className="flex flex-col gap-1.5">
              <label htmlFor="count" className="text-sm font-medium">Count</label>
              <input
                id="count"
                type="text"
                inputMode="numeric"
                value={count}
                onChange={(e) => setCount(e.target.value.replace(/\D/g, ''))}
                className="h-9 w-20 rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs focus:outline-none focus:ring-1 focus:ring-ring"
              />
            </div>
            <Button
              className="ml-auto"
              onClick={handleSubmit}
              disabled={!seed || !event || isLoading}
            >
              <Search data-icon="inline-start" />
              {isLoading ? 'Loading...' : 'Fetch Tracks'}
            </Button>
          </div>
          <div className="w-full">
            <EventSelector value={event} onChange={setEvent} lang={lang} />
          </div>
        </CardContent>
      </Card>

      {/* Error */}
      {isError && (
        <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
          {(error as Error).message || 'Failed to fetch tracks'}
        </div>
      )}

      {/* Cat picker & Targets — 僅在已載入軌道資料時顯示 */}
      {data && (
        <Card>
          <CardContent className="grid grid-cols-1 gap-4 pt-4 md:grid-cols-2">
            <CatPicker
              data={data}
              targets={highlightNames}
              onToggle={handleToggleCat}
            />
            <TargetList
              targets={targets}
              onRemove={handleRemoveTarget}
              onClear={handleClearTargets}
            />
          </CardContent>
        </Card>
      )}

      {/* Simulate */}
      {data && targets.length > 0 && (
        <div className="flex items-center gap-3">
          <Button
            onClick={handleSimulate}
            disabled={simulate.isPending}
            size="lg"
          >
            <Play data-icon="inline-start" />
            {simulate.isPending ? 'Simulating...' : 'Find Optimal Path'}
          </Button>
          {simulate.isError && (
            <span className="text-sm text-destructive">
              {(simulate.error as Error).message}
            </span>
          )}
        </div>
      )}

      {/* Result */}
      {simulate.data && <ResultPanel result={simulate.data} />}

      {/* Dual Track View */}
      {data && (
        <DualTrackView
          data={data}
          highlightNames={highlightNames}
          pathStepMap={pathStepMap}
          onSelectCat={handleToggleCat}
        />
      )}
    </div>
  );
}

export default App;
