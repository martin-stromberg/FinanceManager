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
| `ScheduleDeficit` | `decimal?` | Rückstand gegenüber dem erkannten Einzahlungsrhythmus, nur im laufenden Finanzierungszyklus (erwartete minus erhaltene Einzahlungen seit Zyklusbeginn, min. 0); `null` ohne Buchungen, ohne erkennbare dominante Betragsklasse, ohne erkennbaren Rhythmus oder bei revolvierendem Plan (zyklisch ausgeglichen) |
| `ExpectedToDate` | `decimal?` | Erwartete Summe der Einzahlungen seit Zyklusbeginn (Betrag der dominanten Betragsklasse × voll abgelaufene Einzahlungsperioden der beobachteten Kadenz, laufender Monat nicht fällig) |
| `ReceivedToDate` | `decimal?` | Bei erkanntem Rhythmus Summe der Buchungen der dominanten Betragsklasse seit Zyklusbeginn (letzte fällige Entnahme ≈ Zielbetrag, sonst letztes erreichtes Zieldatum, sonst erste Einzahlung) — Mikro-Postings wie Zinsgutschriften (unter ~50 % des dominanten Klassenbetrags) fließen nicht ein; ohne Rhythmus die faktische Summe aller positiven, nicht stornierten Buchungen |
| `ObservedMonthlyRate` | `decimal?` | Betrag der dominanten Betragsklasse der positiven Zyklus-Buchungen (häufigste Klasse, Cluster-Toleranz max(0,50 EUR, 5 %), Gleichstand zugunsten des größeren Betrags; eine häufigste Mikro-Klasse verliert gegen eine vergleichbar häufige größere Klasse), über die beobachtete Einzahlungskadenz auf Monatsrate normiert (Median der Monatsabstände zwischen den Belegmonaten der dominanten Klasse; das Plan-Intervall — der Fälligkeitsrhythmus der Ausgabe — greift nur als Fallback, wenn keine Kadenz ableitbar ist) |
| `Disbursements` | `decimal?` | Summe aller negativen effektiven Buchungen (Vorleistung/Entnahme) über die gesamte Historie — separat ausgewiesen, fließt nie ins Deficit |

### `SavingsPlanCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |

### `SavingsPlanAccountLink`

Explizite n:m-Verknüpfung zwischen `SavingsPlan` und `Account` (eigene Tabelle
`SavingsPlanAccountLinks`). Ein Sparplan kann mehrere Konten bedienen lassen, ein Konto kann
mehrere Sparpläne bedienen (z. B. Sammelkonto). Die Paarung `(SavingsPlanId, AccountId)` ist
eindeutig; Verknüpfungen sind nur zwischen Plan und Konto desselben Eigentümers zulässig.
Beim Löschen von Plan oder Konto werden die Verknüpfungen per Cascade mitentfernt.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Verknüpfungs-ID |
| `SavingsPlanId` | `Guid` | Verknüpfter Sparplan |
| `AccountId` | `Guid` | Verknüpftes Konto |
| `OwnerUserId` | `Guid` | Eigentümer (redundant, für Owner-Prüfung/Indizes) |
| `CreatedUtc` | `DateTime` | Erstellungszeitpunkt (UTC) |
| `ModifiedUtc` | `DateTime?` | Letzter Änderungszeitpunkt (UTC) |

### `Posting.SavingsPlanSubType`

Optionale Unterart auf Plan-Buchungen (`Kind == SavingsPlan`). Derzeit ein Wert:
`ActualReport` — kennzeichnet eine Istmeldung (Korrekturbuchung ohne Kontobewegung,
nur für Pläne ohne verknüpfte Konten). Buchungen ohne Unterart sind reguläre
Einzahlungs- oder Entnahmebuchungen. Informelle Funding-Buchungen werden nicht
persistiert markiert, sondern aus dem Konto-Link abgeleitet (`informal` in der
Postings-Ausgabe).
