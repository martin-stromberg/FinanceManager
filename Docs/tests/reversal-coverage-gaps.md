# Testlücken: Posting-Stornierung (Reversal)

Branch: `140-buchung-rückgängig-machen`  
Stand: 2025-06  
Analysiert: `PostingReversalService`, `PostingsController`, `PostingsCardViewModel`, `BasePostingsListViewModel`, `Posting`-Domain, DTOs

---

## Zusammenfassung

Die Service-Kernlogik (`PostingReversalService`) ist mit 9 Unit-Tests für die wichtigsten Happy- und Error-Paths gut abgedeckt. Jedoch fehlen Tests für:

- **Domain-Methoden** (`SetReversedBy`, `SetReversalFor` und ihre Guard-Klauseln)
- **Service-Detailverhalten** (StatementImport-Erstellung, Aggregat-Aufruf, Quantity-Negation, Timestamp-Setzung, Gruppen-GroupId der Stornos)
- **`CanReverseAsync`-Fehlerpfade** (direkte Unit-Tests für alle vier Fehlerfälle fehlen)
- **API-Integrationstests** (beide Endpoints `POST /reverse` und `GET /validate-reversal` vollständig ohne Tests)
- **ViewModel-Verhalten** (Ribbon-Deaktivierungslogik, Navigation nach Stornierung, Fehlerbehandlung)
- **Listen-UI** (Storno-Spalte in `BasePostingsListViewModel`)
- **Backup-Roundtrip** der Reversal-Felder

Insgesamt wurden **36 Testlücken** identifiziert, davon **13 mit hoher Priorität**.

---

## Lückentabelle

| ID | Bereich | Beschreibung | Priorität | Testtyp |
|----|---------|-------------|-----------|---------|
| L01 | API | `POST /api/postings/{id}/reverse` – Happy Path (200 OK) | Hoch | Integration |
| L02 | API | `POST /api/postings/{id}/reverse` – bereits storniert (409 Conflict) | Hoch | Integration |
| L03 | API | `POST /api/postings/{id}/reverse` – fremde Buchung (403 Forbidden) | Hoch | Integration |
| L04 | API | `POST /api/postings/{id}/reverse` – nicht gefunden / ungültig (400 Bad Request) | Hoch | Integration |
| L05 | API | `GET /api/postings/{id}/validate-reversal` – Happy Path | Mittel | Integration |
| L06 | API | `POST /api/postings/{id}/reverse` – nicht eingeloggt (401 Unauthorized) | Mittel | Integration |
| L07 | Service | `CanReverseAsync` – Buchung nicht gefunden → `IsValid=false` | Mittel | Unit |
| L08 | Service | `CanReverseAsync` – Benutzer ist nicht Eigentümer → `IsValid=false` | Mittel | Unit |
| L09 | Service | `CanReverseAsync` – Buchung bereits storniert → `IsValid=false` | Mittel | Unit |
| L10 | Service | `CanReverseAsync` – Buchung ist selbst eine Stornobuchung → `IsValid=false` | Mittel | Unit |
| L11 | Service | `CanReverseAsync` – Gruppe teilweise storniert → `IsValid=false` | Mittel | Unit |
| L12 | Service | `ReversePostingAsync` – `StatementImport` mit `ImportFormat.Reversal` wird erstellt | Hoch | Unit |
| L13 | Service | `ReversePostingAsync` – `StatementEntry` mit negiertem Betrag wird erstellt | Hoch | Unit |
| L14 | Service | `ReversePostingAsync` – Aggregat-Service wird für jede Stornobuchung aufgerufen | Mittel | Unit |
| L15 | Service | `ReversePostingAsync` – alle Stornobuchungen einer Gruppe teilen dieselbe neue GroupId | Mittel | Unit |
| L16 | Service | `ReversePostingAsync` – `ReversedAtUtc` wird auf UTC-Zeitstempel gesetzt | Niedrig | Unit |
| L17 | Service | `ReversePostingAsync` – Buchung ohne `AccountId` → wirft `InvalidOperationException` | Hoch | Unit |
| L18 | Service | `ReversePostingAsync` – Storno-Subject bei `null`-Subject ist `"REVERSAL"` (ohne Doppelpunkt) | Niedrig | Unit |
| L19 | Service | `ReversePostingAsync` – Quantity wird für Security-Buchungen negiert | Mittel | Unit |
| L20 | Service | `GetRelatedPostingsAsync` – nicht existente PostingId → leere Liste | Niedrig | Unit |
| L21 | Service | `GetRelatedPostingsAsync` – Buchung ohne GroupId (Guid.Empty) → verhaltensüberprüfung | Hoch | Unit |
| L22 | Domain | `Posting.SetReversedBy` – `reversalPosting == null` → `ArgumentNullException` | Mittel | Unit |
| L23 | Domain | `Posting.SetReversedBy` – bereits storniert → `InvalidOperationException` | Mittel | Unit |
| L24 | Domain | `Posting.SetReversalFor` – `originalPosting == null` → `ArgumentNullException` | Mittel | Unit |
| L25 | Domain | `Posting.SetReversalFor` – bereits als Storno markiert → `InvalidOperationException` | Mittel | Unit |
| L26 | Domain | `Posting.IsReversed` – berechnet korrekt aus `ReversedByPostingId` | Niedrig | Unit |
| L27 | Domain | `Posting.IsReversal` – berechnet korrekt aus `ReversalForPostingId` | Niedrig | Unit |
| L28 | UI/ViewModel | Ribbon-Schaltfläche „Stornieren" ist deaktiviert wenn `IsReversed = true` | Hoch | Unit |
| L29 | UI/ViewModel | Ribbon-Schaltfläche „Stornieren" ist deaktiviert wenn `IsReversal = true` | Hoch | Unit |
| L30 | UI/ViewModel | `ReverseAsync` – navigiert zur neu erstellten Stornobuchung bei Erfolg | Hoch | Unit |
| L31 | UI/ViewModel | `ReverseAsync` – setzt Fehlerstatus wenn API `null` zurückgibt (Fehlercode) | Mittel | Unit |
| L32 | UI/ViewModel | `ReverseAsync` – Exception-Pfad setzt Fehlerstatus | Mittel | Unit |
| L33 | UI/Liste | Storno-Spalte zeigt `"✓"` wenn `IsReversal = true` | Mittel | Unit |
| L34 | UI/Liste | Storno-Spalte zeigt `"—"` wenn `IsReversed = true` | Mittel | Unit |
| L35 | UI/Liste | Storno-Spalte ist leer für normale (nicht stornierte) Buchungen | Niedrig | Unit |
| L36 | Backup | Backup-DTO (`PostingBackupDto`) enthält alle Reversal-Felder im Roundtrip korrekt | Mittel | Unit |

---

## Lücken im Detail

### L01 – API: POST /reverse – Happy Path (200 OK)
**Was soll getestet werden:**  
Ein eingeloggter Benutzer sendet `POST /api/postings/{id}/reverse` für eine eigene, stornierbare Buchung.

**Erwartetes Verhalten:**  
- HTTP 200 OK
- Response-Body ist ein `ReversalResultDto` mit gültigen `ReversedPostingIds`, `CreatedReversalIds` und `StatementImportId`
- Stornobuchung ist anschließend in der Datenbank abrufbar

---

### L02 – API: POST /reverse – Bereits storniert (409 Conflict)
**Was soll getestet werden:**  
`POST /api/postings/{id}/reverse` für eine Buchung, die bereits storniert wurde.

**Erwartetes Verhalten:**  
- HTTP 409 Conflict
- ProblemDetails mit `title: "Conflict"` und aussagekräftigem `detail`

---

### L03 – API: POST /reverse – Fremde Buchung (403 Forbidden)
**Was soll getestet werden:**  
`POST /api/postings/{id}/reverse` für eine Buchung, die einem anderen Benutzer gehört.

**Erwartetes Verhalten:**  
- HTTP 403 Forbidden
- ProblemDetails mit `title: "Forbidden"`

---

### L04 – API: POST /reverse – Nicht gefunden / ungültig (400 Bad Request)
**Was soll getestet werden:**  
`POST /api/postings/{id}/reverse` mit einer nicht existierenden PostingId oder für eine Stornobuchung (IsReversal).

**Erwartetes Verhalten:**  
- HTTP 400 Bad Request
- ProblemDetails mit `title: "Bad Request"` und Fehlerbeschreibung

---

### L05 – API: GET /validate-reversal – Happy Path
**Was soll getestet werden:**  
`GET /api/postings/{id}/validate-reversal` für eine eigene, stornierbare Buchung.

**Erwartetes Verhalten:**  
- HTTP 200 OK
- `ReversalValidationDto` mit `IsValid = true` und leerer Fehlerliste

---

### L06 – API: POST /reverse – Nicht eingeloggt (401)
**Was soll getestet werden:**  
`POST /api/postings/{id}/reverse` ohne Authentifizierungs-Cookie / Token.

**Erwartetes Verhalten:**  
- HTTP 401 Unauthorized

---

### L07 – Service: CanReverseAsync – Buchung nicht gefunden
**Was soll getestet werden:**  
`CanReverseAsync` mit einer PostingId, die nicht in der Datenbank existiert.

**Erwartetes Verhalten:**  
- `ReversalValidationDto.IsValid = false`
- `Errors` enthält Meldung mit der PostingId

---

### L08 – Service: CanReverseAsync – Benutzer nicht Eigentümer
**Was soll getestet werden:**  
`CanReverseAsync` mit einer PostingId, die einem anderen Benutzer gehört.

**Erwartetes Verhalten:**  
- `IsValid = false`
- `Errors` enthält Meldung mit der UserId

---

### L09 – Service: CanReverseAsync – Bereits storniert
**Was soll getestet werden:**  
`CanReverseAsync` für eine Buchung, bei der `ReversedByPostingId` bereits gesetzt ist.

**Erwartetes Verhalten:**  
- `IsValid = false`
- `Errors` enthält `"already been reversed"`

---

### L10 – Service: CanReverseAsync – Buchung ist Stornobuchung
**Was soll getestet werden:**  
`CanReverseAsync` für eine Buchung mit gesetztem `ReversalForPostingId`.

**Erwartetes Verhalten:**  
- `IsValid = false`
- `Errors` enthält Hinweis auf `"reversal"`

---

### L11 – Service: CanReverseAsync – Gruppe teilweise storniert
**Was soll getestet werden:**  
`CanReverseAsync` für eine Buchung, deren Gruppe bereits eine teilweise stornierte Buchung enthält.

**Erwartetes Verhalten:**  
- `IsValid = false`
- `Errors` enthält `"partially reversed"`

---

### L12 – Service: StatementImport mit ImportFormat.Reversal
**Was soll getestet werden:**  
Nach `ReversePostingAsync` wird exakt ein `StatementImport` mit `Format = ImportFormat.Reversal` in der Datenbank angelegt, dessen `OriginalFileName` das Präfix `"REVERSAL_"` trägt.

**Erwartetes Verhalten:**  
- `result.StatementImportId` referenziert ein vorhandenes `StatementImport`-Objekt
- `statementImport.Format == ImportFormat.Reversal`
- `statementImport.OriginalFileName` enthält die originale PostingId

---

### L13 – Service: StatementEntry mit negiertem Betrag
**Was soll getestet werden:**  
Nach `ReversePostingAsync` existiert ein `StatementEntry` mit negiertem Betrag, der dem `StatementImport` (L12) zugeordnet ist.

**Erwartetes Verhalten:**  
- `statementEntry.Amount == -original.Amount`
- `statementEntry.Subject` beginnt mit `"REVERSAL:"`

---

### L14 – Service: Aggregat-Service aufgerufen
**Was soll getestet werden:**  
`_aggregateService.UpsertForPostingAsync` wird für jede erstellte Stornobuchung aufgerufen (genau N mal bei N Stornobuchungen).

**Erwartetes Verhalten:**  
- Mock-Verify: `UpsertForPostingAsync` wurde genau einmal pro Stornobuchung aufgerufen

---

### L15 – Service: Alle Stornobuchungen teilen neue GroupId
**Was soll getestet werden:**  
Bei Gruppen-Stornierung (mehrere Buchungen mit gleicher GroupId) teilen alle erzeugten Stornobuchungen eine einzige neue `GroupId`, die sich von der originalen GroupId unterscheidet.

**Erwartetes Verhalten:**  
- `reversal1.GroupId == reversal2.GroupId`
- `reversal1.GroupId != original1.GroupId`

---

### L16 – Service: ReversedAtUtc gesetzt
**Was soll getestet werden:**  
Nach `ReversePostingAsync` ist `ReversedAtUtc` der ursprünglichen Buchung ein UTC-Zeitstempel (nicht null).

**Erwartetes Verhalten:**  
- `updated.ReversedAtUtc.HasValue == true`
- `updated.ReversedAtUtc.Value` liegt nahe am Ausführungszeitpunkt

---

### L17 – Service: Buchung ohne AccountId
**Was soll getestet werden:**  
`ReversePostingAsync` für eine Buchung, deren `AccountId == null` ist (z. B. Kontakt-Buchung ohne Kontoverknüpfung). Die `GetPostingOwnerUserIdAsync`-Methode wirft in diesem Fall.

**Erwartetes Verhalten:**  
- `InvalidOperationException` mit Hinweis auf fehlende Account-Referenz

---

### L18 – Service: Storno-Subject bei null-Subject
**Was soll getestet werden:**  
`CreateReversalPosting` für eine Buchung mit `Subject == null` → erzeugtes Storno-Subject ist `"REVERSAL"` (ohne Doppelpunkt und Leerzeichen).

**Erwartetes Verhalten:**  
- `reversal.Subject == "REVERSAL"`

---

### L19 – Service: Quantity-Negation bei Security-Buchungen
**Was soll getestet werden:**  
`CreateReversalPosting` für eine Security-Buchung mit gesetzter `Quantity` → die Stornobuchung hat `Quantity == -original.Quantity`.

**Erwartetes Verhalten:**  
- `reversal.Quantity == -original.Quantity`

---

### L20 – Service: GetRelatedPostingsAsync – nicht existente PostingId
**Was soll getestet werden:**  
`GetRelatedPostingsAsync` mit einer PostingId, die nicht in der Datenbank existiert.

**Erwartetes Verhalten:**  
- Gibt leere Liste zurück (kein Exception)

---

### L21 – Service: GetRelatedPostingsAsync – Buchung ohne GroupId
**Was soll getestet werden:**  
`GetRelatedPostingsAsync` für eine Buchung mit `GroupId == Guid.Empty`. Da alle Buchungen ohne explizit gesetzte Gruppe auch `Guid.Empty` haben könnten, wäre die Abfrage `p.GroupId == Guid.Empty` unerwünscht weit.

**Erwartetes Verhalten:**  
- Entweder: leere Liste zurückgegeben (defensives Verhalten)
- Oder: dokumentiertes/kontrolliertes Verhalten für diesen Edge-Case

---

### L22 – Domain: SetReversedBy mit null
**Was soll getestet werden:**  
`posting.SetReversedBy(null, userId)` wirft `ArgumentNullException`.

**Erwartetes Verhalten:**  
- `ArgumentNullException` mit `paramName == "reversalPosting"`

---

### L23 – Domain: SetReversedBy – bereits storniert
**Was soll getestet werden:**  
`posting.SetReversedBy(reversal, userId)` wenn `ReversedByPostingId` bereits gesetzt ist.

**Erwartetes Verhalten:**  
- `InvalidOperationException` mit Hinweis auf die Posting-ID und die bestehende Reversal-ID

---

### L24 – Domain: SetReversalFor mit null
**Was soll getestet werden:**  
`posting.SetReversalFor(null)` wirft `ArgumentNullException`.

**Erwartetes Verhalten:**  
- `ArgumentNullException` mit `paramName == "originalPosting"`

---

### L25 – Domain: SetReversalFor – bereits als Storno markiert
**Was soll getestet werden:**  
`posting.SetReversalFor(original)` wenn `ReversalForPostingId` bereits gesetzt ist.

**Erwartetes Verhalten:**  
- `InvalidOperationException` mit Hinweis auf die bereits gesetzte Reversal-Referenz

---

### L26 – Domain: IsReversed-Eigenschaft
**Was soll getestet werden:**  
`posting.IsReversed` ist `false` initial und `true` nach `SetReversedBy(...)`.

**Erwartetes Verhalten:**  
- `new Posting(...).IsReversed == false`
- Nach `SetReversedBy`: `posting.IsReversed == true`

---

### L27 – Domain: IsReversal-Eigenschaft
**Was soll getestet werden:**  
`posting.IsReversal` ist `false` initial und `true` nach `SetReversalFor(...)`.

**Erwartetes Verhalten:**  
- `new Posting(...).IsReversal == false`
- Nach `SetReversalFor`: `posting.IsReversal == true`

---

### L28 – ViewModel: Ribbon-Schaltfläche deaktiviert wenn IsReversed
**Was soll getestet werden:**  
`PostingsCardViewModel` mit einer Buchung, bei der `IsReversed = true`: Die Ribbon-Aktion `"Reverse"` ist als `IsDisabled = true` konfiguriert.

**Erwartetes Verhalten:**  
- `ribbonActions.Single(a => a.Id == "Reverse").IsDisabled == true`

---

### L29 – ViewModel: Ribbon-Schaltfläche deaktiviert wenn IsReversal
**Was soll getestet werden:**  
`PostingsCardViewModel` mit einer Buchung, bei der `IsReversal = true`: Die Ribbon-Aktion `"Reverse"` ist deaktiviert.

**Erwartetes Verhalten:**  
- `ribbonActions.Single(a => a.Id == "Reverse").IsDisabled == true`

---

### L30 – ViewModel: ReverseAsync navigiert zur Stornobuchung
**Was soll getestet werden:**  
`PostingsCardViewModel.ReverseAsync()` (indirekt via Ribbon-Action-Callback): Wenn der API-Call erfolgreich ist und `CreatedReversalIds` eine gültige ID enthält, wird `UiActionRequested("NavigateToPosting", reversalId)` ausgelöst.

**Erwartetes Verhalten:**  
- `UiActionRequested`-Event mit Name `"NavigateToPosting"` und der Storno-ID als Parameter

---

### L31 – ViewModel: ReverseAsync setzt Fehler bei null-Ergebnis
**Was soll getestet werden:**  
`ReverseAsync()` wenn `api.Postings_ReverseAsync` `null` zurückgibt (API-Fehler mit LastErrorCode).

**Erwartetes Verhalten:**  
- `ViewModel.Error` ist gesetzt (nicht null)
- Keine Navigation erfolgt

---

### L32 – ViewModel: ReverseAsync Exception-Pfad
**Was soll getestet werden:**  
`ReverseAsync()` wenn `api.Postings_ReverseAsync` eine Exception wirft.

**Erwartetes Verhalten:**  
- `ViewModel.Error` ist gesetzt mit der Exception-Message
- `Loading = false` nach dem Aufruf

---

### L33 – UI/Liste: Storno-Spalte zeigt "✓" für IsReversal
**Was soll getestet werden:**  
`BasePostingsListViewModel.BuildRecords()` für eine Buchung mit `IsReversal = true`.

**Erwartetes Verhalten:**  
- Die Zelle in der Storno-Spalte enthält den Text `"✓"`

---

### L34 – UI/Liste: Storno-Spalte zeigt "—" für IsReversed
**Was soll getestet werden:**  
`BasePostingsListViewModel.BuildRecords()` für eine Buchung mit `IsReversed = true` und `IsReversal = false`.

**Erwartetes Verhalten:**  
- Die Zelle in der Storno-Spalte enthält den Text `"—"`

---

### L35 – UI/Liste: Storno-Spalte leer für normale Buchungen
**Was soll getestet werden:**  
`BasePostingsListViewModel.BuildRecords()` für eine Buchung mit `IsReversed = false` und `IsReversal = false`.

**Erwartetes Verhalten:**  
- Die Zelle in der Storno-Spalte enthält `string.Empty`

---

### L36 – Backup: Reversal-Felder im PostingBackupDto
**Was soll getestet werden:**  
`posting.ToBackupDto()` für eine stornierte Buchung enthält alle vier Reversal-Felder (`ReversedByPostingId`, `ReversalForPostingId`, `ReversedByUserId`, `ReversedAtUtc`). Wird dieses DTO als Restore-Payload verwendet, werden dieselben Werte wiederhergestellt.

**Erwartetes Verhalten:**  
- `dto.ReversedByPostingId == posting.ReversedByPostingId`
- `dto.ReversalForPostingId == posting.ReversalForPostingId`
- `dto.ReversedByUserId == posting.ReversedByUserId`
- `dto.ReversedAtUtc == posting.ReversedAtUtc`
