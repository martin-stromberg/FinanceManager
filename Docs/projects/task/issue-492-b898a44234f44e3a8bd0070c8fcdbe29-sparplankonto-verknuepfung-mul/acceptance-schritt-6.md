# Abnahmeprüfung – Entwicklungsschritt 6

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 6 ist in Commit `32099f29` enthalten) gegen Schritt 6 in `project-plan.md` (Zeilen 125–137). Code-Review plus Testlauf.

- **AK 1 – Links/Istmeldung über MCP mit API-gleichen Validierungen:** `fm_savings_plans_manage` (`FinanceManager.Web/Mcp/Tools/FmSavingsPlansManageTool.cs`) erhält die Operationen `addAccountLink`/`removeAccountLink`/`reportActualBalance` (Dispatch Zeilen 118–124; Implementierung `AccountLinkAsync` Zeilen ~274–345, `ReportActualBalanceAsync` Zeilen ~347–396). Validierungen identisch zur REST-Semantik: fremder Plan → `null`-Ergebnis (404-Semantik), fremdes/fehlendes Konto → `Err_Mcp_InvalidArguments` (owner-scoped aufgelöst via `IAccountService.GetAsync`), Duplikat-Link → `Err_Mcp_InvalidState`, fehlender Link beim Entfernen → `Err_Mcp_InvalidArguments`, Istmeldung bei verknüpftem Plan → `Err_Mcp_InvalidState`, fehlende `reportedBalance` → `Err_Mcp_InvalidArguments`. Getestet in `McpMasterDataWriteToolsTests` (8 neue Tests: Delegate+Audit, ForeignAccount, ForeignPlan, Duplicate, MissingLink, MissingBalance, LinkedPlan-Rejection).

- **AK 2 – Scopes, Record-Kontext, Audit-Staging:** Tool-Scopes um `(Konten, Lesen)` ergänzt (`McpToolScope(McpDataType.Accounts, McpRightKind.Read)`, Descriptor Zeilen ~47–53) — exakt die im Plan geforderte Erweiterung; kein neuer `McpDataType` nötig. Record-Level-Scope per `McpMasterDataGuards.RequireRecordWritableAsync` mit `McpRecordContext.SavingsPlanCategoryId` des Plans. Audit-Staging-Konvention eingehalten: `McpWriteAudit.StageDataChangeAsync` vor dem Effekt, `DiscardStaged` auf allen Fehlerpfaden (inkl. generischem `catch`), Change-Records `AccountLinkAdded`/`AccountLinkRemoved`/`ActualReportedBalance`. JSON-Schema des Tools um `accountId`/`reportedBalance` und die drei Operationen erweitert.

- **AK 3 – Descriptor-Texte korrekt:** `fm_savings_plan_analysis`-Description (`FmSavingsPlanAnalysisTool.cs:42–45`) dokumentiert den Konto-Link-Modus verbindlich und korrekt: `accumulatedAmount` = Summe der verknüpften Kontosalden (voller Saldo je Plan, Sammelkonto ohne Aufteilung), Rhythmus/Defizit aus dem Konto-Inflow aller positiven Zuflüsse, informelle Funding-Postings (`informal: true`, zählen nie in `accumulated`/`expected`/`received`, Ableitung ohne Umbuchung, Fallback nach letztem Link-Remove), Disbursements ohne Funding-Gruppe zählen weiter, `ActualReport`-SubType = Istmeldung nur ohne Links, ohne Kontobewegung, nie informell, nie in Budget-Actuals. `fm_savings_plans_manage`-Description und `fm_postings_query`-Description analog angepasst. Beschreibungen decken sich mit der Implementierung in `SavingsPlanService.AnalyzeAsync`/`SavingsPlanLinkAnalysis`.

- **Testnachweis:** `dotnet test FinanceManager.Tests --filter "FullyQualifiedName~McpMasterDataWriteToolsTests"` → **68/68 Tests bestanden** (inkl. aller 8 neuen Link-/Istmeldung-Tests).

- **Strenge beachtet:** Schreiboperationen sind `Schreibvorbereitend`-klassifiziert und wenden Effekte wie bisher direkt an (gleiche Konvention wie create/update); `archive` bleibt approval-required unverändert.
