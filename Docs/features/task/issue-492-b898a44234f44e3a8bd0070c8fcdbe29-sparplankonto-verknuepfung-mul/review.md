# Plan-Review

**Status:** Vollständig umgesetzt

| Planabschnitt | Umsetzung | Status |
|---------------|-----------|--------|
| Info-Button + Erklärtext | `SavingsPlanLinkedAccountsPanel.razor` (`#info`, `_showInfo`, 4 lokalisierte Punkte, ResX neutral/de/en) | Umgesetzt |
| Savings-only Auswahl | `SavingsPlanCardViewModel.LinkableAccounts` (Type==Savings) + `SavingsPlanAccountLinkService.AddLinkAsync` (ArgumentException) | Umgesetzt |
| Symbolbuttons Entfernen/Öffnen | `#unlink`/`#external` icon-btn, `OpenLinkedAccount` → `OpenAccountDetails` (vorhandener Handler in CardPage.razor) | Umgesetzt |
| Saldospalte >1 Link / Ein-Link nur Öffnen-Button | `showDetails = Vm.LinkedAccounts.Count > 1` | Umgesetzt |
| Eigenanteil in Kopfdaten | `SavingsPlanDto.OwnAmount`, `SavingsPlanService.GetAsync`, Kartenfeld `Card_Caption_SavingsPlan_OwnAmount` | Umgesetzt |
| Übersicht gleiche Saldoregel | Via `SavingsPlans_AnalyzeAsync` (Bestand, konto-/buchungsbasiert) | Umgesetzt |
| Demodaten Bauspar | Kontakt „Musterbausparkasse", Konto „Bausparkonto" (Savings), Plan „Bausparen" (Recurring/Monthly, 40.000 €), Link, monatl. -200/+200, 1-%-Jahresendzinsen beide Konten, Budget „Bausparen" + Monatsregel, Favorit „Sparplanbuchungen (monatlich)" mit LinkedAccountPostings | Umgesetzt |
| Report-Quellenoption | `SavingsPlanPostingSource`-Enum, `ReportFavorite`-Spalte + Migration, DTOs, `ReportAggregationService`-Umattribution, Dashboard-Select (nur bei SavingsPlan), Favorit-Persistenz, MCP-Tools | Umgesetzt |
| ResX neutral/de/en | Pages×3, ReportDashboard de+en | Umgesetzt |
| EF-Migration via dotnet ef | `AddReportFavoriteSavingsPlanPostingSource` | Umgesetzt |
