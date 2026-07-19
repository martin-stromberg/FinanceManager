# Plan: F-009 - AlphaVantage API Keys geschuetzt speichern

## Ziel

AlphaVantage API Keys werden nicht mehr als verwendbarer Klartext in der Datenbank gespeichert. Die bestehenden Profil-, Sharing- und Preisabruf-Funktionen bleiben erhalten. Klartext wird nur beim Erfassen, Verschluesseln und unmittelbar vor dem AlphaVantage-Aufruf im Speicher verarbeitet.

## Umsetzungsentscheidungen

- ASP.NET Core Data Protection wird als lokale Secret-Protection-Komponente genutzt; keine externe Vault-/KMS-Abhaengigkeit.
- Die bestehende Domain-Property `User.AlphaVantageApiKey` bleibt als Persistenzfeld erhalten, speichert aber kuenftig nur noch geschuetzte Werte mit Formatpraefix.
- Geschuetztes Format: `dp:v1:{protectedPayload}`. Werte ohne dieses Praefix gelten als Altbestand im Klartext.
- Die EF-Spaltenlaenge fuer `AlphaVantageApiKey` wird von 120 auf 2048 erhoeht. DTO-Eingabelimits bleiben bei 120, weil dort weiterhin nur Nutzer-Klartext eingegeben wird.
- Bestehende Klartextwerte werden beim naechsten erfolgreichen Lesen/Verwenden automatisch geschuetzt gespeichert. Neue oder aktualisierte Keys werden sofort vor der Persistenz geschuetzt.
- Admin-Key-Sharing erhaelt eine technische Nachvollziehbarkeit ueber strukturierte Logs ohne API-Key-Wert: Quelle `personal` oder `shared`, anfragende User-ID und bei Shared-Key die Admin-User-ID.
- Data-Protection-Key-Ring-Persistenz wird dokumentiert und optional ueber Konfiguration erweiterbar gemacht; ohne stabile Key-Ring-Persistenz sind verschluesselte DB-Werte nach Deployment/Containerwechsel nicht verlaesslich lesbar.

## Betroffene Dateien

| Bereich | Dateien |
|---------|---------|
| Secret-Komponente | `FinanceManager.Web/Services/IAlphaVantageSecretProtector.cs` oder eigener Service-Dateiname im selben Ordner |
| Settings-Schreibpfad | `FinanceManager.Web/Controllers/UserSettingsController.cs` |
| Resolver/Lazy-Reprotect | `FinanceManager.Web/Services/IAlphaVantageKeyResolver.cs` |
| DI/Data Protection | `FinanceManager.Web/ProgramExtensions.cs` |
| EF-Konfiguration | `FinanceManager.Infrastructure/AppDbContext.cs` |
| EF-Migration | `FinanceManager.Infrastructure/Migrations/*_ProtectAlphaVantageApiKeys.cs`, `AppDbContextModelSnapshot.cs` |
| Tests | `FinanceManager.Tests/Web` oder `FinanceManager.Tests/Controllers`, ggf. `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs` |
| Dokumentation | `docs/help/` und `README.md` im spaeteren Dokumentationsschritt |

## Umsetzungsschritte

1. Secret-Protector einfuehren
   - Neues Interface und Implementierung fuer AlphaVantage-Key-Schutz anlegen.
   - Methoden vorsehen: `Protect(string plaintext)`, `Unprotect(string storedValue)`, `IsProtected(string storedValue)` und optional `TryUnprotect`.
   - `IDataProtectionProvider.CreateProtector("FinanceManager.AlphaVantageApiKey.v1")` verwenden.
   - Vor dem Verschluesseln trimmen, leere Werte als `null` auf dem Aufruferpfad behandeln.
   - Entschluesselungsfehler in kontrollierte, generische Exception oder Ergebnisform ueberfuehren, ohne gespeicherten Wert oder Klartext in Messages zu schreiben.

2. DI und Data Protection registrieren
   - In `ProgramExtensions.RegisterAppServices` `AddDataProtection()` und den AlphaVantage-Secret-Protector registrieren.
   - Optional eine einfache Konfigurationsoption wie `DataProtection:KeysPath` beruecksichtigen, falls im Projekt bereits ein passendes Konfigurationsmuster vorhanden ist.
   - Falls keine Key-Ring-Persistenz konfiguriert ist, keine harte Laufzeitabhaengigkeit einfuehren; das Betriebsrisiko wird dokumentiert.

3. Schreibpfad im Profil-Update schuetzen
   - `UserSettingsController` um den Secret-Protector ergaenzen.
   - Bei `ClearAlphaVantageApiKey == true` weiterhin `user.SetAlphaVantageKey(null)` verwenden.
   - Bei neuem `req.AlphaVantageApiKey` vor `SetAlphaVantageKey` `Protect` aufrufen und nur den geschuetzten Wert persistieren.
   - Bestehende Admin-/Non-Admin-Logik fuer `ShareAlphaVantageApiKey` unveraendert erhalten.
   - Fehlerantworten generisch lassen; keine Secret-Werte in Logs oder ModelState schreiben.

4. Resolver auf Entschluesselung und Lazy-Reprotect umstellen
   - `AlphaVantageKeyResolver` injiziert Secret-Protector und `ILogger<AlphaVantageKeyResolver>`.
   - Persoenliche und geteilte Keys nicht mehr per reiner String-Projektion lesen, sondern User-Datensatz mit notwendiger Minimalinformation laden.
   - Wenn gespeicherter Wert `dp:v1:` enthaelt: entschluesseln und nur Klartext an den Aufrufer zurueckgeben.
   - Wenn gespeicherter Wert kein Praefix enthaelt: als Altbestand behandeln, Klartext fuer diese Verwendung zurueckgeben, sofort `Protect` ausfuehren, auf dem User speichern und `SaveChangesAsync` ausfuehren.
   - Shared-Key-Fallback weiter deterministisch nach `UserName` waehlen.
   - Strukturierte Audit-Logs schreiben, die nur IDs und Quelle enthalten, z. B. persoenlicher Key genutzt, geteilter Admin-Key genutzt, Lazy-Reprotect erfolgt.

5. Persistenzmodell migrieren
   - In `AppDbContext.OnModelCreating` `AlphaVantageApiKey` auf `HasMaxLength(2048)` oder projektueblich unbounded erweitern.
   - EF-Migration erzeugen, die nur die Spaltenlaenge/Annotationen aktualisiert.
   - Keine SQL-Datenmigration zur Verschluesselung versuchen, weil Data Protection nicht in SQL-Migrationen verfuegbar ist.
   - Migration/Snapshot pruefen, ob die aktive Tabelle `AspNetUsers` oder historisch `Users` betrifft.

6. Log- und Fehlerpfade absichern
   - Neue Secret-Komponente darf keine Exception-Messages mit Klartext oder geschuetztem Payload erzeugen.
   - Resolver-Logs duerfen keine API-Key-Werte und keine AlphaVantage-URLs enthalten.
   - Bestehenden AlphaVantage-Client nicht fachlich umbauen; bei spaeteren Aenderungen weiterhin vermeiden, Request-URLs inklusive Query zu loggen.

7. Tests ergaenzen
   - Unit-Tests fuer Secret-Protector: geschuetzter Wert unterscheidet sich vom Klartext, hat Praefix, laesst sich korrekt entschluesseln.
   - Controller- oder Service-Test: Profil-Update speichert nicht den eingegebenen Klartext in `User.AlphaVantageApiKey`.
   - Clear-Test: Loeschen entfernt den gespeicherten Wert weiterhin.
   - Resolver-Test: geschuetzter persoenlicher Key wird korrekt entschluesselt.
   - Shared-Key-Test: geschuetzter Admin-Key wird als Fallback entschluesselt geliefert.
   - Lazy-Reprotect-Test: vorhandener Klartextwert wird beim erfolgreichen Resolver-Lesen in `dp:v1:`-Format ueberfuehrt.
   - Fehlerfall-Test: ungueltiger geschuetzter Wert fuehrt zu kontrolliertem Fehler ohne Secret in Message/Log.
   - Bestehende Non-Admin-Sharing-Regel weiterhin testen oder absichern.

8. Dokumentation vorbereiten
   - Admin-Key-Sharing beschreiben: nur Admins koennen teilen, Fallback nutzt geteilten Admin-Key, Nutzung ist ueber strukturierte Logs nachvollziehbar.
   - Backup-Auswirkung beschreiben: App-eigene NDJSON-Backups enthalten keine User-Keys; direkte DB-/Dateisystem-Backups enthalten nur geschuetzte Payloads, benoetigen aber den Schutz des Data-Protection-Key-Rings.
   - Betriebsnotiz aufnehmen: bereits kompromittierte Keys muessen organisatorisch rotiert werden; die Umsetzung rotiert keine AlphaVantage-Keys automatisch.

## Tests und Verifikation

Auszufuehrende Mindestpruefungen:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj
```

Falls eine EF-Migration erzeugt wird, zusaetzlich pruefen:

```powershell
dotnet build FinanceManager.sln
```

Manuelle Codepruefung:

- Kein Aufruf speichert `req.AlphaVantageApiKey` direkt in `User.SetAlphaVantageKey`.
- `AlphaVantageKeyResolver` gibt nur entschluesselte Laufzeitwerte zurueck.
- Logs enthalten IDs/Quelle, aber keine API-Key-Werte.
- `AppDbContextModelSnapshot` enthaelt die neue Spaltenlaenge.

## Risiken und Gegenmassnahmen

| Risiko | Gegenmassnahme |
|--------|----------------|
| Data-Protection-Ciphertext passt nicht in die bisherige Spalte | Spaltenlaenge auf 2048 erhoehen und per Migration absichern. |
| Key-Ring geht bei Deployment/Containerwechsel verloren | Persistente Key-Ring-Konfiguration dokumentieren und optional konfigurierbar machen. |
| Altbestand bleibt bis zur Nutzung im Klartext | Lazy-Reprotect beim erfolgreichen Resolver-Lesen umsetzen; beim naechsten Schreiben wird ohnehin geschuetzt. |
| Entschluesselung defekter Werte erzeugt sensitive Logs | Generische Fehlerbehandlung und Tests fuer Message-/Log-Inhalte. |
| Shared-Key-Nutzung ist fachlich sichtbar, aber technisch nicht nachvollziehbar | Strukturierte, secret-freie Audit-Logs im Resolver ergaenzen. |

## Offene Punkte

Keine.
