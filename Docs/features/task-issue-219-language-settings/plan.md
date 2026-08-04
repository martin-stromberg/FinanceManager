# Umsetzungsplan: Bug-Fix "Language settings not considered" (Issue #219)

## Übersicht

Der Bug verursacht, dass Benutzer die UI in ihrer Browser-Sprache statt ihrer explizit gewählten Spracheinstellung sehen. Die Root Cause liegt in der `UserPreferenceRequestCultureProvider` Klasse, die `null` zurückgibt, wenn keine Benutzereinstellung im JWT vorhanden ist, wodurch die Browser-Sprache (Accept-Language Header) die Benutzereinstellung überschreibt. Der Fix erfordert die Erzwingung einer Default-Culture in der Provider-Kette, Verifikation der JWT-Token-Integration und umfassende E2E-Test-Abdeckung.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Culture Provider-Strategie** | `UserPreferenceRequestCultureProvider` gibt explizit `DefaultRequestCulture` ("de") zurück statt `null` | Stellt sicher, dass die Benutzereinstellung nicht delegiert wird. Wenn kein JWT-Claim und keine DB-Einstellung vorhanden ist, fällt die Kette zur Standardsprache zurück, nicht zur Browser-Sprache. |
| **Default-Sprache bei null-Einstellung** | Benutzer mit `PreferredLanguage = null` bekommen Standardsprache "de" | Konsistent mit Framework-Konfiguration in `ProgramExtensions.cs` und verhindert unerwartetes Fallback zur Browser-Sprache. |
| **Token Re-Issue Timing** | Neuer JWT wird sofort nach `PUT /api/user/settings/profile` als Auth-Cookie gespeichert | Bereits implementiert, Verifikation erforderlich dass Cookie korrekt gesetzt wird und nächster Request damit arbeitet. |

## Programmabläufe

### Ablauf 1: Spracheinstellung speichern und aktivieren

Schritt-für-Schritt-Beschreibung des korrigierten Ablaufs:

1. Benutzer öffnet `SetupProfileTab.razor` und wählt neue Sprache in `<select id="lang">`
2. `SetupProfileViewModel.OnChanged()` wird aufgerufen (durch `@bind:after`)
3. Benutzer klickt "Speichern", `SetupProfileViewModel.SaveAsync()` wird ausgelöst
4. ViewModel erstellt `UserProfileSettingsUpdateRequest` mit neuer `PreferredLanguage`
5. API-Aufruf: `PUT /api/user/settings/profile`
6. `UserSettingsController.UpdateProfileAsync()` empfängt Request
7. User-Objekt wird mit `user.SetPreferredLanguage(req.PreferredLanguage)` aktualisiert
8. Änderungen werden mit `await _db.SaveChangesAsync()` in DB persistiert
9. **KRITISCH:** Neuer JWT wird mit aktualisiertem `pref_lang` Claim generiert mittels `_jwt.CreateToken(..., user.PreferredLanguage, ...)`
10. Neuer Token wird als Auth-Cookie gespeichert: `Response.Cookies.Append(AuthCookieName, newToken, ...)`
11. Token-Cache wird invalidiert: `_tokenProvider.InvalidateCache()`
12. Client empfängt 204 NoContent und Cookie enthält neuen Token
13. **Nächster Request:** `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult()` wird aufgerufen
14. JWT `pref_lang` Claim wird gelesen und gültige Culture wird zurückgegeben
15. `app.UseRequestLocalization()` setzt `CultureInfo.CurrentCulture` korrekt
16. ResourceManager wählt .resx-Datei für neue Sprache (z.B. SetupProfileTab.en.resx)
17. UI wird in neuer Sprache angezeigt

Beteiligte Klassen/Komponenten: `SetupProfileTab.razor`, `SetupProfileViewModel`, `ApiClient`, `UserSettingsController`, `User`, `JwtTokenService`, `UserPreferenceRequestCultureProvider`, `IStringLocalizer`

### Ablauf 2: Culture Resolution bei Request ohne gültige Benutzereinstellung

Schritt-für-Schritt-Beschreibung des korrigierten Fehlerfalls:

1. Request kommt an den Server
2. `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult()` wird aufgerufen
3. JWT `pref_lang` Claim wird geprüft
4. Claim fehlt oder ist invalid (z.B. `null`, leerer String, CultureNotFoundException)
5. **ÄNDERUNG:** Provider fällt zu DB-Fallback zurück (wie bisher)
6. DB wird queried: `_db.Users.Where(u => u.Id == userId).Select(u => u.PreferredLanguage)`
7. DB-Wert ist ebenfalls `null` oder leer
8. **ÄNDERUNG:** Provider gibt explizit `ProviderCultureResult` mit Default-Culture "de" zurück (nicht `null`)
9. Keine Delegation zu anderen Providern (Browser-Sprache wird nicht konsultiert)
10. `app.UseRequestLocalization()` verwendet diese Culture
11. UI wird in Standardsprache Deutsch angezeigt

Beteiligte Klassen/Komponenten: `UserPreferenceRequestCultureProvider`, `RequestLocalizationOptions`, `IStringLocalizer`, `ResourceManager`

## Neue Klassen

Keine neuen Klassen erforderlich. Der Fix ist eine Änderung der bestehenden `UserPreferenceRequestCultureProvider` Logik.

| Klasse | Typ | Zweck |
|--------|-----|-------|
| — | — | — |

## Änderungen an bestehenden Klassen

### `UserPreferenceRequestCultureProvider` (Klasse)

- **Geänderte Methode:** `DetermineProviderCultureResult()` — **KRITISCHE ÄNDERUNG**
  - Aktuell: Gibt `null` zurück wenn keine gültige Benutzereinstellung gefunden wird
  - Neu: Gibt `ProviderCultureResult("de")` (oder allgemein `DefaultRequestCulture`) zurück als Fallback
  - Parameter: `HttpContext context`
  - Rückgabewert: `Task<ProviderCultureResult?>` — wird angepasst auf explizites Zurückgeben von Default-Culture statt null
  - **Implementierungsdetail:** Die try-catch für `CultureNotFoundException` muss auf `InvalidCultureException` und andere erweitert werden

### Änderungen-Details für `UserPreferenceRequestCultureProvider`

```
Zeile 34-44 (JWT Auflösung):
  - Besteht: Versucht pref_lang Claim zu lesen
  - Keine Änderung erforderlich

Zeile 46-53 (DB Fallback):
  - Besteht: Liest PreferredLanguage aus DB
  - Keine Änderung erforderlich

Zeile 55-64 (Return-Statement):
  - ÄNDERN: if (string.IsNullOrWhiteSpace(lang)) { return null; }
  - WERDEN: Rückgabe von ProviderCultureResult für Default-Culture "de"
  - Effekt: Browser-Sprache wird nicht konsultiert
```

## Datenbankmigrationen

Keine Datenbankmigrationen erforderlich. Die `PreferredLanguage` Spalte in der `AspNetUsers` Tabelle existiert bereits und wird nicht strukturell geändert.

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| — | — | — |

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Die existierende Validierung in `UserProfileSettingsUpdateRequest` (`[MaxLength(10)]` für `PreferredLanguage`) ist ausreichend.

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| — | — | — |

## Konfigurationsänderungen

Keine Konfigurationsänderungen erforderlich. Die bestehende Konfiguration in `ProgramExtensions.cs` mit Standardsprache "de" und unterstützten Sprachen ["de", "en"] bleibt erhalten.

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| — | — | — | — |

## Seiteneffekte und Risiken

- **Culture Resolution Pipeline:** Die Änderung in `UserPreferenceRequestCultureProvider` beeinflusst alle HTTP-Requests im System. Tests müssen sicherstellen, dass nicht-authentifizierte Requests korrekt zur Default-Culture "de" fallen.
- **Accept-Language Header wird ignoriert:** Nach dem Fix wird der Browser-Accept-Language Header nur noch konsultiert, wenn der Benutzer nicht authentifiziert ist oder explizit `PreferredLanguage = null` gesetzt hat. Dies ist gewünschtes Verhalten, aber könnte für andere Features erwartet werden.
- **Token Cache Invalidation:** Der Code in `UserSettingsController` invalidiert bereits den Token-Cache nach Sprachänderung. Tests müssen verifizieren, dass dies tatsächlich funktioniert und keine stale Tokens verwendet werden.
- **Bestehende Tests:** Unit-Tests in `ApiClientUserSettingsTests.cs` und `SetupProfileViewModelTests.cs` testen bereits Roundtrip der Spracheinstellung. Sie sollten weiterhin erfolgreich sein, müssen aber überprüft werden nach der Änderung in `UserPreferenceRequestCultureProvider`.

## Umsetzungsreihenfolge

1. **Fix in `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult()`**
   - Voraussetzungen: Keine
   - Beschreibung: Änderung der `DetermineProviderCultureResult()` Methode, um anstatt `null` eine Default-Culture `ProviderCultureResult("de")` zurückzugeben wenn keine gültige Benutzereinstellung gefunden wird. Der Fix muss sicherstellen, dass die Standardsprache aus `RequestLocalizationOptions.DefaultRequestCulture` verwendet wird oder hartcodiert "de" ist.

2. **Unit-Tests für korrigierte `UserPreferenceRequestCultureProvider`**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Neue oder angepasste Unit-Tests für `UserPreferenceRequestCultureProvider.DetermineProviderCultureResult()` um folgende Szenarien zu decken:
     - JWT `pref_lang` Claim ist vorhanden und gültig → richtige Culture wird zurückgegeben
     - JWT `pref_lang` Claim ist vorhanden aber ungültig (CultureNotFoundException) → DB-Fallback
     - JWT Claim fehlt, DB hat Wert → DB-Wert wird verwendet
     - JWT Claim fehlt, DB hat keinen Wert → Default-Culture "de" wird zurückgegeben (nicht null)
     - Nicht authentifizierter Request → Default-Culture wird zurückgegeben

3. **Integration-Tests anpassen: `ApiClientUserSettingsTests` und `SetupProfileViewModelTests`**
   - Voraussetzungen: Schritte 1 und 2 abgeschlossen
   - Beschreibung: Bestehende Tests ausführen und überprüfen, dass sie noch passen. Ggf. Tests anpassen wenn Verhalten sich durch die Änderung ändert (sollte nicht der Fall sein, da der externe API-Vertrag gleich bleibt).

4. **E2E-Test: Spracheinstellung wird beachtet nach Speichern**
   - Voraussetzungen: Schritte 1-3 abgeschlossen, Playwright Test-Infrastruktur vorhanden (existiert bereits in `FinanceManager.Tests.E2E`)
   - Beschreibung: Neuer E2E-Test mit Playwright um zu verifizieren:
     - Benutzer speichert Spracheinstellung "Englisch"
     - Seite wird neu geladen (oder neuer Request wird gemacht)
     - UI wird in Englisch angezeigt (überprüft durch lokalisierte Texte)
     - Browser-Accept-Language Header (z.B. "de") wird ignoriert und beeinträchtigt nicht die UI-Sprache

5. **E2E-Test: Default-Culture wird verwendet wenn keine Einstellung gespeichert**
   - Voraussetzungen: Schritte 1-3 abgeschlossen
   - Beschreibung: Neuer E2E-Test um zu verifizieren:
     - Benutzer wird erstellt ohne explizite Spracheinstellung
     - UI wird in Default-Sprache Deutsch angezeigt
     - Browser-Accept-Language Header wird ignoriert

6. **E2E-Test: Auto-Modus (PreferredLanguage = null) verwendet Browser-Sprache**
   - Voraussetzungen: Schritte 1-3 abgeschlossen
   - Beschreibung: Neuer E2E-Test um zu verifizieren:
     - Benutzer wählt "Auto" (setzt PreferredLanguage auf null oder "")
     - Seite wird mit Browser-Accept-Language "en" neu geladen
     - UI wird in Englisch angezeigt (Browser-Sprache wird konsultiert wenn PreferredLanguage = null)

7. **E2E-Test: Token-Cookie wird nach Sprachänderung aktualisiert**
   - Voraussetzungen: Schritte 1-3 abgeschlossen
   - Beschreibung: Neuer E2E-Test um zu verifizieren:
     - Benutzer speichert Spracheinstellung von "de" zu "en"
     - Auth-Cookie wird überprüft dass er den neuen JWT mit `pref_lang: en` Claim enthält
     - Nächste Request verwendet den neuen Cookie und zeigt korrekte Sprache

8. **Manuelle Verifikation und Dokumentation**
   - Voraussetzungen: Alle vorigen Schritte abgeschlossen, alle Tests grün
   - Beschreibung: 
     - Bug-Fix in der Issue #219 dokumentieren
     - Verifikation mit verschiedenen Browser-Konfigurationen durchführen
     - Sicherstellen dass kein Regressions-Szenario übersehen wurde

## Tests

### Neue Tests

Welche neuen Testmethoden und Hilfsmethoden sind erforderlich?

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `UserPreferenceRequestCultureProvider_JwtClaimPresent_ReturnsCorrectCulture` | `UserPreferenceRequestCultureProviderTests` (neu) | JWT `pref_lang` Claim wird korrekt ausgelesen und Culture wird zurückgegeben |
| `UserPreferenceRequestCultureProvider_JwtClaimInvalid_FallsBackToDatabase` | `UserPreferenceRequestCultureProviderTests` (neu) | Ungültiger JWT Claim (CultureNotFoundException) triggert DB-Fallback |
| `UserPreferenceRequestCultureProvider_NoClaimNoDatabaseValue_ReturnsDefaultCulture` | `UserPreferenceRequestCultureProviderTests` (neu) | **KRITISCH:** Wenn JWT-Claim und DB beide leer/null, wird Default-Culture "de" zurückgegeben (nicht null) |
| `UserPreferenceRequestCultureProvider_UnauthenticatedRequest_ReturnsDefaultCulture` | `UserPreferenceRequestCultureProviderTests` (neu) | Nicht-authentifizierter Request fällt zu Default-Culture |
| `SetupProfileTab_ChangeLanguage_SavesAndApplies` | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` (neu) | End-to-End: Benutzer ändert Sprache, speichert, neuer Request zeigt neue Sprache |
| `SetupProfileTab_DefaultCultureApplied_WhenNoPreference` | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` (neu) | Benutzer ohne Spracheinstellung sieht Default-Sprache Deutsch |
| `SetupProfileTab_AutoMode_RespectsBrowserLanguage` | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` (neu) | PreferredLanguage = null → Browser-Sprache wird verwendet |
| `SetupProfileTab_TokenCookieUpdated_AfterLanguageChange` | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` (neu) | Nach PUT /api/user/settings/profile wird Auth-Cookie mit neuem Token aktualisiert |

### Betroffene bestehende Tests

Welche vorhandenen Tests müssen angepasst werden, weil sich Signaturen, Verhalten oder Datenstrukturen ändern?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ApiClientUserSettingsTests.UserSettings_UpdateProfile_Sets_Language_And_Timezone` | **Keine Anpassung erforderlich** — der Test testet den API-Roundtrip, nicht die Culture Resolution. Sollte weiterhin grün sein. |
| `SetupProfileViewModelTests.SaveAsync` | **Keine Anpassung erforderlich** — der Test mockt den API-Aufruf, testet nicht die tatsächliche Culture Resolution. Sollte weiterhin grün sein. |
| Alle Tests in `FinanceManager.Tests.Integration/ApiClient/` | **Überprüfung erforderlich:** Tests die HTTP-Requests mit verschiedenen Browser-Sprachen simulieren, könnten von der Änderung betroffen sein. Durchsicht erforderlich ob Test-Setup die Browser-Sprache explizit setzt und ob Verhalten sich ändert. |
| Alle Tests in `FinanceManager.Tests/Auth/` | **Überprüfung erforderlich:** Tests für `UserAuthService` und `JwtRefreshService` testen JWT-Generation. Sie sollten nicht betroffen sein, aber Durchsicht sichert ab. |

### E2E-Tests (Pflicht)

Für jede neue oder geänderte Benutzerinteraktion mindestens ein E2E-Test. Der Happy Path jedes neuen Features muss durch einen E2E-Test abgedeckt sein.

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Benutzer ändert Spracheinstellung von Deutsch zu Englisch, speichert, und sieht UI in Englisch | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` → `SetupProfileTab_ChangeLanguage_SavesAndApplies` | **AC1:** Benutzer wählt Sprache → UI wird in dieser Sprache angezeigt (nicht Browser-Sprache) |
| Benutzer ohne explizite Spracheinstellung sieht UI in Default-Sprache Deutsch | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` → `SetupProfileTab_DefaultCultureApplied_WhenNoPreference` | **AC2:** Keine Benutzereinstellung → Default-Sprache wird verwendet (nicht Browser-Sprache) |
| Benutzer wählt "Auto"-Modus und sieht UI in Browser-Sprache | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` → `SetupProfileTab_AutoMode_RespectsBrowserLanguage` | **AC3:** PreferredLanguage = null → Browser-Sprache wird respektiert |
| Nach Sprachänderung wird Auth-Cookie mit neuem JWT aktualisiert | `FinanceManager.Tests.E2E/Tests/ProfileSettingsTests.cs` → `SetupProfileTab_TokenCookieUpdated_AfterLanguageChange` | **AC4:** Token Re-Issue funktioniert korrekt nach Sprachänderung |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine bekannten E2E-Tests die Spracheinstellungen überprüfen | Keine Anpassung erforderlich. Alle existierenden E2E-Tests sollten weiterhin laufen, da die Änderung nur die Culture Resolution Pipeline betrifft, nicht die Funktionalität anderer Features. |

## Offene Punkte

Ungeklärte technische oder fachliche Fragen, die vor oder während der Implementierung geklärt werden müssen.

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Welche Culture sollte bei fehlender Benutzereinstellung verwendet werden: hartcodiert "de" oder `RequestLocalizationOptions.DefaultRequestCulture`? | **Empfehlung:** Verwende `RequestLocalizationOptions.DefaultRequestCulture` aus der Middleware-Konfiguration (aktuell "de"). Dies macht den Code flexibler und konsistent mit der Framework-Konfiguration in `ProgramExtensions.cs`. Falls `DefaultRequestCulture` nicht zugreifbar ist, verwende "de" als Fallback-Konstante. |
| 2 | Wird die `CultureNotFoundException` in der Razor-Komponente oder im ViewModel beim Rendern von lokalisierten Texten geworfen, oder nur im `UserPreferenceRequestCultureProvider`? | **Empfehlung:** Lokalisierte Text-Abrufe werfen normalerweise nicht sondern geben einen Fallback-Text zurück. Die `CultureNotFoundException` wird nur geworfen wenn eine ungültige Culture explizit gesetzt wird. Der Code sollte diese Exception nur im `UserPreferenceRequestCultureProvider` bei der Validierung des JWT-Claims fangen. |
| 3 | Sollten die E2E-Tests die Accept-Language Header explizit manipulieren oder reliert der Test auf die Browser-Standardeinstellung? | **Empfehlung:** Manipuliere den Accept-Language Header explizit mit Playwright `context.addInitializer()` oder HTTP-Header-Overrides um Test-Szenarien zu isolieren. Dies stellt sicher, dass der Test reproduzierbar ist und nicht von der lokalen Browser-Konfiguration des Test-Läufers abhängt. |
| 4 | Welche lokalisierten Texte sollten in E2E-Tests überprüft werden um korrekte Sprachanwendung zu verifizieren? | **Empfehlung:** Verwende eindeutige, sprachspezifische Texte die sicher nur in einer Sprache vorkommen. Z.B. "Deutsch (de)" vs "German (de)" oder "Einstellungen" vs "Settings". Überprüfe diese Texte nach Seiten-Reload um sicherzustellen dass Culture tatsächlich korrekt angewendet wurde. |
| 5 | Ist der Token-Cache (`_tokenProvider.InvalidateCache()`) thread-safe und wird er korrekt implementiert in `JwtTokenProvider`? | **Empfehlung:** Überprüfe die Implementierung von `JwtTokenProvider.InvalidateCache()` ob sie korrekt die Auth-Tokens invalidiert. Falls keine Implementierung vorhanden oder unsicher ist, prüfe die Test-Coverage für diesen Mechanismus. |

