// Probe v4: with blazor.web.js blocked, do /list/statement-drafts and
// /list/securities/categories render .fm-table visible?
const { chromium } = require('playwright');
const { startApp, registerFirstUser, login, installImportmap, waitForDemoData } = require('./lib/app-harness');

(async () => {
  const app = await startApp({ tempPrefix: 'financemanager-probe-' });
  try {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: 'de-DE' });
    await installImportmap(context);
    let blockBlazor = false;
    await context.route('**/_framework/blazor.web.js*', r => blockBlazor ? r.abort('blockedbyclient') : r.continue());
    const page = await context.newPage();
    page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') console.log(`    [console.${m.type()}]`, m.text().slice(0, 160)); });
    page.on('framenavigated', f => { if (f === page.mainFrame()) console.log('    << nav ->', f.url().replace(app.baseUrl, '')); });

    const username = `probe${Date.now().toString(36)}`;
    const password = 'ProbeTest!123';
    await registerFirstUser(page, app.baseUrl, { username, password, createDemoData: true });
    const token = await login(page, context, app.baseUrl, { username, password });
    await waitForDemoData(app.baseUrl, token);
    blockBlazor = true;

    for (const url of ['/list/statement-drafts', '/list/securities/categories', '/list/accounts']) {
      console.log(`--- goto ${url} (blazor blocked) ---`);
      try {
        await page.goto(`${app.baseUrl}${url}`, { waitUntil: 'load', timeout: 20000 });
        console.log('    goto ok');
      } catch (e) { console.log('    goto ERR:', e.message.split('\n')[0]); }
      for (let d = 0; d <= 4000; d += 2000) {
        const st = await page.evaluate(() => {
          const t = document.querySelector('.fm-table');
          const f = document.querySelector('.list-filters');
          const r = t ? t.getBoundingClientRect() : null;
          return { url: location.pathname, table: !!t, rect: r ? `${Math.round(r.width)}x${Math.round(r.height)}` : 'n/a', filters: !!f, visible: t ? !!(t.offsetWidth || t.offsetHeight) : false };
        });
        console.log(`    [+${d}ms]`, JSON.stringify(st));
        if (d < 4000) await page.waitForTimeout(2000);
      }
    }
    await browser.close();
  } finally {
    app.stop();
    app.cleanupTempDir();
  }
})();
