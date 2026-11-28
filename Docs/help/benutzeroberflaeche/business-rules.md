← [Zurück zur Übersicht](index.md)

# Benutzeroberfläche — Business Rules

## Verbindliche Ribbon-Standardkonfiguration je Ansicht

**Beschreibung:** Mit Issue #590 (Schritt 2, Variante B) ist für jede Übersichts-, Detail-, Berichts- und Einstellungsansicht festgelegt, welche Ribbon-Gruppen (`UiRibbonTab`) in welcher Reihenfolge erscheinen und ob jede Aktion (`UiRibbonAction`) als großes oder kleines Symbol (`UiRibbonItemSize.Large`/`.Small`) dargestellt wird. Die Regeln wirken bei jedem Rendern des Menübands; Ansichten ohne Nennung sind bewusst nicht geregelt.

**Bedingungen:**
- Die Reihenfolge wird über `UiRibbonTab.Sort` aus einer festen Skala gesteuert: `Navigieren` = 0, `Verwalten` = 10, `Daten` = 15, `Profil` = 20, `Ansicht`/`Filter` = 30, `KI` = 35, `Analyse` = 40, `Felder` = 47, `Verknüpfte Informationen` = 55, `Berichte` = 70, `Export` = 80, `Sicherungen` = `int.MaxValue` (stets letzte Gruppe).
- Die primäre Aktion einer Ansicht trägt `UiRibbonItemSize.Large` und `MobileShortcut = true`; ergänzende Aktionen tragen `UiRibbonItemSize.Small`.
- „Suche zurücksetzen" bleibt sichtbar, ist aber deaktiviert (`Disabled`), solange kein Suchbegriff eingegeben ist.
- Bedingte Sichtbarkeit läuft über bedingte Gruppenerzeugung bzw. das `Hidden`-Flag — siehe Regel „Konditionaler Zurück-Button".
- Labels stammen aus den resx-Ressourcen (de + en), Icons aus `wwwroot/icons/sprite.svg`.
- Gruppen-/Register-Umbenennungen dieses Schritts: `Setup_Section_Update` „Aktualisierungen" → „Programmversion", `SetupBackup_Titel` „Datensicherungen" → „Sicherungen", `Ribbon_SetPassword` „Passwort zuweisen" → „Passwort zurücksetzen"; neue Gruppen-Keys `Ribbon_Group_View` („Ansicht") und `Ribbon_Group_Data` („Daten").

**Verhalten — Zielkonfiguration je Ansicht** (Reihenfolge = Gruppenreihenfolge; `groß`/`klein` = Symbolgröße):

| Ansicht (ViewModel) | Gruppen und Aktionen |
|---|---|
| Kontenübersicht (`BankAccountListViewModel`) | `Navigieren` (nur mit `?back=`-Kontext): Zurück — groß · `Verwalten`: Neu — groß · `Filter`: Suche zurücksetzen — groß, nur aktiv bei Suchbegriff |
| Kontaktübersicht (`ContactListViewModel`) | `Verwalten`: Neu — groß, Gruppen — groß · `Ansicht`: Neu laden — groß, Suche zurücksetzen — klein |
| Kontaktdetail (`ContactCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein, Zusammenführen — klein · `Verknüpfte Informationen`: Buchungen — klein, Anhänge — klein |
| Sparplanübersicht (`SavingsPlansListViewModel`) | `Verwalten`: Neu — groß, Kategorien — groß · `Ansicht`: Neu laden — groß, Suche zurücksetzen — klein, Nur aktive/Archiviert — klein |
| Sparplandetail (`SavingsPlanCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein, Archivieren — klein · `Analyse`: Neu berechnen — groß · `Buchungen`: Istmeldung — groß · `Verknüpfte Informationen`: Kategorie, Buchungen, Anhänge — klein |
| Sparplankategorieübersicht (`SavingsPlanCategoryListViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Neu — groß |
| Sparplankategoriedetail (`SavingsPlanCategoryCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein |
| Wertpapierübersicht (`SecuritiesListViewModel`) | `Verwalten`: Neu — groß, Kategorien — groß · `Ansicht`: Neu laden — groß, Suche zurücksetzen — klein, Nur aktive/Archiviert — klein · `Berichte`: Depot-Bericht — groß |
| Wertpapierdetail (`SecurityCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein, Archivieren — klein · `Verknüpfte Informationen`: Buchungen, Anhänge, Kurse — klein · `Berichte`: Detaillierte Analyse — groß |
| Wertpapierkategorieübersicht (`SecurityCategoriesListViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Neu — groß |
| Wertpapierkategoriedetail (`SecurityCategoryCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein |
| Budgetübersicht (`BudgetPurposeListViewModel`) | `Verwalten`: Neu — groß, Kategorien — groß · `Ansicht`: Neu laden — groß, Suche zurücksetzen — klein · `Analyse`: Vormonat, Aktueller Monat, Nächster Monat, Vorjahr, Nächstes Jahr, Aktuelles Jahr — klein · `Berichte`: Bericht — groß |
| Budgetzweckdetail (`BudgetPurposeCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein · `Verknüpfte Informationen`: Kategorie — klein |
| Budgetzweckregeldetail (`BudgetRuleCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein · `Felder`: Zweckmuster leeren — klein |
| Budgetbericht (`BudgetReportViewModel`) | `Navigieren` (nur mit `?back=`-Kontext): Zurück — groß · `Verwalten`: Einstellungen — groß · `Analyse`: Vormonat, Aktueller Monat, Nächster Monat, Vorjahr, Nächstes Jahr — klein · `Export`: Excel exportieren — klein |
| Benutzerübersicht (`UserListViewModel`) | `Verwalten`: Neu — groß |
| Benutzerdetail (`UserCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Löschen — klein, Passwort zurücksetzen — klein, Deaktivieren bzw. Aktivieren — klein (statusabhängig), Entsperren — klein (nur bei gesperrtem Benutzer) |
| KI-Zugänge-Übersicht (`McpAccessListViewModel`) | `Verwalten`: Neu — groß · `Ansicht`: Neu laden — groß · `Verknüpfte Informationen`: Genehmigungen, Protokoll, KI-Sicherheit — klein |
| KI-Zugang-Detail (`McpAccessCardViewModel`) | `Navigieren`: Zurück — groß · `Verwalten`: Speichern — groß, Token rotieren — klein, Deaktivieren bzw. Aktivieren — klein (statusabhängig), Zugang widerrufen — klein |
| Einstellungen (`SetupCardViewModel`, `SetupProfileViewModel`, `SetupMcpViewModel`, `SetupUpdateViewModel`, `SetupBackupsViewModel`) | `Verwalten`: Speichern — groß, Zurücksetzen — klein · `Daten`: Aggregatposten neu berechnen, Cache zurücksetzen — klein · `Profil`: Zeitzone ermitteln — klein · `KI`: Genehmigungen, Protokoll, KI-Sicherheit — klein (`KI-Sicherheit` nur für Administratoren sichtbar) · `Programmversion`: Jetzt prüfen, Update installieren, Update-Lock zurücksetzen — klein · `Sicherungen`: Backup erstellen, Backup einlesen — klein |

**Umsetzung:** `GetRibbonRegisters(...)`/`GetRibbonRegisterDefinition(...)` der jeweiligen ViewModels baut die `UiRibbonTab`-Liste mit den `Sort`-Werten der Skala; `Ribbon.BuildTabsToRender()` merged registerübergreifend nach Gruppentitel und sortiert stabil nach `Sort` — dadurch kann die Setup-Ansicht ihr Ribbon aus den Registern mehrerer ViewModels zusammensetzen.

> **Offene Ergänzungen:** In der Zielkonfiguration vorgesehene, heute noch nicht implementierte Aktionen (u. a. „Zurücksetzen" auf den Detailansichten, „Neu laden"/„Suche zurücksetzen" auf den Übersichten Sparplankategorie, Benutzer und KI-Zugänge, „Kategorie" im Wertpapierdetail, „Regeln → Neu" im Budgetzweck) werden nicht gestubbt; sie sind als GitHub-Issues #593–#609 erfasst und ergänzen das Menü, sobald sie implementiert sind.

## Konditionaler Zurück-Button auf Übersichts- und Berichtsansichten

**Beschreibung:** Auf der Kontenübersicht und dem Budgetbericht darf die Gruppe „Navigieren" mit der Zurück-Aktion nur erscheinen, wenn die Ansicht aus einem Kontext geöffnet wurde, der eine Rückkehr vorsieht — ein leerer oder funktionsloser Zurück-Button in der regulären Ansicht gilt als Fehlverhalten.

**Bedingungen:**
- Der Navigationskontext wird über den `?back=`-Query-Parameter übergeben und im ViewModel als `InitBack` hinterlegt (`BaseViewModel.SetBackNavigation`).
- Vor der Verwendung wird das Ziel gegen die Anwendungsherkunft geprüft (`NavigationManagerExtensions.IsSameOriginUri`): Nur relative Pfade und absolute URIs auf derselben Herkunft (Scheme + Authority) werden akzeptiert — fremde Origins, scheme-relative URIs, Nicht-http(s)-Schemes sowie Eingaben mit Kontroll-/Whitespace-Zeichen oder Scheme-Präfixen ohne `//` (auch Prozent-kodiert) werden verworfen.

**Verhalten:**
- Ohne `InitBack` wird die Gruppe `Ribbon_Group_Navigation` nicht erzeugt (bedingte Gruppenerzeugung statt verstecktem Eintrag — es entsteht kein leerer Gruppenrahmen).
- Mit gültigem `InitBack` erscheint die Gruppe an Position `Sort: 0` mit der Zurück-Aktion als großem Symbol.
- Wird ein `back`-Ziel abgelehnt, greifen die bisherigen Fallback-Routen (z. B. Liste der jeweiligen Entität).

**Umsetzung:** `BankAccountListViewModel` und `BudgetReportViewModel` (bedingte Gruppenerzeugung), `ListPage`/`CardPage` (Parameter-Bindung, Validierung, `SetBackNavigation`), `NavigationManagerExtensions.IsSameOriginUri` (Open-Redirect-Abwehr).
