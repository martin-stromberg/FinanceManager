# Tests und Verifikationsluecken

## Vorhandene Tests

### ViewModel

`FinanceManager.Tests/ViewModels/SetupProfileViewModelTests.cs` prueft:

- Profil wird geladen und `HasKey`/`ShareKey` werden gesetzt.
- Speichern sendet ein Profil-Update und leert `KeyInput`.
- `ClearKey()` setzt Dirty-State und sendet `ClearAlphaVantageApiKey`.
- Timezone-Erkennung veraendert das Modell.

Diese Tests sind nuetzlich fuer UI-Verhalten, pruefen aber keine Persistenz und keine Verschluesselung.

### API-Client / Integration

`FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs` prueft:

- Default-Profil enthaelt keinen AlphaVantage-Key.
- Language/Timezone-Update funktioniert.
- Notification- und ImportSplit-Endpunkte funktionieren.

Es gibt keinen Integrationstest, der einen AlphaVantage-Key setzt und anschliessend den Datenbankwert inspiziert.

### Controller

`FinanceManager.Tests/Controllers/UserImportSplitSettingsControllerTests.cs` erstellt `UserSettingsController` mit InMemory-DB, deckt aber ImportSplit ab. AlphaVantage-spezifische Controller-Tests fuer Setzen, Loeschen, Admin-Sharing und Non-Admin-Forbid fehlen.

### SecurityPrice / PriceProvider

`FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs` mockt `IAlphaVantageKeyResolver`. Dadurch werden Preisabruf-Fehlerpfade getestet, aber nicht die konkrete Resolver-Implementierung oder Secret-Entschluesselung.

## Fehlende Tests Fuer Die Anforderung

- Unit-Test fuer Secret-Komponente: `Protect` liefert nicht den Klartext und `Unprotect` liefert den urspruenglichen Key.
- Persistenztest fuer `UserSettingsController` oder einen neuen Settings-Service: gespeicherter DB-Wert entspricht nicht der Eingabe.
- Resolver-Test: geschuetzter persoenlicher Key wird entschluesselt zurueckgegeben.
- Shared-Key-Test: geschuetzter Admin-Key wird fuer andere Nutzer entschluesselt als Fallback geliefert.
- Clear-Test: `ClearAlphaVantageApiKey` entfernt den gespeicherten Wert weiterhin.
- Non-Admin-Sharing-Test: Nicht-Admin kann `ShareAlphaVantageApiKey = true` nicht setzen.
- Fehlerfall-Test: defekter/alter Ciphertext fuehrt zu kontrollierter Meldung und loggt nicht den Secret-Wert.
- Migration/Lazy-Reprotect-Test: vorhandener Klartext wird beim definierten Ereignis in geschuetztes Format ueberfuehrt.
- Log-/Exception-Test: typische Fehlerpfade enthalten den eingegebenen Key nicht im Klartext.

## Geeignete Testorte

- Secret-Komponente: `FinanceManager.Tests` oder `FinanceManager.Tests.Infrastructure`, je nach Zielprojekt der Implementierung.
- Controller/Settings-Service: `FinanceManager.Tests/Controllers` oder neuer Service-Test mit SQLite/InMemory.
- Integration: `FinanceManager.Tests.Integration/ApiClient/ApiClientUserSettingsTests.cs` erweitern, falls TestFactory DB-Zugriff ausreichend kontrollierbar ist.
- Resolver: eigener Test fuer `AlphaVantageKeyResolver` mit SQLite/InMemory und Fake/DataProtection-Test-Protector.

## Testdaten

API-Key-Testwerte sollten synthetisch sein, z. B. `TEST-ALPHA-KEY-123456`. Tests sollten nie echte AlphaVantage-Keys verwenden und keine externen HTTP-Aufrufe ausloesen.
