// Generates the two Stitch design variants for the FinanceManager UI redesign.
//
//   node generate-proposals.mjs [--variant a|b] [--only <id,id2>] [--mobile]
//
// API key handling: the key lives in the Windows user environment variable
// GOOGLE_STITCH (worker shells do not inherit the user scope). It is read via
// powershell GetEnvironmentVariable and forwarded to process.env.STITCH_API_KEY
// (the env var the SDK singleton reads). The value is never printed or stored.
//
// Output: Docs/design/proposals/{variantA,variantB}/{desktop,mobile}/<view>.png|.html
// plus manifest.json — resumable, existing artifacts are skipped.

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '..', '..');
const outputRoot = path.join(repoRoot, 'Docs', 'design', 'proposals');
const viewsPath = path.join(here, 'proposal-views.json');
const viewDefs = JSON.parse(fs.readFileSync(viewsPath, 'utf8'));

const args = process.argv.slice(2);
const argValue = (name) => {
  const i = args.indexOf(`--${name}`);
  return i >= 0 ? args[i + 1] : null;
};
const onlyVariant = argValue('variant');
const onlyIds = argValue('only') ? new Set(argValue('only').split(',')) : null;
const includeMobile = args.includes('--mobile');
// Representative subset for MOBILE generation (all views get DESKTOP).
const MOBILE_VIEWS = new Set(['home', 'login', 'list-accounts', 'card-account', 'card-setup', 'reports-dashboard']);

// --- API key -----------------------------------------------------------------

function resolveApiKey() {
  if (process.env.GOOGLE_STITCH && process.env.GOOGLE_STITCH.trim().length > 0) {
    return process.env.GOOGLE_STITCH.trim();
  }
  try {
    const value = execFileSync(
      'powershell',
      ['-NoProfile', '-Command', "[Environment]::GetEnvironmentVariable('GOOGLE_STITCH','User')"],
      { encoding: 'utf8' }
    ).trim();
    if (value.length > 0) return value;
  } catch { /* fall through */ }
  return null;
}

const apiKey = resolveApiKey();
if (!apiKey) {
  console.error('GOOGLE_STITCH key not found in process env or Windows user scope. Set it in the user environment and retry.');
  process.exit(2);
}
process.env.STITCH_API_KEY = apiKey;

const { Stitch, StitchToolClient } = await import('@google/stitch-sdk');
const client = new StitchToolClient({ apiKey });
const stitch = new Stitch(client);

// --- Prompt assembly ----------------------------------------------------------

const SHARED_CONSTRAINTS = [
  'Design a DARK-MODE screen for "FinanceManager", a German personal-finance web app (Blazor SPA).',
  'The UI language is German (labels in German where text is shown).',
  'HARD CONSTRAINT: show ONLY the functionality and content described below — do not invent new features, buttons, menu items, stats or sections.',
  'The global information architecture is fixed: a left sidebar with the app logo and navigation entries (Start, Konten, Kontakte, Sparpläne, Wertpapiere, Kontoauszüge, Budget, Berichte, Benutzerverwaltung, KI-Zugänge, Einstellungen, Hilfe, Abmelden), a ribbon toolbar at the top of the content area with grouped actions, and a scrollable content area.',
  'Everything must stay readable and usable on desktop; the layout must conceptually also work on small screens (sidebar collapses to a hamburger menu).'
].join(' ');

const VARIANTS = {
  a: {
    dir: 'variantA',
    title: 'FinanceManager Redesign - Variante A (modernisiert)',
    style: [
      'VARIANT A — MODERNIZED EVOLUTION of the existing design. Keep the current layout structure recognizable (wide left sidebar with text labels, classic ribbon with grouped buttons, content max-width ~1200px, data tables).',
      'Refine it to a clean, contemporary dark UI: deep neutral background (#10131a surface, #0b0e13 page), subtle 1px borders (#232a36), restrained elevation, a single indigo accent (#6d8dff), Inter/system typography with clear hierarchy, comfortable but data-dense spacing, rounded corners (8-10px), compact elegant tables with zebra-free clean rows, small monochrome icons.',
      'Goal: same app, noticeably more polished and consistent.'
    ].join(' ')
  },
  b: {
    dir: 'variantB',
    title: 'FinanceManager Redesign - Variante B (neu gedacht)',
    style: [
      'VARIANT B — COMPLETELY RETHOUGHT visual language, identical functionality. Free yourself from the current look: e.g. a slim icon-rail sidebar, a contextual top action bar instead of a classic ribbon (same actions!), card-based content composition on a layered dark background (#0a0d12 with soft teal-to-violet accent glows), glassy surfaces, expressive KPI typography, a modern accent palette (teal #2dd4bf primary, violet #8b5cf6 secondary), bold spacing rhythm.',
      'Goal: a fresh fintech/productivity aesthetic that would be impossible to confuse with the old design — while every described element stays present and findable.'
    ].join(' ')
  }
};

function buildPrompt(variantKey, view) {
  return `${SHARED_CONSTRAINTS}\n\n${VARIANTS[variantKey].style}\n\nSCREEN TO DESIGN — "${view.title}":\n${view.description}`;
}

// --- Download helpers ---------------------------------------------------------

async function downloadTo(url, filePath) {
  const res = await fetch(url, { headers: { 'X-Goog-Api-Key': apiKey } });
  if (!res.ok) throw new Error(`download failed ${res.status} for ${url.slice(0, 80)}`);
  let buf = Buffer.from(await res.arrayBuffer());
  // The files endpoint returns a JSON wrapper { "dataString": ..., "imageMetadata"? }
  // rather than raw bytes: images are base64 in dataString, HTML is plain text.
  if (buf.length > 0 && buf[0] === 0x7b) {
    try {
      const payload = JSON.parse(buf.toString('utf8'));
      if (typeof payload?.dataString === 'string') {
        buf = payload.imageMetadata ? Buffer.from(payload.dataString, 'base64') : Buffer.from(payload.dataString, 'utf8');
      }
    } catch { /* not JSON — keep raw bytes */ }
  }
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, buf);
}

// --- Main ---------------------------------------------------------------------

fs.mkdirSync(outputRoot, { recursive: true });
const results = [];
const manifestFile = path.join(outputRoot, 'manifest.json');
if (fs.existsSync(manifestFile)) {
  results.push(...JSON.parse(fs.readFileSync(manifestFile, 'utf8')).entries);
}

for (const [variantKey, variant] of Object.entries(VARIANTS)) {
  if (onlyVariant && onlyVariant !== variantKey) continue;

  console.log(`Creating Stitch project: ${variant.title}`);
  const project = await stitch.createProject(variant.title);
  console.log(`  projectId=${project.projectId}`);

  for (const view of viewDefs.views) {
    if (onlyIds && !onlyIds.has(view.id)) continue;

    const devices = ['DESKTOP'];
    if (includeMobile && MOBILE_VIEWS.has(view.id)) devices.push('MOBILE');

    for (const deviceType of devices) {
      const dir = deviceType === 'MOBILE' ? 'mobile' : 'desktop';
      const pngPath = path.join(outputRoot, variant.dir, dir, `${view.id}.png`);
      const htmlPath = path.join(outputRoot, variant.dir, dir, `${view.id}.html`);
      const existing = results.find(r => r.variant === variantKey && r.id === view.id && r.device === dir);
      if (fs.existsSync(pngPath) && fs.existsSync(htmlPath)) {
        if (existing?.status !== 'ok') {
          // Backfill manifest record for artifacts restored/skipped via file check
          // (keeps concurrent per-variant runs consistent).
          const record = { variant: variantKey, id: view.id, title: view.title, device: dir, status: 'ok',
            image: path.relative(outputRoot, pngPath).replace(/\\/g, '/'),
            html: path.relative(outputRoot, htmlPath).replace(/\\/g, '/'), screenId: null, error: null };
          const idx = results.findIndex(r => r.variant === variantKey && r.id === view.id && r.device === dir);
          if (idx >= 0) results[idx] = record; else results.push(record);
          fs.writeFileSync(manifestFile, JSON.stringify({ generatedAt: new Date().toISOString(), entries: results }, null, 2), 'utf8');
        }
        console.log(`  skip ${variantKey}/${dir}/${view.id} (exists)`);
        continue;
      }
      const record = { variant: variantKey, id: view.id, title: view.title, device: dir, status: 'pending', image: null, html: null, screenId: null, error: null };
      try {
        const screen = await project.generate(buildPrompt(variantKey, view), deviceType);
        record.screenId = screen.screenId;
        const imageUrl = await screen.getImage().catch(() => null);
        const htmlUrl = await screen.getHtml().catch(() => null);
        if (imageUrl) {
          await downloadTo(imageUrl, pngPath);
          record.image = path.relative(outputRoot, pngPath).replace(/\\/g, '/');
        }
        if (htmlUrl) {
          await downloadTo(htmlUrl, htmlPath);
          record.html = path.relative(outputRoot, htmlPath).replace(/\\/g, '/');
        }
        record.status = 'ok';
        console.log(`  generated ${variantKey}/${dir}/${view.id}`);
      } catch (err) {
        record.status = 'failed';
        record.error = String(err?.message || err).split('\n')[0];
        console.error(`  FAILED ${variantKey}/${dir}/${view.id}: ${record.error}`);
      }
      const idx = results.findIndex(r => r.variant === variantKey && r.id === view.id && r.device === dir);
      if (idx >= 0) results[idx] = record; else results.push(record);
      fs.writeFileSync(manifestFile, JSON.stringify({ generatedAt: new Date().toISOString(), entries: results }, null, 2), 'utf8');
      // Gentle pacing to stay within API rate limits.
      await new Promise(r => setTimeout(r, 1500));
    }
  }
}

const ok = results.filter(r => r.status === 'ok').length;
const failed = results.filter(r => r.status === 'failed');
console.log(`Done: ${ok} generated, ${failed.length} failed.`);
if (failed.length) {
  console.log('Failed views: ' + failed.map(f => `${f.variant}/${f.id}`).join(', '));
}
