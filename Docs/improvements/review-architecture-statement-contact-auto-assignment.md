# Architektur-Review: Automatische Kontaktzuordnung nach Kontaktanlage aus Kontoauszugseintrag

> **Feature:** Statement Contact Auto Assignment  
> **Datum:** 2026-07-01  
> **Reviewer:** Architektur-Review & Bewertung  
> **Review-Basis:**  
> - Requirements: [`../requirements/statement-contact-auto-assignment-requirements.md`](../requirements/statement-contact-auto-assignment-requirements.md)  
> - Architektur-Blueprint: [`../architecture/architecture-blueprint-statement-contact-auto-assignment.md`](../architecture/architecture-blueprint-statement-contact-auto-assignment.md)  
> - ERM: [`../architecture/entity-relationship-model-statement-contact-auto-assignment.md`](../architecture/entity-relationship-model-statement-contact-auto-assignment.md)  
> - Planung: [`../planning/planning-statement-contact-auto-assignment.md`](../planning/planning-statement-contact-auto-assignment.md)

---

## 1) Executive Summary

Die Zielarchitektur ist grundsätzlich tragfähig: Sie nutzt bestehende Schichten (Blazor → API → ParentAssignmentService → EF Core) konsistent und adressiert den Kernfehler (fehlender Assignment-Aufruf nach Kontaktanlage) direkt.

**Gesamtbewertung:** fachlich und technisch solide, aber mit kritischen Präzisierungsbedarfen in Fehlervertrag, Idempotenz und operativer Absicherung.

**Freigabeempfehlung:** **CONDITIONAL GO**  
Freigabe zur Umsetzung unter Auflagen (siehe Abschnitt 7 und 8).

---

## 2) Bewertung der Systemarchitektur

### Stärken
- Klare End-to-End-Kette vom UI-Kontext (`parentId=EntryId`) bis zur Persistenz.
- Wiederverwendung bestehender Domänen-/Service-Mechanik (`IParentAssignmentService`) statt Sonderpfad.
- Ownership-Schutz ist architektonisch vorgesehen (OwnerUserId-Kette über Draft/Contact).

### Schwachpunkte
- Fehlerstrategie ist noch nicht eindeutig entschieden (teilerfolgreich vs. transaktional-abbrechend); das erzeugt fachliche Unschärfe zu FR-2.
- Race-/Doppel-Event-Verhalten (NFR-3) ist genannt, aber ohne konkrete technische Durchsetzung (z. B. Idempotency-Key, Concurrency-Guard).
- Umsetzungs- und Testschritte sind inzwischen in der Planungsdatei dokumentiert, müssen aber als verbindliche Merge-Gates umgesetzt werden.

**Bewertung Systemarchitektur:** **7/10**

---

## 3) Bewertung der Technologieentscheidungen

### Positiv
- Entscheidung für bestehenden Stack (ASP.NET Core, EF Core, Blazor) ist angemessen und risikoarm.
- Keine unnötigen Schema-Änderungen; Problem liegt im Orchestrierungsfluss, nicht im Datenmodell.
- Serverseitige Zuordnung als Source of Truth ist korrekt.

### Kritische Punkte
- Fehlende explizite Transaktions-/Konsistenzgrenze zwischen `Contact.Create` und `TryAssignAsync`.
- Logging-Anforderung (NFR-4) ist sinnvoll, aber noch ohne verbindliches Logging-Schema (Eventnamen, Felder, Fehlercodes).

**Bewertung Technologieentscheidungen:** **8/10**

---

## 4) UI/UX-Review

### Positiv
- Nutzerfluss ist fachlich stimmig: im Kontext erstellen, zurückkehren, Zuordnung sofort sehen.
- Fokus auf „kein manueller Reload“ adressiert Kernfriktion direkt.

### Risiken
- Fehlerfall-Kommunikation ist noch ambivalent („Kontakt erstellt, aber Zuordnung fehlgeschlagen“ vs. „Anlage nicht abgeschlossen“).
- Für Parallelkontexte fehlen UX-Leitplanken gegen Fehlverständnisse (z. B. eindeutiger Kontextindikator am Entry).

**UI/UX-Bewertung:** **7/10**

---

## 5) Bewertung der Qualitätsziele

| Qualitätsziel | Bewertung | Kommentar |
|---|---|---|
| NFR-1 Datenintegrität/Mandantenschutz | Gut | Architektur passt, Testtiefe muss verbindlich nachgewiesen werden. |
| NFR-2 Reaktionszeit P95 < 2s | Teilweise | Ziel definiert, aber ohne Mess-/Monitoring-Plan. |
| NFR-3 Idempotenz bei Doppel-Events | Kritisch offen | Ziel vorhanden, technische Umsetzung fehlt. |
| NFR-4 Nachvollziehbarkeit | Teilweise | Logging gefordert, aber nicht operativ spezifiziert. |
| NFR-5 Regressionsschutz | Teilweise | E2E-Gate gefordert; Planungsdokument vorhanden, konkrete Pipeline-Umsetzung steht noch aus. |

**Gesamtbewertung Qualitätsziele:** **teilweise erfüllt / absicherungsbedürftig**

---

## 6) Schwachstellen & Risiken (priorisiert)

| Prio | Risiko | Auswirkung | Eintritt | Bewertung |
|---|---|---|---|---|
| **Hoch** | Uneinheitlicher Fehlervertrag bei Assignment-Fehler | Inkonsistenter Fachzustand, unklare UX, Supportaufwand | Mittel | **P1** |
| **Hoch** | Keine konkrete Idempotenzstrategie (NFR-3) | Fehlzuordnungen bei Doppelklick/Retry/Parallelität | Mittel | **P1** |
| **Mittel** | Fehlender konkreter Monitoring-/SLO-Nachweis für NFR-2/NFR-4 | Qualitätsziele nicht objektiv belegbar | Mittel | **P2** |
| **Mittel** | Planungsartefakt muss als verbindliches Umsetzungsgate genutzt werden | Risiken bleiben offen, wenn die dokumentierten Schritte nicht verpflichtend umgesetzt werden | Mittel | **P2** |
| **Niedrig** | Optionale harte FK von `Entry.ContactId` auf `Contact.Id` fehlt | Schwächere DB-seitige Integrität (falls nicht anderweitig abgesichert) | Niedrig | **P3** |

---

## 7) Konkrete Verbesserungsvorschläge (umsetzbare Maßnahmen)

1. **Fehlervertrag final festlegen (P1)**  
   - Entscheidung treffen: strikt transaktional **oder** teilerfolgsfähig mit kompensierbarer Nachbearbeitung.  
   - API-Statuscodes und ProblemDetails pro Fehlerklasse verbindlich dokumentieren.

2. **Idempotenz technisch absichern (P1)**  
   - Für Kontaktanlage mit Parent-Kontext eine deduplizierende Schutzlogik einführen (z. B. Request-Token/Correlation + serverseitige Guard-Condition).  
   - Negativ-/Paralleltests explizit in Integration/E2E aufnehmen.

3. **Observability spezifizieren (P2)**  
   - Verbindliches Log-Schema definieren: `DraftId`, `EntryId`, `ContactId`, `OwnerUserId`, `AssignmentResult`, `TraceId`, `ErrorCode`.  
   - Mindestens ein Dashboard/Query zur Fehlerrate und Latenz nachziehen.

4. **Planungsdokument verbindlich operationalisieren (P2)**  
   - `../planning/planning-statement-contact-auto-assignment.md` als Merge-Gate nutzen (Umsetzungsreihenfolge, Testfälle, CI-Gate, Rollout/Backout).

5. **Integritäts-Hardening prüfen (P3)**  
   - Validieren, ob eine harte FK `StatementDraftEntry.ContactId -> Contact.Id` sinnvoll und migrationsverträglich ist.

---

## 8) Freigabeempfehlung

**CONDITIONAL GO**

Freigabe unter folgenden **MUSS-Auflagen vor Merge**:
1. Finaler Fehlervertrag für Assignment-Fehler ist festgelegt und dokumentiert.
2. Idempotenz-/Parallelitätsabsicherung ist implementiert oder verbindlich für diesen Scope ausgeschlossen und begründet.
3. Mindestens ein stabiler E2E-Regressionstest für FR-1/FR-1.1 ist in der CI aktiv.
4. Planungsdokument ist vorhanden und wird als verbindliches Umsetzungsgate angewendet.

Wenn eine der P1-Auflagen offen bleibt, ist auf **NO-GO** zu wechseln.

---

## 9) Entscheidungsreife / Abschluss

Das Architekturdesign ist in der Grundstruktur richtig und kann mit überschaubarem Aufwand produktionsreif gemacht werden.  
Die verbleibenden Risiken liegen primär in **Vertragsklarheit**, **Idempotenz** und **operativer Nachweisbarkeit** – nicht in der Wahl des Stacks oder des Domänenmodells.
