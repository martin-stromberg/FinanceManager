# Datenmodell — Bestandsaufnahme

## `Posting`

Datei: `FinanceManager.Domain/Postings/Posting.cs` (551 Zeilen)

`public sealed class Posting : Entity, IAggregateRoot` — Buchungszeile, Aggregatwurzel. **Kein `OwnerUserId`**; der Besitzerpfad ist indirekt über die vier referenzierten Aggregate.

### Konstruktoren (alle fünf delegieren auf den vollständigen Konstruktor)

| Konstruktor | Zeilen | Signatur (Kern) | Delegation |
|-------------|--------|------------------|------------|
| Basis | 22–23 | `(sourceId, kind, accountId, contactId, savingsPlanId, securityId, bookingDate, amount)` | `: this(..., bookingDate, bookingDate, amount, null×5)` |
| +Details | 41–54 | `… amount, subject, recipientName, description, securitySubType` | `: this(..., bookingDate, bookingDate, …, null)` |
| +Quantity | 74–88 | `… description, securitySubType, quantity` | `: this(..., bookingDate, bookingDate, …, quantity)` |
| **Hauptkonstruktor** | **108–141** | `… bookingDate, valutaDate, amount, subject, recipientName, description, securitySubType, quantity` | — (einziger Konstruktor mit Body; einzige Guard: `Guards.NotEmpty(sourceId)`, Z. 124) |
| Text-Überladung | 160–161 | `… bookingDate, valutaDate, amount, text1, text2, text3, subType` | `: this(..., subType, null)` |

Es existiert **kein parameterloser/privater Konstruktor** — EF-Core-Materialisierung läuft über den Hauptkonstruktor (oder wird von EF per Konvention gebunden). Alle vier Referenzparameter sind `Guid?` und werden ungeprüft zugewiesen (Z. 126–129): `AccountId`, `ContactId`, `SavingsPlanId`, `SecurityId` können gleichzeitig `null` sein — **keine Invariante vorhanden**.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceId` | `Guid` | Quell-/Import-ID; Guard `NotEmpty` im Konstruktor (Z. 124) |
| `GroupId` | `Guid` | Gruppenschlüssel; `Guid.Empty` nach Konstruktion, gesetzt via `SetGroup` (Z. 320–328) |
| `Kind` | `PostingKind` | Ziel-Domäne der Buchung (Bank/Contact/SavingsPlan/Security) |
| `AccountId` | `Guid?` | Optional — Referenz Konto (Z. 185) |
| `ContactId` | `Guid?` | Optional — Referenz Kontakt (Z. 191) |
| `SavingsPlanId` | `Guid?` | Optional — Referenz Sparplan (Z. 197) |
| `SecurityId` | `Guid?` | Optional — Referenz Wertpapier (Z. 203) |
| `BookingDate` / `ValutaDate` | `DateTime` | Buchungs-/Valutadatum |
| `Amount` / `OriginalAmount` | `decimal` / `decimal?` | Betrag; `OriginalAmount` bei genullten Buchungen (`ZeroAmount`, Z. 468–477) |
| `Subject` / `RecipientName` / `Description` | `string?` | Textfelder |
| `SecuritySubType` | `SecurityPostingSubType?` | Subtyp Wertpapierbuchungen |
| `SavingsPlanSubType` | `SavingsPlanPostingSubType?` | Subtyp Sparplanbuchungen (z. B. „Istmeldung"); Setter `SetSavingsPlanSubType` (Z. 257–262) |
| `Quantity` | `decimal?` | Menge (Wertpapier) |
| `ParentId` / `LinkedPostingId` | `Guid?` | Split-/Gegenbuchungs-Verknüpfungen (`SetParent` Z. 336–344, `SetLinkedPosting` Z. 352–360) |
| `ReversedByPostingId` / `ReversalForPostingId` / `ReversedByUserId` / `ReversedAtUtc` | `Guid?` / `Guid?` / `Guid?` / `DateTime?` | Storno-Verknüpfungen (`SetReversedBy` Z. 381–397, `SetReversalFor` Z. 405–419) |
| `IsReversed` / `IsReversal` | `bool` (computed) | Abgeleitet aus den Storno-Referenzen (Z. 425, 431) |
| `IsPreliminary` | `bool` | Vorläufige Buchung (`SetIsPreliminary` Z. 444–449) |

### Backup-Pfad

- `PostingBackupDto` (Z. 509): `sealed record` mit 24 Parametern; die vier Referenzen sind `Guid?` ohne Constraint — ein referenzloser Zustand ist im DTO abbildbar. Neue DTO-Parameter haben Default-Werte (`IsPreliminary = false`, `SavingsPlanSubType = null`).
- `ToBackupDto()` (Z. 515–516): 1:1-Abbildung.
- `AssignBackupDto(PostingBackupDto)` (Z. 523–550): setzt **alle vier Referenzen direkt** (Z. 528–531) und umgeht damit jeden Konstruktor-Guard — relevant für Offene Frage 2 der Anforderung.

## `PostingAggregate`

Datei: `FinanceManager.Domain/Postings/PostingAggregate.cs` (149 Zeilen; Datei enthält auch Enums `AggregatePeriod` Z. 8–29, `AggregateDateKind` Z. 34–45)

`public sealed class PostingAggregate : Entity, IAggregateRoot` — voraggregierte Posting-Summen pro Periode/Gruppierung. **Dasselbe Vier-Referenzen-Muster wie `Posting`, ebenfalls ohne Guard** (Scope-Ausschluss des Issues).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `PostingKind` | Art der aggregierten Buchungen |
| `AccountId` / `ContactId` / `SavingsPlanId` / `SecurityId` | `Guid?` | Gruppierungsschlüssel (Z. 97–112), alle optional, kein Guard im Konstruktor (Z. 66–87) |
| `SecuritySubType` | `SecurityPostingSubType?` | Subtyp-Aufschlüsselung |
| `DateKind` | `AggregateDateKind` | Booking/Valuta |
| `PeriodStart` / `Period` | `DateTime` / `AggregatePeriod` | Periode |
| `Amount` | `decimal` | Summe; `Add(delta)` Z. 143–148 |

Zusätzlich privater parameterloser Konstruktor (`private PostingAggregate() { }`, Z. 52) für EF.

## `IpBlock` (global — ohne Globalitäts-Hinweis)

Datei: `FinanceManager.Domain/Security/IpBlock.cs` (127 Zeilen)

`public sealed class IpBlock : Entity, IAggregateRoot` — IP-Sperrliste. **Kein `OwnerUserId`, kein Hinweis auf Globalität im XML-Doc** (Klassen-Doc Z. 3–7 beschreibt nur die Fachlichkeit).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `IpAddress` | `string` | Getrackte IP; Guard `NotNullOrWhiteSpace` (Z. 21); unique Index (`AppDbContext` Z. 754) |
| `IsBlocked` / `BlockedAtUtc` / `BlockReason` | `bool` / `DateTime?` / `string?` | Blockstatus (`Block` Z. 109–115, `Unblock` Z. 120–126) |
| `UnknownUserFailedAttempts` / `UnknownUserLastFailedUtc` | `int` / `DateTime?` | Fehlversuchszähler (`RegisterUnknownUserFailure` Z. 82–92, `ResetUnknownUserCounters` Z. 97–102) |

Privater parameterloser Konstruktor Z. 12; öffentlicher `IpBlock(string ipAddress)` Z. 19–23.

## `SecurityTxtSettings` (global — ohne Globalitäts-Hinweis)

Datei: `FinanceManager.Domain/Security/SecurityTxtSettings.cs` (84 Zeilen)

`public sealed class SecurityTxtSettings : Entity, IAggregateRoot` — konfigurierbare security.txt-Direktiven. **Kein `OwnerUserId`, kein Globalitäts-Hinweis** (Klassen-Doc Z. 3–6 nur eine Zeile). Singleton-Charakter implizit über `CreateUnconfigured()` (Z. 16–24) und Doku in `Docs/help/systemverwaltung-und-setup/datenmodell.md` Z. 95.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Contact` | `string` | Pflicht-Direktive; Guard `NotNullOrWhiteSpace` (Z. 33, 60) |
| `Expires` | `DateTimeOffset` | Muss in Zukunft liegen (`EnsureFutureExpires` Z. 77–83) |
| `Encryption` / `Acknowledgments` / `PreferredLanguages` / `Policy` / `Hiring` / `Canonical` | `string?` | Optionale Direktiven (`Update` Z. 57–70) |

## `WellKnownSettings` (global — ohne Globalitäts-Hinweis)

Datei: `FinanceManager.Domain/WellKnown/WellKnownSettings.cs` (60 Zeilen)

`public sealed class WellKnownSettings : Entity, IAggregateRoot` — well-known-Endpunkt-Einstellungen. **Kein `OwnerUserId`, kein Globalitäts-Hinweis** (Klassen-Doc Z. 5–8 nur eine Zeile); Singleton bereits über `SingletonId` (Z. 19) + `CreateDefault()` (Z. 30–37) dokumentiert.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `SingletonId` | `static readonly Guid` | Feste ID der einen Zeile (Z. 19) |
| `DefaultChangePasswordUrl` | `const string` | `/change-password` (Z. 11) |
| `ChangePasswordUrl` | `string` | Redirect-Ziel; `Update` mit URL-Validierung (Z. 44–54), `IsValidUrl` (Z. 59) |

## `McpSettings` (Referenz/Vorbild)

Datei: `FinanceManager.Domain/Mcp/McpSettings.cs` (65 Zeilen)

`public sealed class McpSettings : Entity, IAggregateRoot` — MCP-Integrationseinstellungen. **Kein `OwnerUserId`.** Klassen-Doc (Z. 3–10) beschreibt „Database-stored, administrator-editable settings" + die beiden Schalter; Singleton über `SingletonId` (Z. 19) + `CreateDefault()` (Z. 31–39) mit Doku „the one and only settings row". **Auch hier kein wörtlicher „globaler Datenpool"-Hinweis** — die Anforderung nimmt diese Doku als Vorbild, tatsächlich trägt auch `McpSettings` die Globalität nur implizit (Singleton ohne Besitzer). Explizite „global"-Formulierungen existieren stattdessen bei `Notification.OwnerUserId` (`Notifications/Notification.cs` Z. 17: „when null the notification is global") und `McpAuditEntry.OwnerUserId` (`Mcp/McpAuditEntry.cs` Z. 104: „null for purely global events").

## `Guards`

Datei: `FinanceManager.Domain/Guards.cs` (22 Zeilen)

`internal static class Guards` — bestehende Guard-Helfer, alle werfen `ArgumentException`:

| Methode | Parameter | Verhalten |
|---------|-----------|-----------|
| `NotNullOrWhiteSpace` | `(string value, string paramName)` | wirft bei `IsNullOrWhiteSpace` |
| `NotEmpty` | `(Guid value, string paramName)` | wirft bei `Guid.Empty` |
| `NotZero` | `(decimal value, string paramName)` | wirft bei `0m` |

Kein Helfer für „mindestens eine von n Referenzen gesetzt" vorhanden.

## `Entity` / `IAggregateRoot`

Datei: `FinanceManager.Domain/Entity.cs` (49 Zeilen)

- `Entity` (abstract): `Id` (`Guid`, `protected set`, Default `Guid.NewGuid()`), `CreatedUtc`, `ModifiedUtc`, `Touch()`, `SetDates()`.
- `IAggregateRoot`: Marker-Interface ohne Member — implementiert von allen hier betrachteten Entitäten.
