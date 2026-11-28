const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const {
  repoRoot,
  waitFor,
  startApp,
  registerFirstUser,
  login,
  waitForDemoData,
  installImportmap
} = require('./lib/app-harness');

const outputDir = path.join(repoRoot, 'Docs', 'screenshots');

fs.mkdirSync(outputDir, { recursive: true });

(async () => {
  const app = await startApp();

  let keepTempDir = false;
  try {
    const browser = await chromium.launch({ headless: true, args: ['--disable-features=LocalNetworkAccessChecks,BlockInsecurePrivateNetworkRequests', '--allow-insecure-localhost'] });
    const context = await browser.newContext({
      viewport: { width: 1280, height: 900 },
      locale: 'de-DE',
      timezoneId: 'Europe/Berlin'
    });
    await installImportmap(context);
    const page = await context.newPage();

    const screenshot = (name) => page.screenshot({ path: path.join(outputDir, `${name}.png`), fullPage: false });

    // 1. Registration page with the demo-data checkbox (only shown for the first user).
    await page.goto(`${app.baseUrl}/register`, { waitUntil: 'load' });
    await page.waitForSelector('#create-demo-data', { timeout: 30000 });
    await page.waitForTimeout(500);
    await screenshot('register');
    console.log('Screenshot: register');

    const username = `demo${Date.now().toString(36)}`;
    const password = 'DemoTest!123';

    // Register through the real UI so the demo-data flag goes through the same
    // path a user takes. Blazor may need a moment to attach event handlers, so
    // the submit is retried like in the E2E tests.
    await registerFirstUser(page, app.baseUrl, { username, password, createDemoData: true });

    // 2. Registration itself does not authenticate the browser, so log in
    // through the UI afterwards.
    const token = await login(page, context, app.baseUrl, { username, password });

    // 3. Wait for the demo-data background task to finish. The auth cookie
    // contains the raw JWT, so it can be reused as a bearer token.
    await waitForDemoData(app.baseUrl, token);

    // Generic helper for the table-based list pages.
    const shootListPage = async (route, name, minRows) => {
      await page.goto(`${app.baseUrl}${route}`, { waitUntil: 'load' });
      await waitFor(async () => {
        const rows = await page.locator('.fm-table tbody tr').count();
        return rows >= minRows;
      }, 60000, 500, `list rows on ${route}`);
      await page.waitForTimeout(400);
      await screenshot(name);
      console.log(`Screenshot: ${name}`);
    };

    // 4. Home with the seeded KPI tiles.
    await page.goto(`${app.baseUrl}/`, { waitUntil: 'load' });
    await waitFor(async () => {
      const count = await page.locator('.kpi-grid .kpi-tile').count();
      return count >= 3;
    }, 60000, 500, 'KPI tiles on /');
    await page.waitForTimeout(1000);
    await screenshot('home');
    console.log('Screenshot: home');

    // 5. Master data and statement drafts.
    await shootListPage('/list/accounts', 'accounts', 3);
    await shootListPage('/list/statement-drafts', 'statement-drafts', 1);
    await shootListPage('/list/contacts', 'contacts', 10);
    await shootListPage('/list/savings-plans', 'savings-plans', 5);
    await shootListPage('/list/securities', 'securities', 2);
    await shootListPage('/list/budget/purposes', 'budget-purposes', 5);

    // 6. Reports overview with the seeded report favorites.
    await page.goto(`${app.baseUrl}/reports`, { waitUntil: 'load' });
    await waitFor(async () => {
      const count = await page.locator('.fav-grid .fav-card').count();
      return count >= 1;
    }, 60000, 500, 'report favorites on /reports');
    await page.waitForTimeout(400);
    await screenshot('reports');
    console.log('Screenshot: reports');

    // 7. Budget report for the previous month (contains booked postings).
    await page.goto(`${app.baseUrl}/reports/budget`, { waitUntil: 'load' });
    await waitFor(async () => {
      return await page.locator('.budget-report-table').count() > 0;
    }, 90000, 500, 'budget report table');
    const periodBefore = await page.locator('.budget-report-period').first().innerText().catch(() => '');
    const prevMonth = page.locator('#PrevMonth');
    if (await prevMonth.count() > 0) {
      await prevMonth.click();
      await waitFor(async () => {
        const loading = await page.locator('.budget-report-loading').count();
        const periodNow = await page.locator('.budget-report-period').first().innerText().catch(() => '');
        return loading === 0 && periodNow !== periodBefore;
      }, 60000, 500, 'budget report month change');
      await page.waitForTimeout(400);
    }
    await screenshot('budget-report');
    console.log('Screenshot: budget-report');

    await context.close();
    await browser.close();
    console.log(`Screenshots saved to ${outputDir}`);
  } catch (err) {
    keepTempDir = true;
    console.error(`Failed; keeping app log and database at ${app.tempDbDir}`);
    throw err;
  } finally {
    app.stop();
    if (!keepTempDir) {
      app.cleanupTempDir();
    }
  }
})();
