# Navigation und Formularinteraktionen

## Navigation

Die zentrale Menue-Navigation befindet sich in `Components/Layout/MainLayout.razor` und verwendet `NavLink` fuer Home, Listen, Reports, Setup, Benutzer, Hilfe und Rechtliches.

Weitere Navigation ist in Page-Komponenten verteilt. Relevante Aufrufmuster sind:

- `NavigationManager.NavigateTo(...)` fuer Detail-, Listen- und Ruecksprungnavigation.
- `Nav.NavigateTo(..., forceLoad: true)` in Authentifizierungs- und ausgewaehlten externen/klassischen Seitenwechseln.
- Direkte `href`-Links fuer Links, die nicht als Blazor-`NavLink` modelliert sind.

Die Bestandsaufnahme sollte daher zwischen internen Blazor-Navigationen, Force-Load-Navigationen und normalen externen Links unterscheiden.

## Formulare

Submit- und Formularlogik ist ueber die Razor-Komponenten in `Components/Pages`, `Components/Shared` und `Components/Statements` verteilt. Die Suche zeigt kein einheitliches globales Submit-Event und keine einzige Submit-Basiskomponente.

Typische Auswirkungen fuer die Anforderung:

- Ein Submit kann einen API-Aufruf ohne Navigation ausloesen.
- Ein Submit kann anschliessend explizit navigieren.
- Ein Submit kann Validierung oder einen Fehlerpfad nehmen und darf den Ladebalken nicht dauerhaft sichtbar lassen.
- Mehrere schnelle Interaktionen muessen dieselbe sichtbare Instanz neu starten, statt Komponenten zu stapeln.

## Konsequenz

Eine robuste Loesung braucht eine globale Beobachtung der relevanten Dokumentereignisse oder einen zentralen Service, der von Navigation und Formularverarbeitung angesprochen wird. Die Detailplanung muss entscheiden, wie Blazor-Navigation, klassische Seitenwechsel und asynchrone Formulare ohne Doppeltrigger zusammengefuehrt werden.

