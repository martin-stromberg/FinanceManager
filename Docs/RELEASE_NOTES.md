# Release Notes

## Important Notes Before Update

- Self-update: the vendored updater library `msTools.Updater` was upgraded to 0.11.0-rc.8 — a scheduled update installation whose time was missed (e.g. because the application was not running) is now caught up immediately instead of waiting for the next configured time window; a previously missed installation may therefore start right after this update (issue #588).

## What's New

- UI: the entire web interface was redesigned to the approved design variant A — dark shell (`#0b0e13`) with indigo accent and Inter typography, a canonical 13-entry sidebar navigation with icons, a slim top bar per view, a calmer ribbon with icon-on-top actions, and restyled tables, cards, forms and dialogs; public pages (login/register/legal/error) now show a minimal brand bar with a centered card (issue #590, step 1).
- MCP: new opt-in approval mode "Granular" (expert mode) for MCP accesses — approval grants can be configured per data type and right, harmless read operations run directly while write/delete operations remain approval-required; manageable via wizard/editor UI and the `approval-grants` API (issue #578).
- MCP: `fm_securities_analysis` supports `includeEurValue` — per position `valueEur`/`fxRate { rate, date, source }` plus `marketValueEur`/`totalMarketValueEur` in the statistics, based on ECB reference rates (Frankfurter); missing rates yield `null`, never an error (issue #561).
- MCP: `fm_securities_analysis` supports `includeStatistics` — an opt-in `statistics` aggregate over the filtered set with `securityCount`, `positionCount`, `positionsWithoutPrice`, per-currency/per-category market values, `totalMarketValue` (single currency only) and `oldestPriceDate`/`newestPriceDate`; also fixes a double-counting of active securities when `onlyActive` is set (issue #560).
- MCP: price-error transparency in `fm_securities_analysis` — securities now report `priceErrorSinceUtc` (top level) and `untrusted_data.priceErrorMessage`, making the start and cause of a price error visible (issue #562).
- MCP: the read tool `fm_securities_analysis` now always returns a top-level `latestPrice` field `{ close, date }` per security — the most recently stored closing price and its date — in both the list and the single-security query, regardless of `includePrices`/`priceTake`; `latestPrice` is `null` when no price is stored and is still returned when `hasPriceError` is set (the flag remains an independent signal), so the previous `includePrices: true` + `priceTake: 1` workaround is no longer needed (issue #559).

## Wichtige Hinweise vor dem Update

- Self-Update: die mitgelieferte Updater-Bibliothek `msTools.Updater` wurde auf 0.11.0-rc.8 angehoben — eine geplante Update-Installation, deren Zeitpunkt versäumt wurde (z. B. weil die Anwendung nicht lief), wird jetzt unmittelbar nachgeholt statt auf das nächste konfigurierte Zeitfenster zu warten; eine zuvor versäumte Installation kann daher direkt nach diesem Update starten (Issue #588).

## Neuerungen

- UI: die gesamte Weboberfläche wurde auf die freigegebene Design-Variante A umgestellt — dunkles Grundgerüst (`#0b0e13`) mit Indigo-Akzent und Inter-Typografie, eine kanonische 13er-Sidebar-Navigation mit Icons, eine schlanke Titelzeile je Ansicht, ein ruhigeres Ribbon mit Icon-über-Label-Aktionen sowie neu gestaltete Tabellen, Karten, Formulare und Dialoge; öffentliche Seiten (Anmeldung/Registrierung/Impressum/Fehler) zeigen jetzt eine schmale Brand-Leiste mit zentrierter Karte (Issue #590, Schritt 1).
- MCP: neuer Opt-in-Genehmigungsmodus „Granular" (Expertenmodus) für MCP-Zugänge — Genehmigungen lassen sich je Datentyp und Recht konfigurieren, harmlose Leseoperationen laufen direkt, während Schreib-/Löschoperationen weiterhin genehmigungspflichtig bleiben; verwaltbar über Wizard-/Editor-UI und die `approval-grants`-API (Issue #578).
- MCP: `fm_securities_analysis` unterstützt `includeEurValue` — je Position `valueEur`/`fxRate { rate, date, source }` sowie `marketValueEur`/`totalMarketValueEur` in der Statistik, basierend auf EZB-Referenzkursen (Frankfurter); fehlende Kurse liefern `null`, nie einen Fehler (Issue #561).
- MCP: `fm_securities_analysis` unterstützt `includeStatistics` — ein Opt-in-Aggregat `statistics` über der gefilterten Menge mit `securityCount`, `positionCount`, `positionsWithoutPrice`, Marktwerten je Währung/Kategorie, `totalMarketValue` (nur bei Einzelwährung) sowie `oldestPriceDate`/`newestPriceDate`; behebt nebenbei eine Doppelzählung aktiver Wertpapiere bei gesetztem `onlyActive` (Issue #560).
- MCP: Preisfehler-Transparenz in `fm_securities_analysis` — Wertpapiere melden jetzt `priceErrorSinceUtc` (Top-Level) und `untrusted_data.priceErrorMessage`, sodass Beginn und Ursache eines Kursfehlers sichtbar werden (Issue #562).
- MCP: Das Lesetool `fm_securities_analysis` liefert jetzt je Wertpapier ein immer vorhandenes Top-Level-Feld `latestPrice` `{ close, date }` — den zuletzt gespeicherten Schlusskurs samt Datum — in der Listen- wie in der Einzelabfrage, unabhängig von `includePrices`/`priceTake`; `latestPrice` ist `null`, wenn kein Kurs gespeichert ist, und wird auch bei gesetztem `hasPriceError` ausgegeben (das Flag bleibt eigenständiges Signal) — der bisherige Workaround `includePrices: true` + `priceTake: 1` entfällt damit (Issue #559).
