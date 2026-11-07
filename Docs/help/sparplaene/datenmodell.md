← [Zurück zur Übersicht](index.md)

# Sparpläne — Datenmodell

## Entitäten

### `SavingsPlan`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Sparplan-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
| `Type` | `SavingsPlanType` | Einmalig oder wiederkehrend |
| `TargetAmount` | `decimal?` | Optionales Ziel |
| `TargetDate` | `DateTime?` | Optionales Zieldatum |
| `Interval` | `SavingsPlanInterval?` | Wiederholintervall |
| `CategoryId` | `Guid?` | Kategorie |
| `IsActive` | `bool` | Aktivstatus |
| `ArchivedUtc` | `DateTime?` | Archivzeit |

### `SavingsPlanDto`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `CurrentAmount` | `decimal` | Aktueller Saldo des Sparplans aus den zugeordneten Buchungen |
| `RemainingAmount` | `decimal` | Noch offener Betrag bis zum Zielbetrag |

### `SavingsPlanAnalysisDto`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `RequiredMonthly` | `decimal` | Durchschnittlich benötigter Monatsbetrag bis zum Zieldatum |
| `ScheduleDeficit` | `decimal?` | Rückstand gegenüber dem erkannten Einzahlungsrhythmus (erwartete minus erhaltene Einzahlungen, min. 0); `null` ohne Buchungen bzw. ohne erkennbaren Rhythmus |
| `ExpectedToDate` | `decimal?` | Erwartete Summe der Einzahlungen bis heute (beobachtete Rate × abgelaufene Perioden) |
| `ReceivedToDate` | `decimal?` | Summe positiver, nicht stornierter Einzahlungsbuchungen |
| `ObservedMonthlyRate` | `decimal?` | Median der Einzahlungssummen pro Belegmonat, über das Plan-Intervall auf Monatsrate normiert |
| `Disbursements` | `decimal?` | Summe außerordentlicher negativer Buchungen (Vorleistung/Entnahme), separat ausgewiesen — fließt nie ins Deficit |

### `SavingsPlanCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
