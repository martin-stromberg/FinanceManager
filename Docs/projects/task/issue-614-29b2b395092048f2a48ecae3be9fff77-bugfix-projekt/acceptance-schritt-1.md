# Abnahme Schritt 1 — Back-Navigation (#600, #602, #606, #607, #608)

Datum: 2026-11-28
Schritt-Branch: `task/issue-614-29b2b395092048f2a48ecae3be9fff77-bugfix-projekt-schritt-1-back-navigation`

## Umgesetzte Änderungen

- `CardPage.razor`: Zentraler Back-Handler erkennt app-relative Karten-/Listen-/Reports-Payloads
  (`card/`, `list/`, `reports/`, OrdinalIgnoreCase, kein `:`, `IsSameOriginUri`) und navigiert
  direkt — Position nach absoluten URLs und `?back=`-Check, vor GUID-/Komma-/Legacy-Fallbacks.
- `CardPage.razor` (`OpenCategory`): hängt `?back=` mit `BuildCardPath(Kind, SubKind, Id, Back)`
  an — nur bei `Id != Guid.Empty` und ohne vorhandenen `back=`-Schlüssel.
- `BudgetPurposeCardViewModel`: Linked-Ribbon „Kategorie" löst `OpenCategory` aus (statt Back-Payload).
- `BudgetRuleCardViewModel`: Fallback `list/budget/purposes` bei regel- ohne Zweck-Kontext.

## Reviews

- `plan-check.md`: bestanden (3 Testlücken gemeldet → geschlossen)
- `review.md` / `review-code.md`: Härtung OrdinalIgnoreCase + `IsSameOriginUri`, Double-`back=`-Guard
- `review-usability.md`: 3 Low-Findings → behoben (Purpose-Kategorie-Link, ungültiger `list/budget/rules`-Fallback)

## Tests

- Unit/bUnit: `FinanceManager.Tests/Components/CardPageBackNavigationTests.cs` — 11 Tests, grün
- E2E (Playwright): `FinanceManager.Tests.E2E/Tests/Navigation/CardBackNavigationPlaywrightTests.cs` — 3 Tests, grün (~18 s)

## E2E-Abnahme-Screenshots (sichtgeprüft)

| Datei | Inhalt |
|-------|--------|
| `s1-savings-plan-category-card.png` | Kategorie-Karte „E2E Back Nav Category" — nach OpenCategory vom Sparplan, `?back=` am URL |
| `s1-savings-plan-card-returned.png` | Sparplan-Karte „E2E Back Nav Plan" — nach Zurück-Klick auf der Kategorie-Karte |
| `s1-budget-rule-back-purpose-card.png` | Verwendungszweck-Karte „E2E Back Nav Purpose" — nach Zurück-Klick auf der Regel-Karte |
| `s1-mcp-audit-back-setup-card.png` | Setup-Karte „Einstellungen" — nach Zurück-Klick auf der MCP-Audit-Karte (`card/setup`-Payload) |

Ablage: SCM-attachmentsPath `138706f7-…/screenshots/`.

## Bekannte Verhaltensänderung

Auf der Sparplan-Kategorie-Karte kehren `Saved`/`Deleted` nun via `?back=` zur Ursprungskarte
zurück (konsistent mit `?back=`-Semantik; dokumentiert, PO/QA-Entscheid).

## Merge

`git merge --no-ff` auf Basisbranch — Merge-Commit mit `Fixes #600, #602, #606, #607, #608`.
