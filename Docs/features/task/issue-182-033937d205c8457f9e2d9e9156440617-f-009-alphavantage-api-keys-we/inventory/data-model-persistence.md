# Datenmodell und Persistenz

## Domainmodell

`FinanceManager.Domain/Users/User.AlphaVantage.cs` erweitert `User` um AlphaVantage-Einstellungen:

- `AlphaVantageApiKey` ist `string?` mit privatem Setter.
- `ShareAlphaVantageApiKey` ist `bool` mit privatem Setter.
- `SetAlphaVantageKey(string? apiKey)` speichert `null` bei leerem Wert, ansonsten `apiKey.Trim()`.
- `SetShareAlphaVantageKey(bool share)` setzt nur das Sharing-Flag.

Der Setter enthaelt keine Verschluesselung, Redaction, Formatmarkierung oder Validierung ueber Laenge/Whitespace hinaus. Aus Domain-Sicht ist nicht unterscheidbar, ob die Property Klartext oder geschuetzten Text enthaelt.

## EF-Konfiguration

`FinanceManager.Infrastructure/AppDbContext.cs` konfiguriert die User-Entity in `OnModelCreating`:

- `AlphaVantageApiKey` bekommt `HasMaxLength(120)`.
- `ShareAlphaVantageApiKey` bekommt `HasDefaultValue(false)`.
- Es gibt keinen ValueConverter, keine owned Secret-Entity und keine separate Secret-Tabelle.

Wichtig: Data-Protection-Ciphertexte sind typischerweise laenger als der eingegebene API-Key. Die aktuelle Maximallaenge 120 ist fuer geschuetzte Payloads wahrscheinlich zu klein. Eine Umsetzung muss die Spaltenlaenge erhoehen oder ein anderes Persistenzfeld verwenden.

## Migrationen und Snapshot

`FinanceManager.Infrastructure/Migrations/20251004185146_AddAlphaVantageSettings.cs` fuehrte die Spalten ein:

- `AlphaVantageApiKey` als nullable `TEXT` mit `maxLength: 120`.
- `ShareAlphaVantageApiKey` als nicht-nullable Boolean mit Default `false`.

Aktuelle Designer-/Snapshot-Dateien enthalten weiterhin `AlphaVantageApiKey` als String-Property. Eine Umsetzung braucht eine neue EF-Migration fuer mindestens die erweiterte Laenge oder eine Umbenennung/Neuanlage, falls Klartext- und geschuetzte Werte getrennt werden sollen.

## Persistierte Tabellen

Die User-Entity erbt von ASP.NET Identity. Der aktuelle Snapshot zeigt die Property in `AspNetUsers`, waehrend die alte Migration `Users` nennt. Das Projekt nutzt historisch beide Benennungen in Migrationen/Snapshots. Bei der Planung muss die tatsaechliche aktive Migration-Historie und Provider-Konfiguration geprueft werden, bevor SQL-spezifische Migrationen geschrieben werden.

## Auswirkungen Einer Secret-Komponente

Eine zentrale Komponente sollte folgende Verantwortungen kapseln:

- Formatpraefix fuer geschuetzte AlphaVantage-Werte, z. B. `dp:v1:...`.
- Verschluesselung mit Data Protection oder alternativer KMS-/Vault-Implementierung.
- Entschluesselung mit kontrollierter Fehlerbehandlung.
- Erkennung alter Klartextwerte fuer Migration/Lazy-Reprotect.
- Keine Ausgabe des Klartexts in Exceptions oder Logs.

Die Domain-Property kann technisch weiter als Persistenzfeld dienen, sollte fachlich aber nicht mehr als Klartext-Key interpretiert werden.
