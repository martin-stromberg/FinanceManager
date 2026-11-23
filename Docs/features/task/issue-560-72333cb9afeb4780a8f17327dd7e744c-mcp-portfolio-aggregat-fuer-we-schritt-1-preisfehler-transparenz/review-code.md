# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Korrektheit

- **Mapping:** `MapToDtoAsync` (`SecurityService.cs:269-270`) überträgt `PriceErrorMessage`/`PriceErrorSinceUtc` 1:1 aus der Entität. Es ist die einzige `SecurityDto`-Erzeugungsstelle im Produktivcode — `ListAsync` (Z. 49), `GetAsync` (Z. 66) und die Create/Update-Rückgaben laufen alle hierdurch. Inkonsistente Teilzustände (z. B. Flag ohne Zeitstempel) können nur aus manipulierten Backups stammen; die Werte werden ungeprüft durchgereicht — wie im Plan-Check (Hinweis 4) dokumentiert und akzeptiert, da die Entität die Felder stets gemeinsam setzt/löscht (`SetPriceError`/`ClearPriceError`, `Security.cs:212-226`).
- **Tool-Projektion:** `priceErrorSinceUtc` Top-Level neben `hasPriceError` (`FmSecuritiesAnalysisTool.cs:144`), `priceErrorMessage` im `untrusted_data`-Block (Z. 159). Platzierung korrekt: Provider-Fehlertext kann Infrastrukturdetails enthalten und ist Freitext mit Drittinhalten → `untrusted_data` (sicherheitskonzept.md §5.2); der UTC-Zeitstempel ist strukturierte Systemmetadaten → Top-Level legitim. Beide Response-Pfade (Liste/Einzel) teilen sich die Projektionsschleife — kein Driftrisiko.
- **Null-Semantik:** Nullable-Felder serialisieren als JSON `null` statt zu fehlen — entspricht dem bestehenden `prices`/`latestPrice`-Muster (`null` = „nicht vorhanden", Feld immer emittiert).
- **Descriptor:** Beschreibung fachlich korrekt — Flag-Lifecycle (nur erfolgreicher Fetch löscht), drei Ursachen für leeres `prices`, `openQuantity` als Buchungsinkonsistenz statt Short-Position. Entspricht der Entitäts-Semantik (`ClearPriceError` wird nur bei Erfolg aufgerufen, vgl. `SecurityPriceUpdateService.cs`).

## Stil und Konventionen

- XML-Doku der neuen DTO-Member im Stil der Datei (englisch, einzeilig, konsistent mit `HasPriceError`/`OpenQuantity` direkt daneben).
- Beschreibung als String-Konkatenation über drei Zeilen — gleiche Bauweise wie zuvor; englischer Text wie im Referenz-Descriptor.
- Keine unnötigen Kommentare, keine entfernten Kommentare, keine Formatierungsartefakte im Diff (57 `+`, 2 `−`, davon 44 Zeilen Test).
- Keine Enum-Erweiterung → `EnumCoverageTests` unberührt; keine Resx-/UI-Änderung nötig; keine EF-Migration (Felder bereits persistiert, `SecurityBackupDto` enthält sie bereits mit Defaults — AGENT.md-Backupregel erfüllt).

## Sicherheit

- Owner-Filter unverändert aus `context.Access.OwnerUserId` (`FmSecuritiesAnalysisTool.cs:82`), kein Owner-Parameter vom Client; Record-Scope-Filter (`_recordFilter.ApplyAsync`) greift vor der Projektion unverändert.
- Provider-Fehlertext ausschließlich unter `untrusted_data`, kein Top-Level-Duplikat (per Unit- und Wire-Test assertiert) — Prompt-Injection-Oberfläche konventionskonform behandelt.
- REST-Nebeneffekt beachtet: `SecuritiesController` liefert `SecurityDto` direkt, die Felder erscheinen dort ebenfalls — besitzerbezogene Daten, dokumentierte Annahme, kein Befund.

## Tests

- `FmSecuritiesAnalysisPriceErrorTests` (4 Fälle): Listenpfad mit Fehler (Top-Level-Zeitstempel, `untrusted_data`-Nachricht, **kein** Top-Level-Duplikat via `KeyNotFoundException`-Assert), Null-Fall (`JsonValueKind.Null` für beide Felder), Einzelpfad (`securityId`), Descriptor-Inhaltstest (sechs Schlüsselbegriffe inkl. „short").
- `SecurityServicePriceErrorMappingTests` (2 Fälle): Entity→DTO-Mapping bei gesetztem Fehler (reales `SetPriceError` über InMemory-DB) und Null-Fall.
- `McpSecuritiesAnalysisTests.ToolsCall_SecuritiesAnalysis_ReturnsPriceErrorDetails`: Wire-Level über `/mcp` mit geseedeter Entität — prüft `hasPriceError`, `priceErrorSinceUtc`, `untrusted_data.priceErrorMessage` und `TryGetProperty("priceErrorMessage") == false` auf Top-Level.
- Nachvollzug lokal: gefilterter Lauf (`FmSecuritiesAnalysisPriceErrorTests` + `SecurityServicePriceErrorMappingTests` + `FmSecuritiesAnalysisToolTests`) → **12/12 bestanden**, kein Bestandsbruch.
- Abdeckung vollständig gegenüber Plan (Liste, Single, Null, Beschreibung, Wire). Einzig nicht testbar/abgedeckt: exakter Beschreibungstext (nur Schlüsselbegriffe assertiert) — ausreichend, da Freitext.

## Hinweise (keine Befunde)

- Hilfeseiten-Doku (`Docs/help/mcp-server/api.md` T07-Zeile, `scope-und-toolkatalog.md`) steht noch aus — ist Workflow-Schritt 12, nicht Teil der Implementierung; siehe `review.md`.
- Testdatei `FmSecuritiesAnalysisPriceErrorTests.cs` nutzt `Mock.Of<ISecurityCategoryService>()` — `includeCategories` defaultet auf `false`, der Mock wird nie aufgerufen; korrekt und ohne Overhead.

## Geprüfte Dateien

- `FinanceManager.Shared/Dtos/Securities/SecurityDto.cs`
- `FinanceManager.Infrastructure/Securities/SecurityService.cs`
- `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs`
- `FinanceManager.Domain/Securities/Security.cs` (Feld-/Backup-Kontext)
- `FinanceManager.Tests/Mcp/FmSecuritiesAnalysisPriceErrorTests.cs` (neu)
- `FinanceManager.Tests/Infrastructure/Securities/SecurityServicePriceErrorMappingTests.cs` (neu)
- `FinanceManager.Tests.Integration/McpSecuritiesAnalysisTests.cs`
- `FinanceManager.Tests/Mcp/McpToolTestBase.cs` (Test-Helfer)
- `Docs/planung/mcp-integration/sicherheitskonzept.md` (§5.2, Konventionsnachweis)
