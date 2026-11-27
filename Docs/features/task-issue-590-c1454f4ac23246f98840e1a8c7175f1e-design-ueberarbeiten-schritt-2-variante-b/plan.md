# Umsetzungsplan: Menüband-Aktionen — Standardgröße und Reihenfolge je Ansicht + Korrekturen (Issue #590, Schritt 2, Variante B)

## Übersicht

Die Ribbon-Konfiguration von 20 Ansichten wird auf eine verbindliche Zielkonfiguration (Gruppen, Reihenfolge, `UiRibbonItemSize`, `Disabled`-/`Hidden`-Bedingungen) gebracht. Ergänzend werden drei Fehler korrigiert (fehlendes Inhaltspanel auf Übersichtsseiten, unleserliches Löschen-Symbol in der Sicherungs-Tabelle, neuer Favicon-Vorschlag) und für nachweislich fehlende Ribbon-Aktionen sowie zehn gemeldete Fehlverhalten GitHub-Issues angelegt. Es gibt keine Datenbankänderungen; die Lokalisierung erfolgt über `Resources/Pages.*.resx` (de + en + neutral).

## Scope-Klärung

||| Feld | Festlegung |
|||------|------------|
||| Verarbeitungseinheit | Die Ribbon-Konfiguration einer Ansicht: die von `IRibbonProvider.GetRibbonRegisters(IStringLocalizer)` des jeweiligen ViewModels gelieferten `UiRibbonTab`-Gruppen samt `UiRibbonAction`-Einträgen (`Id`, Label, `UiRibbonItemSize`, `Disabled`, `Hidden`, `MobileShortcut`, `Sort`) |
||| Auslösende Ereignisse | Kein einmaliges Ereignis — die Festlegung wirkt bei jedem Rendern des Menübands (`Ribbon.razor` → `BuildTabsToRender`). Bedingte Elemente reagieren auf Ansichtszustand: „Zurück" via **bedingter Gruppenerzeugung** (`?back=` → `InitBack`), „Suche zurücksetzen" via `Disabled = string.IsNullOrWhiteSpace(Search)` |
||| Erwartete Wirkung | Jede genannte Ansicht zeigt exakt die gelisteten Gruppen in der gelisteten Reihenfolge und Größe; keine leeren sichtbaren Gruppen; Übersichtsseiten erhalten einen `.card-page-content`-konsistenten Inhaltscontainer (kein Layout-Sprung Übersicht ↔ Detail); das Löschen-Symbol in der Sicherungs-Tabelle ist im Nicht-Hover-Zustand schwarz lesbar; ein neuer Favicon liegt als Datei vor und ist eingebunden; die GitHub-Issues existieren im Repository |
||| Umgang mit fehlenden Objekten | **Fehlende Aktionen** (`Reset`/`Zurücksetzen` auf 9 Detailansichten, `Reload`/`ClearSearch` auf 5 Listenansichten, `Category`-Link in `SecurityCardViewModel`): nicht implementieren — je fehlender Aktionsart ein GitHub-Issue; eine Gruppe wird nur erzeugt, wenn mindestens eine vorhandene Aktion hineingehört (kein `UiRibbonTab` ohne sichtbare Aktionen wird emittiert). **Konditionaler „Zurück"-Kontext** auf `ListPage`/`BudgetReport.razor`: `?back=`-Query-Parameter auswerten und über `SetBackNavigation` ins ViewModel transportieren; die „Navigieren"-Gruppe entsteht nur bei gesetztem `InitBack` (bedingte Gruppenerzeugung — kein `Hidden` auf der Back-Aktion nötig). Zusätzlich verwirft `Ribbon.razor` Gruppen, die nach `Hidden`-Filterung keine Items mehr enthalten (defensive Härtung). **Fehlende resx-Keys** (`Ribbon_Group_View`, `Ribbon_Group_Data`, `Hint_RebuildAggregates` im `Pages`-Localizer): neu anlegen (de + en + neutral). **Favicon**: komplett neu erzeugen (SVG-Master + `.ico` + `.png`) und in `App.razor`/`manifest.json` einbinden |

### Annahmen (aus der Bestandsaufnahme dokumentiert)

- **Doppelte Überschrift „Sparplanübersicht"** im Kommentar: Die zweite Auflistung (mit „Zurück", „Neu laden", „Suche zurücksetzen") wird als **Sparplankategorieübersicht** (`SavingsPlanCategoryListViewModel`) interpretiert — sie ist die einzige Ansicht, zu der die genannten Aktionen passen (die erste Auflistung deckt `SavingsPlansListViewModel` bereits ab).
- **`NewRule`/`SetPassword`/`Deactivate`/`Revoke` existieren bereits** (Kind-VM `BudgetRuleListViewModel`, `UserCardViewModel`, `McpAccessCardViewModel`). Für sie wird **kein** Issue angelegt; es erfolgen nur die nötigen Größen-/Gruppierungs-/Sortkorrekturen.
- **`Reload`/`ClearSearch` auf `SavingsPlansListViewModel` und `SecuritiesListViewModel`** fehlen ebenfalls nachweislich, sind aber im Kommentar nicht separat als Issue gelistet. Da die Zielkonfiguration sie verlangt, wird für beide Übersichten **ein gemeinsames Issue** geplant (statt sie in die Fehlverhalten-Issues Nr. 1/3 zu falten — dort geht es um defekte Suchfunktion, nicht um fehlende Ribbon-Aktionen).
- **`Ribbon_SetPassword`** trägt heute den Wert „Passwort zuweisen"; die Zielkonfiguration nennt die Aktion „Passwort zurücksetzen". Der resx-Wert wird auf „Passwort zurücksetzen"/„Reset password" angeglichen (Labelkorrektur, keine Verhaltensänderung).
- **Konditionale Zusatzaktionen**, die die Zielkonfiguration nicht nennt (`Activate`/`Deactivate`/`Unblock` in `UserCardViewModel`, `RotateToken`/`Activate` in `McpAccessCardViewModel`, `ClearPurposePattern`-Gruppe „Felder" in `BudgetRuleCardViewModel`, `UpdateResetLock` in `SetupUpdateViewModel`), bleiben erhalten und **behalten ihre bisherige relative Position innerhalb ihrer Gruppe**: Die genannten Aktionen werden in der gelisteten Reihenfolge angeordnet, die ungenannten verbleiben an ihrer bestehenden Position relativ dazu. Konkret: `RotateToken` bleibt in `McpAccessCardViewModel` zwischen `Save`/`[Reset]` und `Deactivate`/`Activate`/`Revoke` (aktuelle Code-Reihenfolge, deckt sich mit der Zieltabelle unten); `SetPassword` wird in `UserCardViewModel` als genannte Aktion vor die konditionalen `Deactivate`/`Activate`/`Unblock` gezogen (heute steht es am Listenende).

### Scope-Ausschlüsse

||| # | Einschränkung / Ausschluss | Begründung | Status |
|||---|----------------------------|------------|--------|
||| 1 | Die nachweislich fehlenden Aktionen werden **nicht** implementiert, nur als GitHub-Issues erfasst | Explizite Vorgabe des Auftraggebers („Erfasse dafür ein issue, damit wir die Funktion später implementieren") | bestätigt (direkte Anforderungsvorgabe, kein Ausschluss gegenüber Nutzererwartung) |
||| 2 | Die zehn gemeldeten Fehlverhalten (Suche, Zurück-Routen, Analyse-Semantik, KI-Sperre) werden **nicht** behoben, nur als Issues erfasst | Explizite Vorgabe; das Menü wird mit den vorhandenen Aktionen in Zielreihenfolge aufgebaut | bestätigt (direkte Anforderungsvorgabe) |
||| 3 | Ansichten ohne Nennung im Kommentar (u. a. Posten-/Import-Entwurfsansichten, Startseite, Berichte-Dashboard, Wertpapier-Kurslisten, Performance-Seite, `BankAccountCardViewModel`) bleiben unverändert | „Falls für einen Bereich nichts genannt wird, ist es in Ordnung wie es aktuell ist" | bestätigt (direkte Anforderungsvorgabe) |
||| 4 | Keine Verhaltensänderung bestehender Aktionen (Callbacks, `Disabled`-Logik, Navigationsziele) — Ausnahme: `OpenBudgetReport` erhält einen `?back=`-Parameter, damit der geforderte konditionale Zurück-Button des Budgetberichts funktioniert | Die Semantik-Fehler der Bestandsaktionen sind eigene Issues | bestätigt (direkte Anforderungsvorgabe) |
||| 5 | `UiRibbonAction.Id`s bleiben unverändert (E2E-Selektoren `button#<Id>` hängen daran); Label werden nur angepasst, wo die Zielkonfiguration es ausdrücklich verlangt (`Ribbon_SetPassword`) | Bestandsschutz der E2E-Suite | bestätigt (implementationsintern, keine nutzerseitige Einschränkung) |

## Designentscheidungen

|| Komponente / Bereich | Gewählter Ansatz | Begründung |
||----------------------|-----------------|------------|
|| Konditionales „Zurück" auf `ListPage` + `BudgetReport.razor` | `?back=`-Query-Parameter (`SupplyParameterFromQuery`) + `SetBackNavigation`/`InitBack` von `BaseCardViewModel` nach `BaseViewModel` hochziehen; **bedingte Gruppenerzeugung**: die Navigieren-`UiRibbonTab` wird in `BankAccountListViewModels`/`BudgetReportViewModel` nur erzeugt, wenn `InitBack` gesetzt ist (die `Back`-Aktion braucht dann kein `Hidden`); `OpenBudgetReport` hängt `?back=<aktuelle URI>` an. Ergänzend: `Ribbon.razor` verwirft Gruppen, die nach `Hidden`-Filterung keine Items mehr enthalten | `Hidden` auf der einzigen Aktion einer Gruppe erzeugt nachweislich eine **leere, sichtbare Gruppe**: `Ribbon.razor` L183 prüft `tab.Items.Count == 0` **vor** der `Hidden`-Filterung (L196) — die Gruppe wird angelegt, alle Items verworfen, der leere Rahmen + Footer-Titel (`@g.Title`, L82) bleibt. Betroffen wäre der Regelfall der Kontenübersicht und jeder Direktaufruf des Budgetberichts. Bedingte Gruppenerzeugung vermeidet das von vornherein, entspricht der Requirement-Formulierung („Gruppe heute nicht vorhanden → kontextabhängig erzeugen") und ist auf VM-Ebene ohne Rendering testbar. Der zusätzliche `Ribbon.razor`-Filter ist defensive Härtung gegen künftige Hidden-only-Tabs — verifiziert nebenwirkungsfrei: kein Bestands-VM liefert eine ausschließlich versteckte Gruppe (das `StatementDraftCardViewModel`-QuickEdit-Trio hat stets ≥1 sichtbares Item, `McpSecurity` teilt die KI-Gruppe mit immer sichtbaren `McpApprovals`/`McpAudit`); Gruppen mit nur `Disabled`-Aktionen sind unberührt, da `Disabled` nicht gefiltert wird |
|| Gruppenreihenfolge | Explizite `UiRibbonTab.Sort`-Konvention statt impliziter Erzeugungsreihenfolge | Kind-VM-Register werden nach den Eltern-Registern angehängt und die Erzeugungsreihenfolge der Sektions-VMs im Setup ist nicht garantiert; explizite `Sort`-Werte machen die Zielreihenfolge deterministisch (`Ribbon.razor` L251: stabiles `OrderBy(g => g.Sort)`) |
|| `UiRibbonTab.Sort`-Konvention | Navigieren = 0, Verwalten = 10, Daten = 15, Profil = 20, Ansicht = 30, Filter = 30, KI (`Ribbon_Group_Mcp`) = 35, Analyse = 40, Buchungen = 45, Felder = 47, Verknüpfte Informationen = 55, Regeln = 60, Programmversion (`Setup_Section_Update`) = 50 *(bestehender Wert, unverändert)*, Berichte = 70, Export = 80, Sicherungen = `int.MaxValue` *(bestehender Wert)* | Deckt alle Zielreihenfolgen konfliktfrei ab: z. B. Linked(55) vor Regeln(60) auf `BudgetPurposeCardViewModel`, Linked(55) vor Berichte(70) auf `SecurityCardViewModel`, KI(35) vor Programmversion(50) vor Sicherungen(MaxValue) im Setup. Filter und Ansicht teilen sich Wert 30, da sie nie auf derselben Ansicht vorkommen |
|| `?back=`-Timing | `ListPage.razor`: `SetBackNavigation(Back)` in `OnParametersSetAsync` direkt nach der `_provider`-Zuweisung (der Provider wird dort bei **jedem** Parameterwechsel neu erzeugt — auch bei reinem Query-Wechsel —, sodass `InitBack` stets auf dem frischen VM vor `InitializeAsync` gesetzt ist). `BudgetReport.razor`: `SetBackNavigation(Back)` in `OnParametersSet` (nicht `OnInitialized`), damit ein nachträglicher reiner `?back=`-Wechsel `InitBack` auf dem bestehenden VM aktualisiert | `OnParametersSetAsync` auf `ListPage` ruft `factory.Create` + `InitializeAsync` bei jeder Parameteränderung auf — Query-only-Wechsel sind damit automatisch korrekt abgedeckt (Nebeneffekt: voller Neuaufbau + Reload der Liste, konsistent zum bisherigen Param-Wechsel-Verhalten). Auf `BudgetReport.razor` wird `_vm` in `OnInitialized` nur einmal erzeugt; `OnParametersSet` läuft beim ersten Render nach `OnInitialized` und bei jedem späteren Query-Wechsel erneut → die Gruppe erscheint/verschwindet zur Laufzeit korrekt |
|| Inhaltspanel Übersichtsseiten | `<div class="card-page-content">`-Wrapper in `ListPage.razor` um den gesamten Inhaltszweig (Ribbon, Titel, `GenericListPage`, Panels) — identisch zur `CardPage`-Struktur | `GenericListPage.razor` wird auch eingebettet (`SetupMcpTab`, `GenericCardPage`-EmbeddedList) verwendet; ein Wrapper dort würde Doppel-Panels erzeugen. Der Wrapper auf `ListPage`-Ebene ergibt exakt dasselbe Layout wie die Detailansicht → kein Layout-Sprung |
|| `.icon-btn.danger` Lesbarkeit | Neue spezifischere Regel `button.icon-btn.danger` mit `color: #000` in `theme.Dark.css`; Hover-Regel L190–193 (`color: var(--danger)` auf `--panel-bg`) bleibt | `button.danger` (Spezifität 0,1,1) schlägt `.icon-btn`-Hintergrund → roter Grund; `color: #000` erfüllt die Anforderung „Text/Symbol schwarz im Nicht-Hover"; im Hover übernimmt `.icon-btn:hover` den dunklen `--panel-bg`, sodass die rote Hover-Farbe lesbar bleibt. Nur Dark-Theme vorhanden — kein Light-Pendant nötig |
|| Favicon | Konkreter Vorschlag: **abgerundete dunkle Kachel mit „F"-Ligatur aus aufsteigendem Chart** — dunkler Verlauf `#0b1120 → #16203a` als Kachel, eine stilisierte „F"-Form, deren vertikaler Balken einer Chart-Achse und deren zwei Querbalken Rasterlinien entsprechen, durchzogen von einer aufsteigenden Trendlinie im Teal→Violett-Verlauf (`#2dd4bf → #8b5cf6`, = `--accent`/`--accent-2`), abgeschlossen mit einem leuchtenden Endpunkt-Knoten. Lieferumfang: SVG-Master `wwwroot/icons/finance_manager.svg`, neu gerendertes Multi-Size-`finance_manager-64x64.ico` (16/32/48/64), neues `finance_manager.png` (1024²) für `manifest.json`; Einbindung: zusätzliches `<link rel="icon" type="image/svg+xml">` in `App.razor`, `manifest.json` `theme_color`/`background_color` auf `#080a0f` | Ersetzt das generische Altsymbol und schließt farblich an die neue Teal/Violett-Dark-Gestaltung an; das F-Chart-Motiv ist spezifisch für „FinanceManager" statt generischem Finanz-Clipart. Erzeugung deterministisch per Python/PIL-Skript (Pillow ist im Repo-Umfeld verfügbar); `.ico`-Dateiname bleibt gleich → bestehender `<link>` in `App.razor` L8 bleibt kompatibel |
|| Issue-Granularität | Je fehlender **Aktionsart je Ansichtsbereich** ein Issue (6 Aktions-Issues A1–A6) + je gemeldetem Fehlverhalten ein Issue (10, B1–B10) = **16 Issues** | „Erfasse dafür ein issue" — die neun `Reset`-Fehlstellen sind dieselbe Funktionslücke und werden in einem Issue gebündelt (Vorlage `BankAccountCardViewModel.ResetPendingAsync`); die übrigen Aktionslücken sind ansichtsspezifisch |

## Programmabläufe

### Ribbon-Rendering in Zielkonfiguration (alle betroffenen Ansichten)

1. `Ribbon.razor` rendert → `BuildTabsToRender()` ruft `Provider.GetRibbonRegisters(Localizer)`.
2. `BaseViewModel.GetRibbonRegisters` aggregiert `GetRibbonRegisterDefinition` des Ansichts-VMs plus Register aktiver Kind-VMs (`IsChildViewModelActive`).
3. `Ribbon.razor` merged `UiRibbonTab`s über den Gruppen-Titel (first-seen-Reihenfolge), filtert `Hidden`-Aktionen (L196), **verwirft Gruppen ohne verbliebene Items (neu, nach `var groups = ...` L219)** und sortiert Gruppen über `UiRibbonTab.Sort` stabil (L251).
4. Jede `UiRibbonAction` wird mit vorgegebener `UiRibbonItemSize`, `Disabled`-Bedingung und `MobileShortcut` als `button#<Id>` gerendert.

Beteiligte Klassen: `Components/Shared/Ribbon.razor`, `ViewModels/Common/BaseViewModel.cs`, `ViewModels/Common/RibbonModels.cs`, alle in „Änderungen an bestehenden Klassen" gelisteten ViewModels.

### Konditionaler „Zurück"-Button auf `ListPage` (Kontenübersicht)

1. `ListPage.razor` deklariert `[SupplyParameterFromQuery(Name = "back")] string? Back`.
2. `OnParametersSetAsync` erzeugt per `ListViewModelFactory.Create(Kind, SubKind, Id)` bei jedem Parameterwechsel (auch reinem `?back=`-Query-Wechsel) einen neuen Provider; unmittelbar nach der Zuweisung `_provider = result?.Provider` wird `(_provider as BaseViewModel)?.SetBackNavigation(Back)` aufgerufen — **vor** `await _provider.InitializeAsync()`.
3. `BankAccountListViewModels` erzeugt die „Navigieren"-`UiRibbonTab` mit `Back` (`UiRibbonItemSize.Large`) **nur**, wenn `InitBack` gesetzt ist; andernfalls entsteht die Gruppe gar nicht.
4. Klick auf `Back` → `HandleProviderActionAsync` `case "Back"` navigiert bei gesetztem `Back`-Parameter zuerst `Nav.NavigateTo(Back)`, sonst bisherige Fallbacks (Postings-SubKind-Mapping, `list/{Kind}`).

Beteiligte Klassen: `Components/Pages/ListPage.razor`, `ViewModels/Common/BaseViewModel.cs`, `ViewModels/Accounts/BankAccountListViewModels.cs`.

### Konditionaler „Zurück"-Button auf dem Budgetbericht

1. `ListPage.HandleProviderActionAsync` `case "OpenBudgetReport"` navigiert auf `reports/budget?back=<Uri.EscapeDataString(Nav.Uri)>`.
2. `BudgetReport.razor` deklariert `[SupplyParameterFromQuery(Name = "back")] string? Back` und ruft in `OnParametersSet` `_vm?.SetBackNavigation(Back)` auf (das VM entsteht in `OnInitialized` nur einmal; `OnParametersSet` läuft erstmalig danach und bei jedem späteren Query-Wechsel erneut → `InitBack` folgt dem aktuellen Parameter).
3. `BudgetReportViewModel` erzeugt die „Navigieren"-Gruppe (`Sort = 0`) mit `Back` (`UiRibbonItemSize.Large`) nur bei gesetztem `InitBack`; Callback → `RaiseUiActionRequested("Back")`.
4. `BudgetReport.razor` `OnUiActionRequested` behandelt `case "Back"` → `Nav.NavigateTo(Back)` (direkter Aufruf ohne Parameter erzeugt die Gruppe nicht — kein Button, keine leere Gruppe).

Beteiligte Klassen: `Components/Pages/BudgetReport.razor`, `ViewModels/Budget/BudgetReportViewModel.cs`, `Components/Pages/ListPage.razor`, `ViewModels/Common/BaseViewModel.cs`.

### Inhaltspanel auf Übersichtsseiten

1. `ListPage.razor`: der gesamte `else`-Renderblock (ab `Ribbon` bis `EmbeddedPanelHost`) erhält einen umschließenden `<div class="card-page-content">` — analog `CardPage.razor` L35.
2. `.card-page-content` (app.css L334/L347/L361) liefert Hintergrund, Rahmen, `padding: 1rem 1.25rem` — identisch zur Detailansicht → kein Layout-Sprung.

Beteiligte Klassen: `Components/Pages/ListPage.razor`, `wwwroot/css/app.css` (unverändert — Klasse existiert).

### Lesbares Löschen-Symbol in der Sicherungs-Tabelle

1. In `theme.Dark.css` wird `.icon-btn.danger` durch eine spezifischere Regel `button.icon-btn.danger` (Spezifität 0,2,1 > `button.danger` 0,1,1) mit `color: #000` im Nicht-Hover-Zustand überschrieben.
2. Hover bleibt bei `color: var(--danger)` — im Hover greift `.icon-btn:hover` mit `--panel-bg`-Hintergrund (Spezifität 0,3,1), sodass Rot auf Dunkel lesbar bleibt.

Beteiligte Klassen: `wwwroot/css/theme.Dark.css` (L186–193), `Components/Pages/Setup/SetupBackupTab.razor` (unverändert — Markup L58/L105 korrekt).

### Favicon-Erzeugung und -Einbindung

1. `scripts/icons/generate_favicon.py` (neu): zeichnet das Icon-Motiv per Pillow in 1024², skaliert auf 64/48/32/16, speichert `finance_manager-64x64.ico` (Multi-Size) und `finance_manager.png` in `wwwroot/icons/`.
2. SVG-Master `wwwroot/icons/finance_manager.svg` wird von Hand erstellt (gleiche Geometrie, Vektorquelle).
3. `App.razor` ergänzt `<link rel="icon" type="image/svg+xml" href="icons/finance_manager.svg">`; `manifest.json` verweist weiter auf `icons/finance_manager.png`, `theme_color`/`background_color` werden auf `#080a0f` gesetzt.

Beteiligte Klassen: `scripts/icons/generate_favicon.py` (neu), `wwwroot/icons/finance_manager.svg` (neu), `Components/App.razor`, `wwwroot/manifest.json`.

### GitHub-Issue-Erstellung

1. Für jede fehlende Aktionsart bzw. jedes Fehlverhalten wird ein Issue via `gh issue create` angelegt: Titel + Beschreibung aus dem Kommentartext (`requirement-source.txt`), Repro-Schritte wo im Kommentar beschrieben, Bezug „Ref #590".

Beteiligte Artefakte: GitHub-Issues im Projekt-Repository.

### Anzulegende GitHub-Issues (konkrete Liste)

**A. Fehlende Menüaktionen (6 Issues)**

|| # | Arbeitstitel | Inhalt (aus Kommentar + Inventur) |
||---|--------------|-----------------------------------|
|| A1 | „Zurücksetzen"-Aktion (Änderungen verwerfen) auf Detailansichten ergänzen | Auf den Detailansichten Kontakt, Sparplan, Sparplankategorie, Wertpapier, Wertpapierkategorie, Budgetzweck, Budgetzweckregel, Benutzer und KI-Zugang fehlt die Ribbon-Aktion „Zurücksetzen" (kleines Symbol, Gruppe „Verwalten", hinter „Speichern"). Gewünscht: Discard-Changes-Aktion analog der bestehenden `Reset`-Aktion in `BankAccountCardViewModel` (`ResetPendingAsync`: `ClearPendingChanges` + `CardRecord` aus dem geladenen Modell neu aufbauen). Ref #590 |
|| A2 | „Neu laden" und „Suche zurücksetzen" auf der Sparplankategorieübersicht | `SavingsPlanCategoryListViewModel` besitzt weder `Reload` noch `ClearSearch`; Ziel: Ansicht-Gruppe mit „Neu laden" (groß) und „Suche zurücksetzen" (klein, nur aktiviert bei Suchbegriff). Ref #590 |
|| A3 | „Neu laden" und „Suche zurücksetzen" auf der Benutzerübersicht | `UserListViewModel` besitzt weder `Reload` noch `ClearSearch`; Ziel wie A2. Ref #590 |
|| A4 | „Suche zurücksetzen" auf der KI-Zugänge-Übersicht | `McpAccessListViewModel` besitzt `Reload` bereits; es fehlt `ClearSearch` (klein, nur aktiviert bei Suchbegriff) in der neuen Ansicht-Gruppe. Ref #590 |
|| A5 | „Kategorie" als verknüpfte Information in der Wertpapierdetailansicht | `SecurityCardViewModel` hat keine `Category`-Aktion; Ziel: kleines Symbol in „Verknüpfte Informationen", Navigation auf `card/securities/categories/{CategoryId}` (Muster: `Category`-Aktion in `SavingsPlanCardViewModel`), `Disabled` ohne `CategoryId`. Ref #590 |
|| A6 | „Neu laden" und „Suche zurücksetzen" auf Sparplan- und Wertpapierübersicht | `SavingsPlansListViewModel` und `SecuritiesListViewModel` besitzen weder `Reload` noch `ClearSearch`; Ziel wie A2. *(Dokumentierte Ergänzung: im Kommentar nicht separat gelistet, aber von der Zielkonfiguration gefordert und im Code nachweislich fehlend.)* Ref #590 |

**B. Gemeldete Fehlverhalten (10 Issues, Titel + Beschreibung aus dem Kommentar)**

|| # | Arbeitstitel | Inhalt |
||---|--------------|--------|
|| B1 | Sparplanübersicht: Suche und Datumsfilter ohne Wirkung | Suche filtert nicht (Liste bleibt vollständig). Datumsfilter funktioniert ebenfalls nicht; er muss auf das Zieldatum filtern und bei wiederkehrenden Plänen berücksichtigen, dass der Plan im angegebenen Zeitraum fällig sein kann. Ref #590 |
|| B2 | Zurück-Button navigiert nicht zur Herkunftsansicht (Sparplandetail → Kategoriedetail) | Nach Navigation von der Sparplandetailansicht zur Kategoriedetailansicht führt „Zurück" zur Kategorieübersicht statt zur Sparplandetailansicht; der Zurück-Button muss dynamisch zur Herkunftsansicht navigieren. Ref #590 |
|| B3 | Wertpapierübersicht: Suche ohne Wirkung; Datumsfilter entfernen | Suche filtert nicht (Liste bleibt vollständig); der Datumsfilter ist zu entfernen. Ref #590 |
|| B4 | Budgetzweckregeldetail: Zurück-Button erzeugt ungültige Route | Beispiel: von `/card/budget/rules/{id}` nach `/list/budget/card/budget/purposes/{id}` statt zur Budgetzweckdetailansicht. Ref #590 |
|| B5 | Analyse-Buttons Budgetübersicht/Budgetbericht vereinheitlichen | Uneinheitliches Verhalten; verbindliche Semantik: „Vormonat"/„Nächster Monat" verschieben die gezeigte Periode um je einen Monat in Vergangenheit/Zukunft, „Aktueller Monat" setzt auf den aktuellen Monat zurück, „Vorjahr"/„Nächstes Jahr" um je ein Jahr. Ref #590 |
|| B6 | Benutzerübersicht: Suche ohne Wirkung; Datumsfilter entfernen | Suche filtert nicht (Liste bleibt vollständig); der Datumsfilter ist zu entfernen. Ref #590 |
|| B7 | KI-Zugänge: „Neu" bei deaktivierter KI-Integration nicht sperrbar | Ist die KI-Integration in den Einstellungen nicht aktiviert, darf die Neuanlage nicht aufrufbar sein (aktuell durchläuft der Anwender den Wizard und scheitert erst am Ende). Ref #590 |
|| B8 | KI-Protokollansicht: Zurück-Button erzeugt ungültige Route | „Zurück" navigiert auf `/list/mcp-audit/card/setup`; Anzeige bleibt bei „Laden …" hängen. Ref #590 |
|| B9 | KI-Sicherheitsansicht: Zurück-Button erzeugt ungültige Route | Gleiche ungültige Route `/list/mcp-audit/card/setup` mit hängender Anzeige. Ref #590 |
|| B10 | KI-Genehmigungsübersicht: Zurück-Button erzeugt ungültige Route | Gleiche ungültige Route `/list/mcp-audit/card/setup` mit hängender Anzeige. Ref #590 |

**Nicht angelegt (existieren bereits, verifiziert):** „Regeln → Neu" in der Budgetzweckdetailansicht (`NewRule` via Kind-VM `BudgetRuleListViewModel`), „Passwort zurücksetzen" (`SetPassword` in `UserCardViewModel`), „Deaktivieren" und „Zugang widerrufen" (`Deactivate`/`Revoke` in `McpAccessCardViewModel`).

## Neue Klassen

|| Klasse | Typ | Zweck |
||--------|-----|-------|
|| `scripts/icons/generate_favicon.py` | Skript | Generiert `finance_manager-64x64.ico` + `finance_manager.png` aus dem Favicon-Motiv (Pillow) |
|| `wwwroot/icons/finance_manager.svg` | Statisches Asset | SVG-Master des neuen Favicons (Vektorquelle, eingebunden via `<link rel="icon" type="image/svg+xml">`) |

Keine neuen C#-Klassen — `Hidden`, `Sort`, das `?back=`/`SetBackNavigation`-Muster und die bedingte Gruppenerzeugung nutzen vorhandene Strukturen.

## Änderungen an bestehenden Klassen

### `ViewModels/Common/BaseViewModel.cs`

- **Neue Eigenschaften:** `protected string? InitBack` — konditionaler Zurück-Kontext (aus `BaseCardViewModel` hochgezogen).
- **Neue Methoden:** `public void SetBackNavigation(string? backUrl)` — setzt `InitBack` (aus `BaseCardViewModel` hochgezogen; `ICardInitializable` bleibt unverändert, `BaseCardViewModel` erbt die Implementierung).

### `ViewModels/Common/BaseCardViewModel.cs`

- **Entfernt:** `_initBack`, `SetBackNavigation`, `InitBack` — ziehen in `BaseViewModel` um (Signatur/Verhalten identisch, `ICardInitializable` weiterhin erfüllt).

### `Components/Shared/Ribbon.razor`

- **Geänderte Methode `BuildTabsToRender`:** nach dem Aufbau der `groups`-Liste (L219) Gruppen mit `Items.Count == 0` verwerfen (`groups.RemoveAll(g => g.Items.Count == 0)`) — Gruppen, deren sämtliche Aktionen `Hidden` sind, rendern künftig keinen leeren Rahmen mit Footer-Titel (Desktop- und Mobile-Pfad betroffen, beide iterieren `CurrentGroups`).

### `Components/Pages/ListPage.razor`

- **Neuer Parameter:** `[Parameter, SupplyParameterFromQuery(Name = "back")] public string? Back` — Zurück-Kontext für konditionale Back-Aktionen.
- **Geänderte Methode `OnParametersSetAsync`:** unmittelbar nach `_provider = result?.Provider` `(_provider as BaseViewModel)?.SetBackNavigation(Back)` aufrufen (vor `InitializeAsync`). Da der Provider bei jedem Parameterwechsel — auch reinem `?back=`-Query-Wechsel — neu erzeugt wird, ist `InitBack` stets auf dem frischen VM korrekt gesetzt; der damit verbundene Neuaufbau/Reload entspricht dem bisherigen Verhalten bei Parameterwechseln.
- **Geänderter `case "Back"` in `HandleProviderActionAsync`:** bei gesetztem `Back` zuerst `Nav.NavigateTo(Back)` (vor den bisherigen Fallbacks).
- **Geänderter `case "OpenBudgetReport"`:** Navigationsziel `reports/budget?back={Uri.EscapeDataString(Nav.Uri)}`.
- **Markup:** gesamter `else`-Block erhält einen `<div class="card-page-content">`-Wrapper (Ribbon, Titel, `GenericListPage`, Panels hinein — analog `CardPage.razor` L35).

### `Components/Pages/BudgetReport.razor`

- **Neuer Parameter:** `[Parameter, SupplyParameterFromQuery(Name = "back")] public string? Back`.
- **Neue Methode `OnParametersSet`:** `_vm?.SetBackNavigation(Back)` aufrufen — läuft erstmalig nach `OnInitialized` (dort entsteht `_vm`) und bei jedem späteren Parameter-/Query-Wechsel erneut; damit erscheint/verschwindet die Navigieren-Gruppe auch bei reinem Query-Wechsel korrekt.
- **Geänderter `OnUiActionRequested`:** neuer `case "Back"` → `Nav.NavigateTo(Back)` bei gesetztem Parameter.

### `Components/App.razor`

- **Geändertes Markup:** zusätzliches `<link rel="icon" type="image/svg+xml" href="icons/finance_manager.svg">`; bestehender `.ico`-Link bleibt als Fallback.

### `wwwroot/manifest.json`

- **Geänderte Einträge:** `theme_color`/`background_color` → `#080a0f` (Icon-Dateiname unverändert `icons/finance_manager.png`, Datei wird neu erzeugt).

### `wwwroot/css/theme.Dark.css`

- **Neue Regel:** `button.icon-btn.danger { color: #000; }` (Nicht-Hover, Spezifität über `button.danger`); bestehende Hover-Regel (L190–193) bleibt.

### Ribbon-ViewModels — Zielkonfiguration je Ansicht

Konventionen für alle: Labels aus `Pages.*.resx`, Icons aus `icons/sprite.svg`, `MobileShortcut = true` auf der primären Aktion (Neuanlage bzw. `Save`), Gruppen als `UiRibbonTab` mit `Sort` gemäß Konventionstabelle, fehlende Aktionen (→ Issue) in Klammern `[→Issue]` — sie werden nicht erzeugt, und eine Gruppe wird nur emittiert, wenn mindestens eine vorhandene Aktion hineingehört. Bestehende `Disabled`-Bedingungen bleiben unverändert.

|| ViewModel (Datei) | Ziel: Gruppe (Sort) → Aktionen (Größe) | Konkrete Änderung ggü. Ist |
||---|---|---|
|| `Accounts/BankAccountListViewModels.cs` | Navigieren(0): `Back` L — **Tab nur bei gesetztem `InitBack` erzeugen**; Verwalten(10): `New` L; Filter(30): `ClearSearch` L, dis: kein Suchbegriff | `Back`/`New`/`ClearSearch` S→L; eine Gruppe → bis zu drei Gruppen; Navigieren-Tab konditional statt `Hidden` (leere Gruppe vermeiden) |
|| `Contacts/ContactListViewModel.cs` | Verwalten(10): `New` L, `Groups` L; Ansicht(30, `Ribbon_Group_View` neu): `Reload` L, `ClearFilter` S | `Reload`/`ClearFilter` in neue Gruppe verschieben; `ClearFilter` L→S |
|| `Contacts/ContactCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S, `OpenMerge` S; Verknüpfte Informationen(55): `OpenPostings` S, `Attachments` S | `OpenMerge` von Linked → Manage |
|| `SavingsPlans/SavingsPlansListViewModel.cs` | Verwalten(10): `New` L, `Categories` L; Ansicht(30): [`Reload`→Issue], [`ClearSearch`→Issue], `ToggleActive` S | `Categories` S→L; `ToggleActive` aus Register `Custom`/Gruppe „Filter" in neue Ansicht-Gruppe; Filter-Gruppe entfällt |
|| `SavingsPlans/SavingsPlanCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L (+`MobileShortcut`), [`Reset`→Issue], `Delete` S, `Archive` S; Analyse(40): `Recalculate` L; Buchungen(45): `ActualReport` L; Verknüpfte Informationen(55): `Category` S, `OpenPostings` S, `OpenAttachments` S | `Recalculate`/`ActualReport` S→L; `MobileShortcut` auf `Save` ergänzen; Sort explizit setzen |
|| `SavingsPlans/Categories/SavingsPlanCategoryListViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `New` L; (Ansicht: [`Reload`→Issue], [`ClearSearch`→Issue] — Gruppe entsteht erst mit den Aktionen) | Sort explizit; `Back`-Ziel `list/savings-plans` bleibt (dynamische Herkunft → Issue Nr. 2) |
|| `SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S | nur Sort explizit (Struktur entspricht bereits Ziel) |
|| `Securities/SecuritiesListViewModel.cs` | Verwalten(10): `New` L, `Categories` L; Ansicht(30): [`Reload`→Issue], [`ClearSearch`→Issue], `ToggleActive` S; Berichte(70): `PortfolioAnalysisReport` L | Gruppe `Ribbon_Group_Actions` → `Ribbon_Group_Manage`; `Categories`/`PortfolioAnalysisReport` S→L; `ToggleActive` aus Filter-Gruppe in Ansicht-Gruppe |
|| `Securities/SecurityCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S, `Archive` S; Verknüpfte Informationen(55): [`Category`→Issue], `Postings` S, `Attachments` S, `Prices` S; Berichte(70): `Performance` L (`Ribbon_Performance` = „Detaillierte Analyse") | `Performance` aus Linked → neue Berichte-Gruppe, S→L; Reihenfolge Linked: Postings, Attachments, Prices |
|| `Securities/Categories/SecurityCategoriesListViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `New` L | beide Aktionen S→L |
|| `Securities/Categories/SecurityCategoryCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S | nur Sort explizit |
|| `Budget/BudgetPurposeListViewModel.cs` | Verwalten(10): `New` L, `Categories` L; Ansicht(30): `Reload` L, `ClearFilter` S; Analyse(40): `PrevMonth`/`ThisMonth`/`PrevYear`/`ThisYear` S; Berichte(70): `BudgetReport` L | Navigation-Gruppe aufgeteilt (New+Categories→Verwalten, Reload+ClearFilter→Ansicht); `Categories`/`BudgetReport`/`Reload` S→L bzw. neu gruppiert; `OpenBudgetReport` erhält `?back=` (in `ListPage`) |
|| `Budget/BudgetPurposeCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S; Verknüpfte Informationen(55): `Category` S; Regeln(60): `NewRule` L (bestehend, via Kind-VM `BudgetRuleListViewModel`) | `BudgetRuleListViewModel`-Tab erhält `Sort = 60`; prüfen, dass `NewRule` via `IsChildViewModelActive` sichtbar bleibt — **kein Issue nötig** |
|| `Budget/BudgetRuleListViewModel.cs` | Regeln-Tab `Sort = 60` | `UiRibbonTab`-Sort ergänzen (Aktion selbst unverändert) |
|| `Budget/BudgetRuleCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S; Felder(47): `ClearPurposePattern` S (ungenannt → bleibt an bisheriger Position) | nur Sort explizit; defekte Back-Route → Issue Nr. 4 (nicht fixen) |
|| `Budget/BudgetReportViewModel.cs` | Navigieren(0) **neu**: `Back` L — **Tab nur bei gesetztem `InitBack` erzeugen**; Verwalten(10): `Settings` L; Analyse(40): `PrevMonth`/`ThisMonth`/`NextMonth`/`PrevYear`/`NextYear` S; Export(80): `ExportExcel` S | neue Back-Aktion + konditionale Gruppe + Sort; `Settings` S→L |
|| `Setup/UserListViewModel.cs` | Verwalten(10): `New` L; (Ansicht: [`Reload`→Issue], [`ClearSearch`→Issue] — Gruppe entsteht erst mit den Aktionen) | nur Sort explizit |
|| `Setup/UserCardViewModel.cs` | Navigieren(0): `Back` L; Verwalten(10): `Save` L, [`Reset`→Issue], `Delete` S, `SetPassword` S (Label → „Passwort zurücksetzen"), danach unverändert konditional `Deactivate`/`Activate`/`Unblock` S | `SetPassword` existiert — kein Issue; resx-Wert `Ribbon_SetPassword` angleichen; `SetPassword` als genannte Aktion vor die konditionalen Aktionen ziehen (heute am Listenende); Sort explizit |
|| `Setup/SetupCardViewModel.cs` | Verwalten(10): `Save` L, `Reset` S; Daten(15, `Ribbon_Group_Data` neu): `RebuildAggregates` S (Tooltip `Hint_RebuildAggregates` → neu im `Pages`-Localizer), `ResetReportCache` S | `RebuildAggregates`/`ResetReportCache` aus Manage → neue Daten-Gruppe; Tooltip-Key-Nachzug |
|| `Setup/SetupBackupsViewModel.cs` | Sicherungen(`int.MaxValue`, `SetupBackup_Titel` → „Sicherungen"): `CreateBackup` S, `UploadBackup` S | beide Aktionen L→S; resx-Wert umbenennen (betrifft auch `SetupBackupTab`-Überschrift — gleicher Key) |
|| `Setup/SetupUpdateViewModel.cs` | Programmversion(50, `Setup_Section_Update` → „Programmversion"/„Program version"): `UpdateCheckNow` S, `UpdateInstall` S, danach `UpdateResetLock` S (ungenannt → bleibt an bisheriger Position) | resx-Wert umbenennen (betrifft Gruppen-Titel **und** Setup-Tab-Label — gleicher Key, gewünscht); Aktionen unverändert |
|| `Setup/SetupProfileViewModel.cs` | Profil(20, `SetupProfile_Title`): `DetectTimezone` S | nur Sort explizit |
|| `Setup/SetupMcpViewModel.cs` | KI(35, `Ribbon_Group_Mcp`): `McpApprovals` S, `McpAudit` S, `McpSecurity` S (`Hidden = !IsAdmin` bleibt) | `McpApprovals` L→S; Sort explizit |
|| `Mcp/McpAccessListViewModel.cs` | Verwalten(10): `New` L; Ansicht(30): `Reload` L, [`ClearSearch`→Issue]; Verknüpfte Informationen(55): `Approvals` S, `AuditLog` S, `Security` S (dis: `!IsAdmin`) | `Approvals`/`AuditLog`/`Security` → Linked; `Reload` → Ansicht, S→L; `New`-Sperre bei deaktivierter KI → Issue Nr. 7 (nicht fixen) |
|| `Mcp/McpAccessCardViewModel.cs` | Navigieren(0): `Back` L → `card/setup`; Verwalten(10): `Save` L, [`Reset`→Issue], `RotateToken` S, `Deactivate`/`Activate` S, `Revoke` S (alle konditional, bestehend — ungenannte Aktionen behalten ihre bisherige Position: `RotateToken` nach `Save`/`[Reset]`, `Revoke` zuletzt) | `Deactivate`/`Revoke` existieren bereits — **kein Issue**; nur Sort explizit |

### Ressourcen `FinanceManager.Web/Resources/Pages.{resx,de.resx,en.resx}`

- **Neu:** `Ribbon_Group_View` — de „Ansicht", en „View", neutral „View".
- **Neu:** `Ribbon_Group_Data` — de „Daten", en „Data", neutral „Data".
- **Neu:** `Hint_RebuildAggregates` — de „Berechnet alle Summen aus den Buchungen neu. Kann je nach Datenmenge dauern.", en/neutral „Recomputes all pre-aggregated sums from postings. May take a while depending on data size." (Werte aus `Resources/Components/Pages/Setup.*.resx` übernommen; der `Pages`-Localizer zeigt sonst den Roh-Key).
- **Geändert:** `Setup_Section_Update` — de „Programmversion", en/neutral „Program version" (betrifft Ribbon-Gruppe und Setup-Tab-Label — gewünscht).
- **Geändert:** `SetupBackup_Titel` — de „Sicherungen" (en/neutral „Backups" bereits passend, unverändert; betrifft Ribbon-Gruppe und `SetupBackupTab`-Überschrift).
- **Geändert:** `Ribbon_SetPassword` — de „Passwort zurücksetzen", en/neutral „Reset password".

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es gibt keine neuen oder geänderten Benutzereingaben; `Hidden`/`Disabled` und die bedingte Gruppenerzeugung sind Darstellungsbedingungen, keine Validierungen.

## Konfigurationsänderungen

|| Eintrag | Typ | Standardwert | Zweck |
||---------|-----|--------------|-------|
|| `wwwroot/manifest.json` → `theme_color`, `background_color` | statisches Asset | `#080a0f` | PWA-Theme an neue Dark-Farbgebung angleichen (bisher `#ff5722`) |

## Seiteneffekte und Risiken

- **`Ribbon.razor` Empty-Group-Filter:** Gruppen ohne sichtbare Items werden künftig nicht gerendert. Verifiziert ohne Bestands-Nebenwirkung: Kein vorhandenes VM liefert eine ausschließlich versteckte Gruppe (`StatementDraftCardViewModel`-QuickEdit-Aktionen sind wechselseitig versteckt, mindestens eines ist immer sichtbar; `McpSecurity` teilt die KI-Gruppe mit immer sichtbaren `McpApprovals`/`McpAudit`). Gruppen mit nur `Disabled`-Aktionen bleiben sichtbar (`Disabled` wird nicht gefiltert). Die Mobile-Ansicht profitiert mit (leeres Gruppen-Panel entfällt).
- **E2E-Selektoren:** `UiRibbonAction.Id`s bleiben stabil → `button#<Id>`-Selektoren funktionieren weiter; **aber** `BudgetReportDemoDataE2ETests` L79 assertiert `WaitForURLAsync("**/reports/budget")` — der Glob matcht die neue `?back=`-Query **nicht** → muss auf Predicate `url.Contains("/reports/budget")` o. ä. umgestellt werden. `RibbonGroupOrderPlaywrightTests` (Gruppen-First/Last-Assertions auf Card-Seiten) bleibt gültig, wird aber für die geänderten Ansichten erweitert.
- **`?back=` auf `ListPage`:** künftig kann jede Listenroute den Parameter tragen; nur `BankAccountListViewModels` nutzt `InitBack` vorerst — andere List-VMs bleiben unverändert, da sie keine bedingte Navigieren-Gruppe definieren. Ein reiner `?back=`-Query-Wechsel löst `OnParametersSetAsync` erneut aus und erzeugt den Provider neu (inkl. Reload der Liste) — konsistent zum bestehenden Verhalten bei Parameterwechseln, als akzeptierter Nebeneffekt dokumentiert.
- **`Setup_Section_Update`-Umbenennung** trifft Ribbon-Gruppen-Titel *und* Setup-Navigations-Tab (gleicher resx-Key) — gewünscht laut Anforderung („Auch das Register umbenennen").
- **`SetupBackup_Titel`-Umbenennung** trifft zusätzlich die `<h4>`-Überschrift in `SetupBackupTab.razor` L9 — fachlich konsistent, dokumentiert.
- **Kind-VM-Aggregation:** `BudgetPurposeCardViewModel` hängt `NewRule` von der Aktivität des eingebetteten `BudgetRuleListViewModel` ab (`IsChildViewModelActive`); mit `Sort = 60` bleibt „Regeln" auch bei anderer Erzeugungsreihenfolge hinter „Verknüpfte Informationen".
- **`Pages.resx.new`-Artefakte:** drei leere `*.resx.new`-Dateien liegen im `Resources/`-Ordner (Merge-Skript-Reste); sie werden im Zuge der resx-Arbeiten entfernt, damit sie nicht versehentlich als aktuelle Ressourcen missverstanden werden.
- **Eingebettete `GenericListPage`-Nutzungen** (`SetupMcpTab`, `GenericCardPage`-EmbeddedList): der `.card-page-content`-Wrapper wird bewusst in `ListPage.razor` gesetzt — eingebettete Nutzungen bleiben unverändert ohne Doppel-Panel.
- **Analyse-Buttons Budgetübersicht/-bericht:** nur Gruppen-/Größenzuordnung wird geändert; das uneinheitliche Verhalten bleibt (Issue Nr. 5).
- **`BudgetPurposeListViewModel`:** die bisherige Gruppe trug den Titel `Ribbon_Group_Navigation` für `New`/`Reload`/`Categories`/`ClearFilter`; sie wird aufgeteilt in `Ribbon_Group_Manage` und `Ribbon_Group_View` — bei der Umsetzung sicherstellen, dass kein bUnit-/E2E-Test den alten Titel auf dieser Ansicht erwartet (in `RibbonGroupOrderPlaywrightTests` mit prüfen).

## Umsetzungsreihenfolge

1. **resx-Keys anlegen/umbenennen**
   - Voraussetzungen: keine.
   - `Ribbon_Group_View`, `Ribbon_Group_Data`, `Hint_RebuildAggregates` in `Pages.resx`/`Pages.de.resx`/`Pages.en.resx` anlegen; Werte `Setup_Section_Update`, `SetupBackup_Titel`, `Ribbon_SetPassword` umbenennen; leere `*.resx.new`-Artefakte entfernen.

2. **`SetBackNavigation`/`InitBack` in `BaseViewModel` hochziehen**
   - Voraussetzungen: keine (Umzug aus `BaseCardViewModel`; `ICardInitializable` bleibt bestehen).
   - `_initBack`, `InitBack`, `SetBackNavigation` nach `BaseViewModel` verschieben; `BaseCardViewModel` erbt.

3. **`Ribbon.razor` Empty-Group-Filter + `ListPage.razor` erweitern (`?back=`, `OpenBudgetReport`-Ziel, `.card-page-content`-Wrapper)**
   - Voraussetzungen: Schritt 2 (`SetBackNavigation` auf `BaseViewModel`).
   - `groups.RemoveAll(g => g.Items.Count == 0)` nach L219; Query-Parameter, `SetBackNavigation`-Aufruf direkt nach Provider-Erzeugung, `case "Back"`-Präferenz auf den `Back`-Parameter, `reports/budget?back=`-Ziel, Wrapper-Markup.

4. **`BudgetReport.razor` `?back=` auswerten**
   - Voraussetzungen: Schritt 2.
   - Parameter + `SetBackNavigation` in `OnParametersSet` + `case "Back"`-Handling.

5. **Ribbon-Zielkonfiguration in allen 20 ViewModels umsetzen**
   - Voraussetzungen: Schritte 1–4 (neue resx-Keys, `InitBack` für `BankAccountListViewModels`/`BudgetReportViewModel`, `?back=` für den Budgetbericht-Kontext).
   - Pro VM `GetRibbonRegisterDefinition` gemäß Zieltabelle umstellen: Gruppen mit `Sort`, Aktionen mit Ziel-`UiRibbonItemSize`, **bedingte Gruppenerzeugung** der Navigieren-Tabs bei `InitBack`; inkl. `BudgetRuleListViewModel`-Sort.

6. **`.icon-btn.danger`-CSS-Korrektur**
   - Voraussetzungen: keine (parallel zu Schritt 5 möglich).
   - Neue spezifischere Regel in `theme.Dark.css` ergänzen.

7. **Favicon erzeugen + einbinden**
   - Voraussetzungen: Pillow (im Umfeld verfügbar).
   - `finance_manager.svg` (Master), `scripts/icons/generate_favicon.py` → `finance_manager-64x64.ico` + `finance_manager.png`; `App.razor`-Link, `manifest.json`-Farben.

8. **Tests anpassen/neu schreiben (bUnit + E2E)**
   - Voraussetzungen: Schritte 1–5.
   - Betroffene Tests anpassen (s. „Betroffene bestehende Tests"), neue bUnit-/E2E-Tests gemäß Testabschnitt.

9. **GitHub-Issues anlegen**
   - Voraussetzungen: keine (parallel jederzeit möglich; sinnvoll nach Festigung der Ist-Analyse).
   - **6 Aktions-Issues (A1–A6) + 10 Fehlverhalten-Issues (B1–B10) = 16 Issues** via `gh issue create`, jeweils mit Bezug „Ref #590".

10. **Screenshot-Abnahme**
    - Voraussetzungen: Schritte 1–7.
    - `scripts/screenshots/generate-screenshots.js` ausführen (App-Start, Demo-Daten) → `Docs/screenshots` aktualisieren; ergänzend gezielte Shots der geänderten Ribbon-Ansichten, des Sicherungs-Tabs und des Favicons zur Sichtprüfung bereitstellen.

## Tests

### Neue Tests

|| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
||--------------------|------------|-------------------------------------|
|| `GetRibbonRegisters` liefert Ziel-Gruppenreihenfolge/-titel und `UiRibbonItemSize` je Aktion | `FinanceManager.Tests/ViewModels/*RibbonConfigTests` (je VM oder parametrisiert) | bUnit/VM-Test der Zielkonfiguration für repräsentative VMs: `ContactListViewModel` (Verwalten→Ansicht, ClearFilter S), `SecuritiesListViewModel` (Manage/View/Reports, Größen), `SecurityCardViewModel` (Linked ohne Performance, Reports L), `BankAccountListViewModels` (Verwalten+Filter, Navigieren konditional), `BudgetReportViewModel` (Navigieren konditional, Settings L), `SetupCardViewModel`+Sektions-VMs (Daten-Gruppe, Sort-Ordnung Verwalten→Daten→Profil→KI→Programmversion→Sicherungen) |
|| Navigieren-Tab wird nur bei gesetztem `InitBack` erzeugt | VM-Test `BankAccountListViewModels`, `BudgetReportViewModel` | Ohne `SetBackNavigation`: kein `Ribbon_Group_Navigation`-Tab in den Registern; nach `SetBackNavigation(url)`: Tab mit `Back` L vorhanden; nach `SetBackNavigation(null)`: Tab wieder weg |
|| Tab mit ausschließlich `Hidden`-Aktionen rendert keine Gruppe | `Components/RibbonTests.cs` (neuer Fact, z. B. `HiddenOnlyGroup_IsNotRendered`) | Register mit einem Tab, dessen einziges Item `Hidden = true` → kein `.fm-ribbon-group`, kein Footer-Titel im DOM (sichert den `Ribbon.razor`-Empty-Group-Filter) |
|| Keine „Ansicht"-Gruppe ohne vorhandene Aktionen | `Web/UsersViewModelTests` (Schärfung von `GetRibbon_ShouldComposeGroups_ByState`) | `UserListViewModel` liefert genau einen Tab mit Titel `Ribbon_Group_Manage`; kein `Ribbon_Group_View`-Tab (bUnit-Anker für Akzeptanzszenario 10) |

### Betroffene bestehende Tests

|| Test / Testklasse | Grund der Anpassung |
||-------------------|---------------------|
|| `Web/ViewModels/ContactsViewModelTests.GetRibbon_UsesManageGroup_WithAllActionsLarge` (L168–183) | **Bricht:** assertiert `Assert.Single(tabs)`, Titel `Ribbon_Group_Manage`, IDs `{New, Groups, Reload, ClearFilter}` alle `Large`. Neu: zwei Tabs — Verwalten (`New` L, `Groups` L) + Ansicht (`Reload` L, `ClearFilter` S). Anpassung: 2 Tabs mit Titeln `Ribbon_Group_Manage`/`Ribbon_Group_View`, Größen je Gruppe assertieren, `ClearFilter` = `Small` |
|| `ViewModels/SetupCardViewModelTests.GetRibbonRegisters_AfterLoad_PlacesManageGroupFirst` (L227–245) | **Bricht:** assertiert `orderedTabs[0].Sort == 0` — Konvention setzt Verwalten = 10. Anpassung: `orderedTabs[0]` bleibt `Ribbon_Group_Manage`, `Sort == 10` (bzw. Sort relativ zu den übrigen Gruppen assertieren) |
|| `ViewModels/SetupCardViewModelTests.GetRibbonRegisters_AfterLoad_UsesExpectedActionSizes` (L252–271) | **Bricht:** liest `RebuildAggregates` aus dem `Ribbon_Group_Manage`-Tab — wandert in `Ribbon_Group_Data`. Anpassung: `Save` L/`Reset` S weiter in Manage; `RebuildAggregates`/`ResetReportCache` (beide S) aus dem `Ribbon_Group_Data`-Tab assertieren |
|| `ViewModels/AccountsViewModelTests.SetFilter_AffectsLoad_AndRibbon` (L113–130) | **Bricht:** `g.Items` auf `UiRibbonRegister` liefert nur `Tabs[0].Items` (`RibbonModels.cs` L107) — `ClearSearch` wandert in den Filter-Tab und ist dort nicht mehr sichtbar. Anpassung: über `r.Tabs.SelectMany(t => t.Items)` flachen und `ClearSearch` im Filter-Tab assertieren |
|| `RibbonGroupOrderPlaywrightTests` (E2E) | Anpassen/**erweitern**: die drei Bestands-Facts bleiben gültig (`SavingsPlanCard`/`BankAccountCard`/`PostingsCard` — Reihenfolge unverändert); neue Facts für geänderte Ansichten (Gruppentitel/-reihenfolge auf Kontaktliste, Wertpapierliste, Einstellungen, Benutzerliste — s. E2E-Tabelle) |
|| `AccountsOverviewStatisticsPlaywrightTests` (E2E) | Erweitern um Disabled/Enabled-Assertion für `#ClearSearch`; bestehende `ToBeVisibleAsync`-Assertions auf `#New`/`#ClearSearch` bleiben gültig (beide Aktionen bleiben sichtbar) |
|| `BudgetReportDemoDataE2ETests` (E2E, L79) | **Bricht:** `WaitForURLAsync("**/reports/budget")` matcht nicht mehr, sobald `?back=…` angehängt wird. Anpassung: Predicate `url.Contains("/reports/budget")` oder Glob `**/reports/budget**`; restliche Assertions unverändert (`#Back` erscheint zusätzlich — kein Konflikt) |
|| `Web/UsersViewModelTests.GetRibbon_ShouldComposeGroups_ByState` (L224–232) | Bleibt gültig (`groups.Count == 1` — Ansicht-Tab entsteht nicht), wird **geschärft**: Tab-Titel = `Ribbon_Group_Manage` assertieren (Nachweis „keine leere Ansicht-Gruppe") |

**Verifiziert weiterhin gültig (keine Anpassung nötig):** `ContactsViewModelTests.SetFilterAsync_ShouldResetAndReload_AndRibbonIncludesClear` (`ClearFilter`/`ClearSearch` existiert weiter), `AccountsViewModelTests.GetRibbon_ContainsNew` (`New` liegt in `Tabs[0]` = Verwalten, da Navigieren ohne `InitBack` nicht erzeugt wird), `SavingsPlanCardLinkTests` (`GetRibbonRegisters_GroupOrder_PlacesLinkedLast` L333–353 — gleiche 5 Gruppentitel/-reihenfolge; `Ribbon_ActualReport_EnabledOnlyWithoutLinks` — nur `Disabled`/`Tooltip`), `SecurityCategoryDetailViewModelTests.Ribbon_Disables_Save_When_Name_Short` (`groups.Count == 2` bleibt: Navigieren+Verwalten), `SecurityCategoriesViewModelTests.Ribbon_Has_Actions` (resilient flach assertiert), `SecurityCardViewModelTests` (ImportPrices-Absenz/Prices-Callback unverändert), `SetupUpdateViewModelTests.GetRibbonRegisters_WhenReadyButNoConfirmationCallback_DisablesInstallAction` (nur `Disabled`), `SetupCardViewModelTests` übrige Facts (`ExposesSingleGlobalSaveAndReset`, `IncludesBackupSectionActions`, `AfterAdminLoad_IncludesUpdateActions`, `HidesMcpSecurityForNonAdmin`, `AfterEnabledLoad_IncludesMcpGroupActions`, globaler Save-Callback — flachen alle Tabs oder assertieren nur Präsenz), `SavingsPlan*`-Smoke-Tests (`NotNull`-Assertions), `ContactCategoriesViewModelTests` und `AccountDetailViewModelTests` (unbenannte Ansichten, unverändert), `RibbonTests.cs` Bestands-Facts (kein Signatur-/Verhaltensbruch — Empty-Group-Filter ändert keinen bestehenden Fall, da kein Test Hidden-only-Tabs rendert).

### E2E-Tests (primärer Funktionsnachweis)

|| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
||-----------|----------|------------------------|-------------------------------|-------------------|
|| Pflicht | `/list/accounts` ohne `?back=`: kein `Back`-Button **und keine leere „Navigieren"-Gruppe** (`.fm-ribbon-group` zählen / Footer-Texte prüfen); mit `?back=<url>`: `#Back` sichtbar (groß) in Navigieren-Gruppe an Position 0, Klick navigiert zum Ziel | `AccountsBackNavigationPlaywrightTests` (neu) | Szenario 3 | Nur Browser-Navigation erzeugt den `?back=`-Kontext; bedingte Gruppenerzeugung muss im echten DOM wirken — inkl. Negativ-Assertion „keine leere Gruppe" |
|| Pflicht | `list/budget` → `#BudgetReport` → Budgetbericht zeigt `#Back` (groß) → Klick zurück auf `list/budget`; direkter Aufruf `reports/budget` zeigt **weder `#Back` noch eine leere Navigieren-Gruppe** | `BudgetReportBackNavigationPlaywrightTests` (neu) | Szenario 4 | Ketten-Navigation über zwei Seiten + konditionale Gruppe im DOM |
|| Pflicht | Gruppenreihenfolge/-titel und Button-Größen im DOM je geänderter Ansicht (Kontaktliste Verwalten→Ansicht; Wertpapierliste Verwalten→Ansicht→Berichte; Einstellungen Verwalten→Daten→…→Sicherungen) | `RibbonGroupOrderPlaywrightTests` (anpassen/erweitern) | Szenario 1, 8 | Gruppenstruktur/-reihenfolge ist nur im gerenderten Ribbon prüfbar |
|| Pflicht | Benutzerübersicht `/list/users` (Admin-Login via `TestUserSeeder.EnsureUserAsync(isAdmin: true)`): genau eine `.fm-ribbon-group` („Verwalten"), **kein „Ansicht"-Gruppen-Header** | `RibbonGroupOrderPlaywrightTests` (neuer Fact, z. B. `UsersList_RendersNoEmptyViewGroup`) | Szenario 10 | Der Fehlerfall „leere sichtbare Gruppe" ist nur im gerenderten DOM nachweisbar; fehlende Aktionen (→ Issues A3) dürfen keine Geister-Gruppe erzeugen |
|| Pflicht | Übersichtsseite rendert Inhalt innerhalb `.card-page-content`; BoundingBox-Vergleich: Listeninhalt liegt innerhalb des Panels, kein Versatz gegenüber `CardPage`-Inhalt | `ListPageContentPanelPlaywrightTests` (neu) | Szenario 5 | Layout-Überlauf/Sprung ist nur geometrisch im Browser nachweisbar (Panel-Assertion analog Overlay-Referenz `0a94bb74`) |
|| Pflicht | `#ClearSearch`/`#ClearFilter` disabled ohne Suchbegriff, enabled nach Eingabe | Erweiterung `AccountsOverviewStatisticsPlaywrightTests` bzw. `ListPageGateway` | Szenario 2 | `Disabled`-Zustand entsteht erst durch UI-Interaktion (Tippen ins Suchfeld) |
|| Pflicht | Sicherungs-Tab: `button.icon-btn.danger` hat `color: #000` ≠ Hintergrund (Kontrast) im Nicht-Hover | `SetupBackupTabPlaywrightTests` (neu, CSS-Assertion via `evaluate`) | Szenario 6 | Farbkontrast ist nur mit gerendertem CSS prüfbar |
|| Pflicht | Setup-Ribbon: Tooltip von `RebuildAggregates` zeigt übersetzten Text statt Roh-Key | `RibbonGroupOrderPlaywrightTests`-Erweiterung (Tooltip-Assertion, Szenario 8) | Szenario 8 | Übersetzter Tooltip-Text ist DOM-Attribut, nur im Browser prüfbar |
|| Optional | `<link rel="icon"…>` zeigt auf neues Icon; Icon-Datei liefert HTTP 200 | einfacher E2E-Check oder Bestandteil des Setup-Tests | Szenario 7 | Verifikation der Einbindung |

**Bestehende E2E-Tests anzupassen:**

|| Test / Testklasse | Grund der Anpassung |
||-------------------|---------------------|
|| `BudgetReportDemoDataE2ETests` (L79) | `WaitForURLAsync("**/reports/budget")` bricht durch angehängte `?back=`-Query → Predicate/`**`-Glob umstellen |
|| `RibbonGroupOrderPlaywrightTests` | Erweiterung um neue Facts (s. o.); Bestands-Facts bleiben unverändert gültig |
|| `AccountsOverviewStatisticsPlaywrightTests` | Erweiterung um Disabled/Enabled-Assertion `#ClearSearch` |

**Verifiziert unbetroffen:** `AuthenticationFlowPlaywrightTests` (`#Reload` liegt auf `/reports/dashboard` — nicht im Scope; `/list/accounts`-Aufruf L202 ohne Ribbon-Assertion), `ConfirmationDialogE2ETests` (`#Delete`/`#Attachments` auf Card-Seiten, unverändert sichtbar), `PostingsCardLinkedActionsPlaywrightTests` (`#Back` auf Posting-Card — `?back=`-Muster dort unverändert), `SetupUpdateGateway`/`UpdateSetupPlaywrightTests` (`#UpdateCheckNow` bleibt sichtbar in Programmversion-Gruppe), `LoadingBarPlaywrightTests` (`#Settings` auf `reports/budget` bleibt sichtbar), Report-Tests (`#Reload` auf nicht betroffenen Berichtsseiten). Bekannter Bestandsfehler `HomeMassImportPlaywrightTests.UploadIngCsvNewFormat…` ist ribbon-unabhängig und bleibt als Bestandsfehler dokumentiert.

## Akzeptanzszenarien

||| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|||---|----------|----------|------------------|----------|-------------------|----------|
||| 1 | Happy Path: Zielkonfiguration im Ribbon | Nutzer öffnet Kontaktübersicht (`/list/contacts`) | Nutzer angemeldet; VM liefert Register | Seite rendert → `Ribbon` baut Gruppen | Gruppen „Verwalten" (Neu L, Gruppen L) und „Ansicht" (Neu laden L, Suche zurücksetzen S) in dieser Reihenfolge; „Suche zurücksetzen" disabled | E2E `RibbonGroupOrderPlaywrightTests` + Screenshot-Abnahme |
||| 2 | „Suche zurücksetzen"-Aktivierung | Nutzer tippt Suchbegriff in Listen-Suchfeld | Kontaktübersicht geöffnet, `#ClearFilter` disabled | Eingabe → `Search` gesetzt → Re-Render | `#ClearFilter` wird enabled; Klick leert Suche und lädt Liste neu | E2E Disabled/Enabled-Assertion |
||| 3 | Konditionales Zurück — Kontenübersicht | Direktaufruf `/list/accounts` bzw. `/list/accounts?back=<url>` | Kein `?back=` im ersten Fall | Seite rendert Ribbon | Ohne Parameter: **keine** „Navigieren"-Gruppe (kein Button, kein leerer Gruppenrahmen); mit Parameter: `Back` groß an Position 0, Klick navigiert zum `back`-Ziel | E2E `AccountsBackNavigationPlaywrightTests` |
||| 4 | Konditionales Zurück — Budgetbericht | Klick auf „Bericht" in Budgetübersicht | `list/budget` geöffnet | `#BudgetReport` → `reports/budget?back=…` → `#Back` sichtbar → Klick | `Back` groß in Gruppe „Navigieren" nur in diesem Kontext; Rücksprung auf `list/budget`; Direktaufruf `reports/budget` ohne `Back` und ohne leere Gruppe | E2E `BudgetReportBackNavigationPlaywrightTests` |
||| 5 | Inhaltspanel ohne Layout-Sprung | Nutzer wechselt Übersicht ↔ Detail | Beliebige Liste und Karte | `/list/contacts` → Klick auf Eintrag → `/card/contacts/{id}` | Liste und Karte haben denselben `.card-page-content`-Container — gleiche Abstände, kein sichtbarer Versatz von Menü/Inhalt | E2E BoundingBox + Screenshot-Abnahme |
||| 6 | Lesbares Löschen-Symbol | Nutzer öffnet Einstellungen → Sicherungen | Mind. ein Backup vorhanden | `SetupBackupTab` rendert Tabelle | Delete-Icon ist schwarz auf rotem Grund lesbar; im Hover roter Text auf dunklem Panel | E2E CSS-Assertion + Screenshot |
||| 7 | Neuer Favicon | Nutzer öffnet beliebige Seite | Neue Icon-Dateien ausgeliefert | Browser lädt `App.razor`-Links | `icons/finance_manager.svg` + `finance_manager-64x64.ico` liefern 200 und zeigen das neue Motiv (Teal/Violett auf Dunkel) | HTTP-Check + Sichtprüfung |
||| 8 | Setup-Gruppen umbenannt + neu sortiert | Nutzer öffnet Einstellungen | Sektions-VMs aktiv | Ribbon rendert | Gruppenreihenfolge Verwalten → Daten → Profil → KI → Programmversion → Sicherungen; Tooltip von „Aggregatposten neu berechnen" zeigt übersetzten Text statt Roh-Key | E2E Gruppenreihenfolge + Tooltip-Text-Assertion |
||| 9 | Issues angelegt | Umsetzung des Issue-Schritts | `gh` authentifiziert | `gh issue create` × 16 | 16 Issues (6 fehlende Aktionsarten A1–A6 + 10 Fehlverhalten B1–B10) im Repo, jeweils mit Bezug „Ref #590" | `gh issue list` |
||| 10 | Fehlende Aktion ohne Auswirkung | Nutzer (Admin) öffnet Benutzerübersicht `/list/users` | `Reload`/`ClearSearch` noch nicht implementiert | Ribbon rendert | Genau eine Gruppe „Verwalten" mit „Neu"; **kein** leerer „Ansicht"-Gruppen-Header/Rahmen | E2E `RibbonGroupOrderPlaywrightTests` (neuer Fact) + bUnit `UsersViewModelTests` (geschärft) |

## Offene Punkte

Alle Punkte wurden per dokumentiertem Vorschlag entschieden (keine fachliche Rückfrage nötig — Entscheidungen folgen direkt aus Anforderungstext und Bestandskonventionen; die Annahme zu Punkt 1 wird im Abschlusskommentar am PR transparent gemacht):

||| # | Offener Punkt | Entscheidung |
|||---|---------------|--------------|
||| 1 | Bestätigung der Interpretation „zweite Sparplanübersicht = Sparplankategorieübersicht" (Annahme oben) | **Übernommen** — die erste Auflistung deckt `SavingsPlansListViewModel` bereits ab; die zweite mit „Zurück"/„Neu laden"/„Suche zurücksetzen" passt nur zur Kategorieübersicht. Wird im PR-Kommentar als Annahme kenntlich gemacht |
||| 2 | Auf welche Ziel-URI `OpenBudgetReport` den `?back=`-Wert setzt — absolute aktuelle URI (`Nav.Uri`, wie `BaseViewModel` L354) oder feste Route `list/budget` | **`Nav.Uri`** — konsistent zum bestehenden `?back=`-Muster; funktioniert auch bei Unterrouten/Suchfiltern |
||| 3 | Ob `NewRule` in der Budgetzweckdetailansicht tatsächlich sichtbar ist, hängt von `IsChildViewModelActive`/`CreateSubViewModel`-Aktivierung des eingebetteten Regel-VMs ab | **Wird in der Implementierung verifiziert**; nur falls unsichtbar, Issue stattdessen anlegen |
||| 4 | Umfang der Icon-Dateien — ob neben `.ico`/`.png` auch die sieben Bereichs-Icons (`finance_manager-accounts.png` u. a.) neu gestaltet werden sollen | **Nur das App-Icon** (`finance_manager.*`) — die Bereichs-Icons wurden im Kommentar nicht genannt |
