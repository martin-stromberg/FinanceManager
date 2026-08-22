# Navigation, Login und ReturnUrl

## Aktuelle Weiterleitung

`FinanceManager.Web/Components/AuthRedirect.razor` beobachtet `NavigationManager.LocationChanged` und prueft die aktuelle URL. Nicht-oeffentliche Pfade werden bei fehlender Authentifizierung auf `/login` bzw. `/register` umgeleitet. Oeffentlich sind `/login`, `/register`, `/api/auth...` und `/error`.

Die Methode `GetPath` entfernt die Querystring-Komponente. Dadurch ist sie fuer die reine Public-Route-Pruefung geeignet, aber nicht fuer das Speichern eines vollstaendigen Rueckkehrziels. `Nav.NavigateTo(target, forceLoad: true)` verwendet aktuell kein `returnUrl`.

## Aktueller Loginabschluss

`FinanceManager.Web/Components/Pages/Login.razor` ruft `fmAuthLogin` auf. Bei Erfolg erfolgt immer `Nav.NavigateTo("/", forceLoad: true)`. Es gibt keinen Parameter fuer ein Ziel, keine Auswertung der aktuellen Querystring-Parameter und keinen Unterschied zwischen direktem Login-Aufruf und Login nach Sessionverlust.

## Bestehende lokale Navigation

`ReportsHome.razor` baut bei `AuthenticationRequired` nur `/login` und ignoriert `returnUrl`. Die Komponenten und ViewModels verwenden ansonsten direkte absolute interne Routen mit `NavigationManager`; ein etablierter ReturnUrl-Helper ist nicht vorhanden.

## Randbedingungen fuer die Umsetzung

- Das Ziel sollte Pfad, Querystring und gegebenenfalls Fragment enthalten.
- Es sollte nur ein internes Ziel akzeptiert werden, um Open-Redirects zu vermeiden.
- `/login` selbst darf nicht als Rueckkehrziel gespeichert werden.
- Bei direkter Navigation zu `/login` muss der Fallback `/` beibehalten werden.
- Mehrfache oder konkurrierende API-Fehler duerfen keine Redirect-Schleife erzeugen.
- Die Login-Navigation muss das Ziel URL-kodiert uebergeben und nach Erfolg einmalig konsumieren.
