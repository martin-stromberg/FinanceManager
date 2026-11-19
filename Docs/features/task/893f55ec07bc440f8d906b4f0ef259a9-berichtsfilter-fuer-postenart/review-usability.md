# Usability-Review — Berichtsfilter für Postenart

**Status:** Befunde vorhanden

## Befunde

### 1. [Unsichtbar wirksame Filter] Entity-/Kategorie-Auswahl eines entfernten Kinds bleibt unsichtbar aktiv

Beim Abwählen eines Postenkind werden über `ClearKindFilters` (ReportDashboardViewModel.cs:373–402) nur die Subtyp-/Textfilter des Kinds verworfen — nicht jedoch die Entity-Auswahl (`SelectedAccounts`, `SelectedContacts`, `SelectedSavingsPlans`, `SelectedSecurities`) und die Kategorie-Auswahl (`SelectedContactCategories`, `SelectedSavingsCategories`, `SelectedSecurityCategories`).

Konkretes Szenario: Auswahl `[Bank, Kontakt]` → Anwender wählt „Kontakt" ab. Da danach nur noch ein Kind übrig ist und der Primärkind unverändert bleibt, wird das vollständige `ClearFilters()` in `OnToggleKind` (ReportDashboard.razor:1392–1396) **nicht** aufgerufen. `SelectedContacts` bleibt bestehen:

- im Filterdialog nicht mehr sichtbar (kein Kontakt-Tab, Einzelkind-Ansicht zeigt nur Bank-Optionen),
- in `GetSelectedFiltersCount` (Einzelkind-Zweig, VM:1333–1341) für den Bank-Bericht nicht mitgezählt — der Anwender kann den Filter weder sehen noch gezielt abwählen,
- wird aber weiterhin über `BuildFiltersPayload()` (VM:314–329) mitgesendet und beim Favorit-Speichern persistiert; beim erneuten Hinzufügen des Kinds „erwacht" die alte Auswahl wieder, während die Subtyp-/Textfilter desselben Kinds unwiderruflich verworfen wurden.

Damit ist die im Code-Kommentar (razor:1383–1384) formulierte Absicht („cannot stay active invisibly") nur teilweise erreicht und das Verhalten inkonsistent: gleiche Sichtbarkeits-Situation, unterschiedliche Behandlung je nach Filtertyp.

**Empfehlung:** `ClearKindFilters` um die Entity-/Kategorie-Selektionen des entfernten Kinds erweitern (z. B. `PostingKind.Contact` → `SelectedContacts` + `SelectedContactCategories` leeren; analog Bank/SavingsPlan/Security). Alternativ bewusst einheitlich beibehalten und dokumentieren — aktuell ist es ein inkonsistenter Zwischenzustand.

### 2. [Aktionswirksamkeit] Abwählen des letzten Kinds löscht stillschweigend dessen Typ-/Textfilter

Ist nur ein Kind ausgewählt (z. B. `[Bank]`) und der Anwender entfernt den Haken, wird das Kind sofort wieder hinzugefügt (razor:1379–1382) — die Checkbox bleibt also sichtbar gesetzt und die Aktion scheint wirkungslos. Trotzdem läuft `ClearKindFilters(Bank)` (razor:1385) und verwirft die gerade aktiven Postenarten- und Textfilter des Kinds; der Bericht lädt neu und `FiltersDirty` wird gesetzt.

Der Anwender erlebt: Klick auf die Kind-Checkbox verändert die Checkbox nicht, die Daten ändern sich aber und die zuvor gesetzten Filter (z. B. „nur Ausgänge") sind verloren — ohne Hinweis und ohne Wiederherstellungsmöglichkeit im Dialog.

**Empfehlung:** `ClearKindFilters` nur aufrufen, wenn das Kind tatsächlich entfernt bleibt (d. h. nicht in dem Zweig, der das letzte Kind wieder hinzufügt), oder das Abwählen des letzten Kinds von vornherein unterbinden (Checkbox deaktivieren bzw. erzwungenen Mindestzustand sichtbar machen).

### 3. [Einheitliches Verhalten, geringfügig] Typfilter zählen kontextlos in der Filterzählung

`GetSelectedFiltersCount`/`GetSelectedTempFiltersCount` (VM:1189–1227, 1306–1344) addieren `SelectedBankPostingTypes`/`SelectedSecuritySubTypes` unabhängig davon, ob das zugehörige Kind überhaupt gewählt ist — im Gegensatz zu den Textfiltern, die korrekt über `SelectedKinds` summiert werden (`GetSelectedTextFilterCount`, VM:1541–1542). Ein Einzelkind-Favorit, der (z. B. über MCP angelegt oder aus Altdaten) `BankPostingTypes` mitführt, aktiviert damit die Ribbon-Aktion „Filter löschen", ohne dass der Anwender einen entsprechenden Filter im Dialog sehen oder abwählen kann.

**Empfehlung:** Typfilter nur für die aktuell gewählten Kinds mitzählen (analog zur Textfilter-Summe). Auswirkung ist gering (betrifft nur den Enabled-Zustand von „Filter löschen" bzw. „Zurücksetzen" im Dialog), daher niedrige Priorität.

## Geprüfte Interaktionen

- **Postenarten-Filter (Bank):** Checkboxen Eingang/Ausgang/Zinsen/Steuern im Filterdialog — Klartext, lokalisiert (de/en), keine internen Kennungen sichtbar. Wirkt auf Aggregat, Hochrechnung und Drilldown konsistent; Sparplan-Verknüpfungspfad bewusst ausgenommen (im Code dokumentierte Scope-Abgrenzung).
- **Subtyp-Filter (Security):** analog, fünf lokalisierte Checkboxen.
- **Textfilter:** pro Kind-Tab, Add/Remove mit de/en-Texten, Platzhalter und Hinweistext vorhanden.
- **Kind-Auswahl (Multi):** Tabs im Filterdialog nur für gewählte Kinds; Abwahl eines Kinds → siehe Befunde 1 und 2.
- **Favorit speichern/aktualisieren:** `BankPostingTypes` wird jetzt vollständig durchgereicht (`BuildFiltersPayload` → `ToFavoriteFilters`, VM:781–874); Fehlermeldungen beim Speichern/Aktualisieren sind neu lokalisiert (`Error_SaveFavorite`/`Error_UpdateFavorite` in beiden resx; werden über `Localizer[...]` gerendert, kein roher Exception-Text mehr).
- **Favorit laden:** `ApplyFavorite` (razor:1448–1487) stellt `SelectedBankPostingTypes` und alle übrigen Filter wieder her — Roundtrip konsistent.
- **KPI-Kachel (Home):** `HomeKpiGrid.razor:657` reicht `fav.Filters.BankPostingTypes` in die Aggregatabfrage durch — Kachel wertet denselben Filter wie der Bericht aus.
- **Drilldown (Posteneinsicht):** `ShowPostingsAsync` nutzt `BuildFiltersPayload()`; serverseitig wird `BankPostingTypes` auf Bank-Zellen angewendet, verknüpfte Sparplan-Quellposten bewusst ungefiltert — konsistent zur Zellanzeige.
- **Hochrechnung:** Projektionspfad filtert Bank-`ProjectionEvent`s nun nach der Postenarten-Allowlist; Konsistenz zu Konto-/Textfiltern gegeben.
- **Filter-Badge/-Zählung:** kein numerisches Badge; Zählstand steuert nur Enabled-Zustand von „Filter löschen"/„Zurücksetzen" (Befund 3).

## Geprüfte Dateien

- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` (Filterdialog, Kind-Toggles, Favoritendialog, Fehleranzeige, Drilldown-Overlay)
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` (`ClearKindFilters`, `BuildFiltersPayload`, `ToFavoriteFilters`, Temp-/Selected-Filterverwaltung, `SubmitFavoriteDialogAsync`, `ShowPostingsAsync`, Filterzählung)
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` (Favoriten-KPI-Abfrage)
- `FinanceManager.Web/Controllers/ReportsController.cs` (Filter-Mapping Aggregate/Postings/Favoriten-CRUD)
- `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.de.resx` / `.en.resx` (Vollständigkeit der Beschriftungen inkl. neuer Fehlertexte)
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs`, `ReportProjectionEventLoader.cs`, `ReportAggregationService.cs` (soweit für Filterwirksamkeit aus Anwendersicht relevant)
