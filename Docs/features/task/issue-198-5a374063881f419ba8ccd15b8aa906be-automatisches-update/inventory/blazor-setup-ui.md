# Blazor/Razor-UI und Setup-Seiten

## Fundstellen

- `FinanceManager.Web/Components/Pages/SetupSections.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupBackupTab.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupBackupsViewModel.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupSecurityTab.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupSecurityViewModel.cs`
- `FinanceManager.Web/Components/BackgroundTaskStatusPanel.razor`
- `FinanceManager.Web/Components/Pages/CardPage.razor`

## Bestehendes Muster

Der Setup-Bereich ist eine Card-Route `setup` ueber `SetupCardViewModel`. Setup-Sektionen werden ueber ein internes `SetupSectionDefinition[]` definiert. Jede Sektion hat:

- einen technischen Key
- einen Localization-Key
- einen Fallback-Titel
- einen ViewModel-Typ
- einen Razor-Komponententyp

`SetupSections.razor` rendert die Sektionen als Accordion. Beim Oeffnen wird per `DynamicComponent` der Sektionstyp gerendert und ein ViewModel als Parameter uebergeben.

Aktuelle Setup-Sektionen:

- `profile`
- `notifications`
- `statements`
- `attachments`
- `backup`
- `security`
- `returnanalysis`

`SetupBackupTab.razor` zeigt ein gutes Pattern fuer tabellarische Desktop-Ansicht plus mobile Cards, Icon-Buttons, Restore-Bestaetigungsdialog, Download-Link und Upload ueber verstecktes `InputFile`.

`SetupSecurityTab.razor` zeigt Admin-only UI-Gating per `CurrentUser.IsAuthenticated` und `CurrentUser.IsAdmin`.

## Relevanz fuer Self-Update

Eine neue Setup-Sektion `update` passt in das vorhandene Modell:

- `SetupUpdateViewModel`
- `SetupUpdateTab.razor`
- Localization-Keys in Ressourcen analog zu anderen Setup-Tabs
- Eintrag in `SetupCardViewModel.SectionDefinitions`
- ggf. Einbindung in `HasPendingChanges`, `SaveAllAsync` und `ResetAll`, wenn Einstellungen im Tab gespeichert werden

Die UI sollte die Anforderung abdecken:

- Aktivierung der Updatepruefung
- Pruefintervall
- Updatequelle/Repository
- gefundene Version, Veroeffentlichungsdatum, Beschreibung, Hash, Dateigroesse
- Download-/Ready-/Failed-/Installing-Status
- geplante Installationszeit
- manuelle Installation
- Warteseite bzw. Installationsansicht mit Polling gegen einen anonym erreichbaren Health-Endpunkt

## Einschraenkungen

Die vorhandene UI pollt BackgroundTasks ueber `BackgroundTaskStatusPanel`, aber die Updateinstallation beendet den Prozess. Fuer die Warteseite darf daher nicht allein SignalR/Blazor-Server-State vorausgesetzt werden. Eine robuste Umsetzung braucht eine einfache Seite oder Komponente, die nach Start der Installation clientseitig per JS/HTTP alle zwei Sekunden einen Health-Endpunkt abfragt und bei Erfolg neu laedt.

