# Umsetzungsplan: Ladezeit der Startseite optimieren

## Zielbild

Die authentifizierte Startseite rendert das KPI-Grid und alle übrigen Inhalte, ohne auf `Budgets_GetMonthlyKpiAsync` zu warten. Die Monats-KPI zeigt zunächst ihr bestehendes Grundlayout als stabilen Lade-/Skeleton-Zustand. Nach Abschluss des vorhandenen API-Aufrufs werden dieselben KPI-Werte und dieselbe Darstellung wie bisher gerendert.

## Technische Leitentscheidung

Der API-Aufruf bleibt in `MonthlyBudgetKpiViewModel.LoadAsync` und verwendet weiterhin `GetMonthlyKpiAsync.GetMonthlyKpiAsync` beziehungsweise den bestehenden `IApiClient`-Vertrag. Die Komponente startet ihn erst nach ihrem ersten Rendern (`OnAfterRenderAsync`), beobachtet den Task vollständig und ruft nach erfolgreichem Abschluss `InvokeAsync(StateHasChanged)` auf. Dadurch bleibt der initiale Renderpfad frei von einem `await` auf den Monats-KPI-Aufruf.

Das ViewModel für eine Monats-KPI wird in `HomeKpiGrid` anhand der KPI-ID stabil gehalten. Es darf nicht bei jedem Aufruf von `RenderKpiTile` neu erzeugt werden, da sonst wiederholte Renderzyklen neue ViewModels und damit neue Requests auslösen würden.

## Umsetzungsschritte

### 1. Monats-KPI-Instanz und Ladezyklus stabilisieren

**Datei:** `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`

- Eine private Zuordnung von `HomeKpiDto.Id` zu `MonthlyBudgetKpiViewModel` ergänzen.
- Für `HomeKpiPredefined.MonthlyBudget` die vorhandene Instanz aus dieser Zuordnung verwenden oder einmalig erzeugen.
- Beim Entfernen beziehungsweise Aktualisieren von KPI-Einträgen veraltete Zuordnungen bereinigen, damit keine unbounded State-Ansammlung entsteht.
- Den bestehenden Ladeaufruf für die Home-KPI-Konfiguration unverändert lassen; nur der Monats-KPI-Datenabruf wird aus dem initialen Lifecycle entkoppelt.

### 2. Asynchronen Monats-KPI-Ladevorgang entkoppeln

**Dateien:**

- `FinanceManager.Web/Components/Shared/MonthlyBudgetKpi.razor`
- gegebenenfalls `FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs`

- `OnParametersSetAsync` darf den Monats-KPI-Request nicht mehr abwarten.
- Im ersten `OnAfterRenderAsync` den Ladevorgang genau einmal starten und den laufenden Task beziehungsweise eine Startmarkierung verwalten.
- Den vorhandenen `DataLoaded == false`-Zustand für die Skeleton-Darstellung nutzen. Die Tile-Abmessungen und das Layout bleiben während des Ladens stabil; vorhandene Fill-/Marker-/Ergebnisbereiche werden erst bei `DataLoaded` angezeigt.
- Nach erfolgreichem `LoadAsync` einen UI-State-Update über `InvokeAsync(StateHasChanged)` ausführen, damit die echten Werte ohne weiteren Benutzer- oder Seiten-Refresh erscheinen.
- Einen `CancellationTokenSource` pro Komponente verwenden, das Token an `LoadAsync` weitergeben und die Quelle beim Disposen der Komponente abbrechen und freigeben. Cancellation beim normalen Navigieren darf kein Fehlerzustand sein.
- Den Hintergrund-Task in der Komponente vollständig beobachten: `OperationCanceledException` bei Disposal ignorieren, unerwartete Exceptions nicht unbeobachtet lassen und über das bestehende Fehler-/Logging-Muster behandeln. HTTP-Fehler behalten die bisherige `ErrorMessage`-Darstellung.
- Die fachlichen Berechnungen, Wertezuordnung und API-/DTO-Verträge nicht ändern. Leere oder vollständig null-/wertlose, aber erfolgreiche API-Antworten werden wie bisher als erfolgreich geladene Antwort dargestellt; eine zusätzliche fachliche Interpretation ist nicht Teil dieser Anforderung.

### 3. Skeleton- und Fehlerdarstellung absichern

**Dateien:**

- `FinanceManager.Web/Components/Shared/MonthlyBudgetKpi.razor`
- `FinanceManager.Web/wwwroot/css/app.HomeKpiGrid.css` nur falls erforderlich

- Prüfen, dass der Zustand vor dem Abschluss des Requests sichtbar als Ladezustand/Skeleton erkennbar ist und keine Ergebniswerte aus den ViewModel-Defaultwerten suggeriert.
- Bestehende KPI-Darstellung nach `DataLoaded` unverändert übernehmen.
- Bestehende HTTP-Fehleranzeige beibehalten und sicherstellen, dass sie nach einem fehlgeschlagenen Hintergrundrequest den initialen Seitenaufbau nicht blockiert.
- CSS nur ergänzen, wenn für Skeleton-Elemente ein stabiler Platzhalter oder eine Mindesthöhe fehlt; keine Änderung an API- oder Backend-Styling-Verträgen.

### 4. Tests ergänzen und anpassen

**Dateien:**

- `FinanceManager.Tests/Components/HomeKpiGridTests.cs`
- neue oder passende Komponententests für `MonthlyBudgetKpi`
- `FinanceManager.Tests/ViewModels/MonthlyBudgetKpiViewModelTests.cs` bei Bedarf

Abzudecken sind:

1. Ein nicht abgeschlossenes `Budgets_GetMonthlyKpiAsync` rendert zunächst die Monats-KPI im Skeleton-/Ladezustand.
2. Ein später abgeschlossener Request rendert die tatsächlichen Werte und entfernt den Skeleton-Zustand.
3. Der Home-KPI-Renderpfad wird nicht durch einen langsamen Monats-KPI-Request blockiert; andere KPI-Inhalte sind vorher sichtbar.
4. Wiederholte Renderzyklen starten für dieselbe KPI-ID keinen zweiten Request.
5. Ein HTTP-Fehler zeigt den bestehenden Fehlerzustand und erzeugt keine unbeobachtete Task-Exception.
6. Die ViewModel-Tests für HTTP-Fehler, Cancellation und unerwartete Exceptions bleiben konsistent mit dem gewählten Komponenten-Handling.

Für bUnit-Tests sind kontrollierbare `TaskCompletionSource`-Instanzen mit `RunContinuationsAsynchronously` zu verwenden. Nach dem Abschluss des Requests ist mit `WaitForAssertion` auf den UI-State-Update zu warten. Der Testaufbau soll die tatsächliche Reihenfolge `Render -> Request läuft -> Request beendet -> StateHasChanged` prüfen.

### 5. Verifikation

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj`
- Falls der Skeleton-Zustand durch bUnit nicht ausreichend repräsentativ geprüft werden kann, den bestehenden Startseiten-E2E-Testbestand aus `FinanceManager.Tests.E2E` ergänzend ausführen.
- Sicherstellen, dass keine Änderungen an `ApiClient.BudgetReport.cs`, Controller, DTOs oder `BudgetReportService.cs` erforderlich wurden.

## Änderungsumfang

### Voraussichtlich zu ändern

- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`
- `FinanceManager.Web/Components/Shared/MonthlyBudgetKpi.razor`
- gezielte Tests unter `FinanceManager.Tests/Components/` und gegebenenfalls `FinanceManager.Tests/ViewModels/`
- CSS nur bei nachgewiesenem Bedarf

### Ausdrücklich unverändert

- `FinanceManager.Web/Components/Pages/Home.razor` und `HomeViewModel`, sofern die Prüfung keine zusätzliche Synchronisierung verlangt
- `FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs` fachlich und im API-Vertrag
- `FinanceManager.Shared/ApiClient.BudgetReport.cs`
- Backend-Controller, `BudgetReportService` und KPI-Berechnung

## Risiken und Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| VM wird bei jedem Render neu erzeugt | VM nach KPI-ID cachen und gezielt bereinigen |
| Hintergrund-Task läuft nach Navigation weiter | `CancellationTokenSource`, `IAsyncDisposable`/`IDisposable` und Cancellation-Ausnahme behandeln |
| Abschluss aktualisiert die UI nicht | State-Update über `InvokeAsync(StateHasChanged)` nach beobachtetem Task |
| Defaultwerte wirken wie echte KPI | Ergebnisbereiche erst bei `DataLoaded` rendern und Skeleton mit stabiler Größe verwenden |
| Unerwartete Exception bleibt unbeobachtet | Hintergrund-Task in einer beobachteten Wrapper-Methode ausführen und Fehler behandeln/loggen |

## Offene Punkte

Keine. Das nicht spezifizierte Fehlerverhalten wird auf den bestehenden Vertrag festgelegt: HTTP-Fehler bleiben als bestehende KPI-Fehleranzeige sichtbar, Cancellation beim Verlassen der Seite wird ignoriert, und unerwartete Hintergrundfehler werden beobachtet/behandelt, ohne den initialen Seitenaufbau nachträglich zu blockieren. Erfolgreiche leere Daten werden ohne zusätzliche fachliche Sonderlogik als geladene Antwort behandelt.
