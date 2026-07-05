← [Zurück zur Übersicht](index.md)

# Mobile Ansicht (Responsive Web-UI) — Installation und Konfiguration

## Voraussetzungen

- Die Web-Anwendung `FinanceManager.Web` ist gebaut und startet regulär.
- Für mobile E2E-Prüfungen ist die Playwright-Testumgebung verfügbar.

## Installationsschritte

1. Keine zusätzlichen Installationsschritte für die responsive UI erforderlich.
2. Für Testausführung die vorhandene E2E-Infrastruktur verwenden.

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `@media (max-width: 900px)` | CSS-Breakpoint | `900px` | Schaltet mobile Layoutregeln in den globalen und seitenbezogenen Styles ein. |
| `PlaywrightSessionOptions.ViewportSize.Width` | int | `390` | Mobile Test-Viewport-Breite in `CreateMobileSessionAsync()`. |
| `PlaywrightSessionOptions.ViewportSize.Height` | int | `844` | Mobile Test-Viewport-Höhe in `CreateMobileSessionAsync()`. |
| `PlaywrightSessionOptions.IsMobile` | bool | `true` | Aktiviert mobiles Browserverhalten für E2E-Tests. |
| `PlaywrightSessionOptions.HasTouch` | bool | `true` | Aktiviert Touch-Interaktionen für E2E-Tests. |

## Überprüfung

- UI-Überprüfung: Anwendung im schmalen Browserfenster öffnen und Navigation, Listen, Karten und Berichtsseiten bedienen.
- Automatisierte Überprüfung: Mobile Tests mit Suffix `_OnMobileViewport` in der E2E-Suite ausführen.

