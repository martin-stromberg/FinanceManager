// E2E acceptance for iteration 5 (PR #592 comment 7212645702):
// single-row ribbon — groups never wrap to multiple rows; on space shortage
// labels compact first, then rightmost groups collapse into popover triggers.
// Mobile accordion (<=900px) stays unchanged.
const path = require('path');
const fs = require('fs');
const { chromium } = require('playwright');
const {
  startApp, installImportmap, registerFirstUser, login, waitForDemoData
} = require('./lib/app-harness');

const outDir = path.join(__dirname, 'out-iter5');
fs.mkdirSync(outDir, { recursive: true });

const results = [];
function check(name, ok, detail = '') {
  results.push({ name, ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${name}${detail ? ' — ' + detail : ''}`);
}

const rowInfo = () => {
  const groups = document.querySelector('.fm-ribbon-groups');
  if (!groups) return { items: 0, tops: [], overflow: false };
  const bounds = groups.getBoundingClientRect();
  const items = Array.from(groups.children).filter(el => el.offsetParent !== null);
  const tops = [...new Set(items.map(el => Math.round(el.getBoundingClientRect().top)))];
  const last = items.length ? items[items.length - 1].getBoundingClientRect() : null;
  return {
    items: items.length,
    tops,
    overflow: last ? last.right > bounds.right + 1 : false,
    docScrollW: document.documentElement.scrollWidth,
    docClientW: document.documentElement.clientWidth
  };
};

(async () => {
  const app = await startApp({ tempPrefix: 'fm-iter5-' });
  const browser = await chromium.launch();
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    await installImportmap(context);
    const page = await context.newPage();
    page.on('pageerror', e => console.log('PAGEERROR', e.message));

    await registerFirstUser(page, app.baseUrl, { username: 'iter5', password: 'Iter5-Pass!' });
    const token = await login(page, context, app.baseUrl, { username: 'iter5', password: 'Iter5-Pass!' });
    await waitForDemoData(app.baseUrl, token);

    const settle = async () => page.waitForTimeout(450);

    // --- Scenario 1: accounts overview at ~half screen width (the complaint page)
    await page.setViewportSize({ width: 960, height: 900 });
    await page.goto(`${app.baseUrl}/list/accounts`, { waitUntil: 'load' });
    await page.waitForSelector('.fm-ribbon-group-desktop', { timeout: 30000 });
    await settle();
    let info = await page.evaluate(rowInfo);
    check('accounts@960 single row', info.tops.length === 1 && info.items > 0, JSON.stringify(info));
    check('accounts@960 no row overflow', !info.overflow && info.docScrollW <= info.docClientW + 1, `scrollW=${info.docScrollW} clientW=${info.docClientW}`);
    let labelInfo = await page.evaluate(() => Array.from(document.querySelectorAll('.fm-ribbon-group-desktop .fm-ribbon-btn')).map(b => ({
      text: b.innerText.trim(),
      hasLabel: getComputedStyle(b.querySelector('.text, .text-inline') || b).display !== 'none'
    })));
    check('accounts@960 labels visible', labelInfo.length > 0 && labelInfo.every(b => b.text.length > 0 && b.hasLabel), JSON.stringify(labelInfo));
    await page.screenshot({ path: path.join(outDir, '01-accounts-960-single-row.png'), clip: { x: 0, y: 0, width: 960, height: 320 } });

    // --- Scenario 2: busy ribbon page, natural widths
    await page.goto(`${app.baseUrl}/card/setup`, { waitUntil: 'load' });
    await page.waitForSelector('.fm-ribbon-group-desktop', { timeout: 30000 });
    await settle();
    info = await page.evaluate(rowInfo);
    check('setup@960 single row', info.tops.length === 1, JSON.stringify(info));
    await page.screenshot({ path: path.join(outDir, '02-setup-960.png'), clip: { x: 0, y: 0, width: 960, height: 320 } });

    await page.setViewportSize({ width: 905, height: 900 });
    await settle();
    info = await page.evaluate(rowInfo);
    check('setup@905 single row, no overflow', info.tops.length === 1 && !info.overflow, JSON.stringify(info));
    const collapsed905 = await page.evaluate(() =>
      document.querySelectorAll('.fm-ribbon-group-desktop.desktop-collapsed').length);
    console.log(`  collapsed groups @905: ${collapsed905}`);
    await page.screenshot({ path: path.join(outDir, '03-setup-905.png'), clip: { x: 0, y: 0, width: 905, height: 320 } });

    // --- Scenario 3: forced collapse via artificial max-width
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(`${app.baseUrl}/card/setup`, { waitUntil: 'load' });
    await page.waitForSelector('.fm-ribbon-group-desktop', { timeout: 30000 });
    await settle();
    // Constrain the ribbon element itself: this resizes the observed box and
    // exercises the real ResizeObserver -> evaluate -> collapse path.
    await page.evaluate(() => {
      document.querySelector('.fm-ribbon').style.maxWidth = '130px';
    });
    await settle();
    info = await page.evaluate(rowInfo);
    const collapseInfo = await page.evaluate(() => {
      const collapsed = Array.from(document.querySelectorAll('.fm-ribbon-group-desktop.desktop-collapsed'));
      const triggers = collapsed.map(g => {
        const panel = g.nextElementSibling;
        const t = panel && panel.querySelector('.fm-ribbon-mobile-group-title-toggle');
        return { visible: t && t.offsetParent !== null, text: t ? t.innerText.trim() : null };
      });
      return { count: collapsed.length, triggers };
    });
    const groupCount = await page.evaluate(() =>
      document.querySelectorAll('.fm-ribbon-group-desktop').length);
    check('forced 130px: still single row', info.tops.length === 1, JSON.stringify(info));
    check('forced 130px: all groups collapsed with visible triggers', collapseInfo.count === groupCount && collapseInfo.count > 0 && collapseInfo.triggers.every(t => t.visible && t.text), JSON.stringify(collapseInfo));
    await page.screenshot({ path: path.join(outDir, '04-collapsed-130.png'), clip: { x: 0, y: 0, width: 640, height: 320 } });

    // --- Scenario 4: open collapsed group popover, check menu items
    const popState = await page.evaluate(() => {
      const g = document.querySelector('.fm-ribbon-group-desktop.desktop-collapsed');
      const panel = g && g.nextElementSibling;
      const t = panel && panel.querySelector('.fm-ribbon-mobile-group-title-toggle');
      if (t) t.click();
      return !!t;
    });
    await page.waitForTimeout(400);
    const menuInfo = await page.evaluate(() => {
      const menu = document.querySelector('.desktop-collapsed + .fm-ribbon-mobile-group-panel .fm-ribbon-mobile-menu.open');
      if (!menu) return { open: false };
      const r = menu.getBoundingClientRect();
      const items = Array.from(menu.querySelectorAll('.fm-ribbon-mobile-menu-item')).map(i => i.innerText.trim());
      return { open: true, items, inViewport: r.left >= 0 && r.right <= innerWidth && r.bottom <= innerHeight, rect: { l: r.left, r: r.right, b: r.bottom } };
    });
    check('popover opens on trigger click', popState && menuInfo.open, JSON.stringify(menuInfo));
    check('popover lists group actions', menuInfo.open && menuInfo.items.length > 0, JSON.stringify(menuInfo.items));
    check('popover inside viewport', !menuInfo.open || menuInfo.inViewport, JSON.stringify(menuInfo.rect || {}));
    await page.screenshot({ path: path.join(outDir, '05-popover-open.png'), clip: { x: 0, y: 0, width: 640, height: 480 } });

    // --- Scenario 5: release constraint -> groups restore
    await page.evaluate(() => {
      document.querySelector('.fm-ribbon').style.maxWidth = '';
    });
    await settle();
    const restored = await page.evaluate(() => ({
      collapsed: document.querySelectorAll('.fm-ribbon-group-desktop.desktop-collapsed').length,
      ...(() => { const groups = document.querySelector('.fm-ribbon-groups');
        const items = Array.from(groups.children).filter(el => el.offsetParent !== null);
        return { tops: [...new Set(items.map(el => Math.round(el.getBoundingClientRect().top)))] }; })()
    }));
    check('release: groups restored single row', restored.collapsed === 0 && restored.tops.length === 1, JSON.stringify(restored));
    await page.screenshot({ path: path.join(outDir, '06-restored.png'), clip: { x: 0, y: 0, width: 1440, height: 320 } });

    // --- Scenario 6: mobile <=900 unchanged (accordion panels)
    await page.setViewportSize({ width: 700, height: 900 });
    await settle();
    const mobile = await page.evaluate(() => ({
      desktopGroupsVisible: Array.from(document.querySelectorAll('.fm-ribbon-group-desktop')).some(g => g.offsetParent !== null),
      mobilePanels: Array.from(document.querySelectorAll('.fm-ribbon-mobile-group-panel')).filter(p => p.offsetParent !== null).length
    }));
    check('mobile@700 accordion unchanged', !mobile.desktopGroupsVisible && mobile.mobilePanels > 0, JSON.stringify(mobile));
    await page.screenshot({ path: path.join(outDir, '07-mobile-700.png'), clip: { x: 0, y: 0, width: 700, height: 500 } });

    const failed = results.filter(r => !r.ok);
    console.log(`\n${results.length - failed.length}/${results.length} checks passed`);
    process.exitCode = failed.length ? 1 : 0;
  } finally {
    await browser.close();
    app.stop();
    app.cleanupTempDir();
  }
})().catch(err => { console.error('VERIFY FAILED', err); process.exit(2); });
