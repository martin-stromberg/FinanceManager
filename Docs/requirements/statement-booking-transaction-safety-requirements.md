# Anforderungsanalyse: Transaction-Safe Statement Booking

> **Status:** 📋 Geplant  
> **Version:** 0.2  
> **Datum:** 2026-06-06  
> **Autor:** Requirements Analysis Agent

## 1 Überblick und Projektkontext

Beim Buchen von `StatementDraft` über `BookAsync` konnte ein hängender/unklarer Request durch erneutes Auslösen doppelte `Posting`-Einträge erzeugen. Ziel ist eine transaktionssichere Buchung, die Teilpersistenz verhindert und parallele Doppelverarbeitung desselben Drafts ausschließt.

**Geschäftsziele**
- Keine Doppelbuchungen bei Retrigger derselben Buchungsaktion.
- Konsistente, atomare Verarbeitung von `StatementDraft` und `StatementDraftEntry`.
- Eindeutiges Fehler- und Retry-Verhalten für UI und API-Clients.

**Stakeholder**
- Endnutzer im Kontoauszugs-Buchungsprozess
- Produktverantwortung für Buchungslogik
- Backend-Entwicklung (Application/Infrastructure/API)
- QA/Testing

**Abgrenzung**
Im Scope liegt die transaktionssichere Buchung von Gesamt- und Einzelbuchung:
- `POST /api/statement-drafts/{draftId}/book`
- `POST /api/statement-drafts/{draftId}/entries/{entryId}/book`

Allgemeine Performance-Maßnahmen ohne Bezug zur Buchungssicherheit sind nicht Ziel dieses Features.

## 2 Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Atomare Datenbanktransaktion für BookAsync:** Jede Buchungsoperation läuft in genau **einer** DB-Transaktion. Bei technischem Fehler erfolgt vollständiger Rollback ohne persistierte Teil-`Posting`-Mengen und ohne inkonsistenten `StatementDraftStatus`. → [Architektur-Blueprint Statement Booking Transaction Safety](../architecture/architecture-blueprint-statement-booking-transaction-safety.md) · [ERM Statement Booking Transaction Safety](../architecture/entity-relationship-model-statement-booking-transaction-safety.md) · [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-1.1** | **Gültiger Statusübergang:** `StatementDraftStatus` darf beim Buchen nur `Draft -> Committed` durchlaufen; bei Fehler bleibt der Draft konsistent in `Draft`. Ein bereits `Committed` gesetzter Draft wird nicht erneut gebucht. | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-2** | **Idempotenz bei Retrigger/Replay:** Wiederholte Requests auf denselben Draft/Entry erzeugen **keine zusätzlichen Postings**. Wiederholaufrufe liefern ein deterministisches Ergebnis („bereits verarbeitet“ bzw. Replay derselben Ergebnissemantik). → [Architektur-Blueprint Statement Booking Transaction Safety](../architecture/architecture-blueprint-statement-booking-transaction-safety.md) · [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **FR-2.1** | **Duplikatschutz pro Entry:** Pro `StatementDraftEntry` ist fachlich maximal eine erfolgreiche Überführung in Posting-Gruppen zulässig, auch bei Netzwerk-Timeout, UI-Retry oder manuellem Retrigger. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-3** | **Sperr-/Locking-Strategie gegen Parallelität:** Vor Beginn der Buchung wird eine exklusive Verarbeitungssperre für `(OwnerUserId, DraftId)` erworben. Parallele Requests auf denselben Draft werden deterministisch als Konflikt behandelt und erzeugen keine zusätzlichen Postings. → [Architektur-Blueprint Statement Booking Transaction Safety](../architecture/architecture-blueprint-statement-booking-transaction-safety.md) · [ERM Statement Booking Transaction Safety](../architecture/entity-relationship-model-statement-booking-transaction-safety.md) | Sicherheit | MUST HAVE | 📋 Geplant |
| **FR-4** | **Fehler- und Retry-Verhalten:** Fachliche/validierungsbezogene Fehler werden von transienten technischen Fehlern getrennt. Nur transiente Fehler sind retry-fähig und bleiben duplikatsicher. → [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **FR-5** | **API-Fehlervertrag für Clients:** Endpunkte liefern standardisierte ProblemDetails mit maschinenlesbarem Fehlercode, Retry-Hinweis und TraceId, damit Clients deterministisch auf `Conflict`, `Transient Failure` und `Already Processed` reagieren können. | Wartbarkeit | HIGH | 📋 Geplant |

## 3 Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **ACID-Konsistenz:** `Posting`, Kontosaldo, Entry-Änderungen und `StatementDraftStatus` bleiben in Fehlerpfaden zu **100 %** konsistent (keine Teilpersistenz in Integrations-Tests). → [Architektur-Blueprint Statement Booking Transaction Safety](../architecture/architecture-blueprint-statement-booking-transaction-safety.md) · [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Konkurrenzfestigkeit:** Bei mindestens **20** parallelen Buchungsversuchen auf denselben Draft ist genau **1** Versuch erfolgreich; zusätzliche Postings = **0**. → [Architektur-Blueprint Statement Booking Transaction Safety](../architecture/architecture-blueprint-statement-booking-transaction-safety.md) · [ERM Statement Booking Transaction Safety](../architecture/entity-relationship-model-statement-booking-transaction-safety.md) | Skalierbarkeit | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Idempotenz-Nachweis:** Bei mindestens **3** identischen Replay-Requests innerhalb von **60 Sekunden** bleibt die Posting-Anzahl nach dem ersten Erfolg unverändert. → [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-4** | **Fehlertransparenz:** Jede Konflikt-/Abbruchantwort enthält in **100 %** der Fälle mindestens `code` und `traceId`; Lock- und Retry-relevante Fehler sind eindeutig unterscheidbar. | Wartbarkeit | HIGH | 📋 Geplant |
| **NFR-5** | **Latenz unter Schutzmechanismen:** P95-Laufzeit für erfolgreiche Draft-Buchung bleibt unter Normal-Last bei < **2 Sekunden** trotz Lock- und Idempotenzmechanismus. | Performance | HIGH | 📋 Geplant |
| **NFR-6** | **Retry-Sicherheit:** Automatisierte Retries bei transienten Fehlern erhöhen die Anzahl erzeugter Postings pro Entry niemals über **1**. → [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) | Sicherheit | MUST HAVE | 📋 Geplant |

## 4 Akzeptanzkriterien

### User Story US-1 – Atomare Datenbanktransaktion
**Als** Nutzer  
**möchte ich**, dass eine Draft-Buchung vollständig oder gar nicht gespeichert wird,  
**damit** keine unvollständigen Buchungsdaten entstehen.

- AC-1.1: Tritt während `BookAsync` nach bereits erzeugten `Posting` ein technischer Fehler auf, sind nach Abschluss **0** neue Postings dieser Operation in der DB.
- AC-1.2: Bei Rollback bleibt `StatementDraftStatus = Draft`; es werden keine `StatementDraftEntry` fälschlich entfernt.
- AC-1.3: Bei Erfolg sind alle Änderungen (Postings, Entry-Änderungen, Status) gemeinsam commitet.

### User Story US-2 – Idempotenz bei Retrigger (Doppelbuchungsschutz)
**Als** Nutzer  
**möchte ich** denselben Request erneut senden können,  
**damit** bei unklarer Antwort oder Timeout keine Doppelbuchung entsteht.

- AC-2.1: Zwei identische Requests auf denselben Draft erzeugen zusammen maximal **eine** neue Posting-Menge.
- AC-2.2: Ein Wiederholaufruf nach erfolgreicher Buchung erzeugt **0** neue Persistierung und liefert ein deterministisches Ergebnis.
- AC-2.3: Für `POST /api/statement-drafts/{draftId}/entries/{entryId}/book` gilt derselbe Schutz auf Entry-Ebene.

### User Story US-3 – Sperr-/Locking-Strategie gegen parallele Verarbeitung
**Als** Systembetreiber  
**möchte ich** parallele Verarbeitung desselben Drafts verhindern,  
**damit** Race Conditions keine Duplikate oder Inkonsistenzen erzeugen.

- AC-3.1: Bei zwei simultanen Requests auf denselben `draftId` erhält höchstens ein Request den exklusiven Lock.
- AC-3.2: Der konkurrierende Request wird kontrolliert (Konfliktantwort) beendet und erzeugt **0** zusätzliche Postings.
- AC-3.3: Nach Ende der laufenden Verarbeitung kann ein neuer regulärer Buchungsversuch gestartet werden.

### User Story US-4 – Fehler- und Retry-Verhalten
**Als** API-Client  
**möchte ich** eindeutig klassifizierte Fehler erhalten,  
**damit** nur bei transienten Fehlern erneut ausgelöst wird.

- AC-4.1: Validierungs-/Fachfehler sind als nicht-retrybar gekennzeichnet und ändern sich ohne Datenänderung nicht.
- AC-4.2: Transiente Fehler (z. B. Timeout/Deadlock/temporärer Lock-Konflikt) sind retrybar gekennzeichnet und bleiben duplikatsicher.
- AC-4.3: Jede Fehlerantwort enthält maschinenlesbaren Fehlercode und `traceId`.

## 5 Annahmen und Abhängigkeiten

| Typ | Beschreibung |
|-----|--------------|
| Annahme | `BookAsync` bleibt der zentrale Einstiegspunkt für Draft- und Entry-Buchung. |
| Annahme | Das Datenmodell ermöglicht eine eindeutige Zuordnung von `Posting` zu `StatementDraftEntry` (z. B. via `SourceId`). |
| Annahme | `StatementDraftStatus` bleibt mit den Werten `Draft`, `Committed`, `Expired` bestehen. |
| Abhängigkeit | Transaktions-, Idempotenz- und Locking-Design wird im [Architektur-Blueprint Statement Booking Transaction Safety](../architecture/architecture-blueprint-statement-booking-transaction-safety.md) verbindlich spezifiziert. |
| Abhängigkeit | Entitäten/Constraints für Idempotenz und Concurrency werden im [ERM Statement Booking Transaction Safety](../architecture/entity-relationship-model-statement-booking-transaction-safety.md) präzisiert. |
| Abhängigkeit | Risiken und Must-Fix-Findings werden über [Architecture Review Statement Booking Transaction Safety](../improvements/review-architecture-statement-booking-transaction-safety.md) nachverfolgt (insb. F-01 bis F-04). |

## 6 Scope und Out-of-Scope

**In-Scope ✅**
- Atomare Transaktionsführung für `BookAsync` (Draft und Entry)
- Idempotenz-/Duplikatschutz bei Retrigger und Replay
- Sperr-/Locking-Strategie gegen parallele Verarbeitung desselben Drafts
- Standardisierte Fehlerklassifikation inkl. Retry-Semantik und ProblemDetails-Codes

**Out-of-Scope ❌**
- Redesign der gesamten Posting-Domäne außerhalb Statement-Buchung
- Einführung externer verteilter Lock-Systeme ohne direkte Feature-Notwendigkeit
- Umfangreiches UI-Redesign jenseits der Konflikt-/Fehlerdarstellung

## 7 Domänenmodell und Glossar

### Schlüsselentitäten
- **StatementDraft**: Buchungsentwurf mit `StatementDraftStatus` (`Draft`, `Committed`, `Expired`)
- **StatementDraftEntry**: Einzelposition eines Drafts als Quelle für Buchungsableitung
- **Posting**: Persistierte Buchungseinheit (z. B. Bank, Contact, SavingsPlan, Security)
- **BookingOperation**: Durable Repräsentation einer Buchungsoperation für Idempotenz/Replays
- **DraftProcessingGuard**: Exklusive Sperrinformation je `(OwnerUserId, DraftId)` während Verarbeitung

### Beziehungen (vereinfacht)
- `StatementDraft` 1..n `StatementDraftEntry`
- `StatementDraftEntry` 1..n `Posting` (fachliche Ableitung)
- `StatementDraft` 1..n `BookingOperation` (zeitlich)
- `StatementDraft` 0..1 aktive `DraftProcessingGuard` (zur Laufzeit)

### Glossar
- **Atomic Booking:** Alles-oder-nichts-Persistierung der kompletten Buchungsoperation
- **Idempotency:** Mehrfaches Auslösen derselben Operation führt zum selben Endzustand ohne Duplikate
- **Lock/Guard:** Exklusiver Mechanismus zur Vermeidung paralleler Verarbeitung desselben Drafts
- **Retryable Error:** Transienter technischer Fehler, bei dem ein erneuter Versuch zulässig ist

## 8 Nutzungsfälle (Use Cases)

### UC-1 Vollbuchung ohne Konkurrenz
**Akteure:** Nutzer, `StatementDraftsController`, `StatementDraftService`, Datenbank  
**Vorbedingungen:** Draft vorhanden, Status `Draft`, gültige Einträge  
**Ablauf:** Request senden → Lock erwerben → atomar buchen → Status auf `Committed` setzen → Erfolg zurückgeben  
**Nachbedingungen:** Konsistente, vollständig persistierte Buchung ohne Teilzustand

### UC-2 Retrigger nach unklarer Antwort
**Akteure:** API-Client, `StatementDraftsController`, `StatementDraftService`  
**Vorbedingungen:** Erstaufruf lief bereits (erfolgreich oder in Bearbeitung)  
**Ablauf:** Gleiches Ziel erneut anfragen → Idempotenz/Lock prüfen → deterministische Antwort liefern  
**Nachbedingungen:** Maximal eine fachliche Buchung, keine Duplikate

### UC-3 Parallele Requests auf denselben Draft
**Akteure:** Zwei API-Clients, `StatementDraftService`, Datenbank  
**Vorbedingungen:** Zeitgleiche Requests mit identischem `draftId`  
**Ablauf:** Request A erhält Lock, Request B Konfliktantwort → A beendet Verarbeitung → Lock frei  
**Nachbedingungen:** Keine race-condition-bedingte Doppelbuchung

## 9 Nächste Schritte

1. Transaktionsgrenzen, Locking und Idempotenz-Store im Architektur-Blueprint final bestätigen.
2. ERM und DB-Constraints für `BookingOperation`/`DraftProcessingGuard` finalisieren.
3. Fehlercode-Matrix (retrybar/nicht-retrybar) inkl. ProblemDetails-Mapping fixieren.
4. Integrations-/Parallelitätstests für Rollback, Retrigger und Lock-Kollisionen implementieren.
5. Review-Findings F-01 bis F-04 als Umsetzungs-Gate schließen.

## 10 Approval & Versionierung

| Version | Datum | Autor | Änderungen | Freigabe |
|---------|-------|-------|-----------|----------|
| 0.1 | 2026-06-06 | Copilot (Requirements Analysis) | Initiale Anforderungen für transaktionssichere Statement-Buchung erstellt | Ausstehend |
| 0.2 | 2026-06-06 | Requirements Analysis Agent | Dokument strukturell finalisiert, SMART-ACs für Atomarität/Idempotenz/Locking/Retry geschärft, Traceability zu Blueprint/ERM/Review präzisiert | Ausstehend |
