# Bestandsaufnahme – Schritt 1 (Back-Navigation)

Verweis: Vollständige Projekt-Bestandsaufnahme in
`Docs/projects/task/issue-614-29b2b395092048f2a48ecae3be9fff77-bugfix-projekt/inventory.md`
(Abschnitte 5 „Karten-/Back-Navigation", 6 „Verknüpfte Karten", 11 „Test-Infrastruktur",
13 „Ribbon-Systematik"). Schritt-relevante Kernaussagen:

- `CardPage.razor` `case "Back"` (Z. 133–198), Auswertungsreihenfolge:
  1. Direkt-URL-Payload (`/` oder `http`, `IsSameOriginUri`) → `NavigateTo(payload)`
  2. `?back=` (`IsSameOriginUri(Back)`) → `NavigateTo(Back!)`
  3. GUID-Payload + Statement-Draft-Sonderfall → `card/statement-drafts/{guid}`
  4. leerer Payload → `BuildListPath()`
  5. Komma-Split: 1 Teil → `list/{Kind}/{parts[0]}` (Legacy-Subtyp); `card,{id}` →
     `card/{Kind}/{arg}`; `list,{sub}` → `list/{Kind}/{arg}`; sonst `BuildListPath()`
- Neue Whitelist-Prüfung gehört zwischen 2 und 3 (Payloads `card/`, `list/`, `reports/`,
  kein `:`, kein `//`-Start) — sonst greift bei `InitBack`-Payloads mit eigenem `?back=`
  (Navigationskette) nicht die Äußere `?back=`, was OK ist; Query-Teil bleibt erhalten.
- App-relative Back-Payloads ohne `/` (komplett verifiziert):
  `McpAccessCardViewModel.cs:740`, `McpSecurityOverviewViewModel.cs:150`,
  `McpAuditViewModel.cs:331`, `McpApprovalCenterViewModel.cs:403` (je `"card/setup"`),
  `BudgetRuleCardViewModel.cs:340/345/377/381` (`InitBack`/`card/budget/purposes/{id}`/
  `card/budget/categories/{id}`), `BudgetPurposeCardViewModel.cs:481`
  (`card/budget/categories/{id}`).
- Regressionsrisiko beachten: `PostingsCardViewModel.cs:124–134` sendet einteilige Payloads
  `account/{guid}`/`contact/{guid}`/`savings-plan/{guid}`/`security/{guid}` → müssen weiter
  `list/postings/{payload}` ergeben (kein Whitelist-Präfix).
- `OpenCategory` in `CardPage` (Z. 257–259) navigiert Payload unverändert;
  `SavingsPlanCardViewModel.cs:860` einziger Sender, Payload `card/savings-plans/categories/{id}`.
- `?back=`-Muster: `OpenBankContact` (Z. 233–243) hängt `?back={BuildCardPath(Kind,SubKind,Id,Back)}`
  an — `BuildCardPath` escapet via `Uri.EscapeDataString` und nistet vorhandenes `Back`.
- `IsSameOriginUri`/`IsSafeNavigationCandidate`: `NavigationManagerExtensions.cs:186–247`.
- Tests: `FinanceManager.Tests/Components/CardPageTests.cs` (bUnit + `Mock<IApiClient>` +
  `FakeNavigationManager`-Mechanik von bUnit — `Services.GetService<FakeNavigationManager>()`
  bzw. `BunitContext.Services.GetRequiredService<NavigationManager>()`); VM-Tests unter
  `FinanceManager.Tests/ViewModels/`; Testkarte „contacts" bewährt.
- Einschränkungen: `NavigateTo` relativ (kein `/`-Präfix — `RazorRootAbsolutePathTests`);
  XML-Doku-Pflicht (CS1591 als Fehler); keine neuen resx nötig.
