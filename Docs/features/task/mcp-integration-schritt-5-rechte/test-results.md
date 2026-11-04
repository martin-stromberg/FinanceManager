# Testergebnisse – EP-05 Individuelle Rechte & Filter

## Build
```
dotnet build FinanceManager.sln  →  erfolgreich, 0 Fehler
```
(XML-Dokumentation vollständig — CS1591 ist Warning-as-Error; keine Stubs.)

## Unit-Tests (`FinanceManager.Tests`)
```
dotnet test FinanceManager.Tests                          → 1659 erfolgreich, 0 fehlgeschlagen (38 s)
dotnet test … --filter "FullyQualifiedName~Mcp"           →  137 erfolgreich (2,6 s)
```

## Neue/angepasste Tests — `Mcp/McpAccessServiceTests.cs` (+5 Fälle, 1 angepasst)

- `SetScopes_InvalidTuple_ThrowsInvalidScope` — `(Postings, Create)` außerhalb der
  F3-Matrix → `Err_Mcp_InvalidScope` (Deckel: nur Einschränkung, nie Erweiterung).
- `SetScopes_NonIndividualLevel_ThrowsInvalidScope` — Scope-Set auf Stufe Analyse
  → `Err_Mcp_InvalidScope` (Standardstufen bleiben zeilenlos).
- `SetScopes_WriteWithoutRead_ThrowsInvalidScope` — `(StatementDrafts, Book)` ohne
  Read-Zeile → `Err_Mcp_InvalidScope`; Read auf *anderem* Datentyp erfüllt die
  Implikation nicht (Implikationsregel ui-konzept §4.3.2, nachgezogen).
- `SetScopes_UnknownEnumValue_ThrowsInvalidScope` — DataType=99 →
  `Err_Mcp_InvalidScope` (Enum-Range-Check vor Matrix).
- `SetScopes_ForeignOwner_ReturnsFalse` — fremder Owner → `false` (404-Semantik).
- `SetScopes_IndividualLevel_AppliesAndFilters` (angepasst) — um die durch die
  Implikationsregel nun erforderliche `(StatementDrafts, Read)`-Zeile ergänzt;
  Filter-Set per `PUT …/filters` weiter verifiziert.

## Bestand weiterhin grün
- `SetScopes_WritesScopeDelta_AddedChangedRemoved` — Audit-Delta
  (hinzugefügte/entfernte/geänderte Tupel, Hash-Kette intakt).
- `SetFilters_WritesFilterDelta_PerDimension` — Filter-Delta je Dimension.
- `Update_WritesFieldDelta_IncludingScopeRemovalOnLevelSwitch` — Stufenwechsel
  verwirft individuelle Zeilen inkl. Delta-Eintrag.
- `AccessEvents_DeltaContainsNoSecrets` — kein Token-Material im Audit.

## Statische Gates (pre-commit/pre-push)
```
translation-check.py --all            → OK (940 Keys, 114 resx-Pakete konsistent)
razor-l10n-check.py --all             → OK (87 .razor, keine harten UI-Strings)
razor-usage-check.py --all --strict   → OK (keine Orphans; McpRightsEditor referenziert)
no-notimplemented-check.py --strict   → OK
enum-coverage-check.py --all --strict → OK
dotnet format --verify (severity error)→ OK
```
Hinweis: `dotnet format --verify` meldete zunächst einen **vorbestehenden**
Whitespace-Verstoß in `FmStatementDraftPrepareBookingTool.cs` (Schritt-4-Commit
098dfb48) — als One-Item-per-Line-Fix mitgefixt, da das Gate sonst den Push blockiert.
