# Schnellbearbeitungsmodus für EmbeddedList (Statement Draft Entries)

Stand: 2026-04-01

## 1. Überblick und Projektkontext
Kurzbeschreibung:
Ein Schnellbearbeitungsmodus (inline-edit / table-edit) für eingebettete Listen auf der Detailseite von Kontoauszügen (StatementDraft Card). Der Modus wird per Ribbon aktiviert/deaktiviert. In diesem Modus werden Einträge als editierbare Tabellenzeilen dargestellt, bestimmte Felder sind editierbar, andere nicht. Änderungen können pro Zeile zurückgesetzt werden. Beim Speichern werden Validierungsergebnisse angezeigt und nur bei vollständiger Freigabe alle Änderungen angewendet.

Geschäftsziele:
- Schnelleres Massenbearbeiten kleiner Änderungen (z. B. Empfänger, Beschreibung, Kontozuweisung).
- Reduzierter Klickaufwand gegenüber Einzel-Edit-Dialogen.
- Konsistente Validierung und atomare Speicherung aller Änderungen.

Stakeholder:
- Buchhaltung/Operative Anwender
- Produktmanagement (Import-Workflows)
- Frontend-Team (Blazor)
- Backend/API-Team

Abgrenzung:
- Fokus: EmbeddedList im `StatementDraftCardViewModel` / `StatementDraftEntriesListViewModel`.
- Nicht: globale List-Editor-Component für alle Listen (aber API-Design soll generisch genug sein).

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|----------:|----------:|--------|
| **FR-1** | **Schnellbearbeitungsmodus aktivieren/deaktivieren:** Ribbon-Aktion `QuickEdit` schaltet Inline-Edit für die EmbeddedList ein/aus. → UI: Ribbon Toggle sichtbar auf StatementDraft Card. | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **FR-2** | **Editable-Felder definierbar:** `BaseListViewModel` stellt virtuelle Property `EditableFields` (IReadOnlyList<string>) und `IsRowEditable(item)` Methode bereit. `StatementDraftEntriesListViewModel` implementiert und gibt per Eintrag die erlaubten Felder zurück. → [Detaildoku](./quick-edit-embeddedlist.md) | UX / Maintainability | MUST HAVE | 📋 Geplant |
| **FR-3** | **Inline-Rendering:** EmbeddedList wechselt im QuickEdit zu einer tabellarischen Ansicht mit Input-Komponenten (Text, Date, Currency, Lookup) pro editierbarem Feld; nicht-editierbare Felder als Text. | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **FR-4** | **Per-Zeile Reset:** Jede Zeile hat `Reset`-Button, der Änderungen in dieser Zeile auf Originalwerte zurücksetzt (Client-seitig). | UX / Usability | MUST HAVE | 📋 Geplant |
| **FR-5** | **Client-seitige Validierung:** Validierungslogik existiert (`ValidateRow`, `ValidateAllChangedRows`); **wird aber aktuell nicht in der UI zur inline-Fehleranzeige genutzt**. Server-seitige Validierung bei Save erfolgt. | UX / Performance | HIGH | ⚠️ Teilweise implementiert |
| **FR-6** | **Server-seitige Validierung & atomare Save:** Ribbon-Aktion `SaveQuickEdit` sendet alle geänderten Zeilen als Batch zur API. API validiert; bei Fehlern: keine Änderungen werden übernommen, und die Fehler pro Zeile werden an UI zurückgegeben und angezeigt. → API: POST `/api/statement-drafts/{id}/entries/batch-update` | Data / Reliability | MUST HAVE | ✅ Implementiert |
| **FR-7** | **Transactional Commit:** Wenn API alle Einträge validiert, werden Änderungen atomar (DB-Transaction) auf die jeweiligen Draft-Entries angewendet; Rückmeldung `200 OK` mit neuem Draft-Snapshot. | Data / Reliability | MUST HAVE | ✅ Implementiert |
| **FR-8** | **UI Undo-Abfrage optional:** Nach erfolgreichem Save optionaler Hinweis mit „Rückgängig“ Link (falls Audit/Versioning vorhanden). | UX | MEDIUM | 📋 Geplant |
| **FR-9** | **Kein Symbol-Upload im QuickEdit:** Symbole/Anhänge sind nicht per Inline-Edit änderbar (sachlich nicht vorgesehen). | UX | LOW | ✅ Umgesetzt |

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|--------:|--------------|----------:|----------:|--------|
| **NFR-1** | **Responsiveness:** QuickEdit-Table reagiert < 100ms auf Eingaben in Standard-Hardware; Server-Roundtrip asynchron mit Spinner. | Performance | HIGH | 📋 Geplant |
| **NFR-2** | **Zugriffsprüfung:** Only authorised users can enable QuickEdit and save. Server prüfen Ownership/Permission. | Security | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Accessibility:** Inputs müssen keyboard-navigable und ARIA-konform sein; Ribbon Toggle mit aria-pressed. **AKTUELL: Input-IDs vorhanden für JS-Focus; ARIA-Attribute (aria-label, aria-invalid, aria-describedby) noch zu ergänzen.** | UX / Accessibility | HIGH | ⚠️ Teilweise implementiert |
| **NFR-4** | **Atomicität & Consistency:** Batch-Save muss DB-Transaktion verwenden; idempotent retry möglich. | Reliability | MUST HAVE | 📋 Geplant |
| **NFR-5** | **Logging:** Save attempts (user, draft, changedCount, success/fail) werden als Information geloggt. Keine sensiblen Daten im Log. | Observability | MEDIUM | 📋 Geplant |

## 4. Akzeptanzkriterien (User Stories)

- User Story 1 (Enable QuickEdit):
  - Als Buchhalter möchte ich den Schnellbearbeitungsmodus per Ribbon aktivieren, damit die Einträge inline editiert werden können.
  - AC: Ribbon zeigt `Quick Edit` Toggle; nach Aktivierung wechselt EmbeddedList zu Tabelle innerhalb 1s; Esc schaltet Modus aus; Fokus auf erste editierbare Zelle.

- User Story 2 (Edit & Reset per row):
  - Als Anwender kann ich Zellen einer Zeile bearbeiten und mit `Reset` diese Zeile auf Ursprungswerte zurücksetzen.
  - AC: Nach klicken `Reset` sind alle lokal veränderten Felder dieser Zeile wieder wie vor Edit; UI zeigt keine Validierungsfehler mehr.

- User Story 3 (Client Validation):
  - Als Anwender erhalte ich unmittelbar sichtbare Fehlermeldungen für ungültige Eingaben (z. B. Datum falsch, Betrag nicht Zahl).
  - AC: Client verhindert Senden wenn lokale Validierungsfehler bestehen; Save-Button disabled; Fehlermeldungen je Feld sichtbar.

- User Story 4 (Atomic Save + Server Validation):
  - Als Anwender kann ich alle Änderungen speichern; nur wenn alle Zeilen serverseitig validiert sind, werden die Änderungen angewendet.
  - AC: API gibt pro Zeile Validierungsfehler zurück; UI zeigt Fehler; keine Änderungen werden in DB geschrieben bei Fehlern; bei Erfolg UI zeigt Erfolg und listet aktualisierten Werte.

- User Story 5 (Permissions & Logging):
  - Nur berechtigte Nutzer sehen und können QuickEdit ausführen; Save-Versuche werden geloggt.
  - AC: Unberechtigte Nutzer sehen Toggle disabled; Server verweigert Save mit 403; Log-Eintrag mit userId & draftId existiert (Info Level).

## 5. Annahmen und Abhängigkeiten

| Annahme / Abhängigkeit | Beschreibung |
|------------------------|--------------|
| API Erweiterung | Backend-API erweitert um Batch-Update Endpoint `/entries/batch-update` mit Validierung und Transactional commit. |
| Model-Felder | DTO `StatementDraftEntryDto` enthält alle zu ändernden Felder (BookingDate, Subject, RecipientName, Amount, Status, etc.). |
| Permissions | Security/Ownership-Prüfung bereits via vorhandene Auth-Mechanismen möglich (CurrentUser). |
| Frontend-Grid | Reuse vorhandener List-Components / ListCell-Komponenten für Inputs; ggf. neue Input-Cell-Komponenten erstellen. |

## 6. Scope und Out-of-Scope

In-Scope ✅
- QuickEdit Toggle in Ribbon
- BaseListViewModel: virtuelle APIs (EditableFields, IsRowEditable, BeginQuickEdit, EndQuickEdit, ValidateRow, CollectChanges)
- StatementDraftEntriesListViewModel: konkrete Umsetzung der Felder, Lookups, initial values
- Client UI: Editable table rendering, per-row Reset, batch Save, per-field error display
- Backend: Batch update API with transactional commit and per-row validation response
- Unit- & Integration-Tests for view model + API

Out-of-Scope ❌
- Inline Attachment / Symbol upload
- Full generic Grid component for other domains (feature must be applicable but not mandatory now)

## 7. Domänenmodell & Glossar

- StatementDraft: Draft container for uploaded statement file
- StatementDraftEntry: Single imported line / movement; editable fields listed below
- QuickEdit Session: ephemeral client-side state that contains original values + edited values for rows
- BatchUpdateRequest: DTO carrying list of { EntryId, EditedFields }
- BatchUpdateResult: { Success: bool, Errors: [{ EntryId, Field, Message }] }

### 📋 Editable Fields (AKTUALISIERT APRIL 2026)

**IMPLEMENTIERTE Felder:**
```csharp
new[] 
{ 
    "BookingDate",           // Datum der Buchung (erforderlich)
    "ValutaDate",            // Wertstellungsdatum (optional)
    "Amount",                // Betrag (erforderlich, decimal)
    "BookingDescription",    // Buchungsbeschreibung 
    "RecipientName",         // Name Empfänger/Absender
    "Subject"                // Verwendungszweck/Betreff
}
```

**Nicht editierbar:**
- Status: Kann nur durch "Reset Duplicate" geändert werden (von AlreadyBooked → Open)
- Amount: Für Bank-Posten schreibgeschützt

**Merke:**
- `EditableFields` arbeitet mit Feld-Keys (z. B. `BookingDate`, `Subject`, `RecipientName`). Mapping ist auf ListViewModel‑Seite zu implementieren.
- Zeilen mit Status `AlreadyBooked` sind nicht editierbar (Read-Only)

## 8. Nutzungsfälle (Use Cases)

Use Case UC-01: Benutzer aktiviert QuickEdit
- Pre: Draft geöffnet, Benutzer berechtigt
- Steps:
  1. Benutzer klickt `Quick Edit` im Ribbon
  2. ViewModel ruft `BeginQuickEdit()` auf (BaseListViewModel)
  3. EmbeddedList rendert Tabelle; Setzt Fokus
- Post: Liste im Edit-Modus

Use Case UC-02: Benutzer bearbeitet mehrere Zeilen und speichert
- Steps:
  1. Benutzer ändert Felder in mehreren Zeilen
  2. UI speichert lokal geänderte Werte; Save-Button wird aktiv
  3. Benutzer klickt `Save` im Ribbon
  4. Client führt `ValidateRow` lokal für alle geänderten Zeilen durch
  5. Client sendet BatchUpdateRequest an API
  6. API validiert jede Zeile; falls Fehler: return 400 mit per-row errors
  7a. Bei Fehler: Client zeigt Fehler Inline; keine DB-Änderung
  7b. Bei Erfolg: API commit; Client aktualisiert Einträge, verlässt optional QuickEdit

Use Case UC-03: Row Reset
- Steps:
  1. Benutzer klickt `Reset` in Zeile
  2. Client ersetzt edit-state mit original Werten
  3. UI entfernt inline-Fehler für diese Zeile

## 10. Implementation Status (APRIL 2026 UPDATE)

### ✅ VOLLSTÄNDIG IMPLEMENTIERT:
- **FR-1**: Ribbon QuickEdit Toggle (ToggleQuickEdit Action in StatementDraftCardViewModel)
- **FR-2**: Editable Fields Definition (EditableFields, IsRowEditable in BaseListViewModel & StatementDraftEntriesListViewModel)
- **FR-3**: Inline-Table Rendering (QuickEditTable.razor mit Input-Komponenten)
- **FR-4**: Per-Row Reset Button (ResetRow Methode)
- **FR-6**: API Batch-Update (SaveQuickEditAsync in StatementDraftCardViewModel + StatementDraftService.BatchUpdateDetailedAsync)
- **FR-7**: Transactional Commit (via API mit DB-Transaktionen)
- **FR-9**: Kein Symbol-Upload im QuickEdit

### ⚠️ TEILWEISE IMPLEMENTIERT:
- **FR-5 Client-Validierung**: 
  - ✅ ValidateRow() & ValidateAllChangedRows() existieren
  - ❌ Werden **nicht in der UI aufgerufen** zur inline-Fehleranzeige
  - ✅ Server validiert bei Save und gibt per-row Fehler zurück
  - **Empfehlung**: Client-Validierung im UI aktivieren für bessere UX

- **NFR-3 Accessibility**:
  - ✅ Input-IDs vorhanden (qe_booking_{id}, etc.)
  - ❌ Fehlen: aria-label, aria-invalid, aria-describedby
  - **Empfehlung**: ARIA-Attribute ergänzen

### ⏳ NICHT IMPLEMENTIERT:
- **FR-8**: "Rückgängig" Link nach erfolgreicher Speicherung (optional feature)

---

## 11. Approval & Versionierung
- Owner: Produktmanagement / Team Lead
- Implementer: Frontend (Blazor) + Backend
- Version: 0.2 (Updated April 2026 - Implementation Status documented)
- Last Updated: 11.04.2026
