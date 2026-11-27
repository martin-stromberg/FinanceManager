# Plan-Gegenprüfung

Geprüft: `plan.md` gegen `requirement.md`, `inventory.md` inkl. Detaildokumente
(`inventory/ribbon-inventory.md`, `inventory/ui-components.md`, `inventory/tests.md`).
Stichproben im Code verifiziert (`Ribbon.razor`, `ListPage.razor`, `BudgetReport.razor`,
`BaseViewModel`/`BaseCardViewModel`, `theme.Dark.css`, `app.css`, betroffene Testklassen).

## Ergebnis

**Status:** Plan lückenhaft

Die fachliche Abdeckung ist sehr gut: Alle 20 Ansichts-Zielkonfigurationen sind in der
Zieltabelle abgebildet (Gruppen, `Sort`-Reihenfolge, `UiRibbonItemSize`, `Disabled`/`Hidden`),
die drei Zusatzkorrekturen sind konkret geplant, die Issue-Liste (6 Aktions-Issues + 10
Fehlverhalten, mit verifizierter „Nicht angelegt"-Begründung für bereits vorhandene Aktionen)
ist vollständig, und die resx-Arbeiten decken alle Lücken der Inventur ab. Zwei Befunde
verhindern den Status „vollständig": der gewählte `Hidden`-Mechanismus erzeugt im Code
nachweislich eine leere, sichtbare Ribbon-Gruppe, und die Liste der betroffenen
Bestandstests ist unvollständig (zwei nachweislich brechende Testklassen fehlen).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Kontenübersicht: Navigieren(Back, Hidden-gesteuert, groß) → Verwalten(Neu L) → Filter(ClearSearch L, disabled ohne Suchbegriff) | Plan-Zieltabelle `BankAccountListViewModels` + `?back=`/`SetBackNavigation` auf `ListPage` | E2E `AccountsBackNavigationPlaywrightTests` (neu) | Lücke — `Hidden` auf einziger Aktion der Gruppe erzeugt leere sichtbare Gruppe (s. „Fehlende Planbestandteile" #1) |
| Kontaktübersicht: Verwalten(Neu L, Gruppen L) → Ansicht(Reload L, ClearSearch S/disabled) | Zieltabelle `ContactListViewModel` + neuer resx-Key `Ribbon_Group_View` | `RibbonGroupOrderPlaywrightTests`-Erweiterung + bUnit-Konfig-Test | Abgedeckt |
| Kontaktdetail: Back L; Verwalten(Save L, Reset→Issue, Delete S, OpenMerge S umgruppiert); Linked(Postings/Attachments S) | Zieltabelle `ContactCardViewModel` | bUnit-Konfig-Test (repräsentative VMs) | Abgedeckt |
| Sparplanübersicht: Verwalten(Neu/Kategorien L) → Ansicht(Reload/ClearSearch→Issue, ToggleActive S) | Zieltabelle `SavingsPlansListViewModel`; fehlende Aktionen → Issue A6 | bUnit + Gruppen-E2E | Abgedeckt |
| Sparplandetail: Back L; Save L(+MS)/Reset→Issue/Delete S/Archive S; Analyse(Recalculate L); Buchungen(ActualReport L); Linked 3×S | Zieltabelle `SavingsPlanCardViewModel` | bUnit (Sort explizit) | Abgedeckt |
| Sparplankategorie-Übersicht/-Detail | Zieltabellen `SavingsPlanCategoryListViewModel` (Reload/ClearSearch→Issue A2, Ansicht-Gruppe nur bei vorhandenen Aktionen) / `…CardViewModel` | bUnit | Abgedeckt |
| Wertpapierübersicht: Verwalten→Ansicht→Berichte, Größenkorrekturen, Actions→Manage-Umbenennung | Zieltabelle `SecuritiesListViewModel` | `RibbonGroupOrderPlaywrightTests` explizit genannt | Abgedeckt |
| Wertpapierdetail: Performance→Berichte L; Linked ohne Category (→Issue A5); Reihenfolge Linked | Zieltabelle `SecurityCardViewModel` | bUnit explizit genannt | Abgedeckt |
| Wertpapierkategorie-Übersicht (beide S→L) / -Detail | Zieltabellen `SecurityCategoriesListViewModel` / `SecurityCategoryCardViewModel` | bUnit | Abgedeckt |
| Budgetübersicht: Navigation-Gruppe aufgeteilt (Verwalten/Ansicht), Analyse 4×S, Berichte(BudgetReport L) | Zieltabelle `BudgetPurposeListViewModel` + `?back=` am `OpenBudgetReport`-Ziel | E2E `BudgetReportBackNavigationPlaywrightTests` | Abgedeckt |
| Budgetzweckdetail inkl. Regeln→Neu (existiert via Kind-VM `BudgetRuleListViewModel`, Sort=60) | Zieltabellen `BudgetPurposeCardViewModel` + `BudgetRuleListViewModel`; Verifikation `IsChildViewModelActive` eingeplant (Offener Punkt 3) | bUnit | Abgedeckt |
| Budgetzweckregeldetail (Back-Route defekt → Issue B4, nicht fixen; Felder-Gruppe bleibt) | Zieltabelle `BudgetRuleCardViewModel` | bUnit | Abgedeckt |
| Budgetbericht: neue konditionale Navigieren-Gruppe (Back L, Hidden); Settings S→L; Analyse 5×S; Export S | Zieltabelle `BudgetReportViewModel` + `BudgetReport.razor`-`?back=`/`case "Back"` | E2E `BudgetReportBackNavigationPlaywrightTests` | Lücke — gleiches Empty-Group-Problem bei Direktaufruf (#1); ansonsten korrekt |
| Benutzerübersicht/-detail (Reload/ClearSearch→Issue A3; `SetPassword` existiert → Labelkorrektur statt Issue; konditionale Aktionen bleiben) | Zieltabellen `UserListViewModel`/`UserCardViewModel` + resx `Ribbon_SetPassword` | Szenario 10 (E2E-Gruppen-Header) | Lücke — Nachweis zu Szenario 10 in keiner geplanten Testdatei verankert (#4) |
| KI-Zugänge-Übersicht (Aufteilung Verwalten/Ansicht/Linked; ClearSearch→Issue A4; New-Sperre→Issue B7) / KI-Zugang-Detail (Deactivate/Revoke existieren → kein Issue) | Zieltabellen `McpAccessListViewModel`/`McpAccessCardViewModel` | bUnit | Abgedeckt |
| Einstellungen: Daten-Gruppe neu (RebuildAggregates/ResetReportCache S + Tooltip-Fix), Profil, KI (alle S), Programmversion-Umbenennung (Gruppe+Tab), Sicherungen-Umbenennung, Create/UploadBackup L→S | Zieltabellen `SetupCardViewModel` + 4 Sektions-VMs; resx-Block (neu: `Ribbon_Group_Data`, `Hint_RebuildAggregates`; geändert: `Setup_Section_Update`, `SetupBackup_Titel`) | bUnit-Sort-Test + Szenario 8 (E2E Gruppenreihenfolge + Tooltip-Assertion) | Abgedeckt |
| Korrektur Inhaltspanel Übersichtsseiten | `.card-page-content`-Wrapper in `ListPage.razor` (Begründung gegen `GenericListPage`: eingebettete Nutzungen in `SetupMcpTab`/`GenericCardPage` — verifiziert korrekt) | E2E `ListPageContentPanelPlaywrightTests` mit BoundingBox-Assertion | Abgedeckt |
| Korrektur `.icon-btn.danger` (schwarz im Nicht-Hover) | `button.icon-btn.danger { color:#000 }` in `theme.Dark.css`; Spezifitätsargumentation gegen `theme.Dark.css` L140/L186–193 verifiziert korrekt | E2E `SetupBackupTabPlaywrightTests` (CSS-Assertion) | Abgedeckt |
| Favicon: neues Motiv + Einbindung | Konkreter Motiv-Vorschlag, SVG-Master + Pillow-Generator, `App.razor`-Link + `manifest.json`-Farben | E2E-Check `<link rel="icon">`/HTTP 200 (optional) + Sichtprüfung | Abgedeckt |
| Issue-Anlage: nur tatsächlich fehlende Aktionen + 10 Fehlverhalten | Liste A1–A6 (inkl. dokumentierter Ergänzung A6) + B1–B10 vollständig; „Nicht angelegt" für `NewRule`/`SetPassword`/`Deactivate`/`Revoke` verifiziert zutreffend (Inventar + Code) | Szenario 9 via `gh issue list` | Abgedeckt (s. Hinweis zu „5 vs. 6 Issues" in Schritt 9) |
| Scope-Ausschlüsse (keine Implementierung fehlender Aktionen/Fehlerkorrekturen; unbenannte Ansichten unverändert; keine Verhaltensänderung außer `?back=`-Ausnahme; stabile Action-IDs) | Scope-Ausschluss-Tabelle, alle mit Status „bestätigt" + Begründung | — | Abgedeckt |
| Scope-Klärung / Akzeptanzszenarien / Happy Path | Alle vier Pflichtfelder gefüllt; 10 Szenarien inkl. vollständigem Happy Path (Szenario 1) und Ausgangszuständen (u. a. fehlende Objekte: Szenario 10) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] `FinanceManager.Tests/Web/ViewModels/ContactsViewModelTests.cs` → `GetRibbon_UsesManageGroup_WithAllActionsLarge` (L168–183): assertiert `Assert.Single(tabs)` und `All(a => Size == Large)` — bricht durch die Aufteilung in „Verwalten"+„Ansicht" und `ClearFilter` L→S. Nicht in „Betroffene bestehende Tests" gelistet (Akzeptanzkriterium Kontaktübersicht).
- [ ] `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`: `GetRibbonRegisters_AfterLoad_PlacesManageGroupFirst` (L227–245) assertiert `Sort == 0` für `Ribbon_Group_Manage` (Plan setzt 10) und `GetRibbonRegisters_AfterLoad_UsesExpectedActionSizes` (L252–271) assertiert `RebuildAggregates` innerhalb der Manage-Gruppe (Plan verschiebt es in die neue „Daten"-Gruppe) — beide Tests brechen nachweislich. Ebenfalls nicht gelistet (Akzeptanzkriterium Einstellungen).
- [ ] Nachweis zu Akzeptanzszenario 10 („kein `Ansicht`-Gruppen-Header auf `UserListViewModel`") ist als „E2E" deklariert, aber keiner Testdatei/-klasse im E2E-Testabschnitt zugeordnet (liefe vermutlich in der `RibbonGroupOrderPlaywrightTests`-Erweiterung mit — explizit zu benennen oder als bUnit-VM-Test auszuweisen).
- [ ] Prüf-/Anpassungsbedarf bei weiteren VM-Tests mit Ribbon-Assertions ist nicht abgedeckt bzw. nicht kategorisiert: `SavingsPlanCardLinkTests.GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (L333–353, assertiert exakte Tab-Titelfolge — vermutlich weiterhin gültig, aber zu verifizieren), `SecurityCategoryDetailViewModelTests` (L208–223), `SecurityCategoriesViewModelTests` (L98), `SecurityCardViewModelTests` (L22), `SetupUpdateViewModelTests` (L132). Eine Einstufung „bleibt gültig / anzupassen" fehlt.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zielkonfiguration sichtbar (Gruppen, Reihenfolge, Größen) je Ansicht | `RibbonGroupOrderPlaywrightTests` (anpassen/erweitern, Szenario 1) | Abgedeckt |
| „Suche zurücksetzen" disabled↔enabled durch UI-Eingabe | Erweiterung `AccountsOverviewStatisticsPlaywrightTests` (Szenario 2) | Abgedeckt |
| Konditionales Zurück Kontenübersicht (ohne/mit `?back=`, Klick navigiert) | `AccountsBackNavigationPlaywrightTests` (neu, Szenario 3) | Abgedeckt |
| Konditionales Zurück Budgetbericht (Kette `list/budget` → Bericht → zurück; Direktaufruf ohne Back) | `BudgetReportBackNavigationPlaywrightTests` (neu, Szenario 4) | Abgedeckt |
| Inhaltspanel ohne Layout-Sprung | `ListPageContentPanelPlaywrightTests` (neu, BoundingBox, Szenario 5) | Abgedeckt |
| `.icon-btn.danger`-Kontrast | `SetupBackupTabPlaywrightTests` (neu, CSS-Assertion, Szenario 6) | Abgedeckt |
| Favicon-Einbindung | Optionaler E2E-Check `<link rel="icon">`/HTTP 200 (Szenario 7) | Abgedeckt (optional ausgewiesen, ausreichend) |
| Setup-Gruppenreihenfolge + Tooltip-Übersetzung | Gruppen-E2E + Tooltip-Assertion (Szenario 8) | Abgedeckt |
| Fehlende Aktionen ohne leere Gruppe (`UserListViewModel`) | Szenario 10 nennt E2E, aber keine Testdatei | Lücke (Zuordnung fehlt, s. o.) |
| GitHub-Issue-Anlage | `gh issue list`-Verifikation (Szenario 9) | Nicht erforderlich als E2E — kein UI-Fluss, Nachweis vorhanden |
| Screenshot-Abnahme | Umsetzungsschritt 10 (`generate-screenshots.js` + gezielte Shots) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Leere sichtbare Gruppe durch `Hidden`-Mechanismus (technischer Defekt):** `Ribbon.razor` L183 überspringt nur Tabs mit `Items.Count == 0` — die `Hidden`-Filterung erfolgt erst danach (L196) beim Befüllen der bereits angelegten Gruppe. Ein Tab, dessen einzige Aktion `Hidden = true` ist, erzeugt eine `RibbonGroup` mit 0 Items, die mit sichtbarem Gruppenrahmen + Footer-Titel (`@g.Title`, L82) gerendert wird — kein `:empty`-/Count-Handling im Markup (L60–144) oder CSS. Betrifft die geplante `Hidden`-`Back`-Aktion in `BankAccountListViewModels` (regulärer Fall = häufigste Ansicht der Kontenübersicht!) und `BudgetReportViewModel` (Direktaufruf). Der Plan verwirft „bedingte Gruppenerzeugung" explizit mit der falschen Begründung, `Hidden` erreiche „denselben Effekt". Erforderlich: entweder die Navigieren-`UiRibbonTab` VM-seitig nur bei gesetztem `InitBack` erzeugen, oder `Ribbon.razor` verwirft Gruppen ohne sichtbare Items (nicht geplant). Auch die geplanten E2E-Tests (Szenarien 3/4) würden den leeren Gruppenrahmen nicht als Fehler erfassen — eine Assertion „keine leere Gruppe" fehlt.
- [ ] **Betroffene Bestandstests unvollständig:** `ContactsViewModelTests` und `SetupCardViewModelTests` brechen nachweislich (s. Testabschnitt oben), fehlen aber in „Betroffene bestehende Tests".
- [ ] **Inkonsistenz Issue-Anzahl:** Umsetzungsreihenfolge Schritt 9 nennt „5 Aktions-Issues", die Plan-Liste definiert jedoch 6 (A1–A6) und Akzeptanzszenario 9 rechnet korrekt mit 16 Issues — Schritt 9 zu korrigieren.
- [ ] **Abhängigkeit `?back=` ↔ `InitBack`-Timing auf `ListPage` nicht explizit:** Der Plan sieht den `SetBackNavigation`-Aufruf in `OnParametersSetAsync` „nach `_provider`-Zuweisung, vor `InitializeAsync`" vor; bei reinem Query-Wechsel (`?back=` wird nachträglich angehängt/entfernt, ohne dass `Kind`/`SubKind` wechseln) wird `OnParametersSetAsync` erneut ausgeführt, aber `_provider` und Init-Status — ob ein erneutes `SetBackNavigation`/`InitializeAsync` den Ribbon-Zustand korrekt aktualisiert (Gruppe erscheint/verschwindet zur Laufzeit), ist nicht behandelt. Mindestens als Risiko/Verifikationsschritt zu benennen.
- [ ] **Annahmen-Widerspruch zu Aktionsreihenfolge (klein):** Die Annahme „ungenannte konditionale Zusatzaktionen sortieren hinter den genannten Aktionen ein" steht im Widerspruch zur `McpAccessCardViewModel`-Zieltabelle, die `RotateToken` (ungenannt) vor `Deactivate`/`Revoke` (genannt) platziert. Entweder Tabelle oder Annahme präzisieren.

## Hinweise

- Sort-Konvention (0/10/15/20/30/35/40/45/47/50/55/60/70/80/MaxValue) deckt alle 20 Zielreihenfolgen konfliktfrei ab; Doppelbelegung Ansicht/Filter = 30 ist sicher (nie gleichzeitig). Setup-Reihenfolge Verwalten→Daten→Profil→KI→Programmversion→Sicherungen korrekt.
- `?back=`/`SetBackNavigation`/`InitBack`-Umzug von `BaseCardViewModel` nach `BaseViewModel` ist im Bestand sauber vorbereitet (`ICardInitializable`-Signatur bleibt erfüllt); `OpenBudgetReport`-Ziel `reports/budget?back=<Nav.Uri>` entspricht dem etablierten Muster (`BaseViewModel` L354–357).
- CSS-Fix `button.icon-btn.danger` (0,2,1) schlägt `button.danger` (0,1,1) für `color`; Hover-Regel `.icon-btn.danger:hover:not(:disabled)` (0,3,1) bleibt wirksam — Spezifitätsargumentation verifiziert.
- Inhaltspanel-Ansatz (Wrapper in `ListPage.razor` statt `GenericListPage.razor`) ist korrekt begründet — eingebettete Nutzungen in `SetupMcpTab` und `GenericCardPage` bestätigt.
- Kleinigkeit `McpAccessListViewModel`: Zieltabelle nennt `Security` mit `dis: !IsAdmin` — Inventar bestätigt `Disabled` (nicht `Hidden`), konsistent.
- `BudgetPurposeListViewModel`: Der bisherige Gruppen-Titel war `Ribbon_Group_Navigation` für New/Reload/Categories/ClearFilter; die Umbenennung/Aufteilung ist beschrieben — bei der Umsetzung darauf achten, dass kein E2E-/bUnit-Test den alten Titel dort erwartet (in `RibbonGroupOrderPlaywrightTests` mit prüfen).
