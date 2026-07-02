# Planning Overview: Statement Contact Auto Assignment

**Feature:** Automatische Kontaktzuordnung nach Kontakt-Neuanlage im Kontoauszugseintrag  
**Status:** 🟠 Planung abgeschlossen – Conditional Go  
**Datum:** 2026-07-01  
**Koordination:** planning-orchestrator

---

## Zielbild

Wird aus einem `StatementDraftEntry` ein neuer Kontakt erstellt, muss der neue Kontakt nach erfolgreicher Speicherung sofort dem auslösenden Eintrag zugeordnet, persistiert und im UI sichtbar sein.

---

## Verlinkte Planungsartefakte

- Anforderungen: [statement-contact-auto-assignment-requirements.md](../requirements/statement-contact-auto-assignment-requirements.md)
- Architektur-Blueprint: [architecture-blueprint-statement-contact-auto-assignment.md](../architecture/architecture-blueprint-statement-contact-auto-assignment.md)
- ERM: [entity-relationship-model-statement-contact-auto-assignment.md](../architecture/entity-relationship-model-statement-contact-auto-assignment.md)
- Architektur-Review: [review-architecture-statement-contact-auto-assignment.md](../improvements/review-architecture-statement-contact-auto-assignment.md)

---

## Orchestrator-Sequenz (vollständig durchgeführt)

1. **Anforderungsanalyse** erstellt (FR/NFR, ACs, Use Cases, Scope).
2. **Architektur-Blueprint** erstellt (E2E-Flow, Designentscheidungen, Fehlerbehandlung, Teststrategie).
3. **ERM** erstellt (bestehende Entitäten/Beziehungen, Schema-Impact, Konsistenzabgleich).
4. **Architektur-Review** durchgeführt (Risiken priorisiert, Maßnahmen definiert, Freigabeempfehlung).

---

## Konsolidierte Kernentscheidungen

1. **Primary Fix:** Nach `Contact`-Erstellung muss verpflichtend `IParentAssignmentService.TryAssignAsync(..., createdKind="contacts")` mit Entry-Kontext aufgerufen werden.
2. **Kein Pflicht-Schema-Change:** Das Feature ist primär ein Workflow-/Service-Fix; `StatementDraftEntry.ContactId` ist bereits vorhanden.
3. **Kontexttreue:** Zuordnung ausschließlich auf den auslösenden `EntryId` (kein Übersprechen auf andere Einträge).
4. **Mandantenschutz:** Ownership-Kette (`StatementDraft`/`Contact`) bleibt harte Voraussetzung für jede Zuordnung.
5. **Regression-Gate:** Mindestens ein stabiler E2E-Test als dauerhafte Absicherung gegen erneuten Funktionsverlust.

---

## Review-Findings und Umsetzungsgates

### P1 (MUSS)
- Fehlervertrag für Assignment-Fehler finalisieren (teilerfolgsfähig vs. strikt transaktional).
- Idempotenz-/Parallelitätsverhalten für Doppel-Events explizit absichern.

### P2 (SOLL)
- Verbindliches Log-/Telemetry-Schema für Zuordnungsereignisse.
- Messbarkeit für NFR-2/NFR-4 (Latenz und Nachvollziehbarkeit) operationalisieren.

### P3 (KANN)
- Optionales DB-Hardening prüfen (explizite FK `StatementDraftEntry.ContactId -> Contact.Id`, optionaler Index).

---

## Empfohlene Implementierungsreihenfolge

1. `ContactsController` um verpflichtenden Parent-Assignment-Aufruf ergänzen.
2. Fehlervertrag (Statuscodes/ProblemDetails) endgültig festlegen und dokumentieren.
3. Idempotenz-/Parallelitätsabsicherung für Doppel-Events umsetzen.
4. Unit + Integration + E2E Regressionstests implementieren.
5. Logging/Telemetry und CI-Gate final aktivieren.

---

## Zusammenfassung

Die Planungsrunde ist vollständig und artefaktübergreifend konsistent abgeschlossen.  
Die Umsetzung ist mit **CONDITIONAL GO** freigegeben, sobald die P1-Auflagen aus dem Architektur-Review verbindlich adressiert sind.

---

## Änderungshistorie

| Version | Datum | Autor | Änderung |
|---|---|---|---|
| 1.0 | 2026-07-01 | planning-orchestrator | Vollständige Konsolidierung von Requirements, Architektur, ERM und Architektur-Review inkl. Verlinkung und Umsetzungsgates |

