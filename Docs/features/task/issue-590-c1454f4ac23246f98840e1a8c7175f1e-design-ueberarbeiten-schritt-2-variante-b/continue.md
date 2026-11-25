# Offene Punkte / Folgeaufgaben — Design-Variante B (Issue #590, Schritt 2)

## Ergebnis

Umsetzung abgeschlossen: Chrome (Icon-Rail + Topbar), Action-Bar (Ribbon→Glaspillen),
komplette Token-Umprägung aller Styles, Help-Doku + Release-Notes aktualisiert,
Screenshots + `demo.gif` neu erzeugt. Build/Unit/Integration grün; E2E ohne neue
Fehlschläge — im Gegenteil: 6 preexisting Nav-Timeouts sind geheilt (168→173 grün).

## Offene Punkte (keine Blocker)

1. **3 verbleibende preexisting E2E-Fehlschläge** (unverändert, siehe `test-results.md`):
   - `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat…` — href-Assertion ohne führenden `/`.
   - `KpiLocalStorageE2ETests.DisableCache_RemovesKpiDataFromLocalStorage` — `button#Save:enabled`-Timeout.
   - `BudgetReportDemoDataE2ETests.…(750)` — Register-Submit-Navigations-Timeout.
2. **PR-Erstellung** gegen `staging` obliegt dem Projekt-Koordinator (Scope-Ausschluss #4).
3. **Screenshot-Skript-Umgebung**: `generate-screenshots.js` benötigt unter neueren
   Chromium-Versionen die Launch-Flags `--disable-features=LocalNetworkAccessChecks,
   BlockInsecurePrivateNetworkRequests --allow-insecure-localhost` (WebSocket-PNA-Block,
   `ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS`). Flag wurde — analog zu
   `mcp-granular-shots.js` — ergänzt; ggf. für `probe-circuit-auth.js`/`render-proposals.js`
   nachziehen, falls diese lokal laufen sollen.
4. **`theme.Dark.SetupNotificationsTab.css.css` gelöscht**: Die Dublette wurde in
   `theme.Dark.SetupNotificationsTab.css` konsolidiert (war bisher nur ein
   `@import`-Shim auf die `.css.css`).
5. Feinschliff möglich: kompakte Darstellung von „Passwort ändern" in der 72-px-Rail
   (aktuell zweizeilig umbrochen) — kosmetisch, kein Funktionsverlust.
