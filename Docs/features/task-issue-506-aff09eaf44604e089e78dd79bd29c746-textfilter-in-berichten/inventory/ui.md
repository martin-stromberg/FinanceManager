# UI-Komponenten

## `ReportDashboard.razor`
Datei: `FinanceManager.Web/Components/Pages/ReportDashboard.razor` (`@rendermode InteractiveServer`, Route `/reports/dashboard`)

**Filterdialog** (Z. 408–468): Modal, gesteuert über `_vm.ShowFilterDialog`.
- Bei Multi-Kind eine Tab-Leiste je `PostingKind` (`_selectedKinds`, aktiver Tab `_vm.GetActiveFilterTabKind()`, Z. 423–434).
- Optionsliste `scroll-box option-list` mit Checkboxen je Option des aktiven Tabs (`_vm.GetOptionsForKind`, `_vm.IsOptionSelectedTemp`, `_vm.ToggleTempForKind`, Z. 435–446).
- Nur für `PostingKind.Security`: zusätzliche Subtype-Checkboxliste Buy/Sell/Dividend/Fee/Tax unter der Optionsliste (`FilterGroup_SecurityTypes`, Z. 447–459) — struktureller Präzedenzfall für einen weiteren Unterabschnitt je Tab (hier würde der Textfilter-Editor sitzen).
- Dialog-Aktionen: `FilterDialog_Apply` → `_vm.ApplyTempAndReloadAsync(_analysisMonth)`, `FilterDialog_Cancel`, `Button_ClearFilters` → `_vm.ClearTempFilters()` (disabled bei `GetSelectedTempFiltersCount()==0`), Z. 461–465.
- **Kein Textfilter-Editor vorhanden.**

**Favoriten-Übernahme** (`ApplyFavorite`, Z. 1020–1050): überträgt `fav.Filters`-Felder eins zu eins in die `Selected*`-Sets des ViewModels (`AccountIds` … `SecuritySubTypes`, `IncludeDividendRelated`), dazu `UseValutaDate` und `SavingsPlanPostingSource`; Textfilter wären hier analog zu übernehmen.

**Payload-Bau** (`BuildFilters`, Z. 1058–1066): baut `ReportAggregatesFiltersRequest` (bei `_includeCategory` nur Accounts + Kategorien + Subtypes, sonst alle Entity-Listen); wird an `SubmitFavoriteDialogAsync` (Z. 972–983) übergeben. Der eigentliche Query-Request geht über `_vm.ReloadAsync` → `BuildFiltersPayload()` im ViewModel.

**Edit-Panel** (Z. 40–107): Einstellungen für Kinds, Interval, Analysemonat, `IncludeCategory`, `Take`, `IncludeDividendRelated`, Datumsart (booking/valuta) und `SavingsPlanPostingSource` (nur bei SavingsPlan-Auswahl sichtbar).

## `HomeKpiGrid.razor`
Datei: `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`

- Rendert `_kpis` (`HomeKpiDto`) sortiert nach `SortOrder`; Favoriten-Kacheln über `RenderKpiTile` → `ReportKpiTile` mit `_favData[k.Id]` (Amount/Prev/Year/Series/Kind).
- KPI-Request wird **vollständig aus dem Favoriten-DTO** gebaut (`LoadFavoriteKpiDataAsync`, Z. 585–651): `fav.Filters` → `ReportAggregatesFiltersRequest` — neue Filterfelder in `ReportFavoriteFiltersDto` fließen automatisch, sobald das DTO erweitert ist.
- LocalStorage-Cache: `_kpiCache` (`IKpiLocalStorageCache`, aufgelöst in `OnAfterRenderAsync`); `RestoreFavoriteCacheAsync` (Z. 175–190) befüllt `_favData` vorab aus `home.kpi.favorite-{k.Id}`; `LoadFavoriteKpiDataAsync` schreibt den frischen Wert zurück (Z. 640–643). Keine Filter-/Versionsbindung im Cache-Schlüssel.
- KPI-Anlage-Dialog (Z. 50–111): Auswahl Predefined vs. Favorit (`Reports_ListFavoritesAsync`) + `HomeKpiDisplayMode`.

## Ressourcen
Dateien: `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.de.resx`, `ReportDashboard.en.resx`

Vorhandene relevante Schlüssel (de/en gepflegt, kein Hardcoding): `FilterDialog_Title`, `FilterDialog_Apply`, `FilterDialog_Cancel`, `Button_ClearFilters`, `FilterGroup_Data`, `FilterGroup_Comparisons`, `FilterGroup_Chart`, `FilterGroup_TopLevelFilter`, `FilterGroup_SecurityTypes`, `SecurityType_Buy/Sell/Dividend/Fee/Tax`, `PostingKind_{Bank|Contact|SavingsPlan|Security}`, `Interval_*`, `Label_*`, `Dialog_Fav_*`, `Ribbon_*`. Neue UI-Texte für den Textfilter-Editor sind hier für de + en zu ergänzen (AGENT.md).

## Ribbon
`ReportDashboardViewModel.GetRibbonRegisterDefinition` liefert drei Gruppen (`Ribbon_Navigation` mit Zurück, `Ribbon_ReportActions`, `Ribbon_Filter` mit `FiltersOpen`/`FiltersClear`). Die Anforderung erweitert den bestehenden Filterdialog — kein neuer Ribbon-Eintrag nötig; `FiltersClear` ist an `GetSelectedFiltersCount()==0` gebunden und müsste Textfilter mitzählen.
