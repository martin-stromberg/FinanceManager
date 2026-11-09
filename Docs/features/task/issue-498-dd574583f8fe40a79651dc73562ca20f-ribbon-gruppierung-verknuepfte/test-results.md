# Test-Ergebnisse

Zeitpunkt: Iteration 2 — 2026-11-09, Verifikationslauf des `/run-tests`-Schritts nach Test-Umbenennungen (vollständiger Suitenlauf: Iteration 1, 2026-11-09 ca. 18:55–19:25 Uhr +0100)
Branch: `task/issue-498-dd574583f8fe40a79651dc73562ca20f-ribbon-gruppierung-verknuepfte`
Runtime: `dotnet` 10.0.403, Microsoft.Testing.Platform, xUnit v3, bUnit, Playwright (Chromium)
Vorausgehender Build (Iteration 2): `dotnet build FinanceManager.sln -c Debug` → **0 Fehler** (10 Warnungen, NU1510-Pakethinweise), Laufzeit 6,3 s — sequenziell vor dem Testlauf, keine parallelen dotnet-Prozesse

**Delta seit Iteration 1:** Ausschließlich drei Testmethoden-Umbenennungen zu `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` in `AccountDetailViewModelTests.cs:211`, `SavingsPlanCardLinkTests.cs:333` und `PostingsCardViewModelReversalTests.cs:247` (letztere trug den Namen bereits). Produktivcode ist seit Iteration 1 unverändert.

## Ergebnis

**Status:** Fehler vorhanden

Einordnung: Der einzige Fehlschlag des gesamten Nachweisstands ist ein **baseline-dokumentierter, nicht change-bezogener („preexisting") E2E-Fehler** ohne fachlichen Bezug zur Ribbon-Gruppenreihenfolge: `ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` schlägt identisch bereits auf `staging` fehl (Baseline-Nachweis: `inventory/test-results/baseline-e2e-tests.trx`, `outcome="Failed"`, identisches Fehlerbild `net::ERR_ABORTED` beim Logout; zusätzlich dokumentiert in `inventory/tests.md`). Alle für diesen Change geplanten Tests — einschließlich sämtlicher Pflicht-E2E-Szenarien — sind bestanden. In Iteration 2 wurde der geänderte Anteil (drei umbenannte Reihenfolge-Tests + bUnit-Reihenfolgetest) per Build + gezieltem Testlauf erneut verifiziert: **4/4 bestanden**, keine alten Testnamen mehr vorhanden.

## Fehlgeschlagene Tests

### FinanceManager.Tests.E2E (`test-results/e2e-tests.trx`, Volllauf Iteration 1)

- **ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi** — `Microsoft.Playwright.PlaywrightException: net::ERR_ABORTED at https://127.0.0.1:65306/login` (Navigation zur Login-Seite beim Logout abgebrochen; **preexisting/nicht change-bezogen** — in der Staging-Baseline `inventory/test-results/baseline-e2e-tests.trx` mit identischem Fehlerbild fehlgeschlagen)

Anmerkung: Der zweite in der Baseline dokumentierte preexisting Fehlschlag `SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle` ist im Volllauf Iteration 1 **bestanden** (flaky, nicht change-bezogen).

## E2E-Abdeckung

Geplante Szenarien aus `plan.md` Abschnitt „E2E-Tests (primärer Funktionsnachweis)" bzw. `plan-check.md` Abschnitt „E2E-Abdeckung":

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Bankkonto-Karte `/card/accounts/{id}`: `Linked` sichtbar letzte Ribbon-Gruppe, `Navigation` erste | `RibbonGroupOrderPlaywrightTests.BankAccountCard_LinkedGroup_IsLastRibbonGroup` | Bestanden |
| Sparplan-Karte `/card/savings-plans/{id}`: `Linked` sichtbar letzte Ribbon-Gruppe | `RibbonGroupOrderPlaywrightTests.SavingsPlanCard_LinkedGroup_IsLastRibbonGroup` | Bestanden |
| Posten-Karte `/card/postings/{id}`: `Linked` sichtbar letzte Ribbon-Gruppe | `RibbonGroupOrderPlaywrightTests.PostingsCard_LinkedGroup_IsLastRibbonGroup` | Bestanden |
| Neuanlage-Karte (Randzustand, Akzeptanzszenario 4) | Kein dedizierter E2E-Test — im Plan begründet über Unit-Ebene: `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (×3, siehe unten); Tab-Liste wird in allen drei ViewModels zustandsunabhängig aufgebaut | Bestanden (Unit-Ebene, plangemäß; in Iteration 2 erneut verifiziert) |
| Reale End-to-End-Abnahme (Projektregel) | Siehe Abschnitt „Reale End-to-End-Abnahme" | Bestanden |

Nachweis der drei Pflicht-E2E-`[Fact]`s der Klasse `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs`:

- `test-results/e2e-tests.trx` (vollständiger E2E-Lauf Iteration 1): alle drei `outcome="Passed"` — verifiziert per TRX-Eintrag (Gesamtlauf: 136 Tests, 135 bestanden, 1 preexisting-Fehlschlag).
- `inventory/test-results/post-change-e2e-ribbonorder.trx` (geführtes Post-Change-Filterlauf der drei Facts): 3/3 `outcome="Passed"`.

**Transparenzhinweis Iteration 2:** An den drei E2E-Tests und am gesamten Produktivcode hat sich seit dem Volllauf Iteration 1 nichts geändert; die einzige Änderung betraf Methodennamen in der Unit-Test-Assembly `FinanceManager.Tests` (andere Assembly, kein Einfluss auf `FinanceManager.Tests.E2E`). Ein erneuter ~19-minütiger E2E-Volllauf wurde daher plangemäß unterlassen — die TRX-Einträge wurden in diesem Schritt erneut gegen die Dateien verifiziert.

## Reale End-to-End-Abnahme

Erforderlich (UI betroffen). Die Abnahme wurde mit der Playwright-Fixture an der **real laufenden Anwendung** durchgeführt: Die Fixture startet die tatsächlich gebaute `FinanceManager.Web.dll` gegen eine echte SQLite-Datenbank; ein reales Chromium-Fenster öffnete die drei Detailseiten (kein Mock, keine Simulation). Die dabei erzeugte Screenshot-Evidenz (`PLAYWRIGHT_ARTIFACTS=1`, abgelegt unter `inventory/test-results/`) wurde in Iteration 1 visuell verifiziert und in **Iteration 2 erneut bewertet** — auf allen drei Screenshots ist die Gruppe „Verknüpfte Informationen" sichtbar die letzte Ribbon-Gruppe und die Navigationsgruppe mit „Zurück" die erste; die sichtbaren Sequenzen entsprechen exakt den Zielreihenfolgen aus `plan.md`. Da Produktivcode seitdem unverändert ist, bleibt die Evidenz gültig.

| Szenario | Durchgeführt über | Ergebnis |
|----------|-------------------|----------|
| Happy Path Sparplan-Karte `/card/savings-plans/{id}` | Real laufende App + echte SQLite-DB + reales Chromium; Screenshot [acceptance-savingsplan-card.png](inventory/test-results/acceptance-savingsplan-card.png) | Bestanden — sichtbare Reihenfolge: Navigieren → Verwalten → Analyse → Buchungen → **Verknüpfte Informationen** (letzte Gruppe) |
| Happy Path Bankkonto-Karte `/card/accounts/{id}` | Real laufende App + echte SQLite-DB + reales Chromium; Screenshot [acceptance-bankaccount-card.png](inventory/test-results/acceptance-bankaccount-card.png) | Bestanden — sichtbare Reihenfolge: Navigieren → Verwalten → Buchungen → **Verknüpfte Informationen** (letzte Gruppe) |
| Happy Path Posten-Karte `/card/postings/{id}` | Real laufende App + echte SQLite-DB + reales Chromium; Screenshot [acceptance-postings-card.png](inventory/test-results/acceptance-postings-card.png) | Bestanden — sichtbare Reihenfolge: Navigieren → Aktionen → **Verknüpfte Informationen** (letzte Gruppe) |

## Zusammenfassung

- Gesamt: 2453 (Vollläufe Iteration 1) + 4 (gezielter Verifikationslauf Iteration 2)
- Bestanden: 2452 + 4
- Fehlgeschlagen: 1 (preexisting, nicht change-bezogen, Baseline-nachgewiesen)
- Übersprungen: 0

Aufschlüsselung nach Suite (alle Läufe sequenziell, jeweils gegen einen Solution-Build mit 0 Fehlern):

| Suite | Befehl (Kern) | Gesamt | Bestanden | Fehlgeschlagen | Nachweis |
|-------|----------------|--------|-----------|----------------|----------|
| Unit + bUnit (Volllauf, Iteration 1) | `dotnet test FinanceManager.Tests -- --filter-not-trait "Category=OsInterface" --coverage --coverage-output-format cobertura` | 2152 | 2152 | 0 | [unit-tests.trx](test-results/unit-tests.trx), [unit-coverage.cobertura.xml](test-results/unit-coverage.cobertura.xml) |
| Integration (Volllauf, Iteration 1) | `dotnet test FinanceManager.Tests.Integration -- --coverage` | 165 | 165 | 0 | [integration-tests.trx](test-results/integration-tests.trx) |
| E2E vollständig (Iteration 1) | `dotnet test FinanceManager.Tests.E2E` | 136 | 135 | 1 (preexisting) | [e2e-tests.trx](test-results/e2e-tests.trx) |
| Unit + bUnit gezielt (**Iteration 2**, nach Umbenennungen) | `dotnet test FinanceManager.Tests --no-build -- --filter-method "*PlacesLinkedLast" --filter-method "*RenderInTabListOrder"` | 4 | 4 | 0 | [iteration2-renamed-tests.trx](test-results/iteration2-renamed-tests.trx) |

Iteration-2-Verifikation im Detail:

- **3× `GetRibbonRegisters_GroupOrder_PlacesLinkedLast`** bestanden: `AccountDetailViewModelTests`, `SavingsPlanCardLinkTests`, `PostingsCardViewModelReversalTests` (je `outcome="Passed"` im TRX).
- **`RibbonTests.Groups_RenderInTabListOrder`** (bUnit) bestanden.
- **Keine alten Testnamen mehr vorhanden:** Quellcode-Grep über `FinanceManager.Tests` findet weder `GetRibbon_GroupOrder_PlacesLinkedLast` noch `Ribbon_GroupOrder_PlacesLinkedLast`; das Iteration-2-TRX enthält ebenfalls keinen alten Namen.

Neue Tests dieses Changes (alle bestanden): `AccountDetailViewModelTests.GetRibbonRegisters_GroupOrder_PlacesLinkedLast`, `PostingsCardViewModelReversalTests.GetRibbonRegisters_GroupOrder_PlacesLinkedLast`, `SavingsPlanCardLinkTests.GetRibbonRegisters_GroupOrder_PlacesLinkedLast`, `RibbonTests.Groups_RenderInTabListOrder` (bUnit), `RibbonGroupOrderPlaywrightTests` (3 `[Fact]`s). Der Rot-Nachweis der drei Reihenfolge-Unit-Tests gegen den Ausgangszustand wurde bereits bei der Umsetzung erbracht (alle drei vor der Korrektur rot, danach grün).

## Testabdeckung

**Abdeckung:** 18,2 % Zeilenabdeckung (Cobertura, Unit-Suite Iteration 1: 38.242 / 209.623 Zeilen, Branch-Rate 33,1 %). Die Integration-Suite lieferte ein Coverage-Binärartefakt ([*.coverage](test-results/0223655f-36be-452c-8737-10b2975bb0d0.coverage)); die niedrige Gesamtheit der Unit-Abdeckung ist der bekannte Projektzustand — große Teile von `FinanceManager.Infrastructure`/`FinanceManager.Shared` werden primär über die Integrations-Suite ausgeübt. Keine change-bezogene Abdeckungslücke: Die drei geänderten Zeilenblöcke (Umordnung der `UiRibbonTab`-Listen) sind überdeckt (`hits ≥ 1` auf allen geänderten Zeilen). Die Umbenennungen in Iteration 2 ändern keine ausführbaren Zeilen — die Coverage bleibt gültig.

Abdeckung der geänderten Dateien (per Datei aggregiert über alle Klassen):

| Datei | Abdeckung |
|-------|-----------|
| `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` | 49,0 % |
| `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` | 52,3 % |
| `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` | 62,9 % |

## Fehlende Tests

Quelle: `Coverage-Daten` (unit-coverage.cobertura.xml)

Projektweit weisen 427 von 960 instrumentierten Quelldateien 0 % Zeilenabdeckung in der Unit-Suite auf — Verteilung: `FinanceManager.Infrastructure` 227 (darunter sämtliche generierten EF-Migrationen), `FinanceManager.Web` 129 (u. a. `.razor`-Seiten ohne bUnit-Test), `FinanceManager.Shared` 66 (v. a. DTOs), `FinanceManager.Application` 3, `FinanceManager.Domain` 2. Dies ist ein **preexisting, change-unabhängiger** Projektzustand (Baseline `baseline-unit-tests.trx` ohne Befundabweichung); große Teile dieser Dateien werden durch die Integrations- und E2E-Suiten ausgeübt.

- Keine change-bezogene Lücke: Der Change fügt **keine neue Quelldatei** hinzu; alle drei geänderten ViewModel-Dateien haben Coverage (49–63 %) und die geänderten Zeilen sind direkt überdeckt (drei dedizierte Reihenfolge-Unit-Tests + bUnit-Test + drei E2E-Tests).
