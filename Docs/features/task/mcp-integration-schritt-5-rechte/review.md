# Review – EP-05 Individuelle Rechte & Filter

## Lieferumfang vs. AK

| AK (Issue #483 / Backlog F-0501…F-0504) | Status | Nachweis |
|---|---|---|
| F-0501: `PUT …/scopes` — Stufe Individuell mit expliziten Scope-Tupeln | ✅ | `McpAccessService.SetScopesAsync` (Individuell-Gate); UI-Pfad `StartIndividualEditAsync` (Update→Seed) + `SaveScopesAsync`; `IApiClient.Mcp_SetScopesAsync` |
| F-0501: Validierung gegen F3-Matrix + Deckel (`Err_Mcp_InvalidScope`, nur Einschränkung) | ✅ | `ValidateScopes`: `McpScopeMatrix.IsValidCombination` je Tupel + Enum-Range-Check; kein erweiternder Scope-Typ existiert (berechtigungsmodell §4.5) — der AccountShare-/Stufe-0-Deckel greift laufzeitseitig datensatzweise (`McpScopeEvaluator`), nicht profilseitig |
| F-0501: Audit `ScopesChanged` mit Delta | ✅ | bestand bereits (`BuildScopeDelta`: Tupel-Keys, Add/Remove/Filter-Change); Test `SetScopes_WritesScopeDelta_AddedChangedRemoved` |
| F-0501: Tests ungültige Tupel / Deckel / Delta | ✅ | Neu: `SetScopes_InvalidTuple`, `SetScopes_NonIndividualLevel`, `SetScopes_WriteWithoutRead`, `SetScopes_UnknownEnumValue`, `SetScopes_ForeignOwner`; Delta-Tests bestanden weiter |
| F-0502: Matrix 18×7, Zellen nur wo F3 erlaubt, „—" gesperrt mit Begründung | ✅ | `McpRightsEditor.razor`: Zeilen aus `Options.DataTypes` (18), Spalten `Options.RightKinds` (7); `AllowedRights` steuert Aktivierbarkeit; gesperrte Zellen mit lokalisiertem Tooltip (`Mcp_Matrix_Lock_*`, 4 Spezialfälle + generisch) |
| F-0502: Stufe ≠ Individuell nur lesend („Profil anzeigen") | ✅ | Checkboxen disabled, Effektiv-Anzeige der Stufen-Profil-Tupel; „Von Vorlage anpassen"-Button + ReadOnly-Hinweis |
| F-0502: Wechsel zu Individuell → Bearbeitung → PUT scopes | ✅ | `StartIndividualEditAsync` (Update mit pending-Werten + Seed des Effektivprofils) → Edit → `SaveScopesAsync` → `Mcp_SetScopesAsync` |
| F-0502: `IConfirmationService` bei Erweiterung (nie mehr als Stufe/Besitzer) | ✅ | Tuple-Diff in `SaveScopesAsync` → Warning-Confirm `Confirmation_McpScopeExpand_*`; serverseitige Obergrenze bleibt F3+Owner |
| F-0502: Implikationshilfe „ohne Lesen" | ✅ | UI: Auto-Selektion `Lesen` + Kaskade beim Entfernen; API: `Err_Mcp_InvalidScope` bei non-Read ohne Read-Zeile (Härtung nachgezogen) |
| F-0502: Stufenwechsel verwirft Anpassungen (Bestätigung) | ✅ | Stufe ≠ Individuell ist numerisch niedriger → bestehende `Confirmation_McpLevelDowngrade`-Bestätigung greift; Server verwirft Zeilen (`Update`/`Scopes.Clear`) |
| F-0502: Deckel-Hinweistext | ✅ | `Mcp_Rights_CapHint` (bestehend) + `Mcp_Matrix_ReadImplication` unter der Matrix |
| F-0503: 5 Filterdimensionen je Scope-Zeile | ✅ | `Options.FilterDimensions` (AccountTypes + 4 Kategorien); Checkbox-Listen je persistierter Zeile |
| F-0503: Auflösungshilfe Namen→Id via IApiClient-Bestandsdaten | ✅ | `Options.*Categories` (Owner-Listen aus `GET …/options` via `IApiClient`); unbekannte Ids muted nachgeführt |
| F-0503: PUT filters + Audit `FiltersChanged` | ✅ | `SaveScopeFiltersAsync` → `Mcp_SetFiltersAsync`; Delta-Test bestand |
| F-0503: OR/AND-Klartext + unklassifiziert + indirekte Wirkung | ✅ | `Mcp_Filters_Hint`/`_Unclassified`/`_Indirect` (de/en) |
| F-0503: leere Liste = unbeschränkt | ✅ | `CsvOrNull` → `null`-CSV; Hinweistext |
| F-0504: Genehmigungsmodus pro Zugang, persistiert + Audit | ✅ | Karten-Feld `Mcp_Card_ApprovalMode` (Enum-Lookup) → UpdateAsync → `AccessUpdated`-Delta (`ApprovalMode` Feld); Anzeige in Rechte-Section |
| F-0504: Kill-Switch-Relation dokumentiert/anzeigen | ✅ | `Mcp_Approval_KillSwitchHint` in Rechte-Section + Wizard |
| F-0504: Critical-Confirm für PreApproved-Buchung/Löschung | ✅ | `SaveAsync`: Wechsel auf `PreApproved` bei Profil mit `Book`/`Delete` → `Confirmation_McpPreApproved_*` (Critical) |
| resx de/en für alle neuen Strings | ✅ | 24 Keys × {neutral,de,en}; translation-check OK |
| Nur `IApiClient` in der UI; XML-Docs; Err_Mcp_* | ✅ | alle neuen VM-Methoden über `_api.*`; Publics dokumentiert |
| Build + Tests grün | ✅ | siehe test-results.md |

## Bewusste Einschränkungen
- Kein Multi-Zeilen-Editor je Tupel (OR-Varianten nur über API/Create erzeugbar);
  Matrix-Save erhält sie verbatim — dokumentierte Grenze des 1-Zelle-Modells.
- `McpAccessOptions` ohne Options-Response → Fallback: alte flache Effektiv-Tabelle.
- Genehmigungsmodus hat weiterhin keine Ausführungswirkung (Approval-Queue EP-08).

## Verifikation
`dotnet build FinanceManager.sln`: 0 Fehler. `dotnet test FinanceManager.Tests`:
1659/1659 (MCP: 137). Statische Gates: translation-check, razor-l10n, razor-usage,
no-notimplemented, enum-coverage (strict), `dotnet format --verify` — alle OK.
