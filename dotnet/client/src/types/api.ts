// 對應後端 BattleCatCalc.Api.Contracts 的 TypeScript 型別

// --- 共用 ---

export interface Position {
  id: number;
  track: string;
}

// --- Track ---

export interface TrackParams {
  seed: string;
  event: string;
  lang?: string;
  count?: number;
}

export type CatRarity =
  | 'Rare'
  | 'SuperRare'
  | 'SuperRareFest'
  | 'UberRare'
  | 'UberRareFest'
  | 'Exclusive'
  | 'Legend';

export interface SlotDto {
  id: number;
  track: string;
  result: string;
  guaranteed: string;
  nextPos: Position | null;
  isDuplicateRare: boolean;
  rarity: CatRarity | null;
  guaranteedRarity: CatRarity | null;
}

export interface TrackResponse {
  seed: string;
  eventId: string;
  trackA: SlotDto[];
  trackB: SlotDto[];
}

// --- Simulate ---

export interface PoolSpec {
  eventId: string;
  lang?: string;
  count?: number;
}

export interface SimulateRequest {
  seed: string;
  pools: PoolSpec[];
  startPos: Position;
  targetCats: string[];
  maxRolls?: number;
  beamWidth?: number;
}

export interface StepDto {
  source: Position;
  target: Position;
  unit: string;
  poolId: string;
  isGuaranteed: boolean;
}

export interface SimulateResponse {
  totalCost: number;
  stepCount: number;
  guaranteedCount: number;
  singleRollCount: number;
  collected: string[];
  startPos: Position;
  endPos: Position | null;
  path: StepDto[];
}

// --- Cat Search ---

export interface CatSearchParams {
  seed: string;
  event: string;
  name: string;
  lang?: string;
  count?: number;
}

export interface CatSearchResult {
  id: number;
  track: string;
  name: string;
  guaranteed: string;
  isDuplicateRare: boolean;
}

// --- Event ---

export interface EventDto {
  value: string;
  label: string;
  group: string;
}

// --- Error ---

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
}
