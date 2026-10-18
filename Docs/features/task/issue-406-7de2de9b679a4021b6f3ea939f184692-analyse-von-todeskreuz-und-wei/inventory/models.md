# Datenmodell

## `Security`
Datei: `FinanceManager.Domain/Securities/Security.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Entitäts-Id (aus `Entity`) |
| `OwnerUserId` | `Guid` | Besitzer (Zugriffsscope) |
| `Name` | `string` | Anzeigename |
| `Identifier` | `string` | WKN/ISIN |
| `AlphaVantageCode` | `string?` | Symbol für externen Kursabruf |
| `CurrencyCode` | `string` | ISO-Währung der Kurse |
| `IsActive` / `ArchivedUtc` | `bool` / `DateTime?` | Archivierung |
| `HasPriceError` / `PriceErrorMessage` / `PriceErrorSinceUtc` | `bool` / `string?` / `DateTime?` | Kursabruf-Fehlerstatus |
| `SymbolAttachmentId` | `Guid?` | Symbol-Anhang |
| `GoldenCrossNotifiedPhase` | `GoldenCrossPhase` | Höchste bereits benachrichtigte Golden-Cross-Phase; Default/Sentinel `Far`; Setter `SetGoldenCrossNotifiedPhase(GoldenCrossPhase)` |

`Security` wird in `AppDbContext` konfiguriert (Zeile ~519): `GoldenCrossNotifiedPhase` mit `HasConversion<string>()`, `HasDefaultValue(Far)`, `HasSentinel(Far)`.

## `SecurityPrice`
Datei: `FinanceManager.Domain/Securities/SecurityPrice.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Datensatz-Id |
| `SecurityId` | `Guid` | Verweis auf `Security` |
| `Date` | `DateTime` | Datum (nur Datumskomponente) |
| `Close` | `decimal` | Schlusskurs |
| `CreatedUtc` | `DateTime` | Anlagezeitpunkt |

## `User` (Ausschnitt Benachrichtigungen)
Datei: `FinanceManager.Domain/Users/User.Notifications.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `GoldenCrossNotificationsEnabled` | `bool` | Home-Page-Hinweise für Goldenes Kreuz; Default `true`; Setter `SetGoldenCrossNotificationsEnabled(bool)` |
| `PreferredLanguage` | `string?` | Sprache für Benachrichtigungstexte |

## Migration
`FinanceManager.Infrastructure/Migrations/20260922184619_AddGoldenCross.cs` — hat `GoldenCrossNotifiedPhase` auf `Securities` und `GoldenCrossNotificationsEnabled` auf `Users` ergänzt. `AppDbContextModelSnapshot.cs` enthält den aktuellen Stand.
