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
| `ScheduleDeficit` | `decimal?` | Rückstand gegenüber dem erkannten Einzahlungsrhythmus, nur im laufenden Finanzierungszyklus (erwartete minus erhaltene Einzahlungen seit Zyklusbeginn, min. 0); `null` ohne Buchungen, ohne erkennbaren Rhythmus oder bei revolvierendem Plan (zyklisch ausgeglichen) |
| `ExpectedToDate` | `decimal?` | Erwartete Summe der Einzahlungen seit Zyklusbeginn (beobachtete Rate × abgelaufene Perioden, laufender Monat nicht fällig) |
| `ReceivedToDate` | `decimal?` | Summe positiver, nicht stornierter Einzahlungsbuchungen seit Zyklusbeginn (letzte fällige Entnahme ≈ Zielbetrag, sonst letztes erreichtes Zieldatum, sonst erste Einzahlung) |
| `ObservedMonthlyRate` | `decimal?` | Median der Einzahlungssummen pro Belegmonat im laufenden Zyklus, über das Plan-Intervall auf Monatsrate normiert |
| `Disbursements` | `decimal?` | Summe aller negativen effektiven Buchungen (Vorleistung/Entnahme) über die gesamte Historie — separat ausgewiesen, fließt nie ins Deficit |

### `SavingsPlanCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
