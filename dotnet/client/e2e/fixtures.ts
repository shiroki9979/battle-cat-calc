import type { Page } from '@playwright/test';
import type {
  TrackResponse,
  SimulateResponse,
  EventDto,
} from '../src/types/api';

// --- Mock data ---

export const MOCK_EVENTS: EventDto[] = [
  { value: '2026-03-30_943', label: '2026-03-30 ~ 2026-04-01: Test Event A', group: 'Upcoming' },
  { value: '2026-04-01_944', label: '2026-04-01 ~ 2026-04-03: Test Event B', group: 'Upcoming' },
  { value: '2025-01-01_100', label: '2025-01-01 ~ 2025-01-03: Old Event', group: 'Past' },
];

export const MOCK_TRACK_RESPONSE: TrackResponse = {
  seed: '12345',
  eventId: '1019',
  trackA: [
    { id: 1, track: 'A', result: 'Archer Cat', guaranteed: 'Bahamut Cat', nextPos: null, isDuplicateRare: false, rarity: 'Rare', guaranteedRarity: 'UberRare' },
    { id: 2, track: 'A', result: 'Swordsman Cat', guaranteed: 'Ururun Wolf', nextPos: null, isDuplicateRare: false, rarity: 'Rare', guaranteedRarity: 'UberRare' },
    { id: 3, track: 'A', result: 'Archer Cat', guaranteed: 'Catman', nextPos: { id: 3, track: 'B' }, isDuplicateRare: true, rarity: 'Rare', guaranteedRarity: 'UberRare' },
    { id: 4, track: 'A', result: 'Cat Machine', guaranteed: 'Cat Machine', nextPos: null, isDuplicateRare: false, rarity: 'UberRare', guaranteedRarity: 'UberRare' },
    { id: 5, track: 'A', result: 'Jizo', guaranteed: 'Jizo', nextPos: null, isDuplicateRare: false, rarity: 'UberRare', guaranteedRarity: 'UberRare' },
  ],
  trackB: [
    { id: 1, track: 'B', result: 'Gross Cat', guaranteed: 'Tecoluga', nextPos: null, isDuplicateRare: false, rarity: 'Rare', guaranteedRarity: 'UberRare' },
    { id: 2, track: 'B', result: 'Cow Cat', guaranteed: 'Togeluga', nextPos: null, isDuplicateRare: false, rarity: 'Rare', guaranteedRarity: 'UberRare' },
    { id: 3, track: 'B', result: 'Jizo', guaranteed: 'Jizo', nextPos: null, isDuplicateRare: false, rarity: 'UberRare', guaranteedRarity: 'UberRare' },
    { id: 4, track: 'B', result: 'Witch Cat', guaranteed: 'Witch Cat', nextPos: null, isDuplicateRare: false, rarity: 'SuperRare', guaranteedRarity: 'SuperRare' },
    { id: 5, track: 'B', result: 'Archer Cat', guaranteed: 'Archer Cat', nextPos: null, isDuplicateRare: false, rarity: 'Rare', guaranteedRarity: 'Rare' },
  ],
};

export const MOCK_SIMULATE_RESPONSE: SimulateResponse = {
  totalCost: 3,
  stepCount: 3,
  guaranteedCount: 0,
  singleRollCount: 3,
  collected: ['Jizo'],
  startPos: { id: 1, track: 'A' },
  endPos: { id: 4, track: 'A' },
  path: [
    { source: { id: 1, track: 'A' }, target: { id: 2, track: 'A' }, unit: 'Archer Cat', poolId: '1019', isGuaranteed: false },
    { source: { id: 2, track: 'A' }, target: { id: 3, track: 'A' }, unit: 'Swordsman Cat', poolId: '1019', isGuaranteed: false },
    { source: { id: 3, track: 'A' }, target: { id: 3, track: 'B' }, unit: 'Archer Cat', poolId: '1019', isGuaranteed: false },
  ],
};

// --- Route helpers ---

/**
 * 攔截所有 /api/v1/* 請求，回傳 mock 資料。
 * 未被 mock 的 API 回傳 404。
 */
export async function mockAllApis(page: Page) {
  await page.route('**/api/v1/events*', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_EVENTS),
    });
  });

  await page.route('**/api/v1/tracks*', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_TRACK_RESPONSE),
    });
  });

  await page.route('**/api/v1/simulate', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_SIMULATE_RESPONSE),
    });
  });
}
