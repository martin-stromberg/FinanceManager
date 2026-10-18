# UI

## `GoldenCrossWidget.razor`
Datei: `FinanceManager.Web/Components/Shared/GoldenCrossWidget.razor`

- `@rendermode InteractiveServer`, Parameter `[Parameter, EditorRequired] Guid SecurityId`.
- Lädt in `OnParametersSetAsync` `ApiClient.Securities_GetGoldenCrossAsync(SecurityId)`; bei Fehler/insufficient data/far bleibt das Widget unsichtbar (`_data.Phase is not (Approaching or Crossed)` → leeres Fragment).
- Aufbau: `div.return-summary-widget.golden-cross-widget` mit `rsw-row`-Zeilen (Label/Wert), Info-Button öffnet Side-Panel (`rsw-panel`) mit Formel-Box, Bedeutungsabschnitten, Einstiegsszenarien, Retest- und Trend-Sektion.
- Lokalisierung via `IStringLocalizer<GoldenCrossWidget>` → `Resources/Components/Shared/GoldenCrossWidget.{de,en}.resx` (Keys wie `Widget_Title`, `Phase_*`, `Label_*`, `Hint_*`, `Section_*`, `Entry_*`, `Retest_*`, `Trend_*`, `Meaning_*`, `Strength_*`, `Window_*`, `DaysAgo`, `CrossDate_Unknown`, `Extended_Unavailable`, `Aria_Info`, `Panel_Close`, `Panel_Formula`, `Formula_Text`).
- Hilfsfunktionen `FormatAmount`, `FormatPercent`, `StrengthKey` privat statisch.
- Styling: `FinanceManager.Web/wwwroot/css/app.ReturnSummaryWidget.css` enthält `golden-cross-widget`/`gcw-*`-Klassen.

## `CardPage.razor`
Datei: `FinanceManager.Web/Components/Pages/CardPage.razor` (Zeilen 53–57)

- Rendert für `Kind == "securities"` (kein SubKind, `Id != Guid.Empty`) `ReturnSummaryWidget` und `GoldenCrossWidget` mit `SecurityId="Id"` — Einbindungspunkt für ein weiteres Widget.

## `SetupNotificationsTab.razor` + `SetupNotificationsViewModel`
- Checkbox `Notifications_GoldenCross` (Label + Help-Text aus `Pages.*.resx`) steuert `GoldenCrossNotificationsEnabled` über `UserSettingsController` (`PUT`-Endpoint, `NotificationSettingsDto/Requests`).

## Muster für vergleichbare Statistik-Widgets
`ReturnSummaryWidget` (gleiche `rsw-*`-Klassen, Panel-Pattern) — das neue Widget sollte demselben Layout folgen.
