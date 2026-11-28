# Plan – PR-Review-Feedback Iteration 3 (Variante B)

## Architektur-Entscheidungen

### R1 — Auf-/zuklappbares Navigationsmenü

**Erkenntnis:** `MainLayout` rendert im statischen SSR-Kontext — nur
Page-Komponenten und explizit markierte Inseln (`@rendermode
InteractiveServer`, z. B. `LogoutNavButton`) sind interaktiv. Ein
`@onclick` im Layout kann nie feuern. Der Toggle wird deshalb
**vollständig clientseitig in JS** umgesetzt (ohnehin schneller und
kreisfehler-resistenter).

- `financeManager.js` → `financeManager.nav` (IIFE-Namespace):
  - `localStorage`-Key `fm.nav.expanded` (`"1"`/`"0"`)
  - `navApply()` spiegelt den Zustand als `html.fm-nav-expanded`-Klasse —
    läuft synchron im `<head>` (kein Flash beim Laden)
  - `navSyncToggle()` aktualisiert `aria-expanded`, `title` und
    Label-Text des Toggle-Buttons (lokalisierte Strings via
    `data-label-expand`/`data-label-collapse` am Button)
  - Delegierter `click`-Listener auf `document` für
    `.nav-collapse-toggle` — überlebt Enhanced Navigation
  - **MutationObserver** auf `document.documentElement[class]`:
    Blazor-Seitenübergänge schreiben `<html>`-Attribute neu (ohne
    `enhancedload`-Event) und würden die Klasse verlieren — der Observer
    setzt sie bei Bedarf wieder. `enhancedload` wird zusätzlich
    abgehört.
- `MainLayout.razor`: statischer `<button class="nav-collapse-toggle">`
  (kein `@onclick`, kein `_navExpanded`-State)
- `app.css`: Expanded-Styles unter `@media (min-width: 901px)` —
  `--rail-width: 224px`, Labels sichtbar, Chevron rotiert; Mobile
  (<900px) bleibt unverändert beim Drawer-Pattern (Toggle dort
  `display:none`).
- Resx: `Nav_Menu_Expand` / `Nav_Menu_Collapse` (de + en)

### R2 — Kartenansicht-Überlauf

Kein globales `box-sizing: border-box` im Projekt: `.card-view`/
`.card-page-content` hatten inline `width:100%` + CSS-Padding →
~2,5 rem Überstand rechts. Fix:
`box-sizing: border-box; min-width: 0; max-width: 100%` auf beiden
Klassen.

### R3 — Menüband ohne horizontale Scrollbars (Issue #586)

- `.fm-ribbon`: `overflow-x:auto` entfernt → `container-type: inline-size`
- `.fm-ribbon-groups`: `flex-wrap: wrap` + `min-width: 0` — Gruppen
  brechen in eine zweite Zeile statt zu scrollen
- `.ribbon-items-grid`: CSS-Grid `grid-auto-flow: column` ×
  `grid-template-rows: repeat(3, auto)` — Small-Aktionen als kompakte
  Zeilen-Pills (icon+text nebeneinander), Large-Aktionen als Kachel
  über `grid-row: span 3` (zwei Button-Größen wie erlaubt)
- Container-Queries (bereits im Projekt etabliert, s. `app.PortfolioReport.css`):
  - `@container (max-width: 1100px)`: `.text-inline` ausgeblendet,
    Small-Buttons → 40px-Icon-Tiles (title/aria-label bleiben)
  - `@container (max-width: 780px)`: auch `.text` der Large-Buttons
    ausgeblendet, Large → 44px
- Mobile Hamburger-Fallback (<900px Viewport) unverändert
- Deaktivierte Aktionen bleiben sichtbar (E2E-Tests assertieren
  `disabled`-Zustand, z. B. `#OpenContact`, `button#Save`)

### Ribbon-Inventar
Extraktion per Python-Skript aus allen `IRibbonProvider`-ViewModels
(`UiRibbonTab`/`UiRibbonAction`, inkl. positionaler + benannter
Argumente und var-Auflösung): **43 ViewModels, 93 Gruppen, 178 Aktionen**
→ `ribbon-inventory.md`, geht mit dem PR-Kommentar an den Anwender
zurück (Stitch-Designplanung).

### Beigefügter Bugfix (blockierte Verifikation)
Gleicher Bare-Module-Specifier-Bug wie auf Variante A:
`import("js/profile.js")` / `import("js/attachments.js")` in
`Register.razor`, `SetupProfileTab.razor`, `AttachmentsPanel.razor` →
`./js/...` + try/catch (ohne Fix stirbt die Circuit beim Registrieren).
