# FA-POST-001: Posting-Stornierung (Reversal)

> **Bezug im Anforderungskatalog:** FA-AUSZ-020 (neu)  
> **Status:** 📋 Geplant  
> **Version:** 0.1  
> **Datum:** 2025-05-08  
> **Autor:** GitHub Copilot

---

## 1. Überblick und Projektkontext

### 1.1 Projektbeschreibung

Der FinanceManager ist eine Blazor-Server-Anwendung zur persönlichen Finanzverwaltung. Benutzer können Kontoauszüge importieren, Buchungen erfassen und Posten (Postings) verschiedener Art verwalten: Bankkonto-, Kontakt-, Wertpapier- und Sparplanposten.

Dieses Dokument beschreibt das neue Feature **Posting-Stornierung (Reversal)**, das es einem Benutzer ermöglicht, einen bereits gebuchten Posten rückgängig zu machen, indem automatisch eine Gegenbuchung mit umgekehrtem Vorzeichen erstellt wird.

### 1.2 Geschäftsziele

| # | Ziel |
|---|------|
| G-1 | Benutzer kann fehlerhafte oder versehentlich gebuchte Postings einfach und schnell rückgängig machen. |
| G-2 | Stornierungen bleiben transparent und nachvollziehbar (Originalposten und Gegenbuchung sind als Storno-Paar erkennbar). |
| G-3 | Zugehörige Posten (Kontakt-, Wertpapier-, Sparplanposten) werden automatisch mit ausgeglichen, um Dateninkonsistenzen zu vermeiden. |
| G-4 | Bereits stornierte Posten können nicht erneut storniert werden (Verhindert versehentliche Mehrfachstornierungen). |
| G-5 | Stornierungen erscheinen im Statement-Import-Prozess als separate Einträge zur korrekten Rekonziliation. |

### 1.3 Stakeholder

| Rolle | Interesse |
|-------|-----------|
| Endanwender | Einfache und zuverlässige Möglichkeit, fehlerhafte Buchungen zu korrigieren |
| Entwickler / Maintainer | Klare Service-Schnittstellen, wartbare Stornologik, testbare Validierung |
| Fachliche Review-Instanz | Sicherstellung der fachlichen Korrektheit und Nachvollziehbarkeit von Stornierungen |

### 1.4 Abgrenzung

Dieses Feature betrifft ausschließlich die **Stornierung einzelner Postings**. Massenoperationen, automatische Rückgängigmachung von Stornierungen (außer durch manuelle Neuerfassung) und nachträgliche Änderungen bereits stornierter Posten sind nicht im Scope.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Stornierung eines Postings über Action-Button:** Benutzer kann auf der Postings-Detailseite (Bankkontoposten, Kontaktposten, Wertpapierposten, Sparplanposten) über einen Action-Button im Ribbon-Menü den Posten stornieren. Das System erstellt automatisch eine Gegenbuchung mit negiertem Betrag, gleichem Datum (Booking- und Valuta-Datum identisch mit Original) und gleichen Referenzen (Account, Contact, SecurityId, SavingsPlanId). → [Postings Dokumentation](../postings.md) · [Statement Draft Booking Flow](../flows/statement-draft-booking.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-1.1** | **Validierung vor Stornierung:** Das System prüft vor der Stornierung, ob der Posten bereits storniert wurde (Feld `ReversedByPostingId` ist nicht null). Falls ja, wird die Aktion abgelehnt und eine Fehlermeldung (HTTP 409 Conflict) zurückgegeben. Der Action-Button ist im UI deaktiviert. | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-1.2** | **Zugriffskontrolle:** Nur der Benutzer, dem der Posten gehört, darf diesen stornieren. Bei unberechtigtem Zugriff wird ein Fehlercode 403 (Forbidden) zurückgegeben, und der Action-Button wird deaktiviert. | Sicherheit | MUST HAVE | 📋 Geplant |
| **FR-2** | **Automatische Gegenbuchung:** Das System erstellt einen neuen Posten mit: (a) gleichem Betrag mit umgekehrtem Vorzeichen (negiert), (b) gleichem Booking- und Valuta-Datum wie das Original, (c) gleichen Referenzen (AccountId, ContactId, SecurityId, SavingsPlanId), (d) neuem Merkmal `ReversalForPostingId` gefüllt mit der ID des Originalpostens. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-2.1** | **Markierung des Originalpostens:** Der Originalposten erhält das Merkmal `ReversedByPostingId` mit der ID der Gegenbuchung, sodass die Stornierung bidirektional nachvollziehbar ist. | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-3** | **Stornierung zugehöriger Posten:** Falls der ursprüngliche Posten mit anderen Postings verknüpft ist (via `GroupId`, `ParentId` oder direkte Referenzen), werden auch diese durch entsprechende Gegenbuchungen ausgeglichen. Das System identifiziert alle zugehörigen Posten automatisch und erstellt für jeden eine Gegenbuchung. | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-3.1** | **Erhaltung von Gruppierungen:** Die Gegenbuchungen erhalten jeweils eine neue `GroupId`, die alle Stornierungsposten eines Vorgangs gruppiert, um die Nachvollziehbarkeit zu gewährleisten. | Datenverwaltung | HIGH | 📋 Geplant |
| **FR-4** | **Statement-Import-Erstellung:** Das System erstellt einen neuen `StatementImport` mit dem Original-Eintrag (ohne Gegenbuchung), um die Stornierung im Statement-Prozess korrekt zu erfassen und zu rekonzilieren. | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-5** | **UI-Anzeige der Stornierung:** Das UI zeigt in den Postings-Listen eine neue Spalte „Storno" (Ja/Nein). Stornierte Posten und Gegenbuchungen sind als solche erkennbar und können gefiltert werden. | UX / Accessibility | HIGH | 📋 Geplant |
| **FR-6** | **Success-Notification:** Nach erfolgreicher Stornierung zeigt das System eine Success-Notification mit Hinweis auf die erstellte Gegenbuchung. Der Benutzer kann zur Gegenbuchung navigieren. | UX / Accessibility | MEDIUM | 📋 Geplant |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Transaktionale Integrität:** Die Stornierung (Erstellen der Gegenbuchung, Markierung des Originals, Stornierung zugehöriger Posten und Erstellung des StatementImport) erfolgt in **einer atomaren Transaktion**. Bei Fehler wird ein vollständiger Rollback durchgeführt, sodass keine inkonsistenten Daten entstehen. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Performanz der Stornierung:** Die Stornierung eines Postings inkl. aller zugehörigen Posten wird in **< 2 Sekunden** abgeschlossen (95. Perzentil bei typischer Gruppengröße von bis zu 10 zugehörigen Posten). | Performance | HIGH | 📋 Geplant |
| **NFR-3** | **Nachvollziehbarkeit:** Stornierte Posten und deren Gegenbuchungen sind im UI und in der Datenbank eindeutig als Storno-Paar erkennbar. In **100 %** der Fälle kann die Verknüpfung über `ReversedByPostingId` und `ReversalForPostingId` nachvollzogen werden. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-4** | **Fehlerbehandlung:** Bei ungültigen Stornierungsversuchen (bereits storniert, ungültiger Zugriff, DB-Fehler) liefert das System aussagekräftige Fehlermeldungen mit HTTP-Statuscodes (400 Bad Request, 403 Forbidden, 409 Conflict, 500 Internal Server Error). | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-5** | **Testbarkeit:** Die Stornierungslogik ist vollständig durch Unit- und Integrationstests abgedeckt, einschließlich aller Validierungen, Fehlerfälle und Transaktionsverhalten (Testabdeckung **≥ 85 %**). | Wartbarkeit | HIGH | 📋 Geplant |
| **NFR-6** | **Lokalisierung:** Alle UI-Texte (Action-Button, Notifications, Fehlermeldungen, Spaltenüberschriften) sind vollständig lokalisiert (DE/EN). | UX / Accessibility | MEDIUM | 📋 Geplant |

---

## 4. Akzeptanzkriterien

### User Story 1 – Stornierung eines Postings

**Als** Benutzer  
**möchte ich** einen fehlerhaft gebuchten Posten über einen Action-Button stornieren,  
**damit** die Buchung automatisch rückgängig gemacht wird und ich sie nicht manuell korrigieren muss.

**Akzeptanzkriterien (SMART)**
1. Der Action-Button „Stornieren" erscheint auf der Postings-Detailseite für alle noch nicht stornierten Posten.
2. Nach Klick auf den Button wird innerhalb von 2 Sekunden eine Gegenbuchung mit negiertem Betrag und gleichem Datum erstellt.
3. Der Originalposten erhält das Merkmal `ReversedByPostingId`, die Gegenbuchung das Merkmal `ReversalForPostingId`.
4. Das System zeigt eine Success-Notification mit Link zur Gegenbuchung.
5. In 100 % der Testfälle wird die Stornierung korrekt durchgeführt und ist als Storno-Paar erkennbar.

### User Story 2 – Validierung bereits stornierter Posten

**Als** Benutzer  
**möchte ich** einen bereits stornierten Posten nicht erneut stornieren können,  
**damit** keine doppelten Gegenbuchungen entstehen.

**Akzeptanzkriterien (SMART)**
1. Ist ein Posten bereits storniert (`ReversedByPostingId` ist nicht null), ist der Action-Button „Stornieren" deaktiviert.
2. Bei Versuch einer erneuten Stornierung (z. B. über API) liefert das Backend den Fehlercode 409 (Conflict) mit aussagekräftiger Fehlermeldung.
3. In 100 % der Testfälle mit bereits stornierten Posten wird die erneute Stornierung korrekt abgelehnt.

### User Story 3 – Stornierung zugehöriger Posten

**Als** Benutzer  
**möchte ich** bei der Stornierung eines Postings automatisch alle zugehörigen Posten (Kontakt, Wertpapier, Sparplan) ausgeglichen sehen,  
**damit** meine Daten konsistent bleiben.

**Akzeptanzkriterien (SMART)**
1. Für jeden zugehörigen Posten (erkennbar via `GroupId`, `ParentId` oder Referenzen) wird automatisch eine Gegenbuchung erstellt.
2. Alle Gegenbuchungen erhalten eine gemeinsame neue `GroupId`, die das Storno-Ereignis gruppiert.
3. In 95 % der Testfälle mit zugehörigen Posten werden alle korrekt identifiziert und storniert.

### User Story 4 – Statement-Import-Erstellung

**Als** Benutzer  
**möchte ich** nach einer Stornierung einen neuen Statement-Import sehen,  
**damit** die Stornierung im Statement-Prozess korrekt erfasst und rekonziliert wird.

**Akzeptanzkriterien (SMART)**
1. Nach erfolgreicher Stornierung wird ein neuer `StatementImport` mit dem Original-Eintrag (ohne Gegenbuchung) erstellt.
2. Der Statement-Import ist im System sichtbar und kann zur Rekonziliation genutzt werden.
3. In 100 % der Testfälle wird der Statement-Import korrekt erstellt.

---

## 5. Annahmen und Abhängigkeiten

| Typ | Beschreibung |
|-----|--------------|
| **Annahme** | Die Domain-Entität `Posting` wird um die Felder `ReversedByPostingId` und `ReversalForPostingId` erweitert (beide `Guid?`). |
| **Annahme** | Der Benutzerkontext (UserId) ist zur Laufzeit verfügbar und kann zur Zugriffskontrolle genutzt werden. |
| **Annahme** | Die `GroupId`, `ParentId` und `LinkedPostingId` sind konsistent gepflegt und können zur Identifikation zugehöriger Posten genutzt werden. |
| **Abhängigkeit** | Die UI-Komponenten für Postings-Detailseiten (Bankkontoposten, Kontaktposten, Wertpapierposten, Sparplanposten) müssen den Action-Button „Stornieren" im Ribbon-Menü integrieren. |
| **Abhängigkeit** | Das Statement-Import-Modul muss erweitert werden, um Stornierungen als separate Einträge zu erfassen. |
| **Abhängigkeit** | Die Posting-Aggregate müssen nach Stornierung aktualisiert werden (siehe `IPostingAggregateService`). |

---

## 6. Scope und Out-of-Scope

### In-Scope ✅

- Stornierung einzelner Postings über Action-Button im UI
- Automatische Erstellung von Gegenbuchungen mit negiertem Betrag und gleichem Datum
- Markierung von Originalposten und Gegenbuchungen als Storno-Paar
- Validierung gegen mehrfache Stornierung
- Zugriffskontrolle (nur eigener Benutzer darf stornieren)
- Automatische Stornierung zugehöriger Posten (Kontakt, Wertpapier, Sparplan)
- Erstellung eines neuen StatementImport zur Rekonziliation
- UI-Anzeige der Stornierung (Spalte „Storno" in Postings-Listen)
- Success-Notification nach erfolgreicher Stornierung

### Out-of-Scope ❌

- Massenoperation zum Stornieren mehrerer Posten gleichzeitig
- Automatische Rückgängigmachung von Stornierungen (nur manuelle Neuerfassung möglich)
- Nachträgliche Änderung von Gegenbuchungen (Gegenbuchungen sind normale Posten und können wie normale Posten bearbeitet, aber nicht erneut storniert werden)
- Erweiterter Audit-Trail für Stornierungen (Standard-Audit für Entity-Änderungen genügt)
- Stornierung von Gegenbuchungen selbst (Gegenbuchungen sind nicht stornierbar)

---

## 7. Domänenmodell und Glossar

### Schlüsselentitäten

```mermaid
erDiagram
    Posting {
        Guid Id PK
        Guid SourceId
        Guid GroupId
        PostingKind Kind
        Guid? AccountId
        Guid? ContactId
        Guid? SavingsPlanId
        Guid? SecurityId
        DateTime BookingDate
        DateTime ValutaDate
        decimal Amount
        Guid? ParentId
        Guid? LinkedPostingId
        Guid? ReversedByPostingId "NEW"
        Guid? ReversalForPostingId "NEW"
    }
    
    Posting ||--o| Posting : "reversed by"
    Posting ||--o| Posting : "reversal for"
    Posting ||--o{ Posting : "grouped via GroupId"
    Posting ||--o| Posting : "parent-child via ParentId"
```

### Beziehungen

- **ReversedByPostingId**: Zeigt vom Originalposten zur Gegenbuchung (Stornierung).
- **ReversalForPostingId**: Zeigt von der Gegenbuchung zum Originalposten.
- **GroupId**: Gruppiert Posten, die denselben realen Vorgang repräsentieren (inkl. Storno-Posten).
- **ParentId**: Verbindet Split- oder abgeleitete Posten mit ihrem Parent.

### Glossar

| Begriff | Definition |
|---------|------------|
| **Posting** | Gebuchte Transaktion (Bankkontoposten, Kontaktposten, Wertpapierposten, Sparplanposten). |
| **Stornierung (Reversal)** | Rückgängigmachung eines Postings durch Erstellung einer Gegenbuchung mit negiertem Betrag. |
| **Gegenbuchung** | Neuer Posten mit umgekehrtem Vorzeichen, der die ursprüngliche Buchung ausgleicht. |
| **Storno-Paar** | Originalposten und Gegenbuchung, die über `ReversedByPostingId` und `ReversalForPostingId` verknüpft sind. |
| **GroupId** | Identifikator, der Posten desselben realen Vorgangs gruppiert. |
| **ParentId** | Referenz auf einen übergeordneten Posten (bei Split-Postings). |
| **StatementImport** | Import eines Kontoauszugs zur Rekonziliation. |
| **Action-Button** | UI-Element im Ribbon-Menü zur Initiierung der Stornierung. |
| **Valuta-Datum** | Wertstellungsdatum einer Transaktion. |
| **SourceId** | Identifikator des Ursprungseintrags, der das Posting erzeugt hat. |

---

## 8. Nutzungsfälle (Use Cases)

### UC-1: Stornierung eines einfachen Bankpostings

**Vorbedingung:** Benutzer ist angemeldet und hat ein gebuchtes Bankposting.

**Ablauf:**
1. Benutzer navigiert zur Detailseite des Bankpostings.
2. Benutzer klickt auf den Action-Button „Stornieren" im Ribbon-Menü.
3. System validiert:
   - Posten ist noch nicht storniert (`ReversedByPostingId` ist null).
   - Benutzer ist berechtigt (Posten gehört zum angemeldeten Benutzer).
4. System erstellt Gegenbuchung:
   - Betrag mit umgekehrtem Vorzeichen.
   - Gleiches Booking- und Valuta-Datum.
   - Gleiche Referenzen (AccountId).
   - `ReversalForPostingId` zeigt auf Originalposten.
5. System markiert Originalposten:
   - `ReversedByPostingId` zeigt auf Gegenbuchung.
6. System identifiziert zugehörige Posten (z. B. Kontaktposten via `GroupId`) und erstellt für diese ebenfalls Gegenbuchungen.
7. System erstellt neuen `StatementImport` mit Original-Eintrag.
8. System aktualisiert Posting-Aggregate.
9. System commitet Transaktion.
10. System zeigt Success-Notification mit Link zur Gegenbuchung.

**Nachbedingung:** Originalposten und alle zugehörigen Posten sind storniert, Gegenbuchungen existieren, Statement-Import ist erstellt.

**Alternative Abläufe:**
- **3a: Posten bereits storniert:** System zeigt Fehler 409, Action-Button ist deaktiviert.
- **3b: Unberechtigter Zugriff:** System zeigt Fehler 403, Action-Button ist deaktiviert.
- **9a: DB-Fehler:** System führt Rollback durch, zeigt Fehler 500 mit aussagekräftiger Meldung.

---

### UC-2: Stornierung eines Wertpapierpostings mit zugehörigen Posten

**Vorbedingung:** Benutzer ist angemeldet und hat ein gebuchtes Wertpapierposting mit zugehörigem Kontaktposten und Bankposting.

**Ablauf:**
1. Benutzer navigiert zur Detailseite des Wertpapierpostings.
2. Benutzer klickt auf „Stornieren".
3. System validiert (analog UC-1).
4. System erstellt Gegenbuchung für Wertpapierposting.
5. System identifiziert zugehörige Posten via `GroupId`:
   - Kontaktposten (gleiches `GroupId`).
   - Bankposting (gleiches `GroupId`).
6. System erstellt Gegenbuchungen für alle zugehörigen Posten.
7. System markiert alle Originalposten mit `ReversedByPostingId`.
8. System gruppiert alle Gegenbuchungen mit neuer `GroupId`.
9. System erstellt StatementImport.
10. System zeigt Success-Notification.

**Nachbedingung:** Wertpapierposting, Kontaktposten und Bankposting sind storniert, alle Gegenbuchungen existieren und sind gruppiert.

---

### UC-3: Versuch, bereits stornierten Posten erneut zu stornieren

**Vorbedingung:** Benutzer ist angemeldet und hat einen bereits stornierten Posten.

**Ablauf:**
1. Benutzer navigiert zur Detailseite des stornierten Postings.
2. System erkennt, dass `ReversedByPostingId` gesetzt ist.
3. System deaktiviert Action-Button „Stornieren" im UI.
4. (Falls Benutzer versucht, über API zu stornieren): System liefert Fehler 409 (Conflict) mit Meldung: „Dieser Posten wurde bereits storniert und kann nicht erneut storniert werden."

**Nachbedingung:** Keine Änderung, Benutzer wird über bestehende Stornierung informiert.

---

## 9. Nächste Schritte

1. **Domain-Erweiterung:** Ergänzung der Felder `ReversedByPostingId` und `ReversalForPostingId` in der `Posting`-Entität.
2. **Datenbank-Migration:** Erstellen und Anwenden einer EF Core-Migration zur Erweiterung der `Postings`-Tabelle.
3. **Service-Implementierung:** Entwicklung eines `IPostingReversalService` mit Methoden für:
   - Validierung (`CanReverse`)
   - Stornierung (`ReversePostingAsync`)
   - Identifikation zugehöriger Posten (`GetRelatedPostingsAsync`)
4. **API-Endpunkt:** Erweiterung des `PostingsController` um `POST /api/postings/{id}/reverse`.
5. **UI-Integration:** Einbindung des Action-Buttons „Stornieren" in Postings-Detailseiten (Ribbon-Menü).
6. **UI-Anzeige:** Erweiterung der Postings-Listen um Spalte „Storno" (Ja/Nein) und Filteroptionen.
7. **Statement-Import-Erweiterung:** Anpassung des Statement-Import-Moduls zur Erfassung von Stornierungen.
8. **Tests:** Erstellung von Unit- und Integrationstests für alle Use Cases und Validierungen.
9. **Dokumentation:** Aktualisierung der [Postings-Dokumentation](../postings.md) und [Statement Draft Booking Flow](../flows/statement-draft-booking.md).

---

## 10. Approval & Versionierung

| Version | Datum | Autor | Änderungen |
|---------|-------|-------|------------|
| 0.1 | 2025-05-08 | GitHub Copilot | Initiale Erstellung basierend auf Feature-Anforderung |

**Status:** 📋 Geplant  
**Genehmigt durch:** (Wird nach Review ausgefüllt)  
**Genehmigungsdatum:** (Wird nach Review ausgefüllt)
