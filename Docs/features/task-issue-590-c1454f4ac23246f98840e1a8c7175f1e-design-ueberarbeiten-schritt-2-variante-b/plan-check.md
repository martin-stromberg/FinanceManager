# Plan-Gegenprüfung

Geprüft: `plan.md` (Rev. 2) gegen `requirement.md`, `inventory.md` inkl. Detaildokumente
(`inventory/ribbon-inventory.md`, `inventory/ui-components.md`, `inventory/tests.md`) sowie
`plan-check.1.md`. Stichproben im Code erneut verifiziert (`Ribbon.razor` L166–253,
`ListPage.razor` L134–157/L187–226, `BudgetReport.razor` L21–50, `BaseViewModel`/
`BaseCardViewModel`/`ICardInitializable`, `RibbonModels.cs` L93–107, `RibbonExtensions.cs`,
`theme.Dark.css` L140–193, `TestUserSeeder.cs`, `resx`-Keys, alle genannten Bestandstests).

## Ergebnis

**Status:** Plan vollständig

Alle vier Lücken aus `plan-check.1.md` wurden korrekt geschlossen, ohne neue Widersprüche:

1. **Leere sichtbare Gruppe (Befund #1):** Der Plan nutzt jetzt **bedingte Gruppenerzeugung**
   (Navigieren-`UiRibbonTab` wird in `BankAccountListViewModels`/`BudgetReportViewModel` nur bei
   gesetztem `InitBack` emittiert) plus defensivem `groups.RemoveAll(g => g.Items.Count == 0)`
   in `Ribbon.razor`. Code-Verifikation bestätigt die Begründung: `Ribbon.razor` L183 prüft
   `tab.Items.Count` vor der `Hidden`-Filterung (L196) — ein Hidden-only-Tab erzeugte bislang
   eine leere `RibbonGroup`. Die Nebenwirkungsfreiheit des neuen Filters ist belegt: Kein
   Bestands-VM liefert eine ausschließlich versteckte Gruppe
   (`StatementDraftCardViewModel` L375–395: QuickEdit-Trio wechselseitig versteckt, mind. 1 Item
   stets sichtbar; `SetupMcpViewModel` L245: `McpSecurity` teilt die KI-Gruppe mit immer
   sichtbaren `McpApprovals`/`McpAudit`). Kein `RibbonTests`-Fact rendert Hidden-only-Tabs.
2. **Bestandstest-Liste (Befund #2):** Alle nachweislich brechenden Tests sind jetzt genannt —
   `ContactsViewModelTests.GetRibbon_UsesManageGroup_WithAllActionsLarge` (verifiziert:
   `Assert.Single(tabs)` + `All(Large)` bricht bei Aufteilung/`ClearFilter`→S),
   `SetupCardViewModelTests` L227–245 (`Sort == 0` bricht bei Verwalten=10) und L252–271
   (`RebuildAggregates` nicht mehr in Manage-Gruppe → `KeyNotFoundException`), ergänzend
   `AccountsViewModelTests.SetFilter_AffectsLoad_AndRibbon` (verifiziert: `UiRibbonRegister.Items`
   = `Tabs[0].Items`, `RibbonModels.cs` L107 — `ClearSearch` wandert in zweiten Tab),
   `BudgetReportDemoDataE2ETests` L79 (Glob `**/reports/budget` matcht `?back=` nicht —
   verifiziert) und `UsersViewModelTests` (Schärfung). Die „verifiziert weiterhin gültig"-Liste
   wurde stichprobenartig nachgeprüft und stimmt (u. a. `SavingsPlanCardLinkTests` L333–353:
   Tab-Reihenfolge unverändert; `SecurityCategoryDetailViewModelTests` L197–227:
   `groups.Count == 2` bleibt; `GetRibbon_ContainsNew`: `New` liegt ohne `InitBack` in
   `Tabs[0]` = Verwalten).
3. **E2E-Zuordnung Szenario 10 (Befund #3):** Explizit verankert — neuer Fact
   `UsersList_RendersNoEmptyViewGroup` in `RibbonGroupOrderPlaywrightTests` (Admin-Login via
   `TestUserSeeder.EnsureUserAsync(isAdmin: true)` — Signatur verifiziert, `isAdmin`-Parameter
   existiert) plus bUnit-Schärfung in `UsersViewModelTests`.
4. **Issue-Anzahl (Befund #4):** Schritt 9 nennt jetzt konsistent „6 Aktions-Issues (A1–A6) +
   10 Fehlverhalten-Issues (B1–B10) = 16 Issues" — deckt sich mit der Liste und Szenario 9.

Zusätzlich behoben: `?back=`/`InitBack`-Timing ist jetzt explizit begründet (verifiziert:
`ListPage.OnParametersSetAsync` erzeugt den Provider via `factory.Create` bei **jedem**
Parameterwechsel neu — reine Query-Wechsel sind abgedeckt; `BudgetReport.razor` erzeugt `_vm`
nur in `OnInitialized`, `OnParametersSet` läuft danach und bei Query-Wechsel erneut — korrekte
Blazor-Reihenfolge), und der Annahmen-Widerspruch zur Aktionsreihenfolge ist aufgelöst
(„bisherige relative Position" mit konkreten Vorgaben für `RotateToken`/`SetPassword`, konsistent
zur Zieltabelle).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Kontenübersicht: Navigieren(Back groß, konditional) → Verwalten(Neu L) → Filter(ClearSearch L, disabled ohne Suchbegriff) | Zieltabelle `BankAccountListViewModels`: Navigieren-Tab nur bei `InitBack` erzeugt; `?back=`/`SetBackNavigation` auf `ListPage`; `case "Back"` → `Nav.NavigateTo(Back)` | E2E `AccountsBackNavigationPlaywrightTests` (inkl. Negativ-Assertion „keine leere Gruppe") + VM-Test `InitBack`-Bedingung | Abgedeckt |
| Kontaktübersicht: Verwalten(Neu L, Gruppen L) → Ansicht(Reload L, ClearSearch S/disabled) | Zieltabelle `ContactListViewModel` + neuer resx-Key `Ribbon_Group_View` | `RibbonGroupOrderPlaywrightTests`-Erweiterung + bUnit-Konfig-Test; Bestandstest-Anpassung benannt | Abgedeckt |
| Kontaktdetail: Back L; Verwalten(Save L, Reset→Issue, Delete S, OpenMerge S umgruppiert); Linked(Postings/Attachments S) | Zieltabelle `ContactCardViewModel` | bUnit-Konfig-Test (repräsentative VMs) | Abgedeckt |
| Sparplanübersicht: Verwalten(Neu/Kategorien L) → Ansicht(Reload/ClearSearch→Issue, ToggleActive S) | Zieltabelle `SavingsPlansListViewModel`; fehlende Aktionen → Issue A6; ToggleActive aus Filter-Register in Ansicht | bUnit + Gruppen-E2E | Abgedeckt |
| Sparplandetail: Back L; Save L(+MS)/Reset→Issue/Delete S/Archive S; Analyse(Recalculate L); Buchungen(ActualReport L); Linked 3×S | Zieltabelle `SavingsPlanCardViewModel` | bUnit; Bestandstest `PlacesLinkedLast` verifiziert gültig | Abgedeckt |
| Sparplankategorie-Übersicht (Navigieren+Verwalten; Ansicht erst mit Aktionen → Issue A2) / -Detail (Reset→Issue) | Zieltabellen `SavingsPlanCategoryListViewModel`/`…CardViewModel` | bUnit | Abgedeckt |
| Wertpapierübersicht: Verwalten→Ansicht→Berichte; Actions→Manage; Größenkorrekturen | Zieltabelle `SecuritiesListViewModel` | `RibbonGroupOrderPlaywrightTests` explizit | Abgedeckt |
| Wertpapierdetail: Performance→Berichte L; Linked ohne Category (→Issue A5); Reihenfolge | Zieltabelle `SecurityCardViewModel` | bUnit | Abgedeckt |
| Wertpapierkategorie-Übersicht (beide S→L) / -Detail (Reset→Issue) | Zieltabellen `SecurityCategoriesListViewModel`/`SecurityCategoryCardViewModel` | bUnit; `groups.Count==2`-Bestandstest verifiziert gültig | Abgedeckt |
| Budgetübersicht: Verwalten/Ansicht-Aufteilung, Analyse 4×S, Berichte(BudgetReport L) | Zieltabelle `BudgetPurposeListViewModel` + `?back=` am `OpenBudgetReport`-Ziel | E2E `BudgetReportBackNavigationPlaywrightTests`; `BudgetReportDemoDataE2ETests`-Anpassung benannt | Abgedeckt |
| Budgetzweckdetail inkl. Regeln→Neu (existiert via Kind-VM `BudgetRuleListViewModel`, Sort=60) | Zieltabellen `BudgetPurposeCardViewModel` + `BudgetRuleListViewModel`; Sichtbarkeits-Verifikation (`IsChildViewModelActive`) als Offener Punkt 3 eingeplant | bUnit | Abgedeckt |
| Budgetzweckregeldetail (Back-Route → Issue B4; Felder-Gruppe bleibt) | Zieltabelle `BudgetRuleCardViewModel` | bUnit | Abgedeckt |
| Budgetbericht: konditionale Navigieren-Gruppe (Back L); Settings S→L; Analyse 5×S; Export S | Zieltabelle `BudgetReportViewModel` + `BudgetReport.razor` `?back=`/`OnParametersSet`/`case "Back"` — bedingte Gruppenerzeugung statt `Hidden` | E2E `BudgetReportBackNavigationPlaywrightTests` inkl. Direktaufruf-Negativfall | Abgedeckt |
| Benutzerübersicht (Reload/ClearSearch→Issue A3, keine leere Ansicht-Gruppe) / -detail (SetPassword existiert → Labelkorrektur; konditionale Aktionen hinten) | Zieltabellen `UserListViewModel`/`UserCardViewModel` + resx `Ribbon_SetPassword` | E2E `UsersList_RendersNoEmptyViewGroup` (Szenario 10) + bUnit-Schärfung | Abgedeckt |
| KI-Zugänge-Übersicht (Verwalten/Ansicht/Linked; ClearSearch→Issue A4; New-Sperre→Issue B7) / KI-Zugang-Detail (Deactivate/Revoke existieren → kein Issue) | Zieltabellen `McpAccessListViewModel`/`McpAccessCardViewModel` | bUnit | Abgedeckt |
| Einstellungen: Daten-Gruppe neu (+ Tooltip-Fix `Hint_RebuildAggregates`), Profil, KI (alle S), Programmversion-/Sicherungen-Umbenennung, Create/UploadBackup L→S | Zieltabellen `SetupCardViewModel` + 4 Sektions-VMs; resx-Block; Sort-Konfiguration (10/15/20/35/50/Max) deckt Zielreihenfolge ab | bUnit-Sort-Test + Szenario 8 (E2E Gruppenreihenfolge + Tooltip-Assertion); Bestandstest-Anpassungen benannt | Abgedeckt |
| Korrektur Inhaltspanel Übersichtsseiten | `.card-page-content`-Wrapper in `ListPage.razor` (korrekt: eingebettete `GenericListPage`-Nutzungen in `SetupMcpTab`/`GenericCardPage` bleiben ohne Doppel-Panel) | E2E `ListPageContentPanelPlaywrightTests` mit BoundingBox-Vergleich | Abgedeckt |
| Korrektur `.icon-btn.danger` (schwarz im Nicht-Hover) | `button.icon-btn.danger { color:#000 }` in `theme.Dark.css` — Spezifität (0,2,1) schlägt `button.danger` (0,1,1); Hover (0,3,0+) bleibt wirksam — verifiziert | E2E `SetupBackupTabPlaywrightTests` (CSS-Assertion) | Abgedeckt |
| Favicon: neues Motiv + Einbindung | Konkreter Motiv-Vorschlag (F-Chart auf dunkler Kachel, Teal/Violett), SVG-Master + Pillow-Generator, `App.razor`-Link (verifiziert L8) + `manifest.json`-Farben | E2E-Check `<link rel="icon">`/HTTP 200 (optional) + Screenshot-Sichtprüfung | Abgedeckt |
| Issue-Anlage: 6 Aktions-Issues + 10 Fehlverhalten; „Nicht angelegt"-Begründung für `NewRule`/`SetPassword`/`Deactivate`/`Revoke` (verifiziert vorhanden) | Liste A1–A6 + B1–B10 vollständig, Schritt 9 konsistent auf 16 | Szenario 9 via `gh issue list` | Abgedeckt |
| Scope-Ausschlüsse (keine Implementierung fehlender Aktionen/Fehlerkorrekturen; unbenannte Ansichten unverändert; keine Verhaltensänderung außer `?back=`-Ausnahme; stabile Action-IDs) | Scope-Ausschluss-Tabelle: alle 5 Einträge mit Status „bestätigt" + Begründung | — | Abgedeckt |
| Scope-Klärung / Akzeptanzszenarien / Happy Path | Alle vier Pflichtfelder gefüllt; 10 Szenarien inkl. vollständigem Happy Path (Szenario 1) und Ausgangszuständen ohne erwartete Objekte (Szenarien 3, 4, 10) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— keine —

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zielkonfiguration sichtbar (Gruppen, Reihenfolge, Größen) je Ansicht | `RibbonGroupOrderPlaywrightTests` (anpassen/erweitern; Kontaktliste, Wertpapierliste, Einstellungen, Benutzerliste) | Abgedeckt |
| „Suche zurücksetzen" disabled↔enabled durch UI-Eingabe | Erweiterung `AccountsOverviewStatisticsPlaywrightTests` (Szenario 2) | Abgedeckt |
| Konditionales Zurück Kontenübersicht (ohne/mit `?back=`; keine leere Gruppe; Klick navigiert) | `AccountsBackNavigationPlaywrightTests` (neu, Szenario 3) | Abgedeckt |
| Konditionales Zurück Budgetbericht (Kette `list/budget` → Bericht → zurück; Direktaufruf ohne Back-Gruppe) | `BudgetReportBackNavigationPlaywrightTests` (neu, Szenario 4) | Abgedeckt |
| Inhaltspanel ohne Layout-Sprung | `ListPageContentPanelPlaywrightTests` (neu, BoundingBox, Szenario 5) | Abgedeckt |
| `.icon-btn.danger`-Kontrast | `SetupBackupTabPlaywrightTests` (neu, CSS-Assertion, Szenario 6) | Abgedeckt |
| Favicon-Einbindung | Optionaler E2E-Check `<link rel="icon">`/HTTP 200 + Sichtprüfung (Szenario 7) | Abgedeckt (ausreichend — Asset-Lieferung, kein Interaktionsfluss) |
| Setup-Gruppenreihenfolge + Tooltip-Übersetzung | Gruppen-E2E + Tooltip-Assertion (Szenario 8) | Abgedeckt |
| Fehlende Aktionen ohne leere Gruppe (`UserListViewModel`) | `RibbonGroupOrderPlaywrightTests` Fact `UsersList_RendersNoEmptyViewGroup` + bUnit `UsersViewModelTests` (Szenario 10) | Abgedeckt |
| GitHub-Issue-Anlage | `gh issue list`-Verifikation (Szenario 9) | Nicht erforderlich als E2E — kein UI-Fluss, Nachweis vorhanden |
| Screenshot-Abnahme | Umsetzungsschritt 10 (`generate-screenshots.js` + gezielte Shots) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

— keine —

## Hinweise

- **Nachverifikation der vier Lücken aus Runde 1:** Die Nachbesserungen schließen die Lücken
  korrekt und erzeugen keine neuen Widersprüche. Die `Hidden`-Verwerfung zugunsten bedingter
  Gruppenerzeugung entspricht der Requirement-Formulierung („`Hidden` bzw. bedingte
  Gruppenerzeugung"); der zusätzliche `Ribbon.razor`-Filter ist abgesichert (neuer
  `HiddenOnlyGroup_IsNotRendered`-Fact + verifizierte Nebenwirkungsfreiheit).
- **`ListNavigationPlaywrightTests.MobileRibbonShortcuts…`** ist in der
  „verifiziert unbetroffen"-Liste nicht explizit aufgeführt, bleibt aber gültig (`#New` behält
  `MobileShortcut` in Verwalten auf allen vier geprüften Listen — verifiziert). Bei der
  Umsetzung kurz gegenprüfen, nicht zwingend eine Planlücke.
- **`?back=`-Quelle zur Kontenübersicht:** Der Plan liefert den Mechanismus (`?back=` +
  bedingte Gruppe), verdrahtet aber keine konkrete Navigation, die `?back=` auf
  `/list/accounts` setzt. Deckt sich mit der Requirement-Formulierung („falls eine Navigation
  … existiert"); falls gewünscht, kann der E2E-Test oder der Abschlusskommentar die Quelle
  konkretisieren.
- **`UiRibbonRegister.Items`-Kompatibilitätsfalle:** Mehrere Bestandstests lesen über den
  Kompat-Accessor nur `Tabs[0].Items` (L107). Bei jeder weiteren Aufteilung von Tabs ist das
  Muster `g.Items.Any(...)` ein Brechungsrisiko — der Plan fängt den bekannten Fall
  (`SetFilter_AffectsLoad_AndRibbon`) ab; bei Umsetzung auf weitere Vorkommen achten.
- **Sort-Konvention** (0/10/15/20/30/35/40/45/47/50/55/60/70/80/MaxValue) deckt alle 20
  Zielreihenfolgen konfliktfrei ab; Doppelbelegung Ansicht/Filter = 30 ist sicher
  (treten nie gemeinsam auf — verifiziert).
- **`manifest.json`-/`App.razor`-Einbindung** verifiziert: `.ico`-Link L8 bleibt als Fallback,
  `finance_manager.png` wird neu erzeugt, `theme_color` #ff5722 → #080a0f dokumentiert.
