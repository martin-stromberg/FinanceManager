# Plan-Review - Loeschen bei Massenaenderungsmodus

Erstellt am: 2026-07-26

## Ergebnis

Die aktuelle Implementierung setzt die Kernfunktion fachlich weitgehend um: Der Batch-Vertrag enthaelt Updates, Deletes und Creates; der Controller akzeptiert kombinierte Requests; der Service validiert vor der Persistenz und arbeitet in einer Transaktion; das QuickEdit-ViewModel verwaltet lokale Loeschungen, neue Zeilen und eine Placeholder-Zeile; die Tabelle bietet eine Delete-Aktion; `SaveQuickEditAsync` sendet den kombinierten Request.

Der Plan ist aber nicht vollstaendig umgesetzt. Es bleiben planrelevante Abweichungen in API-Serialisierung, Testabdeckung und UI-Ausgestaltung.

## Befunde

### 1. Create-Datumswerte werden nicht wie geplant als `yyyy-MM-dd` serialisiert

Status: offen

Planbezug:

- Arbeitspaket 2 verlangt, dass die Datumsnormalisierung auf `yyyy-MM-dd` fuer `Creates.BookingDate` und `Creates.ValutaDate` sichergestellt wird.
- Arbeitspaket 7 verlangt einen API-Client-Test fuer stabile Create-Serialisierung mit `yyyy-MM-dd`.

Ist-Zustand:

- `FinanceManager.Shared/ApiClient.StatementDrafts.cs:212` bis `FinanceManager.Shared/ApiClient.StatementDrafts.cs:221` kopiert `BookingDate = c.BookingDate.Date` und `ValutaDate = c.ValutaDate?.Date`.
- Anschliessend serialisiert `JsonSerializer.Serialize(...)` diese `DateTime`-Werte in `FinanceManager.Shared/ApiClient.StatementDrafts.cs:224` bis `FinanceManager.Shared/ApiClient.StatementDrafts.cs:225`.
- Anders als bei `Updates.Fields` in `FinanceManager.Shared/ApiClient.StatementDrafts.cs:189` bis `FinanceManager.Shared/ApiClient.StatementDrafts.cs:196` wird kein String im Format `yyyy-MM-dd` erzeugt.

Auswirkung:

Create-Datumswerte werden voraussichtlich als ISO-DateTime mit Zeitanteil serialisiert, nicht als date-only String. Damit ist der Planpunkt nicht erfuellt und es fehlt der vorgesehene Regressionstest.

Empfehlung:

Fuer Creates einen serialisierbaren Zwischenvertrag oder gezielte String-Konvertierung nutzen und einen API-Client-Test ergaenzen, der den JSON-Body prueft.

### 2. Die geplante Testabdeckung ist nur teilweise umgesetzt

Status: offen

Planbezug:

Arbeitspaket 7 listet konkrete Tests fuer ViewModel, Service/Persistenz und API-Client:

- QuickEdit-Start zeigt Placeholder.
- Lokales Loeschen ohne API-Aufruf.
- Cancel stellt geloeschte Zeilen wieder her und entfernt neue Zeilen.
- Save-Aktivierung fuer reine Loeschung und reine Neuanlage.
- Ungueltige neue Zeile blockiert Save.
- Kombinierter Request persistiert Updates, Deletes und Creates.
- Fehler in Create verhindert Updates und Deletes.
- Delete fremder/nicht zugehoeriger Entry-IDs wird abgelehnt.
- Reine bestehende Updates bleiben kompatibel.
- API-Client serialisiert Create-Daten mit `yyyy-MM-dd`.

Ist-Zustand:

- Geaendert wurde nur `FinanceManager.Tests/Statements/StatementDraftServiceTests.cs`.
- Dort wurden drei Service-Tests ergaenzt:
  - kombinierter Save,
  - Atomaritaet bei invalidem Create,
  - Ablehnung von Announced-Delete.
- Keine Aenderungen liegen in `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs`, `FinanceManager.Tests/Statements/StatementDraftPersistenceTests.cs` oder `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs`.

Auswirkung:

Die wichtigsten UI-/ViewModel-Regeln und die API-Client-Serialisierung sind nicht automatisiert abgesichert. Das ist besonders relevant, weil die neue Funktion stark von lokalem UI-Zustand abhaengt.

Empfehlung:

Mindestens die fehlenden ViewModel-Tests fuer Placeholder, Delete, Cancel, Save-Aktivierung und Create-Validierung sowie den API-Client-Serialisierungstest nachziehen. Den Fremd-/Draft-ID-Delete-Fall ebenfalls serverseitig absichern.

### 3. Placeholder-/New-Row-Klassen werden gesetzt, aber nicht wie geplant visuell ausgestaltet

Status: offen

Planbezug:

Arbeitspaket 5 nennt neben der Razor-Datei auch:

- `FinanceManager.Web/wwwroot/css/app.StatementDraftDetail.css`
- `FinanceManager.Web/wwwroot/css/theme.Dark.StatementDraftDetail.css`

Der Placeholder soll visuell als Eingabezeile am Tabellenende angezeigt werden.

Ist-Zustand:

- `FinanceManager.Web/Shared/QuickEdit/QuickEditTable.razor:196` setzt `quick-edit-placeholder-row` und `quick-edit-new-row`.
- In den geplanten CSS-Dateien gibt es keine Regeln fuer diese Klassen.
- Die CSS-Dateien wurden nicht geaendert.

Auswirkung:

Die Funktion ist bedienbar, aber der visuelle Planpunkt ist nicht vollstaendig umgesetzt. Neue und Placeholder-Zeilen unterscheiden sich ohne passende CSS-Regeln nur durch vorhandene Standardfelder und nicht durch den vorgesehenen etablierten Tabellenstil.

Empfehlung:

Kurze CSS-Regeln fuer Light- und Dark-Theme ergaenzen, die Placeholder und neue lokale Zeilen sichtbar, aber zurueckhaltend kennzeichnen.

## Umgesetzte Planpunkte

- DTOs wurden um `Deletes`, `Creates`, `EntryCreateDto` und `ClientId`-Fehlermapping erweitert.
- Der bestehende Batch-Endpunkt bleibt kompatibel und akzeptiert kombinierte Requests.
- Servervalidierung prueft Draft-Status, Entry-Zugehoerigkeit, Delete-Konflikte, Delete-Sperren fuer `AlreadyBooked`/`Announced` und Create-Pflichtfelder.
- Persistenz erfolgt im kombinierten Pfad innerhalb einer Transaktion.
- Lokaler QuickEdit-Zustand fuer Pending Deletes, neue Zeilen und Placeholder ist vorhanden.
- `CollectChangedRows()` ignoriert geloeschte, neue und Placeholder-Zeilen.
- `CollectQuickEditSaveRequest()` baut Updates, Deletes und Creates gemeinsam.
- `SaveQuickEditAsync` nutzt Gesamtvalidierung, mappt `EntryId`/`ClientId`-Fehler und laedt den Draft nach erfolgreichem Save neu.
- Die Tabelle rendert im QuickEdit-Modus die sichtbare QuickEdit-Liste und bietet eine Delete-Aktion fuer loeschbare Zeilen.

## Gesamtbewertung

Nicht vollstaendig plan-konform. Die Implementierung deckt den Hauptnutzen ab, sollte aber vor Abschluss mindestens bei der Create-Datumsserialisierung und der fehlenden Testabdeckung nachgebessert werden.
