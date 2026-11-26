// Design inventory run: captures every view defined in inventory-routes.json
// as PNG screenshot + rendered-HTML dump, in Desktop (1280x900) and Mobile
// (390x844) viewports, Dark-Mode only. Output lands under Docs/design/inventory/
// together with index.json / index.md / data-gaps.md.
//
// Flow: start published app with temp SQLite -> capture public views
// (logged-out context, incl. first-user /register state) -> register first user
// with demo data -> login -> wait for CreateDemoData -> capture authenticated
// views (first user is auto-admin, covering /list/users et al.).
//
// CLI: node generate-inventory.js [--only=<id1,id2,...>]
//   --only restricts the run to the listed view ids (public views keep their
//   own phase; register+login still run when auth views are selected).
//
// Note on auth: the SSR prerender renders fully authenticated (cookie) with
// data, but the interactive Blazor circuit re-renders pages UNAUTHENTICATED
// a few seconds after connect — IHttpContextAccessor.HttpContext is null in
// circuits, so ICurrentUserService.IsAuthenticated flips to false (empty
// tables, "Login" status, ".signed-out" home). For every view that does not
// need live interactivity we therefore block /_framework/blazor.web.js so the
// deterministic SSR DOM is captured. Views with actions/multiCapture keep a
// live circuit (their chrome works; data may be partial — see data-gaps.md).

const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const {
  repoRoot,
  waitFor,
  startApp,
  registerFirstUser,
  login,
  isAdminToken,
  waitForDemoData,
  apiGet,
  resolveFirstId
} = require('./lib/app-harness');

const outputRoot = path.join(repoRoot, 'Docs', 'design', 'inventory');
const manifestPath = path.join(__dirname, 'inventory-routes.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));

const onlyArg = process.argv.find(a => a.startsWith('--only='));
const onlyIds = onlyArg ? new Set(onlyArg.slice('--only='.length).split(',').map(s => s.trim()).filter(Boolean)) : null;
if (onlyIds) {
  manifest.views = manifest.views.filter(v => onlyIds.has(v.id));
  console.log(`--only active: ${manifest.views.length} view(s) selected`);
}

const results = [];
const gaps = [];

// Views needing a live Blazor circuit (clicks, accordions, file inputs).
// Everything else is captured from the authenticated SSR prerender with
// blazor.web.js blocked — see the header note for why.
const needsCircuit = v => Boolean(
  v.multiCapture ||
  (v.actions && v.actions.length > 0) ||
  (v.postActions && v.postActions.length > 0)
);
let blockBlazorJs = false;

function slugify(text) {
  return String(text || '')
    .toLowerCase()
    .replace(/[äÄ]/g, 'ae').replace(/[öÖ]/g, 'oe').replace(/[üÜ]/g, 'ue').replace(/ß/g, 'ss')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 60) || 'section';
}

async function waitForView(page, wait, label) {
  if (!wait || !wait.selector) {
    return;
  }
  const timeout = wait.timeout || 45000;
  await page.waitForSelector(wait.selector, { state: 'visible', timeout });
  if (wait.minCount && wait.minCount > 1) {
    await waitFor(async () => {
      const count = await page.locator(wait.selector).count();
      return count >= wait.minCount;
    }, timeout, 500, `minCount ${wait.minCount} for ${wait.selector} on ${label}`);
  }
}

// Clicks a ribbon/page action. On the mobile viewport the desktop ribbon
// buttons are hidden; the mobile variants carry "-mobile-shortcut" /
// "-mobile" id suffixes behind the accordion group toggle.
async function smartClick(page, selector, isMobile) {
  const target = page.locator(selector).first();
  if (await target.count() > 0 && await target.isVisible().catch(() => false)) {
    await target.click();
    return true;
  }
  if (!isMobile) {
    // Give the desktop variant a last chance to appear.
    await target.click({ timeout: 5000 });
    return true;
  }
  const shortcut = page.locator(`${selector}-mobile-shortcut`).first();
  if (await shortcut.count() > 0 && await shortcut.isVisible().catch(() => false)) {
    await shortcut.click();
    return true;
  }
  // Open ribbon mobile group panels until the item becomes visible.
  const toggles = page.locator('.fm-ribbon-mobile-group-toggle');
  const toggleCount = await toggles.count();
  for (let i = 0; i < toggleCount; i++) {
    await toggles.nth(i).click().catch(() => {});
    await page.waitForTimeout(150);
    const mobileItem = page.locator(`${selector}-mobile`).first();
    if (await mobileItem.count() > 0 && await mobileItem.isVisible().catch(() => false)) {
      await mobileItem.click();
      return true;
    }
  }
  // Fallback: click even if still hidden (throws with a clear error).
  await page.locator(`${selector}-mobile`).first().click({ timeout: 5000 });
  return true;
}

async function setInputFilesSmart(page, spec, filePath, isMobile) {
  const candidates = isMobile
    ? [spec.selector, '#Import-mobile-shortcut input[type="file"]', '#Import-mobile input[type="file"]']
    : [spec.selector];
  for (const sel of candidates) {
    const loc = page.locator(sel).first();
    if (await loc.count() > 0) {
      await loc.setInputFiles(filePath);
      return true;
    }
  }
  throw new Error(`No file input found for ${spec.selector}`);
}

async function runActions(page, actions, isMobile, actionContext) {
  for (const action of actions || []) {
    if (action.click) {
      await smartClick(page, action.click, isMobile);
    } else if (action.setInputFiles) {
      const filePath = action.setInputFiles.file === '$DEMO_CSV'
        ? actionContext.demoCsvPath
        : action.setInputFiles.file;
      await setInputFilesSmart(page, action.setInputFiles, filePath, isMobile);
    } else if (action.waitMs) {
      await page.waitForTimeout(action.waitMs);
    } else if (action.waitFor) {
      await waitForView(page, action.waitFor, 'action-wait');
    }
    await page.waitForTimeout(200);
  }
}

// Resolves {api:path} placeholders in a URL, innermost first. Returns null
// when a required id cannot be resolved (data gap).
async function resolveUrl(url, baseUrl, token, resolutionNotes) {
  const pattern = /\{api:([^{}]+)\}/;
  let resolved = url;
  for (let guard = 0; guard < 10; guard++) {
    const match = resolved.match(pattern);
    if (!match) break;
    const apiPath = match[1];
    const id = await resolveFirstId(baseUrl, token, apiPath);
    if (!id) {
      resolutionNotes.push(`id lookup failed: GET /${apiPath}`);
      return null;
    }
    resolved = resolved.replace(match[0], id);
  }
  return resolved;
}

async function captureView({ page, baseUrl, token, view, viewportName, isMobile, actionContext }) {
  const record = {
    id: view.id,
    group: view.group,
    url: view.url,
    auth: view.auth,
    viewport: viewportName,
    status: 'pending',
    screenshot: null,
    html: null,
    notes: []
  };

  const resolutionNotes = [];
  const resolvedUrl = await resolveUrl(view.url, baseUrl, token, resolutionNotes);
  if (resolvedUrl === null) {
    record.status = 'gap';
    record.notes.push(`Unresolved id placeholder: ${resolutionNotes.join('; ')}`);
    results.push(record);
    gaps.push({ id: view.id, url: view.url, viewport: viewportName, reason: record.notes[0] });
    return record;
  }
  record.url = resolvedUrl;

  // SSR capture for non-interactive views: block the Blazor bootstrap script
  // so the authenticated prerender DOM is not replaced by the unauthenticated
  // circuit render. Interactive views keep the circuit (clicks needed).
  const useSsrDom = !needsCircuit(view) && view.auth !== 'public';
  blockBlazorJs = useSsrDom;
  const effWait = useSsrDom && view.waitSsr ? view.waitSsr : view.wait;
  if (useSsrDom) record.notes.push('ssr-capture (blazor.web.js blocked)');

  try {
    if (useSsrDom) {
      // SSR prerender is the final DOM — the document IS complete at 'load',
      // so a selector wait adds nothing (and can starve when the page's main
      // thread is busy). Capture unconditionally; only REPORT a missing wait
      // selector as a partial capture.
      await page.goto(`${baseUrl}${resolvedUrl}`, { waitUntil: 'load' });
      await page.waitForTimeout(view.settle || 400);
      const sel = (effWait || {}).selector;
      if (sel) {
        // "Any match visible" — comma selectors hit the first DOM match, which
        // can be an empty (invisible) container like .list-filters.
        const matches = await page.locator(sel).all();
        let visible = false;
        for (const el of matches) {
          if (await el.isVisible().catch(() => false)) { visible = true; break; }
        }
        if (!visible) {
          record.status = 'partial';
          record.notes.push(`wait selector not visible in SSR DOM: ${sel}`);
          gaps.push({ id: view.id, url: resolvedUrl, viewport: viewportName, reason: `partial SSR capture — '${sel}' not visible` });
        }
      }
      await writeArtifacts(page, view.id, viewportName, record);
      if (record.status !== 'partial') record.status = 'captured';
      results.push(record);
      return record;
    }

    let lastErr = null;
    for (let attempt = 0; attempt < 2; attempt++) {
      try {
        if (attempt === 0) {
          await page.goto(`${baseUrl}${resolvedUrl}`, { waitUntil: 'load' });
        } else {
          await page.reload({ waitUntil: 'load' });
        }
        await waitForView(page, effWait, view.id);
        lastErr = null;
        if (attempt > 0) record.notes.push('recovered after reload');
        break;
      } catch (err) {
        lastErr = err;
        record.notes.push(`attempt ${attempt + 1} failed (${err.message.split('\n')[0].slice(0, 120)})`);
      }
    }
    if (lastErr) throw lastErr;

    if (view.multiCapture) {
      return await captureMulti({ page, view, viewportName, record });
    }

    if (view.actions && view.actions.length > 0) {
      await runActions(page, view.actions, isMobile, actionContext);
      if (view.waitAfter) {
        try {
          await waitForView(page, view.waitAfter, `${view.id} (post-action)`);
        } catch (err) {
          record.notes.push(`Post-action wait failed: ${err.message}`);
        }
      }
    }
    await page.waitForTimeout(view.settle || 400);
  } catch (err) {
    record.status = 'failed';
    record.notes.push(err.message.split('\n')[0]);
    // Still dump whatever the page rendered — the artifacts document the
    // empty/loading state and help diagnose data gaps later.
    await writeArtifacts(page, `${view.id}--failed`, viewportName, record).catch(() => {});
    results.push(record);
    gaps.push({ id: view.id, url: resolvedUrl, viewport: viewportName, reason: err.message.split('\n')[0] });
    return record;
  }

  await writeArtifacts(page, view.id, viewportName, record);
  if (view.postActions) {
    await runActions(page, view.postActions, isMobile, actionContext).catch(() => {});
  }
  record.status = 'captured';
  results.push(record);
  return record;
}

async function captureMulti({ page, view, viewportName, record }) {
  // Expands each accordion toggle one by one and captures every expanded
  // section as its own view (sections stay expanded after toggling).
  const spec = view.multiCapture;
  const toggles = page.locator(spec.toggleSelector);

  // Sections can appear/disappear asynchronously (feature-gated accordions
  // like "Aktualisierungen" / "KI-Zugänge"). Wait until the toggle count is
  // stable before iterating, otherwise nth(i) resolves against a shifting
  // DOM and produces phantom/stale entries.
  try {
    await page.waitForFunction((sel) => {
      const now = document.querySelectorAll(sel).length;
      if (window.__toggleStableCount === now && window.__toggleStableSince && (Date.now() - window.__toggleStableSince) > 800) {
        return true;
      }
      if (window.__toggleStableCount !== now) {
        window.__toggleStableCount = now;
        window.__toggleStableSince = Date.now();
      }
      return false;
    }, spec.toggleSelector, { timeout: 15000, polling: 200 });
  } catch { /* proceed even if count never stabilizes */ }

  const count = await toggles.count();
  if (count === 0) {
    record.status = 'gap';
    record.notes.push(`No toggles found for ${spec.toggleSelector}`);
    results.push(record);
    gaps.push({ id: view.id, url: view.url, viewport: viewportName, reason: record.notes[0] });
    return record;
  }
  const produced = [];
  for (let i = 0; i < count; i++) {
    // Re-resolve the locator every access — async accordion re-renders can
    // detach elements and shift nth() indices between iterations.
    const toggle = () => toggles.nth(i);
    const itemContent = () => toggles.nth(i)
      .locator('xpath=ancestor::*[contains(@class,"setup-section-accordion-item")][1]')
      .locator(spec.contentSelector);
    const title = await toggle().innerText().catch(() => `section-${i}`);
    const titleFirstLine = String(title).split('\n')[0].replace(/[+\-−]\s*$/, '').trim();
    const captureId = `${spec.namePrefix}-${String(i + 1).padStart(2, '0')}-${slugify(titleFirstLine)}`;
    const sub = {
      id: captureId,
      group: view.group,
      url: view.url,
      auth: view.auth,
      viewport: viewportName,
      status: 'pending',
      screenshot: null,
      html: null,
      notes: [`section: ${titleFirstLine}`]
    };
    try {
      // evaluate-based scroll+click avoids actionability stalls while the
      // accordion re-renders (expanded sections can keep mutating the DOM).
      let expanded = false;
      for (let attempt = 0; attempt < 2 && !expanded; attempt++) {
        // A retry must not collapse a section that expanded but whose
        // visibility check raced — only click when still collapsed.
        if (await itemContent().isVisible().catch(() => false)) {
          expanded = true;
          break;
        }
        await toggle().evaluate(el => el.scrollIntoView({ block: 'start' }));
        await toggle().evaluate(el => el.click());
        try {
          // Wait for THIS accordion item's content, not any section content —
          // a still-visible sibling would otherwise satisfy the wait early.
          await itemContent().waitFor({ state: 'visible', timeout: 15000 });
          expanded = true;
        } catch {
          sub.notes.push(`expand attempt ${attempt + 1} timed out`);
        }
      }
      if (!expanded) {
        throw new Error(`section content never became visible after 2 expand attempts`);
      }
      await page.waitForTimeout(spec.settle || 500);
      await toggle().evaluate(el => el.scrollIntoView({ block: 'start' }));
      await writeArtifacts(page, captureId, viewportName, sub);
      sub.status = 'captured';
      // Collapse the section again to keep the DOM small for the next toggle.
      await toggle().evaluate(el => el.click()).catch(() => {});
      await page.waitForTimeout(200);
    } catch (err) {
      sub.status = 'failed';
      sub.notes.push(err.message.split('\n')[0]);
      gaps.push({ id: captureId, url: view.url, viewport: viewportName, reason: err.message.split('\n')[0] });
    }
    results.push(sub);
    produced.push(sub);
  }
  record.status = produced.every(p => p.status === 'captured') ? 'captured' : 'partial';
  record.notes.push(`expanded ${produced.length} sections`);
  results.push(record);
  return record;
}

// Demo data contains no MCP access, so /card/mcp-accesses/{id} cannot be
// resolved and the list view stays empty. Seed a minimal read-only access
// once (idempotent) so those views show real content.
async function seedMcpAccessIfMissing(baseUrl, token) {
  const list = await apiGet(baseUrl, token, 'api/mcp-accesses');
  if (list.ok && Array.isArray(list.body) && list.body.length > 0) {
    return;
  }
  const res = await fetch(`${baseUrl}/api/mcp-accesses`, {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: 'Inventar-Demo-Zugang',
      description: 'Read-only Demo-Zugang für die Ansichten-Inventarisierung.',
      level: 0,       // McpAccessLevel.Analyse
      approvalMode: 0 // McpApprovalMode.PerAction
    })
  });
  if (res.ok) {
    console.log('Seeded demo MCP access for card/list captures');
  } else {
    console.warn(`MCP access seed failed: HTTP ${res.status}`);
  }
}

async function writeArtifacts(page, id, viewportName, record) {
  const shotPath = path.join(outputRoot, 'screenshots', viewportName, `${id}.png`);
  const htmlPath = path.join(outputRoot, 'html', viewportName, `${id}.html`);
  fs.mkdirSync(path.dirname(shotPath), { recursive: true });
  fs.mkdirSync(path.dirname(htmlPath), { recursive: true });
  await page.screenshot({ path: shotPath, fullPage: false });
  fs.writeFileSync(htmlPath, await page.content(), 'utf8');
  // A previous (or earlier failed) attempt may have left --failed artifacts
  // for this id — remove them so the inventory only carries final state.
  for (const dir of [path.dirname(shotPath), path.dirname(htmlPath)]) {
    for (const ext of ['.png', '.html']) {
      const stale = path.join(dir, `${id}--failed${ext}`);
      if (fs.existsSync(stale)) fs.unlinkSync(stale);
    }
  }
  record.screenshot = path.relative(outputRoot, shotPath).replace(/\\/g, '/');
  record.html = path.relative(outputRoot, htmlPath).replace(/\\/g, '/');
  console.log(`  captured ${viewportName}/${id}`);
}

function writeIndexAndGaps() {
  fs.mkdirSync(outputRoot, { recursive: true });

  // On --only re-runs, merge with the previous index so partial runs do not
  // wipe the entries captured earlier. Key: viewport + id (multiCapture
  // sections produce their own ids but share the parent view's url/group).
  if (onlyIds) {
    const prevPath = path.join(outputRoot, 'index.json');
    if (fs.existsSync(prevPath)) {
      try {
        const prev = JSON.parse(fs.readFileSync(prevPath, 'utf8'));
        const fresh = new Map(results.map(r => [`${r.viewport}:${r.id}`, r]));
        const coveredParents = new Set(results.map(r => `${r.viewport}:${r.url}:${r.group}`));
        // Gap-merge keys must come from THIS run's results — after results is
        // replaced by the merged set, every previous gap would look covered.
        const freshKeys = new Set(results.map(r => `${r.viewport}:${r.id}`));
        const coveredUrls = new Set(results.map(r => `${r.viewport}:${r.url}`));
        const merged = [];
        for (const e of prev.entries || []) {
          const key = `${e.viewport}:${e.id}`;
          if (fresh.has(key)) continue; // replaced by the fresh entry
          // Drop stale children of a re-captured multiCapture parent.
          if (e.notes && e.notes.some(n => String(n).startsWith('section:')) && coveredParents.has(`${e.viewport}:${e.url}:${e.group}`)) continue;
          merged.push(e);
        }
        // Keep previous order, append genuinely new entries at the end.
        for (const r of results) merged.push(r);
        results.length = 0;
        results.push(...merged);

        // Merge gaps: previous gaps survive unless this run produced a fresh
        // entry for the same viewport+id (captured or failed again).
        const prevGaps = (prev.gaps || []).filter(g => {
          const gk = `${g.viewport}:${g.id}`;
          if (freshKeys.has(gk)) return false;
          // MultiCapture section gaps reference the section id (not the parent
          // view id) — drop them when the parent view was re-run this round.
          if (coveredUrls.has(`${g.viewport}:${g.url}`)) return false;
          return true;
        });
        gaps.unshift(...prevGaps);
      } catch (err) {
        console.warn(`index.json merge skipped: ${err.message}`);
      }
    }
  }

  fs.writeFileSync(path.join(outputRoot, 'index.json'), JSON.stringify({
    generatedAt: new Date().toISOString(),
    viewports: manifest.viewports,
    entries: results,
    gaps
  }, null, 2), 'utf8');

  const lines = [
    '# Ansichten-Inventar (Index)',
    '',
    `Erzeugt: ${new Date().toISOString()} — Dark-Mode, Viewports: Desktop ${manifest.viewports.desktop.width}x${manifest.viewports.desktop.height}, Mobile ${manifest.viewports.mobile.width}x${manifest.viewports.mobile.height}.`,
    'Quelle: `scripts/screenshots/inventory-routes.json` via `generate-inventory.js`.',
    '',
    '| Ansicht | Gruppe | Auth | Route | Status | Screenshot | HTML |',
    '|---------|--------|------|-------|--------|------------|------|'
  ];
  for (const r of results) {
    const shot = r.screenshot ? `[png](${encodeURI(r.screenshot)})` : '—';
    const html = r.html ? `[html](${encodeURI(r.html)})` : '—';
    const note = r.notes.length ? ` (${r.notes.join('; ')})` : '';
    lines.push(`| ${r.id} | ${r.group} | ${r.auth} | \`${r.url}\` | ${r.status}${note} | ${shot} | ${html} |`);
  }
  fs.writeFileSync(path.join(outputRoot, 'index.md'), lines.join('\n') + '\n', 'utf8');

  const gapLines = [
    '# Datenluecken / fehlgeschlagene Ansichten',
    '',
    'Ansichten, die nicht oder nur unvollstaendig erfasst werden konnten.',
    'Kandidaten fuer ein Follow-up-Issue zur Demodatengenerierung.',
    ''
  ];
  if (gaps.length === 0) {
    gapLines.push('Keine Luecken — alle Ansichten erfasst.');
  } else {
    gapLines.push('| Ansicht | Viewport | Route | Grund |');
    gapLines.push('|---------|----------|-------|-------|');
    for (const g of gaps) {
      gapLines.push(`| ${g.id} | ${g.viewport} | \`${g.url}\` | ${g.reason} |`);
    }
  }
  gapLines.push(
    '',
    '## Hinweise',
    '',
    '- **home (partial):** Die KPI-Kacheln laden ausschliesslich interaktiv (`OnAfterRenderAsync` in `HomeKpiGrid`) und stehen im SSR-DOM leer. Ein historischer Screenshot mit gefuelltem KPI-Grid liegt unter `Docs/screenshots/home.png` (Stand Nov. 2024).',
    '- **Circuit-Authentifizierung:** Interaktive Blazor-Circuits rendern in dieser Umgebung unauthentifiziert (`IHttpContextAccessor.HttpContext` ist im Circuit `null` → `IsAuthenticated=false` → leere Tabellen / `.signed-out`). Deshalb werden nicht-interaktive Ansichten aus dem SSR-Prerender erfasst (`blazor.web.js` blockiert). Betroffene interaktive Inhalte: KPI-Kacheln, Mass-Import-Dialog (`overlay-home-mass-import` zeigt die Seite ohne geoeffneten Dialog).',
    '- **Setup-Sektionen:** Erfasst werden die in dieser Konfiguration vorhandenen 9 Sektionen; `Aktualisierungen` und `KI-Zugänge` sind feature-abhaengig und fehlen in der Demo-Konfiguration.',
    ''
  );
  fs.writeFileSync(path.join(outputRoot, 'data-gaps.md'), gapLines.join('\n') + '\n', 'utf8');
}

(async () => {
  const app = await startApp({ tempPrefix: 'financemanager-inventory-' });
  const demoCsvPath = path.join(app.tempDbDir, 'inventory-demo-import.csv');
  fs.writeFileSync(demoCsvPath, 'Buchungstag;Betrag;Verwendungszweck\n01.01.2025;-12,34;Inventar Demoimport\n', 'utf8');

  let keepTempDir = false;
  const username = `demo${Date.now().toString(36)}`;
  const password = 'DemoTest!123';
  let token = null;
  let adminOk = false;

  try {
    const browser = await chromium.launch({ headless: true });
    const actionContext = { demoCsvPath };

    const newViewContext = async (viewportName, vp) => {
      const isMobile = viewportName === 'mobile';
      const context = await browser.newContext({
        viewport: vp,
        locale: 'de-DE',
        timezoneId: 'Europe/Berlin',
        isMobile,
        hasTouch: isMobile
      });
      // Per-view circuit blocker (see needsCircuit): when blockBlazorJs is set,
      // the Blazor bootstrap script is refused so the page stays at the
      // authenticated SSR prerender — no circuit, no unauthenticated re-render.
      await context.route('**/_framework/blazor.web.js*', route => {
        if (blockBlazorJs) return route.abort('blockedbyclient');
        return route.continue();
      });
      const page = await context.newPage();
      return { context, page, isMobile };
    };

    const captureList = async (page, views, viewportName, isMobile) => {
      for (const view of views) {
        if (view.auth === 'admin' && !adminOk) {
          const record = {
            id: view.id, group: view.group, url: view.url, auth: view.auth,
            viewport: viewportName, status: 'gap', screenshot: null, html: null,
            notes: ['Admin-Anspruch fehlt im Token']
          };
          results.push(record);
          gaps.push({ id: view.id, url: view.url, viewport: viewportName, reason: 'Admin-Anspruch fehlt im Token' });
          continue;
        }
        await captureView({
          page, baseUrl: app.baseUrl, token, view,
          viewportName, isMobile, actionContext
        });
      }
    };

    // 1. Public views in logged-out contexts — first, so /register still shows
    //    the first-user state incl. #create-demo-data.
    const publicViews = manifest.views.filter(v => v.auth === 'public');
    for (const [viewportName, vp] of Object.entries(manifest.viewports)) {
      const { context, page, isMobile } = await newViewContext(viewportName, vp);
      await captureList(page, publicViews, viewportName, isMobile);
      await context.close();
    }

    // 2. Register the first user once (auto-admin + demo data), then capture
    //    authenticated views per viewport. The auth cookie is per-context, so
    //    every new context performs a plain UI login (no re-registration).
    const authViews = manifest.views.filter(v => v.auth !== 'public');
    let firstAuth = true;
    for (const [viewportName, vp] of Object.entries(manifest.viewports)) {
      const { context, page, isMobile } = await newViewContext(viewportName, vp);
      // Registration/login need the live circuit — the flag may still be set
      // from a previous viewport's last SSR capture.
      blockBlazorJs = false;
      if (firstAuth) {
        await registerFirstUser(page, app.baseUrl, { username, password, createDemoData: true });
        token = await login(page, context, app.baseUrl, { username, password });
        adminOk = isAdminToken(token);
        console.log(`Auth token acquired (admin claim: ${adminOk})`);
        await waitForDemoData(app.baseUrl, token);
        await seedMcpAccessIfMissing(app.baseUrl, token).catch(err => console.warn(`MCP seed skipped: ${err.message}`));
        firstAuth = false;
      } else {
        await login(page, context, app.baseUrl, { username, password });
      }
      await captureList(page, authViews, viewportName, isMobile);
      await context.close();
    }

    await browser.close();
  } catch (err) {
    keepTempDir = true;
    console.error(`Inventory run failed; keeping app log and database at ${app.tempDbDir}`);
    console.error(err);
  } finally {
    writeIndexAndGaps();
    app.stop();
    if (!keepTempDir) {
      app.cleanupTempDir();
    }
  }

  const captured = results.filter(r => r.status === 'captured').length;
  console.log(`Inventory complete: ${captured}/${results.length} captures, ${gaps.length} gaps. Artifacts: ${outputRoot}`);
})();
