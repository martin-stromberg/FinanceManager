← [Zurück zur Übersicht](index.md)

# Kontoauszüge und Import — API

## Übersicht

Die Schnittstelle wird über `StatementDraftsController` und `StatementDraftEntriesController` bereitgestellt.

## Endpunkte / Methoden

### `POST /api/statement-drafts/upload`

**Beschreibung:** Upload einer Datei und Erzeugung eines Entwurfs.

### `POST /api/statement-drafts/mass-import`

**Beschreibung:** Massenimport mehrerer Dateien.

### `POST /api/statement-drafts/{draftId}/classify`

**Beschreibung:** Startet Klassifikation für einen Entwurf.

### `GET /api/statement-drafts/{draftId}/validate`

**Beschreibung:** Liefert Validierungsergebnis.

### `POST /api/statement-drafts/{draftId}/book`

**Beschreibung:** Verbucht den Entwurf.

### `POST /api/statement-drafts/{draftId}/entries/{entryId}/book`

**Beschreibung:** Verbucht eine einzelne Entwurfszeile.

### `POST /api/statement-draft-entries/batch-update`

**Beschreibung:** Führt Batch-Änderungen auf Entwurfszeilen aus.

### `GET /api/statement-drafts/{draftId}/export`

**Beschreibung:** Exportiert die Einträge eines Kontoauszug-Entwurfs als CSV- oder XLSX-Datei. Der Dateiname folgt dem Muster `StatementDraft_{Name}_{yyyyMMddHHmm}.{ext}`; als `Name` dient die Beschreibung des Entwurfs, ersatzweise der ursprüngliche Dateiname.

**Parameter (Query):**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `format` | `string` | Nein | `csv` (Standard bei fehlendem/leerem Wert) oder `xlsx`; Groß-/Kleinschreibung wird ignoriert |
| `from` | `DateTime` | Nein | Untere Grenze für `BookingDate` (einschließlich) |
| `to` | `DateTime` | Nein | Obere Grenze für `BookingDate` (einschließlich des gesamten Tages) |
| `q` | `string` | Nein | Suchbegriff; filtert `Subject`, `RecipientName` und `BookingDescription` (Teilstringsuche) |

**Rückgabe:**

| Typ | Beschreibung |
|-----|--------------|
| `text/csv; charset=utf-8` | Gestreamte CSV-Datei (`;`-Trenner, UTF-8 mit BOM) |
| `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | XLSX-Datei mit Arbeitsblatt `Entries` |

**Exportierte Spalten (CSV und XLSX identisch):**

`EntryNumber; BookingDate; ValutaDate; Amount; Currency; Subject; RecipientName; Description; Status; IsAnnounced; IsCostNeutral; Contact; SavingsPlan; Security; SecurityTransactionType; SecurityQuantity; SecurityFeeAmount; SecurityTaxAmount`

Die Spalten `Contact`, `SavingsPlan` und `Security` enthalten die aufgelösten Namen der zugeordneten Entitäten. Die Zeilen sind nach `EntryNumber` sortiert; die Anzahl ist durch den Konfigurationsschlüssel `Exports:MaxRows` (Standard 50.000) begrenzt.

**Beispiel:**

```
GET /api/statement-drafts/3fa85f64-5717-4562-b3fc-2c963f66afa6/export?format=xlsx&from=2026-01-01&to=2026-01-31&q=amazon
```

**Fehler:**

| Code | Ursache |
|------|---------|
| 400 | `format` ist weder `csv` noch `xlsx` (`Invalid format`) |
| 400 | Zeilenzahl überschreitet `Exports:MaxRows` (`Export limit exceeded`) |
| 404 | Entwurf existiert nicht oder gehört nicht zum angemeldeten Benutzer |

## Fehler

Typische Fehler: ungültige Draft-ID, fehlender Kontokontext, unvollständige Zuordnung, Validierungsfehler. Beim Export zusätzlich: ungültiges Format (`Invalid format`) und Überschreitung des Zeilenlimits (`Export limit exceeded`).
