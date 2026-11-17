# Übersetzte Anforderung: Nachbesserungen Sparplan↔Konto-Verknüpfung

Quelle: `continue.md` (gesammelte UX-/Fachrückmeldungen zum bereits implementierten Feature „Sparplan–Konto-Verknüpfung").

## Fachliche Zusammenfassung

Vier Nachbesserungspakete zur bestehenden Verknüpfung zwischen Sparplänen und Bankkonten:

1. **UX Verknüpfungs-Panel:** Ein Info-Button erklärt den Zweck der Verknüpfung. Die Kontoauswahl wird auf Konten vom Typ `AccountType.Savings` (Sparkonto) eingeschränkt – in der UI und serverseitig abgesichert.
2. **UX Verknüpfungsliste & Saldoanzeige:** Entfernen und Konto-Aufruf werden Symbolbuttons; bei mehreren Verknüpfungen erscheint der Kontosaldo als eigene Spalte, bei genau einer Verknüpfung nur der Konto-Aufruf-Button. In den Kopfdaten des Sparplans wird bei vorhandenen Links zusätzlich der Posten-Saldo als „Eigenanteil" ausgewiesen; die Sparplanübersicht folgt derselben Saldoregel.
3. **Demodaten:** Bausparkonto (Sparkonto), Sparplan „Bausparen", Verknüpfung, monatliche Umbuchungen Giro→Bausparkonto, Jahresendzinsen 1 % auf die jeweiligen Salden von Giro- und Bausparkonto, Budget für die Einzahlungen sowie ein zusätzlicher Report-Favorit für die monatlichen Sparplanbuchungen.
4. **Reports:** Neue Reporteinstellung `SavingsPlanPostingSource`, mit der für ausgegebene Sparpläne zwischen Sparplanposten und Posten der verknüpften Bankkonten als Postenquelle gewählt wird.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Verknüpfung Sparplan↔Bankkonto und ihre Darstellung (Karte, Liste, Report, Demodaten).
- **Auslösende Ereignisse:** Nutzerinteraktion in der Sparplankarte (Info, Auswahl, Entfernen, Kontoaufruf), Reporteinstellung im Report-Dashboard, Demodaten-Erzeugung.
- **Erwartete Wirkung:** Erläuterungstext sichtbar; nur Sparkonten wählbar/verknüpfbar; Symbolbuttons; Saldospalte bei ≥2 Links; „Eigenanteil"-Feld bei verknüpften Plänen; Bauspar-Demodatensatz inkl. Zinsen, Budget und Report-Favorit; Reports können verknüpfte Bankkontoposten als Quelle für Sparpläne nutzen.
- **Umgang mit fehlenden Objekten:** Ohne Links bleibt alles unverändert (Posten-Betrachtung); Pläne ohne Links liefern im Report weiterhin ihre Sparplanposten; Demodaten werden nur bei Neuinitialisierung erzeugt.
- **Scope-Ausschlüsse:**
  1. Bei genau einer Verknüpfung zeigt die Zeile ausschließlich den Konto-Aufruf-Button – kein Entfernen-Button und keine Saldospalte (wörtliche Umsetzung der Rückmeldung).
  2. Die Übersichtsliste bekommt keine zusätzliche „Eigenanteil"-Spalte – „dieselbe Saldoregel" bezieht sich auf den ausgewiesenen Saldo.
  3. Home-KPIs und andere Report-Aufrufer ohne explizite Wahl nutzen weiterhin die Standardquelle (Sparplanposten).

## Betroffene Klassen und Komponenten

- `SavingsPlanLinkedAccountsPanel.razor` (Info-Button, Symbolbuttons, Saldo-Spalte, Ein-Link-Darstellung)
- `SavingsPlanCardViewModel` (Savings-Filter für `LinkableAccounts`, `OpenLinkedAccount`-Aktion, Eigenanteil-Feld)
- `SavingsPlanAccountLinkService` (Ablehnung nicht-Savings-Konten in `AddLinkAsync`)
- `SavingsPlanDto` + `SavingsPlanService.GetAsync` (neues Feld `OwnAmount`/„Eigenanteil")
- `SavingsPlansListViewModel` (Saldoregel identisch – bereits über `AnalyzeAsync` gegeben, Regressionstest)
- `SavingsPlanPostingSource` (neuer Enum, `FinanceManager.Shared.Dtos.Reports`)
- `ReportFavorite` (+ EF-Migration, Backup-DTO), `ReportFavoriteService`, `ReportFavorite(Create/Update)Request`, `ReportFavorite(Create/Update)ApiRequest`, `ReportFavoriteDto`, `ReportAggregatesQueryRequest`, `ReportAggregationQuery`
- `ReportAggregationService.QueryAsync` (Postenquellen-Umschaltung)
- `ReportDashboard.razor` + `ReportDashboardViewModel` (Option in den Reporteinstellungen, persistieren/laden)
- `ReportsController`, `FmReportingAggregateTool`, `FmReportsConfigurationManageTool` (Durchreichen der Option)
- `DemoDataService` (Bauspar-Demodaten, Budget, Report-Favorit, `ISavingsPlanAccountLinkService`)
- ResX: `Pages.resx/.de/.en`, `ReportDashboard.resx/.de/.en`
- Tests: Link-Service (Savings-only), ViewModel (Filter, Eigenanteil), `SavingsPlanService.GetAsync` (OwnAmount), `ReportAggregationService` (Quellenoption), Demodaten-Integration

## Implementierungsansatz

- Enum `SavingsPlanPostingSource { PlanPostings = 0, LinkedAccountPostings = 1 }` als persistierte Einstellung auf `ReportFavorite` (int-Spalte via EF-Migration) sowie als Parameter auf `ReportAggregationQuery`/`ReportAggregatesQueryRequest`.
- In `ReportAggregationService.QueryAsync`: bei `LinkedAccountPostings` und angefragtem `PostingKind.SavingsPlan` werden die `SavingsPlan`-Rohzeilen verknüpfter Pläne durch Zeilen aus den `Bank`-Aggregaten der verknüpften Konten ersetzt (Umattribution auf die Plan-Id); Pläne ohne Links behalten ihre Planposten.
- Demodaten: zusätzliches Sparkonto „Bausparkonto", Plan „Bausparen" (Recurring/Monthly), Link über `ISavingsPlanAccountLinkService`, monatliche Draft-Einträge Giro (−) mit Plan-Bezug → informeller Planposten + Bausparkonto (+) ohne Plan-Bezug; Jahresendzinsen 1 % auf getrackte Salden beider Konten (positive Salden); Budgetzweck `BudgetSourceType.SavingsPlan` mit monatlicher Regel; Report-Favorit mit `LinkedAccountPostings`.
- „Eigenanteil": `SavingsPlanDto.OwnAmount` (nullable), befüllt in `GetAsync` = Summe der Planposten bis heute, nur wenn Links vorhanden; Kartenfeld `Card_Caption_SavingsPlan_OwnAmount` nur bei Links.

## Konfiguration

Keine zusätzliche Konfiguration.

## Offene Fragen

Keine – alle Rückmeldungen werden wörtlich umgesetzt; der Ein-Link-Sonderfall („nur Konto-Aufruf-Button") ist bewusst als Scope-Entscheidung dokumentiert.
