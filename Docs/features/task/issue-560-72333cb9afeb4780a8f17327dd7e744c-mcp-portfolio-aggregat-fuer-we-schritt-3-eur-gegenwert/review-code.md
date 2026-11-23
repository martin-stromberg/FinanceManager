# Code-Review

## Ergebnis

**Status:** Keine blockierenden Befunde — zwei kleine Robustheits-Hinweise (Backup-Import) plus Dokumentations-/Design-Anmerkungen.

## Korrektheit

### `FxRateService` (`FinanceManager.Infrastructure/Securities/FxRateService.cs`)

- Auflösungsreihenfolge korrekt: EUR-Shortcut (Faktor 1, `"Fixed"`, Z. 56-59) → neuester gespeicherter Kurs ≤ Datum (`LatestStoredAsync`, Z. 124-128) → Provider-Lazy-Fill + Persistenz → früheste gespeicherte Rate als Fallback → `null`.
- **Date-Semantik — „never refresh" bewusst, aber wirkungsvoll:** Sobald *irgendein* gespeicherter Kurs ≤ Request-Datum existiert, wird der Provider nie wieder befragt. Da Kurse nur hinzugefügt, nie erneuert werden, friert die Umrechnung faktisch auf dem zuerst abgefragten Kursdatum ein — ein USD-Kurs vom 19.11. bedient auch noch Requests im Folgejahr. Das ist im Code (`<remarks>` Z. 14-26) und in `business-rules.md` explizit als „kein Staleness-Threshold, minimaler Provider-Traffic" dokumentiert und durch Tests abgesichert; `fxRate.date` macht die Staleness für den Aufrufer transparent. Konsistent zur „stored data"-Philosophie der App (Kurse = gespeicherte Schlusskurse). Fachlich zu bestätigen, ob permanente Staleness für den EUR-Gegenwert so gewollt ist — alternativ wäre ein Refresh, wenn der neueste gespeicherte Kurs deutlich älter als das Request-Datum ist.
- Fallback-Logik korrekt: existiert kein Kurs ≤ Datum, ist die *früheste* gespeicherte Rate die dem Datum nächstliegende (Z. 112-116).
- Race-Handling sauber: Semaphore je Währung, Re-Check im Gate, `DbUpdateException` → Detach + `fetched` zurückgeben (Z. 101-106). Kommentar „re-reads the row the other writer stored" beschreibt minimal ungenau, was passiert — Code nutzt den eigenen `fetched`-Wert (gleichwertig).
- Cancellation propagiert korrekt (`OperationCanceledException` mit `ct.IsCancellationRequested` re-thrown, Z. 83-86); sonstige Provider-Exceptions → geloggt + `null`.
- Kleinigkeiten: `LatestStoredAsync` ignoriert `Source` — gleiches Datum aus zwei Quellen wäre nichtdeterministisch (praktisch irrelevant). `DbUpdateException`-Catch ist breit (jeder Update-Fehler wird als Unique-Race behandelt — Rückgabe von `fetched` bleibt trotzdem korrekt). `FillGates` statisch, durch ISO-Währungsraum (~180) begrenzt.

### `FrankfurterFxRateProvider` (`FinanceManager.Web/Services/FrankfurterFxRateProvider.cs`)

- URL korrekt: `{base}/latest` bzw. `/{yyyy-MM-dd}` + `?from=EUR&to={escaped}`; `Uri.EscapeDataString` gegen Injection. Test assertiert die konkreten URLs.
- JSON-Parsing robust: fehlende Währung, `rate <= 0`, unparsebares Datum → `null` (Z. 68-74). `rates`-Keys der API sind uppercase, Lookup mit normalisiertem Code.
- Retry: genau einmal bei `HttpRequestException`/`TaskCanceledException`/`JsonException` (Z. 81-85) — TaskCanceledException deckt den HttpClient-Timeout ab; echter Cancellation wird vorher abgefangen und propagiert. Timeout via named Client (`ProgramExtensions`, `Fx:TimeoutSeconds`).
- `Enabled=false`-Killswitch greift vor jeder URL-Bildung. `GetLatestRateAsync` wird produktiv **nicht** genutzt (Service ruft nur `GetRateAsync`) — getestete, aber tote Interface-Fläche; kleiner Hinweis.

### `FmSecuritiesAnalysisTool` (Diff)

- Mathematik korrekt: `valueEur = openQty × close / fx.RatePerEur` (Division bei „Fremdwährung je 1 EUR"), EUR-Pfad 1:1 ohne `fxRate`-Objekt. Negativtest im EUR-Test file assertiert explizit gegen Multiplikation.
- Dedupe auf `(currency.ToUpperInvariant(), latestPrice.Date.Date)` vor den Service-Aufrufen — ein Call je Paar, sequentiell; kein Request je Position.
- `fxRates`-Lookup und Pair-Building normalisieren identisch (`Trim().ToUpperInvariant()`); `CurrencyCode` ist required-Spalte mit Entity-Normalisierung → `Trim()` praktisch sicher.
- Statistik: `EurValue` null-tolerant; `marketValueEur`/`totalMarketValueEur` nur, wenn **alle** bewertbaren Positionen der Zeile/des Totals umrechenbar sind — keine Teilsummen. Emission der Felder nur bei beiden Flags (zwei anonyme Contract-Varianten, typsicher je Response konsistent).
- `valueEur`/`fxRate` werden auch ohne Flag als `null` emittiert (wie `latestPrice`); Statistik-Felder dagegen ganz ohne Flag weggelassen — dokumentierte Entscheidung (`test-results.md`), konsistent.
- Kleinigkeiten: `valuedInGroup`/`valuedEur` werden auch bei `includeEurValue=false` berechnet (verschenkte LINQ-Arbeit, kein Fehler). Descriptor-Beschreibung enthält eine deutsche Klammer („Wert zum Kursdatum, nicht Live-Marktwert") im englischen Text — kosmetisch.

### Entität / Backup / Import

- `CurrencyExchangeRate` (Domain): Guards + Normalisierung im Konstruktor korrekt; OwnerUserId-Ausnahme sauber dokumentiert.
- **`AssignBackupDto` (Z. 88-96) hebt die Konstruktor-Normalisierung teilweise auf:** `Date = dto.Date` übernimmt eine etwaige Zeitkomponente unnormalisiert (Konstruktor hätte `.Date` gesetzt, wird dann überschrieben). Ein Backup-DTO mit `Date ≠ 00:00` würde eine Zeile erzeugen, die `r.Date <= requested` (datumsbasiert) nie matcht und die Import-Dedupe (`r.Date == dto.Date.Date`) umgeht — tote bzw. doppelte Zeile. Nur über handeditierte/ferdige Backups erreichbar (eigener Export schreibt immer 00:00), trotzdem inkonsistent: `Date = dto.Date.Date` bzw. `dto with { Date = dto.Date.Date }` wäre robust. Gleiches gilt abgeschwächt für `RatePerEur`/`Source` (Assign ohne Validierung — der Import geht aber durch den validierenden Konstruktor, sodass ungültige Werte ohnehin vorher werfen).
- **`SetupImportService` (neue Sektion):** `new CurrencyExchangeRate(normalized, dto.Date, dto.RatePerEur, dto.Source)` wirft bei malformed DTO (leere/whitespace Currency/Source, Rate ≤ 0) unkontrolliert und bricht den gesamten Restore ab. Andere Sektionen sind überwiegend genauso streng (vereinzelt per-DTO-try/catch wie `BudgetRules`); für einen globalen „netten-to-have"-Cache wäre ein Skip-and-log wie bei BudgetRules denkbar. Niedrige Schwere — Backup-Format ist systemgeneriert.
- Dedupe + Normalisierung des CurrencyCode korrekt; `dto.Id` wird bewusst verworfen (neue Id via `Entity`-Default — PK-sicher, Unique-Key trägt die Dedupe). `progress.Total` 42→43 passt zum neuen `Inc()`.
- `RequiredDataProperties` unverändert ohne `CurrencyExchangeRates` → Alt-Backups passieren die Schema-Validierung (Test belegt Import ohne Sektion).

### DI / Konfiguration / Migration

- `AddScoped<IFxRateService>` (Infrastructure), `AddScoped<IFxRateProvider>` + named HttpClient mit Timeout + User-Agent (Web) — Lifetimes konsistent (Scoped-Handler → Scoped-Context).
- `Fx`-Sektion in `appsettings.json` mit Kommentaren — `//`-Kommentare sind im File bereits Stil und von `JsonConfigurationProvider` unterstützt.
- Migration ist EF-generiert (`<auto-generated>`, `[Migration]`, `ProductVersion 10.0.15`, Snapshot konsistent) — AGENT.md-konform, nicht handgeschrieben.

## Tests

- Neue Suites decken die Kernpunkte ab: Division vs. Multiplikation, EUR-Shortcut ohne Provider (`MockBehavior.Strict`), Cache-Hit ohne Provider, Lazy-Fill + Persistenz + Provider-`Times.Once`, Provider-Fehler → earliest-stored → `null`, Retry-einmal, Enabled-Killswitch, Backup-Export/-Dedupe/-Altformat, Statistik-Emission nur bei beiden Flags.
- Geänderte bestehende Tests: nur `NewHandler`-Signatur um `IFxRateService` erweitert — keine Assertions gelockert/entfernt.
- Nachgeprüft: `dotnet build` 0 Fehler; `dotnet test --filter "FmSecuritiesAnalysis|FxRate|CurrencyExchangeRate"` → **41/41**.

## Hinweise (keine Befunde)

- Asymmetrie `marketValue` (0 bei komplett unbewerteter Zeile) vs. `marketValueEur` (`null`) — begründbar (kein Umrechnungsnachweis), Spezifikation nicht verletzt.
- `source`-Feld ehrlich (`"Frankfurter"` = API, Daten = EZB-Referenzkurse; `"Fixed"` = synthetisch) — sauber dokumentiert.

## Geprüfte Dateien

- `FinanceManager.Domain/Securities/CurrencyExchangeRate.cs` (neu)
- `FinanceManager.Application/Securities/{FxRate,IFxRateProvider,IFxRateService}.cs` (neu)
- `FinanceManager.Infrastructure/Securities/FxRateService.cs` (neu)
- `FinanceManager.Web/Services/{FrankfurterFxRateProvider,FxOptions}.cs` (neu)
- `FinanceManager.Infrastructure/AppDbContext.cs`, `ServiceCollectionExtensions.cs`, Migrations `AddCurrencyExchangeRates` + `AppDbContextModelSnapshot.cs` (Diff)
- `FinanceManager.Infrastructure/Backups/BackupService.cs`, `Setup/SetupImportService.cs` (Diff)
- `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`, `ProgramExtensions.cs`, `appsettings.json` (Diff)
- Tests: `FmSecuritiesAnalysisEurValueTests`, `FxRateServiceTests`, `FrankfurterFxRateProviderTests`, `CurrencyExchangeRateBackupRestoreTests` (neu); `FmSecuritiesAnalysis{Tool,Statistics,PriceError}Tests` (Diff)
- Docs: `Docs/help/mcp-server/{api,beschreibung,business-rules}.md`, `Docs/help/wertpapiermanagement/datenmodell.md`, `Docs/planung/mcp-integration/datenmodell-erweiterungen.md`, `steps.md`, `requirement.md`, `test-results.md`
