import { test, expect } from '@playwright/test';
import { mockAllApis, MOCK_EVENTS } from './fixtures';

// --- Smoke ---

test('homepage loads with header and controls', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'BattleCatCalc Tracker' })).toBeVisible();
  await expect(page.getByLabel('Seed')).toBeVisible();
  await expect(page.locator('#lang')).toBeVisible();
  await expect(page.locator('#event')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Fetch Tracks' })).toBeDisabled();
});

test('fetch button is disabled until seed is filled', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  const fetchBtn = page.getByRole('button', { name: 'Fetch Tracks' });

  // 等待事件下拉選單載入（自動選取第一個事件）
  await expect(page.locator('#event')).toBeVisible();
  await expect(fetchBtn).toBeDisabled();

  // 填入 seed 後按鈕啟用（event 已自動選取）
  await page.getByLabel('Seed').fill('12345');
  await expect(fetchBtn).toBeEnabled();
});

// --- Track display ---

test('fetching tracks displays dual track tables', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();

  // 等待 Track A / Track B 標題出現
  await expect(page.getByText('Track A')).toBeVisible();
  await expect(page.getByText('Track B')).toBeVisible();

  // 驗證第一列資料
  await expect(page.getByText('Archer Cat').first()).toBeVisible();
  await expect(page.getByText('Gross Cat').first()).toBeVisible();
});

test('duplicate rare slots show Dup badge', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();

  await expect(page.getByText('Track A')).toBeVisible();

  // id=3 Track A 的 Archer Cat 是 duplicate rare，應有軌道切換 badge
  await expect(page.getByText('→ 3B')).toBeVisible();
});

// --- Cat picker ---

test('cat picker shows unique cats and adds to target list', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  // 先載入軌道
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await expect(page.getByText('Track A')).toBeVisible();

  // 貓咪選擇器顯示 unique 貓咪
  await expect(page.getByText('Select Targets (0 selected)')).toBeVisible();

  // 點選 badge 加入目標
  await page.locator('[data-slot="badge"]', { hasText: 'Jizo' }).first().click();
  await expect(page.getByText('Targets (1)')).toBeVisible();
  await expect(page.getByText('Select Targets (1 selected)')).toBeVisible();
});

test('clicking track row toggles cat in target list', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await expect(page.getByText('Track A')).toBeVisible();

  // 點選 Track A 的 Cat Machine 列 → 加入目標
  await page.getByText('Cat Machine').first().click();
  await expect(page.getByText('Targets (1)')).toBeVisible();

  // 再次點選 → 移除目標
  await page.getByText('Cat Machine').first().click();
  await expect(page.getByText(/^Targets \(\d+\)$/)).not.toBeVisible();
});

// --- Simulate ---

test('simulate shows result panel with path', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  // 載入軌道
  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await expect(page.getByText('Track A')).toBeVisible();

  // 選取目標
  await page.locator('[data-slot="badge"]', { hasText: 'Jizo' }).first().click();

  // 執行模擬
  await page.getByRole('button', { name: 'Find Optimal Path' }).click();

  // 結果面板
  await expect(page.getByText('Result').first()).toBeVisible();
  await expect(page.getByText('3 rolls')).toBeVisible();
  await expect(page.getByText('3 single + 0 guaranteed')).toBeVisible();

  // 路徑表格有步驟資料
  await expect(page.getByText('Archer Cat').first()).toBeVisible();
});

test('clear all targets removes them', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  await page.getByLabel('Seed').fill('12345');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();
  await expect(page.getByText('Track A')).toBeVisible();

  // 加入目標
  await page.getByText('Cat Machine').first().click();
  await expect(page.getByText('Targets (1)')).toBeVisible();

  // 清除
  await page.getByRole('button', { name: 'Clear all' }).click();
  await expect(page.getByText(/^Targets \(\d+\)$/)).not.toBeVisible();
});

// --- Error handling ---

test('shows error when API returns failure', async ({ page }) => {
  // Mock events API (正常) + tracks API (錯誤)
  await page.route('**/api/v1/events*', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_EVENTS),
    });
  });
  await page.route('**/api/v1/tracks*', async (route) => {
    await route.fulfill({
      status: 422,
      contentType: 'application/json',
      body: JSON.stringify({
        type: 'about:blank',
        title: 'Parse Error',
        status: 422,
        detail: 'Invalid seed format',
      }),
    });
  });

  await page.goto('/');
  await page.getByLabel('Seed').fill('99999');
  await page.getByRole('button', { name: 'Fetch Tracks' }).click();

  await expect(page.getByText('Invalid seed format')).toBeVisible();
});

// --- Input validation ---

test('seed input only accepts numeric characters', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');
  const seedInput = page.getByLabel('Seed');

  await seedInput.fill('abc12345xyz');
  await expect(seedInput).toHaveValue('12345');
});

test('event filter narrows dropdown options', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  // 等待事件下拉載入
  await expect(page.locator('#event option')).not.toHaveCount(0);

  // 預設顯示全部事件
  await expect(page.getByText('3 of 3 events')).toBeVisible();

  // 輸入篩選條件
  await page.getByLabel('Event').fill('Old');
  await expect(page.getByText('1 of 3 events')).toBeVisible();
});

test('language selector defaults to BCTW and can switch', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  const langSelect = page.locator('#lang');

  // 預設為 tw (BCTW)
  await expect(langSelect).toHaveValue('tw');

  // 切換到 en → URL 同步更新
  await langSelect.selectOption('en');
  await expect(langSelect).toHaveValue('en');
  await expect(page).toHaveURL(/[?&]lang=en/);

  // 切換到 jp
  await langSelect.selectOption('jp');
  await expect(langSelect).toHaveValue('jp');
  await expect(page).toHaveURL(/[?&]lang=jp/);
});

test('lang, event, and seed are restored from URL search params', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/?lang=en&event=2025-01-01_100&seed=12345');

  // 語言從 URL 還原
  await expect(page.locator('#lang')).toHaveValue('en');

  // 等待事件下拉載入後，event 從 URL 還原
  await expect(page.locator('#event option')).not.toHaveCount(0);
  await expect(page.locator('#event')).toHaveValue('2025-01-01_100');

  // seed 從 URL 還原
  await expect(page.getByLabel('Seed')).toHaveValue('12345');
});

test('selecting event updates URL search params', async ({ page }) => {
  await mockAllApis(page);
  await page.goto('/');

  // 等待事件下拉載入（自動選取第一個）
  await expect(page.locator('#event option')).not.toHaveCount(0);
  await expect(page).toHaveURL(/[?&]event=/);

  // 手動選擇另一個事件
  await page.locator('#event').selectOption('2025-01-01_100');
  await expect(page).toHaveURL(/event=2025-01-01_100/);
});
