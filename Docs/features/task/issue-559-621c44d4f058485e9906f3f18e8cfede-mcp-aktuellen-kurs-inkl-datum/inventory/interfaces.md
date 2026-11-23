# Interfaces – Bestand

## `ISecurityPriceService`
Datei: `FinanceManager.Application/Securities/ISecurityPriceService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CreateAsync` | `ownerUserId`, `securityId`, `date`, `close`, `ct` | `Task` | Einzelkurs anlegen (Zeile 26) |
| `ListAsync` | `ownerUserId`, `securityId`, `skip`, `take`, `ct` | `Task<IReadOnlyList<SecurityPriceDto>>` | Kurshistorie absteigend nach Datum (Zeile 42); `take: 1` würde funktional den letzten Kurs liefern — aber je Wertpapier eine Abfrage plus Besitzerprüfung (N+1) |
| `GetLatestDateAsync` | `ownerUserId`, `securityId`, `ct` | `Task<DateTime?>` | Nur das Datum des letzten Kurses — reicht für `{ close, date }` nicht aus (Zeile 51) |
| `UpsertDailyPricesAsync` | `ownerUserId`, `securityId`, `items`, `ct` | `Task<SecurityPriceImportResultDto>` | Tageskurs-Upsert, „last item wins" (Zeile 61) |
| `SetPriceErrorAsync` | `ownerUserId`, `securityId`, `message`, `ct` | `Task` | Fehlerflag setzen (Zeile 76) |
| `ClearPriceErrorAsync` | `ownerUserId`, `securityId`, `ct` | `Task` | Fehlerflag löschen (Zeile 85) |

**Keine Batch-Methode** (`SecurityId`-Menge → letzter Kurs) vorhanden. Implementierungen: `SecurityPriceService` (produktiv, einzige) und `SecurityPriceErrorRecoveryTests.SpySecurityPriceService` (privater Test-Spy, `FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs` Zeilen 37–85 — manuelle Implementierung, die bei jedem neuen Interface-Member mit nachgezogen werden muss; `Mock<ISecurityPriceService>`-Stellen in `SecuritiesControllerTests.cs` Zeile 48 und `SecurityPriceErrorRecoveryTests.cs` sind davon nicht betroffen).

## `ISecurityService`
Datei: `FinanceManager.Application/Securities/ISecurityService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListAsync` | `ownerUserId`, `onlyActive`, `ct` | `Task<IReadOnlyList<SecurityDto>>` | Aktive bzw. inaktive Wertpapiere des Besitzers (Zeile 15); der Handler ruft beide Partitionen auf, wenn `onlyActive` nicht gesetzt |
| `GetAsync` | `id`, `ownerUserId`, `ct` | `Task<SecurityDto?>` | Einzelabruf; `null` bei Fremdbesitz/fehlend (Zeile 25) |
| `CreateAsync`/`UpdateAsync`/`ArchiveAsync`/`UnarchiveAsync`/`DeleteAsync`/`CountAsync`/`SetSymbolAttachmentAsync` | div. | div. | Schreib-/Zähloperationen — für die Anforderung nicht relevant |

## `ISecurityCategoryService`
Datei: `FinanceManager.Application/Securities/ISecurityCategoryService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListAsync` | `ownerUserId`, `ct` | `Task<IReadOnlyList<SecurityCategoryDto>>` | Kategorieliste des Besitzers (Zeile 14) — vom Handler nur bei `includeCategories: true` genutzt |
| `GetAsync`/`CreateAsync`/`UpdateAsync`/`DeleteAsync`/`SetSymbolAttachmentAsync` | div. | div. | Nicht vom Handler genutzt |

## `IMcpToolHandler` / `McpToolCallContext`
Datei: `FinanceManager.Application/Mcp/IMcpToolHandler.cs`

| Member | Signatur | Zweck |
|--------|----------|-------|
| `ToolName` | `string` (Property) | `fm_*`-Name des Handlers (Zeile 50) |
| `InvokeAsync` | `(JsonElement arguments, McpToolCallContext context, CancellationToken ct) → Task<object>` | Ausführung; Rückgabewert wird als `structuredContent` serialisiert (Zeile 60) |
| `McpToolCallContext` (Record) | `(IMcpAccessContext Access, IServiceProvider Services, Guid CorrelationId, string? SourceIp, McpToolExecutionMode Mode = Interactive)` | Aufrufkontext; `Access.OwnerUserId` liefert die Besitzer-Id (Zeilen 15–20) |
| `McpToolException` | `(string code, string message)` | Kontrollierter Fehler mit `Err_Mcp_*`-Code → `isError`-Toolresult (Zeilen 26–40) |

## `IMcpAccessContext`
Datei: `FinanceManager.Application/Mcp/IMcpAccessContext.cs`

| Member | Typ | Zweck |
|--------|-----|-------|
| `IsMcpAuthenticated` | `bool` | PAT-Authentifizierung aktiv (Zeile 16) |
| `McpAccessId` | `Guid` | Id des authentifizierten Zugangs (Zeile 22) |
| `OwnerUserId` | `Guid` | Zugangsbesitzer — einzige zulässige Quelle der Owner-Id für Serviceaufrufe (Zeile 28) |
| `Access` | `McpAccess?` | Geladener Zugangsdatensatz (Zeile 34) |

## `IMcpScopeEvaluator`
Datei: `FinanceManager.Application/Mcp/IMcpScopeEvaluator.cs` (Zeilen 76–110)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetEffectiveScopesAsync` | `access`, `ct` | `Task<IReadOnlyList<McpAccessScope>>` | Effektives Profil (Level-Default oder persistierte Zeilen) — Basis von `McpRecordScopeFilter` |
| `IsAllowedAsync` | `access`, `dataType`, `right`, `ct` | `Task<bool>` | Scope-Tupel-Check (Stufe 1) |
| `CheckRecordAccessAsync` | `access`, `dataType`, `right`, `record`, `ct` | `Task<McpAccessVerdict>` | Vollprüfung Stufen 0–3 je Datensatz |

## `IMcpToolDescriptor` / `McpToolDescriptorBase`
Descriptor-Vertrag des Tool-Katalogs (Name, Description, `InputSchema`, `SecurityClass`, `RequiredScopes`, `MetaScope`, `CatalogVersion`-Beitrag). `FmSecuritiesAnalysisDescriptor` erbt die Schema-Übergabe von `McpToolDescriptorBase`; die Anforderung sieht keine Schema-Änderung vor (`latestPrice` ist kein Eingabeparameter).
