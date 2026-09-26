# Stil / Schichtung (z-index)

## `FinanceManager.Web/wwwroot/css/app.css`

| Regel | Zeilen | Relevante Deklarationen |
|-------|--------|--------------------------|
| `.split-center` | 409–417 | `position: fixed; inset: 0; display: flex; align-items: center; justify-content: center; z-index: 1000; padding: 1.5rem;` — einzige Schichtdefinition für Overlay **und** Bestätigungsdialog. |
| `.split-dialog` | 419–423 | `width: 100%; border-radius: var(--radius); padding: 1.25rem 1.35rem 1rem 1.35rem;` — kein `z-index`. |
| `.confirm-dialog` | 425–428 | `max-width: min(28rem, calc(100% - 2rem)); width: 100%;` — rein layoutbezogen, **kein** `z-index`, keine eigene Schicht. |
| `.confirm-dialog h2` / `.confirm-dialog p` | 430–441 | Typografie. |
| Sticky Tabellenkopf (`th`-Kontext) | ~500–506 | `z-index: 500`. |
| `.mobile-overlay` | ~531 | `z-index: 400`. |
| `.sidebar` (Mobile, in `@media`) | ~635 | `z-index: 450`. |
| `.split-center` (Mobile, `@media (max-width: 900px)`) | 692–694 | Nur `padding: .5rem;` — kein `z-index`-Override. |
| `.split-dialog` (Mobile) | 695–699 | `max-height: 92vh; overflow-y: auto; padding: .9rem;` |
| `#blazor-error-ui` | 744–756 | `z-index: 1000`. |

`confirm-severity-*`-Klassen: `ConfirmDialog` vergibt `confirm-severity-default|warning|critical`; in `app.css`/`theme.Dark.css` existieren **keine** entsprechenden Regeln (kein Severity-Styling vorhanden).

## `FinanceManager.Web/wwwroot/css/theme.Dark.css`

| Regel | Zeilen | Relevante Deklarationen |
|-------|--------|--------------------------|
| `.split-center` | 131–133 | `background: rgba(0,0,0,.55);` — nur Backdrop-Farbe, **kein** `z-index`. |
| `.split-dialog` | 135–139 | `background`, `border`, `box-shadow` — **kein** `z-index`. |

Die Schichtung ist damit ausschließlich in `app.css` definiert; `theme.Dark.css` ist konsistent und enthält keinen konkurrierenden `z-index`.

## Weitere Overlay-/Dialog-`z-index`-Regeln in Komponenten-CSS

| Datei | Selektor | `z-index` | Verwendet in |
|-------|----------|-----------|--------------|
| `app.ContactMergeDialog.css` | `.modal-backdrop` (Zeile 12) | 1000 | `Setup/SetupBackupTab.razor` Zeile 117 |
| `app.ReportDashboard.css` | `.modal-overlay` (Zeile 30) | 1000 | `ReportDashboard.razor` Zeilen 102/379/400 |
| `app.SecurityPrices.css` | `.modal-overlay` (Zeile 12) | 1000 | Security-Prices-Dialoge |
| `theme.Dark.HomeKpiGrid.css` | `.modal-overlay` (Zeile 38) | 1000 | `HomeKpiGrid.razor` Zeile 52 |
| `app.ReturnSummaryWidget.css` | `.rsw-backdrop` (Zeile 101) / Panel (Zeile 115) | 900 / 901 | `ReturnSummaryWidget.razor` Zeile 100 |
| `app.HomeKpiGrid.css` | div. (Zeilen 185–365) | 0–60 | KPI-Karteninterna |
| `app.BudgetReport.css` | Zeilen 61, 95 | 3 / 1 | Berichtsinterna |

## `.split-center`-Verwendungen in Razor-Markup

| Datei | Zeile | Zweck |
|-------|-------|-------|
| `Components/Shared/ConfirmDialog.razor` | 7 | Bestätigungsdialog-Backdrop (`@onclick="OnBackdropClick"`) |
| `Components/Shared/OverlayHost.razor` | 10 | Generischer Overlay-Backdrop (`@onclick="CloseOverlay"`) |
| `Components/Pages/ListPage.razor` | 56 | Listen-Overlay `split-center list-overlay` |
| `Components/Pages/Home.razor` | 70 | Mass-Import-Dialog |
| `Components/Pages/BudgetReport.razor` | 313, 373 | Purpose-Postings- bzw. Settings-Overlay |

Feststellung: Alle genannten `.split-center`-Container teilen sich `z-index: 1000`; bei gleicher Schicht entscheidet die DOM-Reihenfolge. `ConfirmationDialogHost` steht auf allen Seiten vor den Overlay-Containern (siehe `logic.md`).
