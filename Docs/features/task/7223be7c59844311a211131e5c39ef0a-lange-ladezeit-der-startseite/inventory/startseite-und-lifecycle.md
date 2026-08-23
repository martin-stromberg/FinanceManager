# Startseite und Komponenten-Lifecycle

## `Home.razor`

Die Seite ist unter `/` erreichbar und verwendet `InteractiveServer`. Fuer authentifizierte Benutzer werden Benachrichtigungen, Importstatus und das `HomeKpiGrid` gerendert. Die Seite reagiert auf `HomeViewModel.StateChanged` mit `InvokeAsync(StateHasChanged)`.

Der `HomeViewModel` selbst laedt keine Monats-KPI. Seine Rolle ist die Verwaltung von Authentifizierung, Importzustand, Ribbon-Aktionen und UI-Zustandsaenderungen. Eine Aenderung an diesem ViewModel ist daher voraussichtlich nicht erforderlich.

## `HomeKpiGrid.razor`

Das Grid laedt in `OnInitializedAsync` die Home-KPI-Konfiguration ueber `Api.HomeKpis_ListAsync()`. Die monatliche Budget-KPI wird dynamisch mit einem neu erzeugten `MonthlyBudgetKpiViewModel` gerendert.

Der dynamische Renderpfad liegt im Fall `HomeKpiPredefined.MonthlyBudget`. Das ViewModel wird aktuell bei jedem Rendern innerhalb der Builder-Logik neu erzeugt. Bei einer Entkopplung des API-Aufrufs vom initialen Rendern muss deshalb die ViewModel-Instanz stabil gehalten oder der Ladeaufruf so gesteuert werden, dass keine wiederholten Requests entstehen.

## `MonthlyBudgetKpi.razor`

Die Komponente berechnet ihre Anzeige aus dem ViewModel. Vor `DataLoaded` werden keine Fortschrittsfuellungen und Ergebnis-Marker gerendert; nach erfolgreichem Laden werden die vorhandenen Werte und die bestehende Darstellung verwendet. Bei `ErrorMessage` wird eine allgemeine Fehleranzeige gerendert.

Der aktuelle `OnParametersSetAsync`-Handler wartet direkt auf den API-Aufruf. Der neue Ablauf muss diesen Handler so umstellen, dass die Komponente zuerst mit dem unveraenderten Grundlayout beziehungsweise Skeleton rendert und das Ergebnis anschliessend ueber einen State-Update einsetzt.

## Randbedingungen

- Abbruch und Entsorgung der Komponente muessen bei einem laufenden Request beruecksichtigt werden.
- Fehler duerfen nicht zu einem unbeobachteten Hintergrund-Task fuehren.
- Die Tile-Groesse sollte waehrend des Ladens stabil bleiben, damit das Grid nicht springt.
