# Inventory: F-009 - AlphaVantage API Keys geschuetzt speichern

## Zusammenfassung

Die AlphaVantage-API-Keys sind aktuell Teil der `User`-Entity und werden als normaler String in der Identity-User-Tabelle persistiert. Es gibt keine zentrale Secret-Komponente, keine Verschluesselung at rest, keine Migrationslogik fuer vorhandene Klartextwerte und keine Tests, die die Nicht-Klartext-Persistenz belegen.

Der fachliche Datenfluss ist ueberschaubar:

1. Die UI nimmt den Key im Profil-Setup als Password-Input entgegen.
2. `UserSettingsController.UpdateProfileAsync` schreibt den Key direkt ueber `User.SetAlphaVantageKey`.
3. `AlphaVantageKeyResolver` liest den gespeicherten Wert direkt aus der Datenbank und gibt ihn an `AlphaVantagePriceProvider` weiter.
4. `AlphaVantage` baut daraus die externe Query-URL.

## Detaildokumente

- [Datenmodell und Persistenz](inventory/data-model-persistence.md)
- [API-, UI- und Laufzeit-Datenfluss](inventory/runtime-data-flow.md)
- [Backups, Logging und Fehlerpfade](inventory/backups-logging-errors.md)
- [Tests und Verifikationsluecken](inventory/tests-and-gaps.md)
- [Implementierungsrelevante Abhaengigkeiten](inventory/implementation-dependencies.md)

## Relevante Codebereiche

| Bereich | Dateien | Befund |
|---------|---------|--------|
| Domainmodell | `FinanceManager.Domain/Users/User.AlphaVantage.cs` | `AlphaVantageApiKey` ist eine private-set String-Property; Setter trimmt und speichert Klartext. |
| EF-Modell | `FinanceManager.Infrastructure/AppDbContext.cs` | Property wird nur mit `HasMaxLength(120)` konfiguriert. |
| Migrationen | `FinanceManager.Infrastructure/Migrations/20251004185146_AddAlphaVantageSettings.cs`, Model-Snapshot | Spalte wurde als `TEXT`/`string` eingefuehrt; aktuelle Snapshots enthalten sie unveraendert. |
| Settings-API | `FinanceManager.Web/Controllers/UserSettingsController.cs` | Profil-Update persistiert Key direkt; GetProfile gibt nur `HasAlphaVantageApiKey` zurueck. |
| Resolver | `FinanceManager.Web/Services/IAlphaVantageKeyResolver.cs` | Resolver liest Klartext aus `Users` und liefert persoenlichen oder geteilten Admin-Key. |
| Nutzung | `FinanceManager.Web/Services/AlphaVantagePriceProvider.cs`, `FinanceManager.Web/Services/AlphaVantage.cs` | Key wird nur fuer externen Request benoetigt, aber URL enthaelt den Key als Query-Parameter. |
| DI | `FinanceManager.Web/ProgramExtensions.cs` | Resolver und PriceProvider sind scoped registriert; keine Data-Protection-/Secret-Komponente vorhanden. |
| UI | `FinanceManager.Web/Components/Pages/Setup/SetupProfileTab.razor`, `FinanceManager.Web/ViewModels/Setup/SetupProfileViewModel.cs` | UI sendet neuen Key nur bei Eingabe; gespeicherter Key wird nicht an die UI zurueckgegeben. |
| Backup | `FinanceManager.Infrastructure/Backups/BackupService.cs` | App-Backups exportieren fachliche Daten, aber nicht die User-Entity; DB-/Dateisystem-Backups bleiben vom Klartextproblem betroffen. |
| Logging | `RequestLoggingMiddleware`, `FileLoggerProvider`, betroffene Controller/Services | Keine direkte Key-Protokollierung gefunden; allgemeine Exception-Logging-Pfade koennen Exceptions inklusive Message/Stacktrace persistieren. |
| Tests | `SetupProfileViewModelTests`, `ApiClientUserSettingsTests`, Controller-Tests | Vorhandene Tests pruefen UI/API-Verhalten, nicht Secret-Schutz. |

## Ist-Zustand Gegen Die Anforderung

| Anforderung | Aktueller Stand | Luecke |
|-------------|-----------------|--------|
| At-rest verschluesselt speichern | Nicht erfuellt | DB-Wert ist der eingegebene Klartext. |
| Setzen, Aktualisieren, Teilen, Verwenden bleiben erhalten | Grundfunktion vorhanden | Muss ueber Secret-Komponente umgeleitet werden. |
| Klartext nur technisch notwendiger Moment | Teilweise | Klartext lebt persistent und als EF-Property. |
| Keine Klartext-Ausgabe in Logs/UI/Exceptions | Teilweise | UI gibt Key nicht aus; keine systematische Redaction/Tests. |
| Admin-Key-Sharing nachvollziehbar dokumentiert | Teilweise | Sharing-Flag und Resolver-Fallback vorhanden; keine Auditierung der Nutzung. |
| Bestehende Klartextwerte ueberfuehren | Nicht vorhanden | Migration oder Lazy-Reprotect fehlt. |

## Risiken Fuer Die Umsetzung

- ASP.NET Core Data Protection erzeugt je nach Persistenz der Key-Ring-Dateien umgebungsabhaengige Ciphertexte. Die Betriebsdokumentation muss klaeren, wo der Key-Ring liegt und wie Backups/Deployments damit umgehen.
- Eine reine EF-ValueConverter-Loesung wuerde zwar Persistenz schuetzen, aber die Secret-Logik weniger explizit kapseln. Die Anforderung fordert eine zentrale Secret-Handling-Komponente; der Resolver/Controller sollten deshalb nicht selbst verschluesseln.
- Bestehende Klartextwerte sind nicht eindeutig markiert. Eine Lazy-Migration braucht eine Erkennung, ob ein Wert bereits geschuetzt ist, oder ein neues Spalten-/Formatkonzept.
- Der AlphaVantage-Client setzt den Key in die Query-URL. Auch wenn aktuell keine URL geloggt wird, koennen HTTP-Diagnostik, Proxies oder Exception-Texte Query-Strings sichtbar machen.
- Shared Admin-Key-Nutzung ist fachlich vorhanden, aber nicht auditierbar. Falls "nachvollziehbar" technisch gemeint ist, braucht es mindestens strukturierte Audit-Logs ohne Key-Wert.

## Naheliegende Umsetzungspunkte

- Neue zentrale Komponente, z. B. `IAlphaVantageSecretProtector` oder allgemeiner `IUserSecretProtector`, die `Protect`, `Unprotect` und `IsProtected`/Format-Erkennung kapselt.
- Registrierung von ASP.NET Core Data Protection in `ProgramExtensions.RegisterAppServices`; Betriebsentscheidung zur Key-Ring-Persistenz.
- Anpassung von `UserSettingsController` oder einem nachgelagerten Settings-Service, sodass nur geschuetzte Werte in `User.AlphaVantageApiKey` gespeichert werden.
- Anpassung von `AlphaVantageKeyResolver`, sodass er geschuetzte Werte entschluesselt und alte Klartextwerte kontrolliert handhabt.
- Migration/Lazy-Migration: vorhandene Klartextwerte bei naechstem Schreiben oder beim erfolgreichen Lesen/Verwenden in geschuetztes Format ueberfuehren.
- Tests fuer Nicht-Klartext-Persistenz, autorisierte Wiederverwendung, Shared-Key-Fallback, Fehler beim Entschluesseln und Nicht-Ausgabe des Keys in Fehler-/Logpfaden.

## Offene Punkte Fuer Die Planung

- Soll die Ueberfuehrung vorhandener Klartextwerte beim naechsten Schreiben genuegen, oder soll beim naechsten erfolgreichen Lesen automatisch re-geschuetzt werden?
- Soll "nachvollziehbar" beim Admin-Key-Sharing eine technische Audit-Spur bedeuten oder reicht Dokumentation plus bestehende Zugriffskontrolle?
- Wo soll der Data-Protection-Key-Ring in Produktion persistiert werden, damit verschluesselte DB-Werte nach Neustart/Deployment lesbar bleiben?
