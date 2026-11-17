# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Änderungen an bestehenden Klassen / Dateien

- [x] Methode `GetRibbonRegisterDefinition(IStringLocalizer)` in `SavingsPlanCardViewModel` (`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs:895`) — Tab-Liste auf `{ nav, manage, analysis, bookings, linked }` umgeordnet; `baseRegs`-Anhängung und Register-Kind unverändert (reiner Ein-Zeilen-Diff)
- [x] Methode `GetRibbonRegisterDefinition(IStringLocalizer)` in `BankAccountCardViewModel` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs:303–308`) — `Ribbon_Group_Linked`-Eintrag hinter `Ribbon_Group_Bookings` verschoben; Zielreihenfolge Navigation → Manage → Bookings → Linked
- [x] Methode `GetRibbonRegisterDefinition(IStringLocalizer)` in `PostingsCardViewModel` (`FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs:154`) — Tab-Liste auf `{ nav, actions, linked }` umgeordnet
- [x] `AGENT.md` — Abschnitt „Gruppierung des Ribbon-Menüs" um einen Satz ergänzt: vorhandene `Navigation`-Gruppe (Zurück-Aktion, `Ribbon_Group_Navigation`) steht stets an Position 0 vor den drei kategorisierten Gruppen

### Neue Tests und Testartefakte

- [x] Testmethode `GetRibbon_GroupOrder_PlacesLinkedLast` in `AccountDetailViewModelTests` (`FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs:213`) — assertiert exakte Titel-Sequenz `Ribbon_Group_Navigation` → `Ribbon_Group_Manage` → `Ribbon_Group_Bookings` → `Ribbon_Group_Linked` im `Actions`-Register und `Linked` als letzten Tab über alle Register; nutzt `CreateVm()` + `DummyLocalizer`; vor Umsetzung rot (in `test-results.md` dokumentiert)
- [x] Testmethode `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` in `PostingsCardViewModelReversalTests` (`FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs:247`) — Sequenz Navigation → Actions → Linked + `Linked` letzter Tab; vor Umsetzung rot
- [x] Testmethode `Ribbon_GroupOrder_PlacesLinkedLast` in `SavingsPlanCardLinkTests` (`FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs:333`) — Sequenz Navigation → Manage → Analysis → Bookings → Linked + `Linked` letzter Tab; vor Umsetzung rot
- [x] Testmethode `Groups_RenderInTabListOrder` in `RibbonTests` (`FinanceManager.Tests/Components/RibbonTests.cs:127`, bUnit) — drei Tabs mit gleichem `Sort` in absichtlich nicht-alphabetischer Reihenfolge; assertiert DOM-Reihenfolge der `.fm-ribbon-group-footer`-Elemente = Listenreihenfolge
- [x] Neue E2E-Klasse `RibbonGroupOrderPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs`) — drei `[Fact]`s (`SavingsPlanCard_`, `BankAccountCard_`, `PostingsCard_LinkedGroup_IsLastRibbonGroup`); Seeding via `POST /api/savings-plans`, `AccountsApiSeedHelper.CreateAccountAsync` bzw. `CreateAccountWithBalanceAsync` + `GET /api/postings/account/{id}`; Assertion: erste `.fm-ribbon-group` enthält `#Back`, letzte enthält `#OpenAttachments`

### Ausgeführte Prüfschritte (Umsetzungsreihenfolge Schritte 6–8)

- [x] Unit-/Komponententestlauf — `post-change-unit-tests.trx`: 2152/2152 bestanden (Baseline 2148 + 4 neue); Integrationstests 165/165 (`post-change-integration-tests.trx`)
- [x] E2E-Testlauf (gefiltert `FullyQualifiedName~RibbonGroupOrder`) — `post-change-e2e-ribbonorder.trx`: 3/3 bestanden
- [x] Reale End-to-End-Abnahme — in `test-results.md` dokumentiert: reale App (gebaute `FinanceManager.Web.dll` gegen echte SQLite-DB) im echten Chromium geöffnet, alle drei Detailseiten mit Screenshot-Nachweis (`acceptance-savingsplan-card.png`, `acceptance-bankaccount-card.png`, `acceptance-postings-card.png`) als „Bestanden" ausgewertet

### Plan-Vorgaben ohne Umsetzungsgegenstand (verifiziert eingehalten)

- [x] Scope-Ausschluss „keine Inhalts-/Label-/Icon-/`MobileShortcut`-Änderungen" — `git diff` zeigt in allen drei ViewModels ausschließlich die Umordnung der Listeneinträge
- [x] Scope-Ausschluss „keine resx-/Key-Änderungen" — keine resx-Datei im Diff; Tests assertieren die unveränderten Key-Namen
- [x] Scope-Ausschluss „kein Refactoring / keine konformen ViewModels angefasst" — keine weiteren ViewModel-Änderungen im Diff; kein generischer Sortiermechanismus eingeführt; `Sort` bleibt überall `0`
- [x] Keine neuen Klassen, Migrationen, Validierungsregeln oder Konfigurationsänderungen — plangemäß keine vorhanden

## Hinweise

- Die Tasks-Datei `Docs/features/task-issue-498-dd574583f8fe40a79651dc73562ca20f-ribbon-gruppierung-verknuepfte-tasks.md` wurde aktualisiert: alle 12 Aufgaben „Erledigt" mit Testnachweis.
- Die reale End-to-End-Abnahme (Plan-Schritt 8) wurde nicht als manuelle Klickstrecke, sondern über die Playwright-Fixture an der real laufenden Anwendung mit anschließender visueller Screenshot-/HTML-Snapshot-Auswertung durchgeführt und in `test-results.md` als „Bestanden" dokumentiert. Falls der Orchestrator eine hands-on manuelle Abnahme durch den Anwender für erforderlich hält, wäre das der einzige nachzuholende Punkt — die geforderte Wirkung (realer Browser, reale App, sichtbare Gruppenreihenfolge) ist jedoch mit Artefakten belegt.
- Die beiden preexisting E2E-Fehlschläge (`ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi`, `SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle`) sind in `inventory/tests.md` change-unabhängig nachgewiesen und kein Umsetzungsdefizit.
- `git status` zeigt neben den geplanten Änderungen keine unerwarteten Code-Diffs; untracked sind nur die E2E-Testklasse, `test-results.md` und die TRX/PNG-Nachweise.
