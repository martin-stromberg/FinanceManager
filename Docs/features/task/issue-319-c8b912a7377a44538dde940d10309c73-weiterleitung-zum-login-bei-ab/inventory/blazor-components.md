# Blazor-Komponenten und ViewModel-Eventweg

## App- und Routing-Struktur

- `FinanceManager.Web/Components/App.razor` bindet `auth.js`, `AuthRedirect`, `NavMenu`, `Routes` und die Blazor-Server-Laufzeit global ein.
- `FinanceManager.Web/Components/Routes.razor` verwendet einen einfachen `Router` mit `RouteView` und `MainLayout`; es gibt keinen separaten globalen `NotAuthorized`- oder Fehler-Handler.
- Die Seiten sind ueberwiegend `@rendermode InteractiveServer` und laden Daten in `OnInitializedAsync` oder ueber ViewModels.

## Vorhandener Auth-Eventweg

- `FinanceManager.Web/ViewModels/ViewModelBase.cs:74-77` definiert `AuthenticationRequired`.
- `RequireAuthentication(string?)` loest das Event aus (`ViewModelBase.cs:143-147`); Sub-ViewModels propagieren es (`ViewModelBase.cs:203-215`).
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` besitzt einen aehnlichen, separaten Eventweg und `EnsureAuthenticated`, der den lokalen `ICurrentUserService` prueft.
- `FinanceManager.Web/Components/Pages/ReportsHome.razor:50-69` abonniert das Event und navigiert zu `/login`, ignoriert aber den Parameter `returnUrl`.

## Aktuelles Verhalten bei abgelaufener Session

Die meisten Seiten reagieren auf fehlgeschlagene API-Aufrufe lokal ueber ViewModel-Zustand, `LastError` oder Exceptions. Es gibt keine zentrale Komponente, die einen `401`/`403` aus einem laufenden Datenabruf in `AuthenticationRequired` oder direkt in eine Login-Navigation umwandelt. Damit kann eine Seite im Lade- oder leeren Zustand verbleiben, wenn der konkrete ViewModel-Aufruf den Fehler lediglich abfaengt oder als `null`/leere Liste behandelt.

## Relevante Dateien

- `FinanceManager.Web/Components/App.razor`
- `FinanceManager.Web/Components/Routes.razor`
- `FinanceManager.Web/Components/AuthRedirect.razor`
- `FinanceManager.Web/Components/Pages/ReportsHome.razor`
- `FinanceManager.Web/ViewModels/ViewModelBase.cs`
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`
