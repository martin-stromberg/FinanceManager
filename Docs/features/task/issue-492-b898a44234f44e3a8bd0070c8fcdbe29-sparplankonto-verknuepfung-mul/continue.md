# Offene Rückmeldungen

Status 2026-11-08: Alle Punkte umgesetzt (siehe plan.md / review.md / test-results.md).

Erstellt am: 2026-11-08

## UX-Rückmeldung

- [x] Im Panel zur Sparplan↔Konto-Verknüpfung einen Info-Button ergänzen.
- [x] Der Info-Button soll erklären, wofür diese Verknüpfung gedacht ist:
  - verknüpfte Konten liefern den tatsächlichen Plan-Saldo,
  - Konto-Inflow dient als Grundlage für Rhythmus- und Defizitanalyse,
  - Funding-Postings auf anderen Konten können als informelle Belegquelle gekennzeichnet werden,
  - ohne verknüpfte Konten bleibt die bisherige Posting-basierte Betrachtung aktiv.
- [x] Umsetzung erst nach weiterer fachlicher Rückmeldung bzw. Freigabe. (freigegeben durch Auftrag)
- [x] Bei der Kontoverknüpfung dürfen ausschließlich Konten vom Typ „Sparkonto“ auswählbar sein.
- [x] Die Einschränkung auf Sparkonten muss durch Tests abgesichert werden.

## UX-Verfeinerung Verknüpfungsliste und Saldoanzeige

- [x] Der Button zum Entfernen einer Kontoverknüpfung soll als Symbolbutton dargestellt werden.
- [x] Jede verknüpfte Bankkonto-Zeile soll einen Symbolbutton zum Aufruf des Bankkontos erhalten.
- [x] Bei mehreren Verknüpfungen sollen die Salden der Bankkonten als eigene Spalte in der Auflistung angezeigt werden.
- [x] Bei nur einer Verknüpfung soll nur der Symbolbutton zum Bankkonto gezeigt werden.
- [x] In den Kopfdaten des Sparplans soll bei vorhandenen Verknüpfungen die Summe der Bankkontosalden als Saldo angezeigt werden.
- [x] Zusätzlich soll der bisherige Saldo aus den Sparplanposten als „Eigenanteil“ ausgewiesen werden.
- [x] Ohne Verknüpfung bleibt die bisherige Saldoausgabe unverändert.
- [x] In der Sparplanübersicht muss die Saldoanzeige denselben Regeln folgen wie in den Kopfdaten.

## Demodaten

- [x] Demodaten um ein Bankkonto für einen Bausparvertrag erweitern.
- [x] Demodaten um einen Sparplan „Bausparen“ erweitern.
- [x] Das Bausparkonto mit dem Sparplan „Bausparen“ verknüpfen.
- [x] Monatliche Buchungen vom Girokonto auf das Bausparkonto erfassen.
- [x] Zum Jahresende Zinsbuchungen in Höhe von 1 % des jeweiligen Saldos erfassen.
- [x] Die Zinsbuchungen sollen sowohl auf dem Girokonto als auch auf dem Bausparkonto saldenwirksam berücksichtigt werden.
- [x] Für die monatlichen Einzahlungen auf das Bausparkonto soll ein Budget in den Demodaten hinterlegt werden.
- [x] In den Demodaten soll ein weiterer Report erfasst werden, der die monatlichen Sparplanbuchungen zeigt.
- [x] Bei Sparplänen mit Kontoverknüpfung muss der Report die Bankkontoposten berücksichtigen können.
- [x] Die Reporteinstellungen sollen um eine neue Option erweitert werden, die gesetzt werden kann, wenn Sparpläne ausgegeben werden sollen.
- [x] Über diese Option soll der Anwender die Postenquelle für die ausgegebenen Sparpläne wählen können, insbesondere zwischen Sparplanposten und Posten der verknüpften Bankkonten.

## Weitere Rückmeldungen

- [x] Beim Löschen einer Kontoverknüpfung soll eine Sicherheitsabfrage angezeigt werden.
  - `SavingsPlanCardViewModel.RemoveAccountLinkAsync` fragt jetzt vor dem API-Aufruf über `IConfirmationService` nach (Warning-Severity, neue ResX-Keys `Confirmation_RemoveAccountLink_*` in Pages.resx/.de/.en); der Dialog wird wie üblich über den globalen `ConfirmationDialogHost` in `CardPage.razor` gerendert.
  - Abgesichert durch `SavingsPlanCardLinkTests` (Confirm=true → API-Call; Cancel → kein API-Call, Link-Zustand bleibt).
- [x] Die Kontoverknüpfung muss über den MCP-Server zugreifbar sein.
  - Bestand bereits über `fm_savings_plans_manage` (`addAccountLink`/`removeAccountLink`); als Lesesicht ergänzt: `fm_savings_plan_analysis` gibt jetzt je Plan `linkedAccountIds` aus (Bulk-Abfrage `ISavingsPlanAccountLinkService.GetLinkedAccountIdsByPlanAsync`), und die Link-Operationen des Manage-Tools liefern die aktuelle `linkedAccountIds`-Liste im Ergebnis. Keine neuen Tools.
- [x] Der MCP-Server muss bei Sparplänen mit Kontoverknüpfung die korrekten konto-basierten Sparplansalden ausgeben.
  - `SavingsPlanService.AnalyzeAsync` (damit `analysis.accumulatedAmount` von `fm_savings_plan_analysis`) und `GetAsync`/`GetCurrentAmountAsync` (damit `currentAmount` bei Einzelabruf) summieren bereits die verknüpften Kontosalden; kein Pfad summiert mehr Planposten für verknüpfte Pläne. Listeneinträge ohne `includeAnalysis` tragen dokumentiert kein `currentAmount` (Descriptor-Hinweis vorhanden).
- [x] MCP-Zugriff und korrekte Salden müssen durch Integrationstests abgesichert werden; falls möglich/sinnvoll, zusätzlich durch E2E-Tests mit realer Demodatenbank.
  - `FinanceManager.Tests.Integration/McpSavingsPlanLinkTests` (Collection `DemoDataSerial`): echter `/mcp`-Host mit PAT, realer Demo-Datenbestand (`IDemoDataService` → „Bausparkonto“+„Bausparen“+Link). Test 1 prüft `linkedAccountIds` und `analysis.accumulatedAmount` gegen den Bausparkonto-Saldo; Test 2 führt `addAccountLink`/`removeAccountLink` über `fm_savings_plans_manage` aus und prüft die Salden-Summe beider verknüpfter Konten.
  - Zusätzlich `SavingsPlanAccountLinkServiceTests_Queries.GetLinkedAccountIdsByPlan_MapsOnlyOwnedLinkedPlans` für die neue Bulk-Abfrage.

## Nachbesserungen Kontext und Bedienbarkeit

- [x] Die Information zu den verknüpften Konten verständlicher formulieren: Der Sparsaldo wird mit den Salden der verknüpften Bankkonten gekoppelt; dem Sparplan zugeordnete Buchungen in Kontoauszügen werden als Eigenanteil gewertet.
  - `LinkedAccounts_Info_Balance` umformuliert (Kopplung an die Kontosalden) und neuer Punkt `LinkedAccounts_Info_OwnShare` (in Kontoauszügen zugeordnete Buchungen = Eigenanteil) in `Pages.resx`/`.de`/`.en` sowie in `SavingsPlanLinkedAccountsPanel.razor` ergänzt.
- [x] Sicherstellen, dass eine Kontoauszugzeile einem Sparplan nicht zugeordnet werden kann, wenn der Kontoauszug zu einem verknüpften Bankkonto gehört; entsprechende Tests ergänzen.
  - `StatementDraftService.SaveEntryAllAsync` und `AssignSavingsPlanAsync` werfen `DomainValidationException("SAVINGSPLAN_ACCOUNT_LINKED", …)`, wenn `DetectedAccountId` verknüpft ist; `ClassifyInternalAsync` überspringt die Plan-Auto-Zuordnung für verknüpfte Konten.
  - API: `SetEntrySavingPlanAsync` bildet die Exception als `409` mit `ApiErrorDto` ab (lokalisierte Meldung `API_StatementDraft_SAVINGSPLAN_ACCOUNT_LINKED` in `Controller.de/.en.resx`); `StatementDrafts_SetEntrySavingsPlanAsync` propagiert den Fehler via `EnsureSuccessOrSetErrorAsync`.
  - UI: neuer Endpunkt `GET /api/accounts/{id}/savings-plan-links` (+ `ApiClient.GetSavingsPlanLinksAsync`); `StatementDraftEntryCardViewModel` blendet das Sparplan-Feld bei verknüpftem Konto aus (Alt-Zuordnungen bleiben zum Leeren sichtbar). MCP: `McpStatementDraftGuards.ValidateSavingsPlanAssignment` + `FmStatementDraftCompleteHandler` prüfen den Link-Status (`Err_Mcp_InvalidArguments`).
  - Tests: `StatementDraftSavingsPlanLinkedAccountTests` (SaveAll/Assign/Klassifizierung, verknüpft + unverknüpft + Leeren erlaubt) und Integration `ApiClientSavingsPlanLinksTests.StatementEntry_SavingsPlanAssignment_RejectedOnLinkedAccount` (beide Endpunkte, 409-Code).
- [x] Auch eine einzelne Kontoverknüpfung muss wieder entfernbar sein; der Entfernen-Button darf bei genau einem Link nicht fehlen.
  - `SavingsPlanLinkedAccountsPanel.razor`: Ein-Link-Zeile zeigt jetzt neben „Konto öffnen" auch den Unlink-Symbolbutton (Saldospalte bleibt ausgeblendet); Sicherheitsabfrage über `RemoveAccountLinkAsync` unverändert. Abgesichert durch `SavingsPlanLinkedAccountsPanelTests` (bUnit: Buttons im Ein-/Mehr-Link-Layout, Klick löst bestätigtes Entfernen aus).

## Nachbesserungen Reportfavorit

- [x] Demodaten-Report „Sparplanbuchungen (monatlich)“ in „Sparplanbwegungen (Monatlich)“ umbenennen.
  - `DemoDataService.EnsureDefaultReportFavoritesAsync`: Favorit umbenannt (gilt für neu erzeugte Demodaten; vorhandene Favoriten werden nicht angefasst, da das Seeding bei vorhandenen Favoriten abbricht).
- [x] Im Demodaten-Report sollen „Vorperiode vergleichen“ und „Vorjahr vergleichen“ aktiviert sein.
  - `ComparePrevious: true` und `CompareYear: true` im selben Seed-Block.
- [x] Als Postenquelle soll „Posten der verknüpften Konten“ gewählt sein.
  - `SavingsPlanPostingSource.LinkedAccountPostings` war bereits gesetzt und bleibt es.
- [x] Fehler beheben: Im Bericht kann ein Filter nur entfernt, aber nicht bearbeitet werden.
  - Ursache (UI/Ribbon): `ReportDashboardViewModel.GetRibbonRegisterDefinition` deaktivierte `FiltersOpen` bei `!EditMode`, während `FiltersClear` aktiv blieb. `FiltersOpen` ist jetzt nur noch bei laufendem Optionsladevorgang deaktiviert; der Filterdialog (`OpenFilterDialog` → Temp-Puffer → `ApplyTempAndReloadAsync`) steht damit auch im Ansichtsmodus zur Verfügung.
  - Zusätzlich (UI-Binding): `ReportDashboard.razor` synchronisierte `_editMode` nicht aus dem `edit`-Query-Parameter — bei direktem Aufruf mit `?edit=true` (z. B. Ribbon „Neu“ auf `/reports`) blieb das Bearbeitungspanel inkl. Postenquelle-Auswahl unsichtbar. `_editMode` wird jetzt in `OnParametersSetAsync` aus `_vm.EditMode` gesetzt.
- [x] Fehler beheben: Die Postenquelle lässt sich nicht ändern; beim Speichern springt die Auswahl auf „Sparplanposten“ zurück.
  - Ursache war ein verteilter Wertverlust auf dem Übertragungs-/Persistenzpfad, nicht das UI-Binding:
    - `ReportsController.CreateFavoriteAsync`/`UpdateFavoriteAsync` übergaben `req.SavingsPlanPostingSource` nicht an `ReportFavoriteCreateRequest`/`ReportFavoriteUpdateRequest` (DTO-Default `PlanPostings` griff).
    - `ReportFavoriteService.UpdateAsync` rief `entity.Update(...)` ohne `request.SavingsPlanPostingSource` auf — der Domain-Default `PlanPostings` überschrieb den persistierten Wert bei jedem Update.
    - `ReportFavoriteService.GetAsync` baute das `ReportFavoriteDto` ohne `r.SavingsPlanPostingSource` — Einzelabrufe meldeten immer `PlanPostings` (ListAsync und Create/Update-Antworten waren korrekt).
    - `HomeKpiGrid.razor` übergab `fav.SavingsPlanPostingSource` nicht an `ReportAggregatesQueryRequest` — KPI-Kacheln ignorierten die Quelle.
  - Alle vier Stellen reichen den Wert jetzt durch; UI-Selekt (`OnSavingsPlanPostingSourceChange`) und `ApplyFavorite` waren bereits korrekt verdrahtet.
- [x] Report-Bearbeitung und Demodaten-Favorit durch Tests absichern.
  - `ReportFavoriteServiceTests.CreateGetListAndUpdate_ShouldRoundtripSavingsPlanPostingSource` (Create/Get/List/Update-Roundtrip inkl. „Update ohne Quellenwechsel setzt nicht zurück“).
  - `ApiClientReportsTests.Reports_Favorites_SavingsPlanPostingSource_Roundtrips` (API-End-to-End: Create → Get → Update → Get).
  - `ApiClientDemoDataTests`: erwartet neuen Namen und `ComparePrevious`/`CompareYear = true`.
  - `ReportDashboardViewModelTests`: `SaveAndUpdateFavorite_ForwardSavingsPlanPostingSource` (VM → ApiRequest-Payload), `FiltersOpen_StaysEnabled_OutsideEditMode` (Ribbon), `FilterDialog_TempChanges_ApplyToActiveFilters` (Filterbearbeitung Temp → Apply → Reload-Payload).
