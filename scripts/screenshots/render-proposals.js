// Re-renders the reworked proposal HTML files to PNG (Playwright).
// Usage: node render-proposals.js [--only <id,id2>] [--variant a|b] [--device desktop|mobile]
// Viewports: desktop 1280x900 @2x, mobile 390x844 @2x — full-page capture.

const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const repoRoot = path.resolve(__dirname, '..', '..');
const propRoot = path.join(repoRoot, 'Docs', 'design', 'proposals');

const args = process.argv.slice(2);
const argValue = (name) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : null;
};
const only = argValue('only') ? new Set(argValue('only').split(',')) : null;
const onlyVariant = argValue('variant');
const onlyDevice = argValue('device');

const VIEWPORTS = {
  desktop: { width: 1280, height: 900 },
  mobile: { width: 390, height: 844 },
};

(async () => {
  const browser = await chromium.launch({ headless: true });
  for (const variant of ['variantA', 'variantB']) {
    if (onlyVariant && variant !== `variant${onlyVariant.toUpperCase()}`) continue;
    for (const device of Object.keys(VIEWPORTS)) {
      if (onlyDevice && device !== onlyDevice) continue;
      const dir = path.join(propRoot, variant, device);
      if (!fs.existsSync(dir)) continue;
      const page = await browser.newPage({
        viewport: VIEWPORTS[device],
        deviceScaleFactor: 2,
        colorScheme: 'dark',
      });
      for (const f of fs.readdirSync(dir)) {
        if (!f.endsWith('.html')) continue;
        const id = f.replace(/\.html$/, '');
        if (only && !only.has(id)) continue;
        const htmlPath = path.join(dir, f);
        const pngPath = path.join(dir, id + '.png');
        try {
          await page.goto('file:///' + htmlPath.replace(/\\/g, '/'), { waitUntil: 'load', timeout: 30000 });
          try { await page.waitForLoadState('networkidle', { timeout: 12000 }); } catch {}
          try { await page.evaluate(() => document.fonts.ready); } catch {}
          await page.waitForTimeout(700);
          await page.screenshot({ path: pngPath, fullPage: true });
          console.log(`${variant}/${device}/${id} -> png ok`);
        } catch (e) {
          console.error(`${variant}/${device}/${id} FAILED: ${e.message}`);
        }
      }
      await page.close();
    }
  }
  await browser.close();
})();
