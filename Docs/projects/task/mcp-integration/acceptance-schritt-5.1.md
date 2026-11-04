# Abnahmeprüfung – Entwicklungsschritt 5 (EP-05)

## Ergebnis

**Status:** Abweichungen gefunden (1 geringfügige Abweichung, Kern-Anforderungen F-0501–F-0504 erfüllt)

## Feature-Abgleich

| Feature | Status | Nachweis |
|---|---|---|
| F-0501 Individuelle Scope-Profile (Server) | erfüllt | `McpAccessesController.SetScopesAsync` (`McpAccessesController.cs:133-143`) → `McpAccessService.SetScopesAsync` (`McpAccessService.cs:160-185`): Owner-Isolation (`OwnerUserId`-Filter Z. 163, Fremd-Id → `false`/404-Semantik), Individuell-Gate (Z. 168-171), F3-Validierung via `McpScopeMatrix.IsValidCombination` (Z. 518), Enum-Range-Check, `Err_Mcp_InvalidScope` durchgängig. **Lesen-Implikation nachgezogen** (Z. 529-536): Recht ≠ `Lesen` verlangt `Lesen`-Zeile auf demselben Datentyp — auch in `CreateAsync` (Z. 93). `ScopesChanged`-Delta-Audit (Z. 181-182, `BuildScopeDelta` Z. 434-445, Tupel-Keys mit Add/Remove/Filter-Change). Standardstufen zeilenlos: `McpAccess.Update` räumt `Scopes` (Domain `McpAccess.cs:173-176`), Ableitung laufzeitseitig via `McpLevelProfiles`/`McpScopeEvaluator.GetEffectiveScopesAsync` (Z. 34-41). Deckel laufzeitseitig plausibel: `CheckRecordAccessAsync` — Stufe-0-Owner/NotFound, AccountShare-Deckel deckelt Recht (Z. 158-162), Tupel + AND/OR-Filter (Z. 164-182). Tests: `SetScopes_InvalidTuple/_NonIndividualLevel/_WriteWithoutRead/_UnknownEnumValue/_ForeignOwner` neu und grün. |
| F-0502 Scope-Matrix-Editor (UI) | erfüllt | `McpRightsEditor.razor`: Matrix aus `Options.DataTypes` (18 Zeilen) × `Options.RightKinds` (7 Spalten, Z. 46-68); Aktivierbarkeit nur bei `AllowedRights`-Treffer, sonst „—" mit lokalisiertem Tooltip (`LockedReasonKey` Z. 359-379, 5 Gründe `Mcp_Matrix_Lock_*`). Stufe ≠ Individuell: Zellen disabled, Effektiv-Anzeige via `_effectiveCells` (Z. 283-285) + „Von Vorlage anpassen" (`StartIndividualEditAsync` VM Z. 163-204: PUT access mit Pending-Werten + Seed des Effektivprofils via PUT scopes). Implikationshilfe: Auto-Selektion `Lesen` + Kaskade beim Entfernen (Z. 287-309). Save ausschließlich via `IApiClient` (`SaveScopesAsync` → `_api.Mcp_SetScopesAsync`); `IConfirmationService` (Warning) bei Tupel-Erweiterung (VM Z. 220-231). Stufenwechsel weg von Individuell löst Downgrade-Bestätigung aus (`SaveAsync` Z. 305-312) und verwirft Zeilen serverseitig. Filter-CSVs beibehaltener Tupel werden verbatim übernommen (`BuildScopeRows` Z. 320-341). |
| F-0503 Filterverwaltung | erfüllt | Je persistierter Scope-Zeile (`EffectiveScopes` mit `Id != Guid.Empty`) die 5 Dimensionen aus `Options.FilterDimensions` (razor Z. 157-199): `AccountTypes` lokalisiert aus Enum, 4 Kategoriedimensionen aus `Options.*Categories` (Namen→Id-Auflösung, Z. 482-505); nicht mehr auflösbare gesetzte Ids werden muted nachgeführt und bleiben entfernbar. Save je Zeile via `Provider.SaveScopeFiltersAsync` → `IApiClient.Mcp_SetFiltersAsync` → `PUT …/filters/{scopeId}` → `SetFiltersAsync` (Service Z. 188-210) mit `FiltersChanged`-Delta je Dimension (`BuildFilterDelta` Z. 418-427). Klartext: `Mcp_Filters_Hint` (leer = unbeschränkt, UND über Dimensionen, ODER über Zeilen), `Mcp_Filters_Unclassified` (unklassifiziert nicht erfasst), `Mcp_Filters_Indirect` (indirekte Wirkung). Editor gesperrt bei ausstehenden Matrix-Änderungen (`_dirty` → `Mcp_Filters_PendingMatrix`). Datensatzscharfe Wirkung laufzeitseitig: `McpRecordScopeFilter`/`McpScopeEvaluator` (OR Zeilen, AND Dimensionen, Whitelist matcht Unklassifizierte nie). |
| F-0504 Genehmigungsmodus-Auswahl | erfüllt, mit Abweichung (s.u.) | Karte: `Mcp_Card_ApprovalMode` editierbar → `UpdateAsync` → `AccessUpdated`-Delta (Feld `ApprovalMode` in `BuildAccessDelta` Z. 407). Anzeige in Rechte-Section mit Klartext `Mcp_ApprovalModeDesc_*` + Kill-Switch-Relation `Mcp_Approval_KillSwitchHint` (razor Z. 19-29). Wizard: Radio PerAction/PreApproved ab Stufe Sachbearbeiter inkl. Kill-Switch-Hinweis (`McpAccessCreateWizard.razor:70-90`). Critical-Confirm bei Wechsel auf `PreApproved` mit Buchung/Löschung im Profil (`SaveAsync` Z. 317-327). Keine Ausführungswirkung — gemäß Abgrenzung (EP-08). |
| Querschnitt | erfüllt | resx: 24 neue Keys in `Pages.resx`/`Pages.de.resx`/`Pages.en.resx` — Key-Mengen identisch, alle verwendeten Keys (inkl. `Mcp_Matrix_Lock_*`, `Mcp_ApprovalModeDesc_*`, `EnumType_AccountType_*`) in de+en vorhanden. `IConfirmationService` über Basisklasse (`BaseViewModel.cs:142`). Kein Client-Bypass: Razor ruft nur ViewModel-Methoden, VM nur `IApiClient` → Controller → Service. |

## Abweichungen

- [ ] **F-0504 – PreApproved-Bestätigung prüft altes Profil (Edge-Case-Umgehung):**
  `McpAccessCardViewModel.SaveAsync` (`McpAccessCardViewModel.cs:317-327`) prüft die
  Critical-Bestätigung gegen `Access.EffectiveScopes` — den *geladenen* Zustand. Ein
  kombinierter Karten-Save, der zugleich die Stufe hochsetzt (z. B. Analyse → Buchhalter)
  und `ApprovalMode` auf `PreApproved` wechselt, aktiviert dauerhaft vorab genehmigte
  Buchung/Löschung **ohne** Critical-Bestätigung, weil das alte Profil noch keine
  `Book`/`Delete`-Tupel enthält. Geringe Schwere (Sonderfall zweier kombinierter
  Feldänderungen in einem Save; Einzelwechsel des Modus ist korrekt abgesichert),
  aber fachlich die höchstwertige Kombination. Fix-Option: Prüfung gegen das
  *resultierende* Profil (z. B. `McpLevelProfiles.GetDefaultScopes(neue Stufe)` bzw.
  persistierte Scopes bei Individuell).

## Hinweise

- `PUT …/scopes`, `PUT …/filters/{scopeId}`, `GET …/options` samt Owner-Isolation und
  Delta-Audit bestanden bereits aus EP-01/EP-03; der Schritt ergänzte serverseitig nur
  die Lesen-Implikation — konsistent mit `requirement.md`/`plan.md`.
- `McpScopeEvaluator.CheckRecordAccessAsync`: Reihenfolge der Stufenkommentare
  (Stage 3 Share-Deckel vor Stage 1 Tupel) ist nur Kommentar-Numerierung, Semantik korrekt.
- `Mcp_Filters_NeedsIndividual` („…auf die Stufe Individuell wechseln") erscheint auch bei
  einem Individuell-Zugang mit leerem Profil (z. B. nach leerem Matrix-Save oder
  fehlgeschlagenem Seeding) — dort leicht irreführend, kosmetisch.
- Multi-Zeilen-Editor (OR-Varianten je Tupel) bewusst nicht umgesetzt; Matrix-Save erhält
  bestehende Mehrfachzeilen verbatim — dokumentierte Abgrenzung des Schritts.
- Testlauf: `dotnet test FinanceManager.Tests --filter Mcp` → **142/142 bestanden**
  (laut `test-results.md` zuvor 137 + 5 neue Fälle). Format-Fix an
  `FmStatementDraftPrepareBookingTool.cs` ist reines Whitespace-Layout.
