# Plan-Review – Schritt 1: Back-Navigation

**Status:** Vollständig umgesetzt

## Offene Aufgaben

Keine. Die ursprünglich gemeldeten Testlücken wurden in derselben Iteration geschlossen:

- [x] Regressions-Test `card,{id}` → `card/{Kind}/{id}` (Komma-Payload) — ergänzt als `StatementDraftEntryCard_Back_NavigatesToParentDraftCard`.
- [x] Regressions-Test „leerer Payload → `BuildListPath()`" — ergänzt als `ContactCard_Back_WithoutPayload_NavigatesToListRoute`.
- [x] Test zur genesteten `?back=`-Kette bei `OpenCategory` — ergänzt als `SavingsPlanCard_OpenCategory_NestsExistingBackParameter`.

## Prüfbericht

### A) `CardPage.razor` — `case "Back"`: Präfix-Whitelist — umgesetzt

`CardPage.razor` `case "Back"` enthält die geplante Prüfung an der geforderten Position: nach Direkt-URL und `?back=`, vor GUID-/Komma-/Leer-Fällen. Präfixe `card/`, `list/`, `reports/` (OrdinalIgnoreCase), `:`-Ausschluss plus `IsSameOriginUri`-Gegenprüfung; `NavigateTo(payload)` erhält den Query-Teil. Betroffene Aufrufer verifiziert: `card/setup` (4 MCP-ViewModels), `card/budget/purposes/{id}` und `card/budget/categories/{id}` (BudgetRuleCardViewModel, BudgetPurposeCardViewModel), `list/budget/purposes` (Fallback in BudgetRuleCardViewModel).

### B) `CardPage.razor` — `case "OpenCategory"` — umgesetzt

Alle Planentscheidungen umgesetzt: `?back=` mit `BuildCardPath(Kind, SubKind, Id, Back)` inkl. Nistung, `Id == Guid.Empty`-Guard, `?`/`&`-Separator, `back=`-Dubletten-Guard. Zusätzlich sendet `BudgetPurposeCardViewModel` seine Kategorie-Verknüpfung jetzt über `OpenCategory` statt den `Back`-Kanal zu missbrauchen — die Kette Zweckkarte → Kategoriekarte → Zurück ist damit geschlossen.

### C) Tests — `CardPageBackNavigationTests.cs` — umgesetzt

11 bUnit-Tests, alle grün: `card/setup`-Payload, `?back=`-Präzedenz, Regelkarte → Purpose-Karte, `account/{guid}`-Legacy-Regression, `OpenCategory` mit/ohne `?back=`-Nistung und `Guid.Empty`-Guard, Komma-Payload `card,{draftId}`, leerer Payload → `list/{kind}`, Purpose-Karte `OpenCategory`, kontextlose Regel → `list/budget/purposes`.

### Akzeptanzszenarien des Plans — funktional abgedeckt

Alle sechs Szenarien implementiert und testabgesichert; der Rückweg der Kategoriekarte läuft über den Bestandspfad (`Back` ohne Payload → `?back=` greift).

### Scope-Ausschlüsse / Projektregeln — eingehalten

Keine resx-/EF-/Enum-Änderungen; XML-Doku vollständig; keine root-absoluten URLs.
