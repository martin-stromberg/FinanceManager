# Inventar: Layout, Styles und Ressourcen

Detaildokument zur Inventur (Commit `67121fa8`, 2026-11-27). Deckt die drei im Requirement
genannten Korrekturen plus resx-Befund ab.

## 1. Inhaltspanel auf Übersichtsseiten (Fehler)

- `FinanceManager.Web/Components/Pages/CardPage.razor` L35ff: Die Detailansicht wrappt Ribbon +
  Inhalt in `<div class="card-page-content">` und rendert `GenericCardPage`.
- `FinanceManager.Web/Components/Pages/ListPage.razor` L28ff: Die Übersicht rendert `Ribbon`
  (L28) und `GenericListPage` (L39) **ohne** umschließenden Content-Container.
- `FinanceManager.Web/Components/Pages/GenericListPage.razor` erzeugt die Tabelle in
  `.table-responsive.generic-list-table-wrap`.
- `FinanceManager.Web/wwwroot/css/app.css` ~L329–366: `.card-view`/`.card-page-content` erhalten
  den Glas-/Panel-Look (Hintergrund + `padding: 1rem 1.25rem`); `.table-responsive` trägt ein
  ähnliches Styling, aber ohne den konsistenten Wrapper-Abstand → Inhalt klebt am aufgeklappten
  Menü, und der Wechsel Übersicht ↔ Detail springt.
- **Fix-Richtung:** Wrapper analog `.card-page-content` um den `GenericListPage`-Bereich in
  `ListPage.razor` (oder Container in `GenericListPage.razor`), damit Abstand/Padding identisch
  zur Kartenansicht sind.

## 2. Löschen-Symbol in der Sicherungs-Tabelle (Fehler)

Markup: `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor`

- Desktop-Tabelle L52–58: `button.icon-btn` (Restore), `a.icon-btn` (Download),
  `button.icon-btn.danger` (Delete, L58) mit `<svg class="icon"><use href="icons/sprite.svg#delete">`.
- Mobile-Cards L99–105: gleiche Struktur, `icon-btn danger` an L105.

CSS:

- `wwwroot/css/theme.Dark.css` L140–147: `button.danger { background: var(--danger); color:
  #2a0606; }` — Spezifität (0,1,1) schlägt `.icon-btn`-Hintergrund (0,1,0).
- `theme.Dark.css` L174–193: `.icon-btn { background: var(--bg-alt); color: var(--text); }`,
  `.icon-btn.danger { color: var(--danger); }` (L186–188), Hover varianten L180–193.
- `wwwroot/css/app.css` L622–631: `.icon-btn`-Basislayout (Größe, `svg`-Sizing).

**Konflikt:** `<button class="icon-btn danger">` erbt `background: var(--danger)` von
`button.danger` (rot), aber `color: var(--danger)` von `.icon-btn.danger` (rot) → rotes Icon auf
rotem Grund = unleserlich. **Fix-Richtung:** `.icon-btn.danger` (nicht-hover) Icon-/Textfarbe auf
schwarz bzw. `--text`-ähnlich stellen (Requirement: „Text/Symbol schwarz, solange die Maus nicht
über dem Button steht; Hover darf abweichen") — z. B. spezifischere Regel `.icon-btn.danger` bzw.
`button.icon-btn.danger` mit eigener `color`, und bestehende Hover-Regel L190–193 beibehalten/anpassen.
Gleiches gilt ggf. für das Light-Theme (`theme.Light.css`/Pendant prüfen — im Dark-Theme befindet
sich die Regel an den genannten Zeilen).

## 3. Favicon

- `FinanceManager.Web/Components/App.razor` L8:
  `<link rel="shortcut icon" type="image/icon" href="icons/finance_manager-64x64.ico">`
- `FinanceManager.Web/wwwroot/manifest.json`: verweist auf `icons/finance_manager.png`
  (1024×1024, `theme_color: #ff5722`).
- `FinanceManager.Web/wwwroot/icons/`: `finance_manager-64x64.ico`, `finance_manager.png` +
  Bereichs-Icons (`finance_manager-accounts.png`, `-contacts`, `-reports`, `-savingplans`,
  `-securities`, `-setup`, `-statementdrafts`) und `sprite.svg`.
- **Fix-Richtung:** neues, zur neuen Farbgestaltung passendes Icon liefern und in `App.razor` L8
  (sowie ggf. `manifest.json`) einbinden. Offene Frage 6: Format-Umfang (nur `.ico` oder auch PNG).

## 4. resx-Befund (`FinanceManager.Web/Resources/`)

Getestet: Vorkommen des `data name="…"`-Keys in `Pages.resx` / `Pages.de.resx` / `Pages.en.resx`.

| Key | Status |
| --- | --- |
| `Ribbon_Group_Navigation`, `_Manage`, `_Actions`, `_Linked`, `_Filter`, `_Analysis`, `_Reports`, `_Export`, `_Bookings`, `_Rules`, `_Fields`, `_Mcp` | vorhanden (3/3 Dateien) |
| **`Ribbon_Group_View` („Ansicht")** | **fehlt (0/3) → neu anlegen (de+en)** |
| `SetupBackup_Titel` („Datensicherungen") | vorhanden; Wert zu „Sicherungen" ändern |
| `Setup_Section_Update` („Aktualisierungen") | vorhanden; Wert zu „Programmversion" ändern (Gruppe + Setup-Tab-Label) |
| `SetupProfile_Title` | vorhanden |
| `Hint_RebuildAggregates` | **fehlt in `Pages.*.resx`** — existiert nur in `Resources/Components/Pages/Setup.de.resx`/`Setup.en.resx`. Da `SetupCardViewModel` den `IStringLocalizer<Pages>` nutzt (`BaseViewModel` L166: `GetService<IStringLocalizer<Pages>>()`), rendert der Tooltip den Roh-Key → bestätigtes Übersetzungsproblem |
| `Hint_ResetReportCache` | vorhanden (3/3) |
| `Hint_SetupUpdate_*` | vorhanden (3/3) |
| `Ribbon_Back`, `Ribbon_New`, `Ribbon_Save`, `Ribbon_Delete`, `Ribbon_Reload`, `Ribbon_Reset`, `Ribbon_Archive`, `Ribbon_Category` | vorhanden (3/3) |

Weitere resx-Key-Familien außerhalb `Pages.*.resx`: `ReportDashboard.*.resx` besitzt eigene Keys
(`Ribbon_Navigation`, `Ribbon_ReportActions`, `Ribbon_Filter`, `Ribbon_View` u. a.) — diese Ansicht
ist nicht im Scope.

**Hinweis:** Im `Resources/`-Verzeichnis liegen `Pages.resx.new`, `Pages.de.resx.new`,
`Pages.en.resx.new` — vermutlich Artefakte eines Generierungs-/Merge-Skripts; bei resx-Änderungen
auf Konsistenz achten bzw. klären, ob sie versioniert/geleert werden müssen.

## 5. Konditionale „Zurück"-Sichtbarkeit (Mechanik)

- `CardPage.razor` L87: `[SupplyParameterFromQuery(Name = "back")] public string? Back` — Detail-
  seiten kennen den `?back=`-Parameter bereits (`BaseViewModel` L357 baut `?back=` in URLs ein;
  `CardPage` L105/236/249/364).
- Die Übersichtsseite (`ListPage.razor`) und die Berichtsseite `reports/budget` kennen `?back=`
  **nicht** — für den konditionalen Zurück-Button auf der Kontenübersicht und dem Budgetbericht
  muss der Kontext anders transportiert werden (z. B. Query-Parameter + `Hidden`, siehe offene
  Fragen 2/3 im Requirement).
- `UiRibbonAction.Hidden` existiert und wird von `Ribbon.razor` L196 ausgewertet;
  Präzedenzfall: `McpAccessCardViewModel`/`SetupMcpViewModel.McpSecurity` (`Hidden = !IsAdmin`).
