// Shared harness for screenshot tooling: starts the published app on a free
// port with a temporary SQLite database, performs UI registration (optionally
// with demo data) and login, and waits for the CreateDemoData background task.
// Extracted from generate-screenshots.js so both the README screenshot set and
// the design inventory run reuse the same startup/auth logic.

const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');
const http = require('http');

const repoRoot = path.resolve(__dirname, '..', '..', '..');
const appDll = path.join(repoRoot, 'FinanceManager.Web', 'bin', 'Release', 'net10.0', 'publish', 'FinanceManager.Web.dll');

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

function waitFor(predicate, timeoutMs = 120000, intervalMs = 500, description = 'predicate') {
  return new Promise((resolve, reject) => {
    const deadline = Date.now() + timeoutMs;
    const check = async () => {
      try {
        const ok = await predicate();
        if (ok) return resolve();
      } catch { /* ignore */ }
      if (Date.now() >= deadline) return reject(new Error(`Timeout while waiting for ${description}`));
      setTimeout(check, intervalMs);
    };
    check();
  });
}

// Starts the published app. Returns a handle with baseUrl, the temp dir
// (kept on failure for diagnosis), and stop() for cleanup.
async function startApp({ tempPrefix = 'financemanager-screenshots-' } = {}) {
  if (!fs.existsSync(appDll)) {
    throw new Error(
      `Published application not found at ${appDll}. Run "dotnet publish FinanceManager.Web -c Release" first.`
    );
  }

  const tempDbDir = fs.mkdtempSync(path.join(os.tmpdir(), tempPrefix));
  const tempDb = path.join(tempDbDir, 'financemanager.db');
  const appLogPath = path.join(tempDbDir, 'app.log');

  const port = await getFreePort();
  const baseUrl = `http://127.0.0.1:${port}`;
  const appLogFd = fs.openSync(appLogPath, 'w');

  const appProcess = spawn('dotnet', [appDll], {
    cwd: path.dirname(appDll),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_ENVIRONMENT: 'Development',
      ASPNETCORE_URLS: baseUrl,
      Api__BaseAddress: `${baseUrl}/`,
      E2E__DisableHttpsRedirection: 'true',
      ConnectionStrings__Default: `Data Source=${tempDb}`,
      Jwt__Key: 'screenshots-demo-signing-key-0123456789abcdef0123456789abcdef',
      BackgroundTasks__Enabled: 'true',
      Workers__SecurityPriceWorker__Enabled: 'false',
      FileLogging__Enabled: 'false',
      Updates__Enabled: 'false',
      Updates__HostedServicesEnabled: 'false',
      DetailedErrors: 'true'
    },
    stdio: ['ignore', appLogFd, appLogFd]
  });

  console.log(`Waiting for app on ${baseUrl} (log: ${appLogPath})`);
  await waitFor(async () => {
    try {
      const res = await fetch(`${baseUrl}/register`);
      return res.status < 500;
    } catch {
      return false;
    }
  }, 120000, 250, 'app readiness');
  console.log('App is up');

  const stop = () => {
    try { appProcess.kill('SIGTERM'); } catch { /* ignore */ }
  };
  const cleanupTempDir = () => {
    try { fs.rmSync(tempDbDir, { recursive: true, force: true }); } catch { /* ignore */ }
  };

  return { baseUrl, port, tempDbDir, tempDb, appLogPath, appProcess, stop, cleanupTempDir };
}

// The app imports "js/profile.js" as a bare module specifier; browsers cannot
// resolve that without an import map, which crashes the Blazor circuit on the
// register page. Inject an importmap into served HTML documents (same
// workaround as mcp-granular-shots.js).
async function installImportmap(context) {
  const importmap = JSON.stringify({ imports: {
    'js/profile.js': '/js/profile.js',
    'js/financeManager.js': '/js/financeManager.js',
    'js/attachments.js': '/js/attachments.js'
  }});
  await context.route('**/*', async (route) => {
    const req = route.request();
    if (req.resourceType() === 'document') {
      try {
        const resp = await route.fetch();
        let body = await resp.text();
        body = body.replace(/<head([^>]*)>/i, `<head$1><script type="importmap">${importmap}</script>`);
        await route.fulfill({ response: resp, body });
      } catch {
        // Server shutting down mid-request — fail the navigation quietly.
        await route.abort().catch(() => {});
      }
    } else {
      await route.continue().catch(() => {});
    }
  });
}

// Registers the first user through the real UI. With createDemoData=true the
// #create-demo-data checkbox (visible only for the first user) is checked so the
// register call enqueues the CreateDemoData background task. The first user is
// automatically granted the Admin role server-side, so the resulting account
// can also capture admin-only views like /list/users.
// page.goto can throw net::ERR_ABORTED when the app itself navigates away
// (e.g. AuthenticationRedirectMiddleware bouncing to /login). One retry with
// a short delay absorbs that race.
async function gotoWithRetry(page, url) {
  try {
    await page.goto(url, { waitUntil: 'load' });
  } catch (err) {
    if (!/ERR_ABORTED/.test(err.message)) throw err;
    await page.waitForTimeout(700);
    await page.goto(url, { waitUntil: 'load' });
  }
}

async function registerFirstUser(page, baseUrl, { username, password, createDemoData = true }) {
  await gotoWithRetry(page, `${baseUrl}/register`);
  await page.waitForSelector('#username', { timeout: 30000 });

  let registered = false;
  for (let attempt = 0; attempt < 3 && !registered; attempt++) {
    await page.fill('#username', username);
    await page.fill('#password', password);
    await page.evaluate((withDemoData) => {
      const userInput = document.querySelector('#username');
      const passInput = document.querySelector('#password');
      const demoCheckbox = document.querySelector('#create-demo-data');
      for (const el of [userInput, passInput]) {
        if (!el) continue;
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
      }
      if (demoCheckbox && withDemoData) {
        demoCheckbox.checked = true;
        demoCheckbox.dispatchEvent(new Event('input', { bubbles: true }));
        demoCheckbox.dispatchEvent(new Event('change', { bubbles: true }));
      }
    }, createDemoData);
    await page.click('button[type="submit"]');
    try {
      await page.waitForFunction(() => location.pathname !== '/register', null, { timeout: 5000 });
      registered = true;
    } catch { /* Blazor event handlers may still be attaching */ }
  }
  if (!registered) {
    throw new Error('Registration did not navigate away from /register');
  }
  console.log(`Registered ${username}${createDemoData ? ' with demo data enabled' : ''}`);
}

// Logs in through the UI (/login) and returns the bearer token extracted from
// the FinanceManager.Auth cookie (raw JWT).
async function login(page, context, baseUrl, { username, password }) {
  await gotoWithRetry(page, `${baseUrl}/login`);
  await page.waitForSelector('#login-user', { timeout: 30000 });
  await page.waitForFunction(() => typeof window.fmAuthLogin === 'function', null, { timeout: 30000 });
  await page.evaluate(() => {
    window.__fmAuthLoginCalls = 0;
    if (!window.__fmAuthLoginOriginal) {
      window.__fmAuthLoginOriginal = window.fmAuthLogin;
    }
    window.fmAuthLogin = async (...args) => {
      window.__fmAuthLoginCalls += 1;
      return await window.__fmAuthLoginOriginal(...args);
    };
  });
  let loggedIn = false;
  for (let attempt = 0; attempt < 5 && !loggedIn; attempt++) {
    // Re-fill and dispatch input+change every attempt: Blazor binds InputText
    // via onchange, and early clicks can land before the circuit is attached.
    await page.fill('#login-user', username);
    await page.fill('#login-pass', password);
    await page.evaluate(() => {
      for (const el of [document.querySelector('#login-user'), document.querySelector('#login-pass')]) {
        if (!el) continue;
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
      }
    });
    await page.click('button[type="submit"]');
    try {
      await page.waitForFunction(() => window.__fmAuthLoginCalls > 0, null, { timeout: 8000 });
      await page.waitForFunction(() => !location.pathname.includes('/login'), null, { timeout: 20000 });
      loggedIn = true;
    } catch { /* Blazor event handlers may still be attaching */ }
  }
  if (!loggedIn) {
    throw new Error('Login after registration failed');
  }

  const cookies = await context.cookies(baseUrl);
  const authCookie = cookies.find(c => c.name === 'FinanceManager.Auth');
  if (!authCookie) {
    throw new Error('FinanceManager.Auth cookie missing after login');
  }
  console.log('Logged in');
  return decodeURIComponent(authCookie.value);
}

// Decodes a JWT payload without verification and checks for the Admin role
// claim (ClaimTypes.Role -> "role" or the long namespace form).
function isAdminToken(token) {
  try {
    const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8'));
    const role = payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    return role === 'Admin' || (Array.isArray(role) && role.includes('Admin'));
  } catch {
    return false;
  }
}

async function apiGet(baseUrl, token, apiPath) {
  const res = await fetch(`${baseUrl}/${apiPath.replace(/^\//, '')}`, {
    headers: { Authorization: `Bearer ${token}` }
  });
  if (!res.ok) return { ok: false, status: res.status, body: null };
  const body = await res.json();
  return { ok: true, status: res.status, body };
}

// Waits for the CreateDemoData background task to appear and finish.
async function waitForDemoData(baseUrl, token) {
  const getActiveTasks = async () => {
    try {
      const res = await fetch(`${baseUrl}/api/background-tasks/active`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!res.ok) return null;
      const tasks = await res.json();
      return Array.isArray(tasks) ? tasks : null;
    } catch {
      return null;
    }
  };

  console.log('Waiting for demo-data background task to start');
  await waitFor(async () => {
    const tasks = await getActiveTasks();
    return tasks !== null && tasks.length > 0;
  }, 90000, 1000, 'demo-data task start');

  console.log('Waiting for demo-data background task to finish');
  await waitFor(async () => {
    const tasks = await getActiveTasks();
    return tasks !== null && tasks.length === 0;
  }, 360000, 2000, 'demo-data task finish');
  console.log('Demo data seeding finished');
}

// Finds the first guid-ish "id" value in a list endpoint response. Tolerates
// both bare arrays and paged envelopes ({items|results|entries|data:[...]}).
async function resolveFirstId(baseUrl, token, apiPath) {
  // Optional "#field" suffix: pick the first element of a named array property
  // on an object response (e.g. draft detail -> entries[0]).
  let arrayField = null;
  let path = apiPath;
  const hashIdx = apiPath.indexOf('#');
  if (hashIdx >= 0) {
    arrayField = apiPath.slice(hashIdx + 1);
    path = apiPath.slice(0, hashIdx);
  }
  const res = await apiGet(baseUrl, token, path);
  if (!res.ok) return null;
  let list = res.body;
  if (!Array.isArray(list) && list && typeof list === 'object') {
    if (arrayField && Array.isArray(list[arrayField])) {
      list = list[arrayField];
    } else {
      list = list.items || list.results || list.entries || list.data || null;
    }
  }
  if (!Array.isArray(list) || list.length === 0) return null;
  const first = list[0];
  if (!first || typeof first !== 'object') return null;
  for (const key of Object.keys(first)) {
    if (/^id$/i.test(key) && typeof first[key] === 'string' && first[key].length >= 32) {
      return first[key];
    }
  }
  for (const key of Object.keys(first)) {
    const v = first[key];
    if (/id$/i.test(key) && typeof v === 'string' && /^[0-9a-f-]{32,36}$/i.test(v)) {
      return v;
    }
  }
  return null;
}

module.exports = {
  repoRoot,
  appDll,
  getFreePort,
  waitFor,
  startApp,
  installImportmap,
  registerFirstUser,
  login,
  isAdminToken,
  apiGet,
  waitForDemoData,
  resolveFirstId
};
