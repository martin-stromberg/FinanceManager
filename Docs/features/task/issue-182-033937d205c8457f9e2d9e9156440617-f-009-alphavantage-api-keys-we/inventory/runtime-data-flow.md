# API-, UI- und Laufzeit-Datenfluss

## Profil-UI

`FinanceManager.Web/Components/Pages/Setup/SetupProfileTab.razor` zeigt den AlphaVantage-Key als Password-Input an. Der bestehende gespeicherte Key wird nicht im Klartext geladen; die UI zeigt nur einen Status ueber `HasKey`.

`FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs` baut beim Speichern einen `UserProfileSettingsUpdateRequest`:

- `AlphaVantageApiKey` wird nur gesetzt, wenn `KeyInput` nicht leer ist.
- `ClearAlphaVantageApiKey` wird gesetzt, wenn `ClearKey()` aufgerufen wurde.
- `ShareAlphaVantageApiKey` wird immer mit dem aktuellen `ShareKey` gesendet.

Das ist fuer Secret Handling guenstig, weil vorhandene Keys nicht zur UI zurueckfliessen. Die UI-Schicht verarbeitet den Klartext nur beim Erfassen eines neuen Keys.

## DTO-Grenze

`FinanceManager.Shared/Dtos/Users/UserProfileSettingsDto.cs` enthaelt nur:

- `PreferredLanguage`
- `TimeZoneId`
- `HasAlphaVantageApiKey`
- `ShareAlphaVantageApiKey`

`FinanceManager.Shared/Dtos/Users/UserProfileSettingsRequests.cs` erlaubt:

- `AlphaVantageApiKey` mit `MaxLength(120)`
- `ClearAlphaVantageApiKey`
- `ShareAlphaVantageApiKey`

Auch hier ist die Laenge 120 relevant: Falls die API nur Klartext-Key entgegennimmt, kann das Limit bleiben; falls ein DTO jemals geschuetzte Werte transportieren sollte, waere es zu knapp. Besser: DTO bleibt Klartext-Eingabe, Persistenzlaenge wird separat erhoeht.

## Settings-Controller

`FinanceManager.Web/Controllers/UserSettingsController.cs`:

- `GetProfileAsync` projiziert `HasAlphaVantageApiKey = u.AlphaVantageApiKey != null`.
- `UpdateProfileAsync` laedt den aktuellen User und ruft direkt `user.SetAlphaVantageKey(null)` oder `user.SetAlphaVantageKey(req.AlphaVantageApiKey)` auf.
- Nicht-Admins duerfen `ShareAlphaVantageApiKey = true` nicht setzen; Admins duerfen das Sharing-Flag setzen.
- Fehler werden mit `_logger.LogError(ex, "Update profile settings failed for {UserId}", _current.UserId)` geloggt und als generischer API-Fehler zurueckgegeben.

Fuer die Umsetzung ist dies der primare Schreibpfad. Entweder der Controller muss eine Secret-Komponente injizieren, oder besser ein kleiner User-Settings-/Secret-Service uebernimmt diese Logik.

## Key-Resolver

`FinanceManager.Web/Services/IAlphaVantageKeyResolver.cs`:

- `GetForUserAsync` liest `u.AlphaVantageApiKey` per `AsNoTracking`.
- Wenn kein persoenlicher Key vorhanden ist, folgt `GetSharedAsync`.
- `GetSharedAsync` sucht Admin-User mit `IsAdmin`, `ShareAlphaVantageApiKey` und nicht-null Key, sortiert nach `UserName` und gibt den ersten Key zurueck.

Der Resolver ist der zentrale Lesepfad und sollte nach der Umsetzung nur noch entschluesselte Werte zur Laufzeit liefern. Da er aktuell `AsNoTracking` verwendet, kann Lazy-Reprotect beim Lesen nicht ohne zusaetzlichen schreibenden Kontext oder separaten Migrationsservice passieren.

## Price-Provider und externer Client

`FinanceManager.Web/Services/AlphaVantagePriceProvider.cs`:

- Authentifizierte Nutzer erhalten bevorzugt ihren eigenen Key.
- Nicht-authentifizierte Nutzung faellt auf einen geteilten Admin-Key zurueck.
- Ohne Key wird eine `InvalidOperationException` mit generischer Nachricht geworfen.
- Der Key wird an `new AlphaVantage(http, apiKey)` uebergeben.

`FinanceManager.Web/Services/AlphaVantage.cs`:

- Speichert den Key in einem privaten Feld.
- Baut die Query-URL mit `apikey={_apiKey}`.
- Bei Rate-Limit/Information/Error werden Messages der externen API als Exceptions weitergegeben.

Aktuell gibt es keine direkte Log-Zeile mit Key, aber Query-Parameter sind ein generelles Risiko fuer HTTP-Diagnostik und Proxy-Logs. Eine Umsetzung sollte mindestens sicherstellen, dass eigene Logs den Request-URI nicht inklusive Query protokollieren.

## Dependency Injection

`FinanceManager.Web/ProgramExtensions.cs` registriert:

- `AddHttpClient("AlphaVantage", ...)`
- `AddScoped<IAlphaVantageKeyResolver, AlphaVantageKeyResolver>()`
- `AddScoped<IPriceProvider, AlphaVantagePriceProvider>()`

Eine neue Secret-Komponente kann hier scoped oder singleton registriert werden. ASP.NET Core Data Protection selbst wird normalerweise als Service registriert; fuer stabile Entschluesselung braucht die Anwendung eine persistente Key-Ring-Konfiguration.
