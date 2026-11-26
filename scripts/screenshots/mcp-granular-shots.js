const { spawn } = require('child_process');
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const os = require('os');
const http = require('http');

const repoRoot = path.resolve(__dirname, '..', '..');
const outputDir = path.join(repoRoot, 'Docs', 'screenshots');
const appDll = path.join(repoRoot, 'FinanceManager.Web', 'bin', 'Debug', 'net10.0', 'FinanceManager.Web.dll');
const tempDbDir = fs.mkdtempSync(path.join(os.tmpdir(), 'fm-mcp-shots-'));
const tempDb = path.join(tempDbDir, 'financemanager.db');
const appLogPath = path.join(tempDbDir, 'app.log');

fs.mkdirSync(outputDir, { recursive: true });

function getFreePort() {
  return new Promise((resolve, reject) => {
    const server = http.createServer();
    server.listen(0, '127.0.0.1', () => {
      const port = server.address().port;
      server.close(() => resolve(port));
    });
    server.on('error', reject);
  });
}

(async () => {
  if (!fs.existsSync(appDll)) throw new Error('Published app missing');
  const port = await getFreePort();
  const baseUrl = `http://127.0.0.1:${port}`;
  const appLogFd = fs.openSync(appLogPath, 'w');
  const appProcess = spawn('dotnet', ['run', '--project', 'FinanceManager.Web', '--no-build', '--no-launch-profile'], {
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: baseUrl,
      ConnectionStrings__Default: `Data Source=${tempDb}`,
      Jwt__Key: 'screenshots-demo-signing-key-0123456789abcdef0123456789abcdef',
    },
    stdio: ['ignore', appLogFd, appLogFd],
  });

  try {
    // wait for app
    const deadline = Date.now() + 120000;
    let up = false;
    while (Date.now() < deadline && !up) {
      try { const r = await fetch(`${baseUrl}/register`); if (r.ok) up = true; } catch {}
      if (!up) await new Promise(r => setTimeout(r, 500));
    }
    if (!up) throw new Error('app did not start, log: ' + appLogPath);

    const browser = await chromium.launch({ args: ['--disable-features=LocalNetworkAccessChecks,BlockInsecurePrivateNetworkRequests', '--allow-insecure-localhost'] });
    const context = await browser.newContext({ viewport: { width: 1600, height: 1000 } });
    const page = await context.newPage();
    page.on('console', m => { if (m.type() === 'error') console.log('PAGE-ERR:', m.text()); });
    const shot = (name) => page.screenshot({ path: path.join(outputDir, `${name}.png`), fullPage: false });

    // register first user (admin, no demo data)
    await page.goto(`${baseUrl}/register`, { waitUntil: 'load' });
    await page.waitForSelector('#username', { timeout: 30000 });
    const username = `admin${Date.now().toString(36)}`;
    const password = 'DemoTest!123';
    let registered = false;
    for (let i = 0; i < 3 && !registered; i++) {
      await page.fill('#username', username);
      await page.fill('#password', password);
      await page.evaluate(() => {
        for (const el of [document.querySelector('#username'), document.querySelector('#password')]) {
          el.dispatchEvent(new Event('input', { bubbles: true }));
          el.dispatchEvent(new Event('change', { bubbles: true }));
        }
      });
      await page.click('button[type="submit"]');
      try {
        await page.waitForFunction(() => location.pathname !== '/register', null, { timeout: 6000 });
        registered = true;
      } catch {}
    }
    if (!registered) throw new Error('registration failed');
    console.log('registered', username);

    // login
    for (let i = 0; i < 3; i++) {
      try {
        await page.goto(`${baseUrl}/login`, { waitUntil: 'domcontentloaded' });
        break;
      } catch (e) {
        if (i === 2 || !String(e).includes('ERR_ABORTED')) throw e;
        await page.waitForTimeout(800);
      }
    }
    await page.waitForSelector('#login-user', { timeout: 30000 });
    await page.waitForFunction(() => typeof window.fmAuthLogin === 'function', null, { timeout: 15000 });
    const setField = async (sel, val) => {
      await page.fill(sel, val);
      await page.locator(sel).evaluate(el => {
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
      });
    };
    let loggedIn = false;
    for (let i = 0; i < 3 && !loggedIn; i++) {
      await setField('#login-user', username);
      await setField('#login-pass', password);
      await page.waitForTimeout(300);
      await page.click('button[type="submit"]');
      try {
        await page.waitForFunction(() => !location.pathname.includes('/login'), null, { timeout: 15000 });
        loggedIn = true;
      } catch {
        const err = await page.locator('.error, .validation-message, .validation-errors').allTextContents().catch(() => []);
        if (err.length) console.log('login attempt error:', err.join(' | '));
      }
    }
    if (!loggedIn) throw new Error('login failed');
    console.log('logged in');

    // enable the MCP integration + write access directly in the temp DB
    // (the admin endpoint requires the JWT bearer scheme; this is test-only setup)
    const { spawnSync } = require('child_process');
    const py = spawnSync('python3', ['-c', `
import sqlite3
con = sqlite3.connect(r'''${tempDb}''')
cur = con.execute("UPDATE McpSettings SET Enabled=1, WriteAccessEnabled=1")
if cur.rowcount == 0:
    con.execute("INSERT INTO McpSettings (Id, Enabled, WriteAccessEnabled, CreatedUtc) VALUES ('3c9a1e2f-7b4d-4a5c-8e6f-0d1b2c3a4e5f', 1, 1, '2026-11-23T00:00:00')")
con.commit()
print('rows:', con.execute("SELECT Enabled, WriteAccessEnabled FROM McpSettings").fetchall())
`], { encoding: 'utf8' });
    console.log('mcp settings:', (py.stdout || '').trim(), (py.stderr || '').trim());

    // MCP access list -> New wizard
    await page.goto(`${baseUrl}/list/mcp-accesses`, { waitUntil: 'load' });
    await page.waitForTimeout(2500);
    await shot('mcp-accesses-list');

    // click the ribbon "New"/"Neu" action
    const newBtn = page.locator('button:has-text("Neu"), button:has-text("New")').first();
    await newBtn.waitFor({ timeout: 10000 });
    await newBtn.click();
    await page.waitForSelector('.mcp-wizard', { timeout: 10000 });
    await page.waitForTimeout(600);

    // step 1: name
    const nameInput = page.locator('.mcp-wizard input.card-input').first();
    await nameInput.fill('Expertenmodus-Demo');
    await nameInput.evaluate(el => el.dispatchEvent(new Event('change', { bubbles: true })));
    await page.waitForTimeout(400);
    await shot('mcp-wizard-step1');
    await page.locator('.mcp-wizard button.btn-primary').last().click(); // Weiter -> step 1
    await page.waitForSelector('.mcp-level-card', { timeout: 10000 });

    // level step: choose the last level card (Individuell) so scopes stay editable
    const levelCards = page.locator('.mcp-level-card');
    await levelCards.last().click();
    await page.waitForTimeout(400);
    await shot('mcp-wizard-level');

    // approval radios appear for level >= Sachbearbeiter; pick Granular (3rd)
    const radios = page.locator('input[name="mcp-approval"]');
    await radios.nth(2).waitFor({ timeout: 10000 });
    await radios.nth(2).check();
    await page.waitForTimeout(400);
    await shot('mcp-wizard-expertmode');
    console.log('shot: mcp-wizard-expertmode');

    // create the access -> step 2 token panel
    await page.locator('.mcp-wizard button.btn-primary').last().click();
    await page.waitForSelector('.mcp-token-panel input[type="checkbox"]', { timeout: 15000 });
    await shot('mcp-wizard-token');

    // confirm + close -> navigates to card/mcp-accesses/{id}
    await page.locator('.mcp-token-panel input[type="checkbox"]').check();
    await page.locator('.mcp-token-panel button.btn-primary').click();
    // wait until the rights editor's grant matrix (checkbox cells) is rendered
    await page.waitForSelector('table.fm-table input[type="checkbox"]', { timeout: 20000 });
    await page.waitForTimeout(800);
    await shot('mcp-card-grants');
    console.log('shot: mcp-card-grants');

    // exercise the grant matrix: check the first two grantable cells, save
    const grantTable = page.locator('table.fm-table:has(input[type="checkbox"])').first();
    const grantCells = grantTable.locator('input[type="checkbox"]:not([disabled])');
    const n = await grantCells.count();
    for (let i = 0; i < Math.min(2, n); i++) await grantCells.nth(i).check();
    await page.waitForTimeout(300);
    await shot('mcp-grant-matrix');
    const saveBtn = page.locator('button.btn-primary:has-text("peichern"), button.btn-primary:has-text("ave")').first();
    if (await saveBtn.count()) {
      await saveBtn.click();
      await page.waitForTimeout(800);
      await shot('mcp-grants-saved');
      console.log('shot: mcp-grants-saved');
      // confirm the "Schreibaktionen vorab freigeben" dialog, then final state
      const confirm = page.locator('button:has-text("Bestätigen"), button:has-text("Confirm")').first();
      if (await confirm.count()) {
        await confirm.click();
        await page.waitForTimeout(1200);
        await shot('mcp-grants-confirmed');
        console.log('shot: mcp-grants-confirmed');
      }
    }

    await browser.close();
  } finally {
    appProcess.kill();
  }
})().catch(e => { console.error(e); process.exit(1); });
