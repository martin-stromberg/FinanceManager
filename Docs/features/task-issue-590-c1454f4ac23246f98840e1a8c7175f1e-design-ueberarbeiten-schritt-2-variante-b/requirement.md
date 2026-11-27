# Übersetzte Anforderung – Menüband-Aktionen: Standardgröße und Reihenfolge je Ansicht + Korrekturen (Issue #590, Schritt 2, Variante B)

## Fachliche Zusammenfassung

Die Konfiguration des Menübands (Ribbon) wird je Ansicht verbindlich neu festgelegt: Für jede genannte Übersichts- und Detailansicht ist definiert, welche Ribbon-Gruppen (`UiRibbonTab`) in welcher Reihenfolge erscheinen, welche Aktionen (`UiRibbonAction`) sie enthalten und ob jede Aktion mit großem oder kleinem Symbol (`UiRibbonItemSize.Large`/`.Small`) dargestellt wird. Ansichten bzw. Bereiche ohne Nennung bleiben unverändert. Zusätzlich sind in demselben Arbeitsumfang drei Fehler zu korrigieren (fehlendes Inhaltspanel auf Übersichtsseiten, unleserliches Löschen-Symbol in der Sicherungs-Tabelle der Einstellungen, neuer Favicon-Vorschlag passend zur neuen Farbgestaltung). Für Aktionen, die es heute noch nicht gibt, sowie für zehn bei der Durchsicht aufgefallene Fehlverhalten sind jeweils GitHub-Issues anzulegen — die Implementierung dieser Punkte ist nicht Teil dieses Vorhabens.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Ribbon-Konfiguration einer Ansicht, d. h. die Menge der von `IRibbonProvider.GetRibbonRegisters(...)` des jeweiligen ViewModels gelieferten `UiRibbonTab`-Gruppen samt ihrer `UiRibbonAction`-Einträge (Label, `UiRibbonItemSize`, `Disabled`-/`Hidden`-Bedingungen, `MobileShortcut`). Fachlich eindeutig ist, welche Ansicht (Listenansicht, Detailansicht, Bericht, Einstellungen) gemeint ist; die Zuordnung Ansicht ↔ ViewModel ist unten tabelliert.
- **Auslösende Ereignisse:** Kein einmaliges Ereignis — die Festlegung wirkt bei jedem Rendern des Menübands der betroffenen Ansichten. Bedingte Elemente reagieren auf Ansichtszustand: „Zurück" nur bei vorhandenem Navigationskontext (Attribut `Hidden` bzw. bedingte Gruppenerzeugung), „Suche zurücksetzen" nur aktiviert (`Disabled`), wenn ein Suchbegriff eingegeben ist.
- **Erwartete Wirkung:** Das Menüband jeder genannten Ansicht zeigt exakt die gelisteten Gruppen in der gelisteten Reihenfolge; jede Aktion erscheint in der vorgegebenen Größe und an der vorgegebenen Position. Zusätzlich sichtbar: Übersichtsseiten besitzen wieder ein Inhaltspanel (Inhalte nicht mehr bündig am aufgeklappten Menü; kein Layout-Sprung beim Wechsel Übersicht ↔ Detailansicht), das Löschen-Symbol in der Sicherungs-Tabelle ist lesbar, und ein Favicon-Vorschlag liegt vor.
- **Umgang mit fehlenden Objekten:**
  - **Heute nicht existierende Aktionen:** nicht implementieren; je fehlender Aktion ein GitHub-Issue anlegen (Liste unter „Anzulegende GitHub-Issues"), damit die Funktion später implementiert und das Menü dann vervollständigt wird.
  - **Konditionaler „Zurück"-Button auf Übersichtsseiten:** auf der Kontenübersicht entfällt „Zurück" in der regulären Ansicht; falls eine Navigation zur Kontenübersicht existiert, die einen Zurück-Button benötigt, ist er an Position 0 zu zeigen, nur in diesem Fall sichtbar (`Hidden`) und mit großem Symbol. Auf dem Budgetbericht ist „Zurück" nur sichtbar nach Navigation von der Budgetübersicht — Realisierung über `Hidden`/kontextabhängige Gruppenerzeugung.
  - **Neue/umbenannte Gruppen-Labels:** neue resx-Keys (de + en) anzulegen (z. B. Gruppe „Ansicht"), bestehende umbenennen („Programmversion" vormals „Aktualisierungen" — Gruppe und zugehöriges Register/Tab; „Sicherungen" vormals „Datensicherungen").
  - **Favicon:** komplett neues Symbol als Vorschlag liefern (ersetzt `wwwroot/icons/finance_manager-64x64.ico` bzw. die referenzierte Icon-Datei); muss zur neuen Farbgestaltung passen und nicht generisch wirken.
- **Scope-Ausschlüsse:**
  - Keine Implementierung der noch fehlenden Aktionen und kein Bugfix der gemeldeten Probleme — hierfür werden ausschließlich GitHub-Issues angelegt.
  - Bereiche/Ansichten, die im Kommentar nicht genannt werden (u. a. Posten-/Import-Entwurfsansichten, Startseite, Berichte-Dashboard, Wertpapier-Kurslisten, Performance-Seite), bleiben unverändert.
  - Keine Änderung des fachlichen Verhaltens bestehender Aktionen (Callbacks, Disabled-Logik außer explizit genannter Aktivierungsbedingungen).
  - Kein neuer Funktionsumfang jenseits der gelisteten Ribbon-Umsortierung und der drei genannten Korrekturen.

## Festlegung pro Ansicht (Zielkonfiguration des Menübands)

Reihenfolge der Gruppen = Reihenfolge der Auflistung; „groß" = `UiRibbonItemSize.Large`, „klein" = `UiRibbonItemSize.Small`. Ansicht ↔ ViewModel-Zuordnung gemäß Ribbon-Inventar (`ribbon-inventory.md`).

### Kontenübersicht — `BankAccountListViewModels`
- **Navigieren:** „Zurück" — entfällt in der regulären Übersicht; nur sichtbar (groß), wenn aus einem Kontext heraus navigiert wurde, der ein Zurück erfordert (`Hidden`-gesteuert).
- **Verwalten:** „Neu" — groß.
- **Filter:** „Suche zurücksetzen" — groß, nur aktiviert bei eingegebenem Suchbegriff.

### Kontaktübersicht — `ContactListViewModel`
- **Verwalten:** „Neu" — groß; „Gruppen" — groß.
- **Ansicht:** „Neu laden" — groß; „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff.

### Kontaktdetailsansicht — `ContactCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (Aktion existiert heute nicht → Issue); „Löschen" — klein; „Zusammenführen" — klein (heute `OpenMerge` in Gruppe „Verknüpfte Informationen" → umgruppieren).
- **Verknüpfte Informationen:** „Buchungen" — klein; „Anhänge" — klein.

### Sparplanübersicht — `SavingsPlansListViewModel`
- **Verwalten:** „Neu" — groß; „Kategorien" — groß.
- **Ansicht:** „Neu laden" — groß; „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff; „Nur aktive"/„Archiviert" (`ToggleActive`) — klein.

### Sparplandetailansicht — `SavingsPlanCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein; „Archivieren" — klein.
- **Analyse:** „Neu berechnen" (`Recalculate`) — groß.
- **Buchungen:** „Istmeldung" (`ActualReport`) — groß.
- **Verknüpfte Informationen:** „Kategorie" — klein; „Buchungen" — klein; „Anhänge" — klein.

### Sparplankategorieübersicht — `SavingsPlanCategoryListViewModel`
*(im Kommentar zweimal als „Sparplanübersicht" überschrieben — zweite Auflistung mit „Zurück" wird als Kategorieübersicht interpretiert; Annahme)*
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Neu" — groß.
- **Ansicht:** „Neu laden" — groß (Aktion heute nicht vorhanden → Issue); „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff (heute nicht vorhanden → Issue).

### Sparplankategoriedetailansicht — `SavingsPlanCategoryCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein.

### Wertpapierübersicht — `SecuritiesListViewModel`
- **Verwalten:** „Neu" — groß; „Kategorien" — groß.
- **Ansicht:** „Neu laden" — groß; „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff; „Nur aktive"/„Archiviert" (`ToggleActive`) — klein.
- **Berichte:** „Depot-Bericht" (`PortfolioAnalysisReport`) — groß.

### Wertpapierdetailansicht — `SecurityCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein; „Archivieren" — klein.
- **Verknüpfte Informationen:** „Kategorie" — klein (Aktion heute nicht vorhanden → Issue); „Buchungen" (`Postings`) — klein; „Anhänge" — klein; „Kurse" (`Prices`) — klein.
- **Berichte:** „Detaillierte Analyse" (`Performance`) — groß (Umgruppierung aus „Verknüpfte Informationen").

### Wertpapierkategorieübersicht — `SecurityCategoriesListViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Neu" — groß.

### Wertpapierkategoriedetailansicht — `SecurityCategoryCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein.

### Budgetübersicht — `BudgetPurposeListViewModel`
- **Verwalten:** „Neu" — groß; „Kategorien" — groß.
- **Ansicht:** „Neu laden" — groß; „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff.
- **Analyse:** „Vormonat" — klein; „Aktueller Monat" — klein; „Vorjahr" — klein; „Aktuelles Jahr" — klein.
- **Berichte:** „Bericht" (`BudgetReport`) — groß.

### Budgetzweckdetailansicht — `BudgetPurposeCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein.
- **Verknüpfte Informationen:** „Kategorie" — klein.
- **Regeln:** „Neu" — groß (Aktion/Gruppe heute auf dieser Ansicht nicht vorhanden → Issue).

### Budgetzweckregeldetailansicht — `BudgetRuleCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein.

### Budgetbericht — `BudgetReportViewModel`
- **Navigieren:** „Zurück" — groß, nur sichtbar nach Navigation von der Budgetübersicht (`Hidden`-gesteuert; Gruppe heute nicht vorhanden → kontextabhängig erzeugen).
- **Verwalten:** „Einstellungen" (`Settings`) — groß.
- **Analyse:** „Vormonat" — klein; „Aktueller Monat" — klein; „Nächster Monat" — klein; „Vorjahr" — klein; „Nächstes Jahr" — klein.
- **Export:** „Excel exportieren" — klein.

### Benutzerübersicht — `UserListViewModel`
- **Verwalten:** „Neu" — groß.
- **Ansicht:** „Neu laden" — groß (heute nicht vorhanden → Issue); „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff (heute nicht vorhanden → Issue).

### Benutzerdetailansicht — `UserCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Löschen" — klein; „Passwort zurücksetzen" — klein (neu → Issue).

### KI-Zugänge-Übersicht — `McpAccessListViewModel`
- **Verwalten:** „Neu" — groß.
- **Ansicht:** „Neu laden" — groß; „Suche zurücksetzen" — klein, nur aktiviert bei Suchbegriff (heute nicht vorhanden → Issue).
- **Verknüpfte Informationen:** „Genehmigungen" — klein; „Protokoll" (`AuditLog`) — klein; „KI-Sicherheit" (`Security`) — klein.

### KI-Zugang-Detailansicht — `McpAccessCardViewModel`
- **Navigieren:** „Zurück" — groß.
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein (neu → Issue); „Deaktivieren" — klein (neu → Issue); „Zugang widerrufen" — klein (neu → Issue).

### Einstellungen — `SetupCardViewModel`, `SetupBackupsViewModel`, `SetupProfileViewModel`, `SetupMcpViewModel`, `SetupUpdateViewModel`
- **Verwalten:** „Speichern" — groß; „Zurücksetzen" — klein.
- **Daten:** „Aggregatposten neu berechnen" (`RebuildAggregates`) — klein, inkl. Korrektur des nicht übersetzten Hint-/Tooltip-Texts; „Cache zurücksetzen" (`ResetReportCache`) — klein.
- **Profil:** „Zeitzone neu ermitteln" (`DetectTimezone`) — klein.
- **KI:** „Genehmigungen" — klein; „Protokoll" — klein; „Sicherheit" — klein.
- **Programmversion** (Umbenennung der bisherigen Gruppe „Aktualisierungen" — auch das zugehörige Register/Tab umbenennen): „Jetzt prüfen" — klein; „Update installieren" — klein.
- **Sicherungen** (Umbenennung der bisherigen Gruppe „Datensicherungen"): „Backup erstellen" (`CreateBackup`) — klein; „Backup einlesen" (`UploadBackup`) — klein.

## Im selben Issue zu korrigierende Punkte

1. **Inhaltspanel auf Übersichtsseiten:** Die Listenansichten (`GenericListPage.razor`, `ListPage.razor`) besitzen kein Inhaltspanel mehr. Folgen: Inhalte stehen zu nah am aufgeklappten Navigationsmenü, und Menüband sowie Inhalte „springen" beim Wechsel zwischen Übersicht und Detailansicht (die Kartenansichten haben einen eigenen Content-Container). Übersichtsseiten erhalten wieder einen zum Detail-Layout konsistenten Inhaltscontainer, sodass kein Layout-Sprung mehr auftritt.
2. **Löschen-Symbol in der Sicherungs-Tabelle:** Auf der Einstellungen-Seite (`SetupBackupTab.razor`, `button.icon-btn.danger` mit `svg.icon`) ist das Löschen-Symbol unleserlich, weil die rote Icon-/Textfarbe auf rotem Hintergrund liegt. Anforderung: Text/Symbol schwarz, solange die Maus nicht über dem Button steht (Hover-Zustand darf abweichen).
3. **Favicon:** Durch das Redesign wurde das Programmsymbol im Menütitel ersetzt; als Favicon ist noch das alte Symbol (`wwwroot/icons/finance_manager-64x64.ico`) festgelegt. Es ist ein neues, zur neuen Farbgestaltung passendes, nicht generisches Icon vorzuschlagen und mit den nächsten Änderungen zu liefern.

## Anzulegende GitHub-Issues

**Für heute fehlende Menüaktionen** (damit Funktion später implementiert und das Menü vervollständigt werden kann):
- „Zurücksetzen" auf den Detailansichten Kontakt, Sparplan, Sparplankategorie, Wertpapier, Wertpapierkategorie, Budgetzweck, Budgetzweckregel, Benutzer, KI-Zugang (Reset/Discard-Changes-Aktion analog bestehender `Reset`-Aktionen).
- „Neu laden" / „Suche zurücksetzen" auf Sparplankategorieübersicht, Benutzerübersicht und KI-Zugänge-Übersicht (soweit im ViewModel nicht vorhanden).
- „Passwort zurücksetzen" in der Benutzerdetailansicht.
- „Deaktivieren" und „Zugang widerrufen" in der KI-Zugang-Detailansicht.
- „Kategorie" als verknüpfte Information in der Wertpapierdetailansicht.
- „Regeln → Neu" in der Budgetzweckdetailansicht (Regel direkt zum Budgetzweck anlegen).

**Für die bei der Durchsicht aufgefallenen Probleme** (je ein Issue):
1. **Sparplanübersicht – Suche/Datumsfilter:** Die Suche filtert nicht (Liste bleibt vollständig). Der Datumsfilter funktioniert ebenfalls nicht; er muss auf das Zieldatum filtern und bei wiederkehrenden Plänen berücksichtigen, dass der Plan im angegebenen Zeitraum fällig sein kann.
2. **Dynamischer Zurück-Button (Sparplandetail → Kategoriedetail):** Nach Navigation von der Sparplandetailansicht zur Kategoriedetailansicht führt „Zurück" zur Kategorieübersicht statt zur Sparplandetailansicht; der Zurück-Button muss dynamisch zur Herkunftsansicht navigieren.
3. **Wertpapierübersicht – Suche/Datumsfilter:** Die Suche filtert nicht; der Datumsfilter ist zu entfernen.
4. **Budgetzweckregeldetail – Zurück-Button:** Erzeugt eine ungültige Route (Beispiel: von `/card/budget/rules/{id}` nach `/list/budget/card/budget/purposes/{id}` statt zur Budgetzweckdetailansicht).
5. **Analyse-Buttons Budgetübersicht vs. Budgetbericht:** Uneinheitliches Verhalten; verbindliche Semantik — „Vormonat"/„Nächster Monat" verschieben die aktuell gezeigte Periode um je einen Monat in Vergangenheit/Zukunft, „Aktueller Monat" setzt auf den aktuellen Monat zurück, „Vorjahr"/„Nächstes Jahr" verschieben um je ein Jahr.
6. **Benutzerübersicht – Suche/Datumsfilter:** Die Suche filtert nicht; der Datumsfilter ist zu entfernen.
7. **KI-Zugänge-Übersicht – „Neu" bei deaktivierter KI-Integration:** Ist die KI-Integration in den Einstellungen nicht aktiviert, darf die Neuanlage nicht aufrufbar sein (aktuell durchläuft der Anwender den Wizard und scheitert erst am Ende).
8. **KI-Protokollansicht – Zurück-Button:** navigiert auf die ungültige Route `/list/mcp-audit/card/setup` (Anzeige bleibt bei „Laden …" hängen).
9. **KI-Sicherheitsansicht – Zurück-Button:** gleiche ungültige Route `/list/mcp-audit/card/setup` mit hängender Anzeige.
10. **KI-Genehmigungsübersicht – Zurück-Button:** gleiche ungültige Route `/list/mcp-audit/card/setup` mit hängender Anzeige.

## Betroffene Klassen und Komponenten

- **Ribbon-ViewModels (`FinanceManager.Web/ViewModels/`):**
  - `Accounts/BankAccountListViewModels.cs`
  - `Contacts/ContactListViewModel.cs`, `Contacts/ContactCardViewModel.cs`
  - `SavingsPlans/SavingsPlansListViewModel.cs`, `SavingsPlans/SavingsPlanCardViewModel.cs`, `SavingsPlans/Categories/SavingsPlanCategoryListViewModel.cs`, `SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs`
  - `Securities/SecuritiesListViewModel.cs`, `Securities/SecurityCardViewModel.cs`, `Securities/Categories/SecurityCategoriesListViewModel.cs`, `Securities/Categories/SecurityCategoryCardViewModel.cs`
  - `Budget/BudgetPurposeListViewModel.cs`, `Budget/BudgetPurposeCardViewModel.cs`, `Budget/BudgetRuleCardViewModel.cs`, `Budget/BudgetReportViewModel.cs`
  - `Setup/UserListViewModel.cs`, `Setup/UserCardViewModel.cs`, `Setup/SetupCardViewModel.cs`, `Setup/SetupBackupsViewModel.cs`, `Setup/SetupProfileViewModel.cs`, `Setup/SetupMcpViewModel.cs`, `Setup/SetupUpdateViewModel.cs`
  - `Mcp/McpAccessListViewModel.cs`, `Mcp/McpAccessCardViewModel.cs`
- **Ribbon-Infrastruktur:** `ViewModels/Common/RibbonModels.cs` (`UiRibbonAction.Size`, `UiRibbonAction.Hidden`, `UiRibbonAction.MobileShortcut`, `UiRibbonTab.Sort`), `ViewModels/Common/RibbonExtensions.cs`, `Components/Shared/Ribbon*.razor` — voraussichtlich ohne Strukturänderung nutzbar (`Hidden` und `Sort` existieren bereits).
- **Ressourcen:** `FinanceManager.Web/Resources/*.resx` (de + en) — neue Gruppen-Keys (u. a. „Ansicht", ggf. „Daten", „Programmversion", „Sicherungen" sofern nicht vorhanden), Umbenennung der Gruppen-/Tab-Texte „Aktualisierungen" → „Programmversion" und „Datensicherungen" → „Sicherungen", fehlende Übersetzung des Hint-/Tooltip-Texts von „Aggregatposten neu berechnen"; ggf. neue Aktionslabels. Icons weiterhin aus `wwwroot/icons/sprite.svg`.
- **Übersichts-Layout:** `Components/Pages/GenericListPage.razor` und `Components/Pages/ListPage.razor` (Inhaltspanel-Container analog dem Card-Layout, vgl. `.card-view`/`.card-page-content` in `GenericCardPage.razor`/`CardPage.razor`) plus zugehörige Styles (`wwwroot/css/app.css` bzw. `app.*.css`/`theme.Dark.*.css`).
- **Sicherungs-Tabelle:** `Components/Pages/Setup/SetupBackupTab.razor` und die Styles für `.icon-btn.danger` / `.icon` (Textfarbe schwarz im Nicht-Hover-Zustand).
- **Favicon:** `wwwroot/icons/finance_manager-64x64.ico` bzw. referenzierte Icon-Dateien, Verlinkung in `Components/App.razor` und ggf. `wwwroot/manifest.json`.
- **Externe Artefakte:** GitHub-Issues im Projekt-Repository (fehlende Aktionen + zehn Fehlverhalten).
- **Tests:** bestehende Suites müssen grün bleiben; Tests, die Ribbon-Gruppen/-Reihenfolge/-Größen oder `disabled`-/`Hidden`-Zustände assertieren (u. a. E2E-Tests mit Selektoren wie `button#Save`, `#OpenContact`), sind auf die neue Zielkonfiguration zu prüfen und ggf. nachzuziehen.

## Implementierungsansatz

- Pro betroffenem ViewModel `GetRibbonRegisters(...)` auf die Zielkonfiguration bringen: Gruppen in der genannten Reihenfolge als `UiRibbonTab` erzeugen (Reihenfolge über Listenreihenfolge/`Sort`), jede `UiRibbonAction` mit der vorgegebenen `UiRibbonItemSize`, Labels aus resx (de + en), Icons aus `icons/sprite.svg`; die primäre Aktion je Ansicht erhält `MobileShortcut = true` (Projektregel).
- Bedingte Sichtbarkeit über das vorhandene `Hidden`-Flag bzw. bedingte Gruppenerzeugung: „Zurück" auf der Kontenübersicht und dem Budgetbericht nur im Navigationskontext; „Suche zurücksetzen" bleibt sichtbar, aber `Disabled` ohne Suchbegriff (analog bestehender `ClearFilter`-Bedingungen).
- Fehlende Aktionen nicht stubben — nur Issues anlegen; das Menü wird mit den vorhandenen Aktionen bereits in Zielreihenfolge/-größe aufgebaut.
- Inhaltspanel: Listenansichten erhalten denselben Content-Wrapper wie Kartenansichten, damit Layout und Abstände zwischen Übersicht und Detail konsistent sind und kein Sprung entsteht.
- `.icon-btn.danger` in der Sicherungs-Tabelle: Icon-/Textfarbe im Nicht-Hover-Zustand auf schwarz stellen; Hover-Verhalten separat definieren.
- Favicon: neuen Icon-Vorschlag entwerfen (Datei + Einbindung), zur neuen Farbgebung passend.
- Issue-Erstellung: je Punkt ein GitHub-Issue mit Titel, Beschreibung und Repro-Schritten aus dem Kommentar; Bezug zum Ursprungsissue #590 herstellen.

## Konfiguration

Keine zusätzliche Konfigurierbarkeit gefordert — Größen, Reihenfolgen und Sichtbarkeiten sind feste Vorgaben je Ansicht. Lokalisierung weiterhin ausschließlich über resx (de + en).

## Offene Fragen

1. **Doppelte Überschrift „Sparplanübersicht":** Der Kommentar enthält zwei Abschnitte „Sparplanübersicht"; der zweite (mit „Zurück", „Neu laden", „Suche zurücksetzen") wird als „Sparplankategorieübersicht" (`SavingsPlanCategoryListViewModel`) interpretiert — zu bestätigen.
2. **„Zurück" auf der Kontenübersicht:** Gibt es aktuell eine Navigation zur Kontenübersicht, die einen Zurück-Button benötigt (und aus welcher Ansicht)? Falls nein: Button vollständig entfernen statt `Hidden`-Mechanik vorzuhalten?
3. **Budgetbericht-„Zurück":** Wie ist der Navigationskontext technisch zu erkennen (z. B. Herkunfts-Route/Parameter), damit die Gruppe nur bei Navigation von der Budgetübersicht erscheint?
4. **Gruppe „Ansicht":** Soll dafür ein neuer resx-Key (z. B. `Ribbon_Group_View`) eingeführt werden, oder wird die bestehende Gruppe „Filter"/„Manage" umbenannt?
5. **Register-/Tab-Umbenennung „Programmversion":** Neben dem Gruppenlabel ist „auch das Register" umzubenennen — ist damit der Setup-Tab (Navigations-/Tab-Titel der Update-Sektion, `SetupUpdateTab`/`SetupSections`) gemeint?
6. **Favicon-Format:** Genügt eine `.ico`-Datei in den vorhandenen Maßen/Formaten, oder sind zusätzliche PNG-Größen/`manifest.json`-Icons mitzuliefern? Gibt es Vorgaben zum Motiv (Ableitung aus dem neuen Menütitel-Symbol)?
7. **Issue-Erstellung:** Direktes Anlegen via `gh issue create` im Projekt-Repository mit Labels/Milestone — oder Entwürfe zur Freigabe vorlegen?
