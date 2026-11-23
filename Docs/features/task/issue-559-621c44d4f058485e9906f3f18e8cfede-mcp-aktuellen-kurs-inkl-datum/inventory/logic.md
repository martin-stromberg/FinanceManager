# Logik – Bestand

## `FmSecuritiesAnalysisDescriptor`
Datei: `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs` (Zeilen 14–40)

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|--------------|------------------|
| `ToolName` (const) | public | `"fm_securities_analysis"` (Zeile 17) |
| Konstruktor | public | Übergibt das JSON-Eingabeschema an `McpToolDescriptorBase`: `securityId`, `onlyActive`, `includePrices`, `priceTake` (1–200), `includeCategories`, `skip`, `take`; `additionalProperties: false` (Zeile 21) |
| `Name` | override | Toolname (Zeile 25) |
| `Description` | override | Beschreibung inkl. Scope-Hinweis (Zeile 29) |
| `SecurityClass` | override | `McpToolSecurityClass.Lesend` (Zeile 32) |
| `RequiredScopes` | override | `(Securities, Read)` + `(SecurityCategories, Read)` (Zeilen 35–39) |

DI-Registrierung: `ProgramExtensions.cs` Zeile 463 (`AddSingleton<IMcpToolDescriptor, FmSecuritiesAnalysisDescriptor>`).

## `FmSecuritiesAnalysisHandler`
Datei: `FinanceManager.Web/Mcp/Tools/FmSecuritiesAnalysisTool.cs` (Zeilen 49–163)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| Konstruktor | public | Injiziert `ISecurityService`, `ISecurityPriceService`, `ISecurityCategoryService`, `McpRecordScopeFilter` (Zeilen 57–67) |
| `ToolName` | public | `FmSecuritiesAnalysisDescriptor.ToolName` (Zeile 70) |
| `InvokeAsync` | public | Kernlogik, Zeilen 73–162: siehe Ablauf unten |

Ablauf von `InvokeAsync`:
1. Argumente parsen via `McpToolArguments` (`securityId`, `onlyActive`, `includePrices`, `priceTake` Default 60 geclampt 1–200, `includeCategories` Default `false`, `skip`, `take`) — Zeilen 76–82.
2. **Einzelpfad** (`securityId` gesetzt): `_securities.GetAsync(id, owner, ct)`; `null`-Rückgabe → `securities` bleibt leer; `includePrices ??= true` — Zeilen 85–93. `GetAsync` ist owner-scoped; nicht gefundene/fremde Ids erzeugen bereits eine leere `securities`-Liste.
3. **Listenpfad** (kein `securityId`): `ListAsync(owner, true)` bzw. `ListAsync(owner, false)` je nach `onlyActive`, dann `Skip(skip).Take(take)`; `includePrices ??= false` — Zeilen 94–108.
4. **Record-Filter**: `_recordFilter.ApplyAsync(context.Access, McpDataType.Securities, securities, s => new McpRecordContext { SecurityCategoryId = s.CategoryId }, ct)` — Zeilen 111–113. Erst danach erfolgt jede Kursabfrage; ein scope-gefiltertes Wertpapier taucht nicht im Ergebnis auf (verhält sich wie „nicht vorhanden").
5. **Pro sichtbarem Wertpapier**: `prices` nur bei `includePrices == true` via `_prices.ListAsync(owner, s.Id, 0, priceTake, ct)` → anonyme `{ date, close }`-Liste (eine Abfrage je Wertpapier = potenzielles N+1); anonymes Ausgabeobjekt mit `id`, `categoryId`, `currencyCode`, `isActive`, `createdUtc`, `archivedUtc`, `symbolAttachmentId`, `hasPriceError`, `openQuantity`, `prices`, `untrusted_data { name, identifier, description, alphaVantageCode, categoryName, region, sector }` — Zeilen 115–147. **Kein `latestPrice`-Feld vorhanden.**
6. Optional `categories` via `_categories.ListAsync(owner, ct)` → `{ id, symbolAttachmentId, untrusted_data.name }` — Zeilen 149–159.
7. Rückgabe `{ securities = result, categories }` — Zeile 161.

DI-Registrierung: `ProgramExtensions.cs` Zeile 509 (`AddScoped<IMcpToolHandler, FmSecuritiesAnalysisHandler>`).

Events: keine (weder abonniert noch publiziert).

## `SecurityPriceService`
Datei: `FinanceManager.Infrastructure/Securities/SecurityPriceService.cs` (Zeilen 18–240)

Implementiert `ISecurityPriceService`; einzige produktive Implementierung (DI: `ServiceCollectionExtensions.cs` Zeile 132, `AddScoped<ISecurityPriceService, SecurityPriceService>`). Jede Methode prüft den Besitzerverweis über `_db.Securities.AnyAsync(s => s.Id == securityId && s.OwnerUserId == ownerUserId)` (bzw. `FirstOrDefaultAsync`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `CreateAsync` | public | Legt `SecurityPrice` an; wirft `ArgumentException` bei Fremdbesitz, `ArgumentOutOfRangeException` bei `close < 0`; invalidiert Portfolio-Cache — Zeilen 52–66 |
| `ListAsync` | public | Besitzerprüfung → leere Liste bei Fremdbesitz; `OrderByDescending(p.Date)` + `Skip/Take` → `SecurityPriceDto`-Liste — Zeilen 82–94. Mit `take: 1` liefe funktional der letzte Kurs, aber eine Abfrage pro Wertpapier (N+1) plus Besitzerprüfung je Abfrage |
| `GetLatestDateAsync` | public | Besitzerprüfung → `null` bei Fremdbesitz oder ohne Kurse; liefert **nur** `DateTime?` (max. `Date`), nicht den Kurs — Zeilen 106–113 |
| `UpsertDailyPricesAsync` | public | Tageskurs-Upsert; „last item wins" bei Duplikaten; Besitzerprüfung; Portfolio-Cache-Invalidierung bei Änderungen — Zeilen 123–193 |
| `SetPriceErrorAsync` | public | Setzt `Security.SetPriceError` — Zeilen 206–216 |
| `ClearPriceErrorAsync` | public | Setzt `Security.ClearPriceError`, No-Op ohne Fehlerflag — Zeilen 226–239 |

**Keine Batch-Methode** für „letzter Kurs je Wertpapier-Menge" vorhanden.

## `SecurityService`
Datei: `FinanceManager.Infrastructure/Securities/SecurityService.cs` (Zeilen 11–274)

Implementiert `ISecurityService`; liefert die `SecurityDto`-Eingangsdaten des MCP-Handlers.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `ListAsync` | public | Owner-Filter + `IsActive`-Filter; `OpenQuantity` gebündelt per `Postings`-`GroupBy` (Batch, kein N+1 bei der Menge); `CategoryName` pro Entität einzeln aufgelöst — Zeilen 28–53 |
| `GetAsync` | public | Owner-scoped Einzelabruf; `null` bei Fremdbesitz/fehlend — Zeilen 62–67 |
| `CreateAsync`/`UpdateAsync`/`ArchiveAsync`/`UnarchiveAsync`/`DeleteAsync`/`CountAsync`/`SetSymbolAttachmentAsync` | public | Schreib-/Zähloperationen, für die Anforderung nicht relevant |
| `ComputeOpenQuantityAsync` | private | Σ Buy − Σ Sell je Wertpapier — Zeilen 238–244 |
| `MapToDtoAsync` | private | Entity → `SecurityDto` inkl. `CategoryName`-Lookup und `HasPriceError`/`OpenQuantity` — Zeilen 246–273 |

## `McpRecordScopeFilter`
Datei: `FinanceManager.Web/Mcp/McpRecordScopeFilter.cs` (Zeilen 25–206)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `GetReadScopesAsync` / `GetScopesAsync` | public | Scope-Zeilen des effektiven Profils je `(DataType, Right)`, pro Request gecacht — Zeilen 45–79 |
| `RequiresRecordFilter` / `HasFilter` | public static | Ob Filter-CSVs gesetzt sind — Zeilen 87–98 |
| `IsAllowed` | public static | OR über Scope-Zeilen, AND über die fünf Filterdimensionen — Zeilen 107–108, 160–165 |
| `ApplyAsync<T>` | public | Filtert materialisierte Liste; ohne Filter-CSV unveränderte Durchleitung — Zeilen 125–139 |
| `IsVisibleAsync` | public | Einzeldatensatz-Variante — Zeilen 149–158 |

Im `fm_securities_analysis`-Aufruf wird die `SecurityCategoryId`-Dimension gegen `Security.CategoryId` geprüft (Whitelist-Semantik: gesetzte CSV schließt unkategorisierte Wertpapiere aus).

## `SecuritiesController`
Datei: `FinanceManager.Web/Controllers/SecuritiesController.cs`

Nur relevant für Scope-Variante (b) der offenen Fragen (Wirkung auf `GET /api/securities`):

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|------------------|
| `ListAsync` | public | `GET /api/securities` → `_service.ListAsync(user, onlyActive)` → `SecurityDto`-Liste (Zeilen 109–112) — trägt heute keinen Kurs |
| `GetAsync` | public | `GET /api/securities/{id}` → `SecurityDto` bzw. 404 (Zeilen 134–141) |
| `GetPricesAsync` | public | `GET /api/securities/{id}/prices` → `_priceService.ListAsync(user, id, skip, take≤250)` (Zeilen 401–410) |
| `ImportPricesAsync` / `EnqueueBackfill` / weitere | public | Schreib-/Task-Pfade, nicht betroffen |

## Weitere Konsumenten von `ISecurityPriceService`

| Klasse | Datei | Nutzung |
|--------|-------|---------|
| `SecurityPricesBackfillExecutor` | `FinanceManager.Web/Services/SecurityPricesBackfillExecutor.cs` | `GetLatestDateAsync` (Zeile 121) zur Backfill-Fensterberechnung; `UpsertDailyPricesAsync`, `ClearPriceErrorAsync` |
| `SecurityPriceUpdateService` | `FinanceManager.Infrastructure/Securities/SecurityPriceUpdateService.cs` | `_priceService` (Zeilen 19, 36) für Upsert/Error-Pfade |
| `SecurityPriceStoreAndEvaluateService` | `FinanceManager.Infrastructure/Securities/SecurityPriceStoreAndEvaluateService.cs` | `UpsertDailyPricesAsync` (Zeile 43) |
| `DemoDataService` | `FinanceManager.Infrastructure/Demo/DemoDataService.cs` | `UpsertDailyPricesAsync` (Zeile 1185) für Demo-Kurse |
| `ReturnAnalysisService` | `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs` | Fragt `_db.SecurityPrices` **direkt** für „letzten Kurs" ab (Zeilen 199 ff., 829 ff.) — kein `ISecurityPriceService`-Konsument; zeigt ein vorhandenes Muster „max. Date je Wertpapier" |

## `McpToolArguments`
Datei: `FinanceManager.Web/Mcp/McpToolArguments.cs`

Statische Parsing-Helper (`Skip`, `Take` mit Clamp 1–200/`MaxTake`, `OptionalGuid`, `OptionalBool`, `OptionalInt`, `Has`, `HasNonNull`); ungültige Werte werfen `McpToolException("Err_Mcp_InvalidArguments")`. Für `latestPrice` ist kein neuer Parameter vorgesehen — keine Änderung nötig.
