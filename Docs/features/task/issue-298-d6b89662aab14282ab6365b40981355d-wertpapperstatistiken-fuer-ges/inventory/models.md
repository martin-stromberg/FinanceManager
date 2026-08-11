# Bestandsaufnahme: Datenmodelle

Übersicht der vorhandenen Datenmodellklassen, die für die Portfolio-Analyse relevant sind.

## `Security`

**Datei:** `FinanceManager.Domain/Securities/Security.cs`

Zentrale Klasse für Wertpapiere. **Aktuell OHNE die in der Anforderung geforderten Eigenschaften!**

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Kennung des Wertpapiers. |
| `OwnerUserId` | `Guid` | Benutzer-ID des Besitzers. |
| `Name` | `string` | Anzeigename des Wertpapiers. |
| `Identifier` | `string` | Primäre Kennung (WKN/ISIN). |
| `Description` | `string?` | Optionale Beschreibung. |
| `AlphaVantageCode` | `string?` | Optionaler Code für externe Preisanbieter. |
| `CurrencyCode` | `string` | ISO-Währungscode (z.B. "EUR"). |
| `CategoryId` | `Guid?` | Optionale Kategorie-ID (für Asset Allocation). |
| `IsActive` | `bool` | Gibt an, ob das Wertpapier aktiv ist. |
| `ArchivedUtc` | `DateTime?` | UTC-Zeitstempel der Archivierung. |
| `HasPriceError` | `bool` | Gibt Fehler beim Kursfetch an. |
| `PriceErrorMessage` | `string?` | Optionale Fehlermeldung. |
| `PriceErrorSinceUtc` | `DateTime?` | UTC-Zeitstempel seit Fehlerzustand. |
| `SymbolAttachmentId` | `Guid?` | Optionale Referenz zu Symbol-Anhang. |
| **FEHLEND:** `Region` | `string?` | **NICHT VORHANDEN** – Erforderlich für regionale Verteilung. |
| **FEHLEND:** `Sector` | `string?` | **NICHT VORHANDEN** – Erforderlich für Sektorverteilung. |

### Wichtige Methoden

- `Update(name, identifier, description, alphaVantageCode, currencyCode, categoryId)` – Aktualisiert Core-Metadaten.
- `Archive()` – Archiviert das Wertpapier.
- `SetPriceError(message)` – Markiert Preisfehler.
- `ClearPriceError()` – Löscht Preisfehler.
- `SetSymbolAttachment(attachmentId)` – Setzt/löscht Symbol-Anhang.

---

## `Posting`

**Datei:** `FinanceManager.Domain/Postings/Posting.cs`

Zentrale Klasse für Transaktionen/Buchungen. Enthält Mengen- und Subtyp-Informationen für Wertpapier-Buchungen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Kennung. |
| `SourceId` | `Guid` | Externe/Import-ID. |
| `GroupId` | `Guid` | Gruppen-ID für verwandte Buchungen. |
| `Kind` | `PostingKind` | Buchungsart (Bank, Kontakt, SavingsPlan, Security). |
| `AccountId` | `Guid?` | Optionale Bank-Konto-ID. |
| `ContactId` | `Guid?` | Optionale Kontakt-ID. |
| `SavingsPlanId` | `Guid?` | Optionale Sparplan-ID. |
| `SecurityId` | `Guid?` | Optionale Wertpapier-ID. |
| `BookingDate` | `DateTime` | Buchungsdatum. |
| `ValutaDate` | `DateTime` | Valuta-/Wertstellungsdatum. |
| `Amount` | `decimal` | Betrag. |
| `OriginalAmount` | `decimal?` | Optionaler Originalbetrag (vor Stornierungen). |
| `Subject` | `string?` | Optionaler Betreff. |
| `RecipientName` | `string?` | Optionaler Name des Empfängers. |
| `Description` | `string?` | Optionale Beschreibung. |
| `SecuritySubType` | `SecurityPostingSubType?` | Optionaler Subtyp (Buy, Sell, Dividend, Fee, Tax). |
| `Quantity` | `decimal?` | Optionale Menge (hauptsächlich für Wertpapier-Buchungen). |
| `ParentId` | `Guid?` | Optionale Referenz zu übergeordneter Buchung. |
| `LinkedPostingId` | `Guid?` | Optionale Gegenposition (Selbst-Transfers). |
| `ReversedByPostingId` | `Guid?` | Referenz zur stornierenden Buchung. |
| `ReversalForPostingId` | `Guid?` | Referenz zur ursprünglichen Buchung (wenn dies eine Stornierung ist). |
| `ReversedByUserId` | `Guid?` | Benutzer-ID der Stornierung. |
| `ReversedAtUtc` | `DateTime?` | Zeitstempel der Stornierung. |

### Wichtige Methoden

- Verschiedene Konstruktoren mit unterschiedlichem Detailgrad.
- `SetGroup(groupId)` – Setzt Gruppen-ID.
- `SetParent(parentId)` – Setzt übergeordnete Buchung.
- `SetLinkedPosting(linkedPostingId)` – Setzt Gegenposition.
- `SetValutaDate(valutaDate)` – Aktualisiert Valutadatum.
- `SetReversedBy(reversalPosting, userId)` – Markiert Stornierung.
- `SetReversalFor(originalPosting)` – Markiert als Gegenbuchung.
- `SetOriginalAmount(amount)` – Setzt Originalbetrag.

---

## `SecurityPrice`

**Datei:** `FinanceManager.Domain/Securities/SecurityPrice.cs`

Speichert historische Schlusskurse für Wertpapiere (verwendet für Performance-Berechnung und Volatilität).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Kennung. |
| `SecurityId` | `Guid` | Referenz zum Wertpapier. |
| `Date` | `DateTime` | Datum des Schlusskurses (nur Datum-Komponente). |
| `Close` | `decimal` | Schlusskurs-Wert. |
| `CreatedUtc` | `DateTime` | UTC-Zeitstempel der Erstellung. |

### Wichtige Methoden

- `SecurityPrice(securityId, date, close)` – Konstruktor.

---

## `SecurityCategory`

**Datei:** `FinanceManager.Domain/Securities/SecurityCategory.cs`

Kategorien für Wertpapiere (z.B. "Aktien", "Anleihen", "Fonds") – verwendet für Asset Allocation.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Kennung. |
| `OwnerUserId` | `Guid` | Benutzer-ID des Besitzers. |
| `Name` | `string` | Name der Kategorie. |
| `SymbolAttachmentId` | `Guid?` | Optionale Symbol-Anhang-ID. |

### Wichtige Methoden

- `Rename(name)` – Benennt Kategorie um.
- `SetSymbolAttachment(attachmentId)` – Setzt/löscht Symbol-Anhang.

---

## `ReportCacheEntry`

**Datei:** `FinanceManager.Domain/Reports/ReportCacheEntry.cs`

Verwaltung von gecachten Report-Daten. **Aktuell OHNE das geforderte `CacheValidUntilUtc` Feld!**

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `Guid` | Eindeutige Kennung des Cache-Eintrags. |
| `OwnerUserId` | `Guid` | Benutzer-ID des Besitzers. |
| `CacheKey` | `string` | Cache-Schlüssel zur Identifikation (z.B. "budgetreportraw-20260101-20261231-ByValuta"). |
| `CacheValue` | `string` | Serialisierte JSON-Daten. |
| `NeedsRefresh` | `bool` | Gibt an, ob die Cache-Daten neu berechnet werden müssen. |
| `Parameter` | `string` | Zusätzliche Parameter (z.B. Datumsbereich als JSON). |
| **FEHLEND:** `CacheValidUntilUtc` | `DateTime?` | **NICHT VORHANDEN** – Für monatliche Gültigkeitsdauer erforderlich. |

### Wichtige Methoden

- `Update(cacheValue, parameter, needsRefresh)` – Aktualisiert Cache-Eintrag.
- `MarkForRefresh()` – Markiert Cache für Neuberechnung.

---

## Zusammenhang der Modelle

- **Security** → **SecurityCategory** (optional via `CategoryId`)
- **Security** → **SecurityPrice** (historische Kurse)
- **Posting** → **Security** (Wertpapier-Buchungen via `SecurityId`)
- **Posting** → **Account** / **Contact** / **SavingsPlan** (andere Buchungsarten)
- **ReportCacheEntry** → wird von `ReportCacheService` verwaltet
