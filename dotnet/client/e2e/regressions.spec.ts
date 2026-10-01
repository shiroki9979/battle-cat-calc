import { test, expect } from '@playwright/test';
import { mockAllApis, MOCK_TRACK_RESPONSE, MOCK_SIMULATE_RESPONSE } from './fixtures';

test('a cat available only as a guaranteed result can be selected and simulated', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();

  const target = page.locator('[data-slot="badge"]').filter({ hasText: /^Bahamut Cat$/ }).first();
  await expect(target).toBeVisible();
  await target.click();
  const request = page.waitForRequest('**/api/v1/simulate');
  await page.getByRole('button', { name: 'Find Optimal Path' }).click();

  expect((await request).postDataJSON().targetCats).toEqual(['Bahamut Cat']);
});

test('changing targets removes the previous simulation result', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await page.locator('[data-slot="badge"]').filter({ hasText: /^Jizo$/ }).first().click();
  await page.getByRole('button', { name: 'Find Optimal Path' }).click();
  await expect(page.getByText('3 rolls')).toBeVisible();

  await page.getByRole('button', { name: 'Clear all' }).click();

  await expect(page.getByText('3 rolls')).not.toBeVisible();
});

test('a late simulation response cannot restore a path after targets change', async ({ page }) => {
  await mockAllApis(page);
  let releaseResponse!: () => void;
  const responseGate = new Promise<void>((resolve) => { releaseResponse = resolve; });
  await page.route('**/api/v1/simulate', async (route) => {
    await responseGate;
    await route.fulfill({ json: MOCK_SIMULATE_RESPONSE });
  });
  await page.goto('/');
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await page.locator('[data-slot="badge"]').filter({ hasText: /^Jizo$/ }).first().click();
  const request = page.waitForRequest('**/api/v1/simulate');
  await page.getByRole('button', { name: 'Find Optimal Path' }).click();
  await request;

  await page.getByRole('button', { name: 'Clear all' }).click();
  const response = page.waitForResponse('**/api/v1/simulate');
  releaseResponse();
  await (await response).finished();
  await page.waitForLoadState('networkidle');

  await expect(page.getByText('3 rolls')).not.toBeVisible();
});

test('fetching a different seed removes the previous simulation result', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await page.locator('[data-slot="badge"]').filter({ hasText: /^Jizo$/ }).first().click();
  await page.getByRole('button', { name: 'Find Optimal Path' }).click();
  await expect(page.getByText('3 rolls')).toBeVisible();
  await page.route('**/api/v1/tracks*', (route) => route.fulfill({
    json: {
      ...MOCK_TRACK_RESPONSE,
      seed: '67890',
      trackA: [{ ...MOCK_TRACK_RESPONSE.trackA[0], result: 'New Seed Cat' }],
    },
  }));

  await page.getByLabel('Seed').fill('67890');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();

  await expect(page.getByText('New Seed Cat', { exact: true }).first()).toBeVisible();
  await expect(page.getByText('3 rolls')).not.toBeVisible();
});

test('clicking a guaranteed name selects that cat instead of the single-roll result', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await page.getByText('Bahamut Cat', { exact: true }).last().click();

  const request = page.waitForRequest('**/api/v1/simulate');
  await page.getByRole('button', { name: 'Find Optimal Path' }).click();

  expect((await request).postDataJSON().targetCats).toEqual(['Bahamut Cat']);
});
