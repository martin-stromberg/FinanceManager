# Architektur-Review: Statement Booking Transaction Safety

> **Repository:** `martin-stromberg/FinanceManager`  
> **Geprüfte Artefakte:**  
> - `Docs/requirements/statement-booking-transaction-safety-requirements.md`  
> - `Docs/architecture/architecture-blueprint-statement-booking-transaction-safety.md`  
> - `Docs/architecture/entity-relationship-model-statement-booking-transaction-safety.md`  
> **Datum:** 2026-06-06  
> **Reviewer:** review-architecture  
> **Ampelstatus:** 🔴 **Red** (nicht umsetzungsreif ohne Vorabkorrekturen)

---

## 1) Executive Summary

Die Zielrichtung der Planung ist richtig: atomare Verarbeitung, Idempotenz, Single-Flight pro Draft und klarer Fehlervertrag sind grundsätzlich abgedeckt.  
Der aktuelle Stand enthält jedoch mehrere Widersprüche in den Kernmechanismen. Zwei davon sind Blocker:

1. **Transaktion vs. persistierter Fehlerzustand** ist logisch inkonsistent (Rollback aller Änderungen, aber gleichzeitig persistierte `Failed*`-States gefordert).
2. **Locking-Verhalten unter SQLite** ist nicht deterministisch genug spezifiziert, um die geforderte 409-Konfliktsemantik robust zu garantieren.

Zusätzlich bestehen zwei Major-Lücken:
- Retry-Vertrag verletzt die FR-Regel „nur transiente Fehler retrybar“.
- Idempotency-Key-Fallback ist nicht stabil genug als langfristiger API-Vertrag spezifiziert.

---

## 2) Bewertung der vier Kernaspekte

| Kernaspekt | Bewertung | Urteil |
|---|---|---|
| Atomare Transaktion | Starkes Zielbild, aber Widerspruch zwischen „vollständigem Rollback“ und persistierten `Failed*`-Operationen. | ❌ Nicht robust |
| Idempotenz / Doppelbuchungsschutz | Gute Basis (Unique + Replay + max. ein Erfolg je Scope), aber RequestKey-Fallback ist semantisch fragil. | ⚠️ Teilweise robust |
| Paralleles Locking pro Draft | Guard/Lease-Modell ist sinnvoll, aber SQLite-spezifisches Konfliktverhalten ist nicht präzise operationalisiert. | ⚠️ Teilweise robust |
| Fehler-/Retry-Vertrag | ProblemDetails-Struktur ist gut, Retry-Regel kollidiert mit FR-4 (`428` als retryable obwohl nicht transient). | ❌ Inkonsistent |

---

## 3) Priorisierte Findings (Blocker / Major / Minor)

| ID | Priorität | Finding | Risiko | Konkrete Maßnahme |
|---|---|---|---|---|
| F-01 | **Blocker** | **Transaktionsmodell widersprüchlich:** Blueprint fordert vollständigen Rollback bei Fehlern, gleichzeitig sollen `BookingOperation`-States `FailedTransient/FailedPermanent` dauerhaft verfügbar sein. | Retry- und Recovery-Pfade verhalten sich in Produktion anders als spezifiziert. | Architekturentscheidung erzwingen: **(A)** rein „rollback-only“ ohne persistierte Failed-States, oder **(B)** zweiphasiges Modell (fachliche Transaktion + separater, sicherer Persistenzpfad für Operationsergebnis). |
| F-02 | **Blocker** | **SQLite-Locking nicht deterministisch genug beschrieben:** Guard-Acquire in derselben Transaktion reicht nicht als Nachweis für stabile `409 BOOKING_IN_PROGRESS`-Antworten bei Parallelität. | Unter Last entstehen DB-`busy/timeout`-Fehler statt definierter Konfliktantwort; NFR-2 gefährdet. | SQLite-spezifischen Acquire-Pfad und Error-Mapping verbindlich festlegen (inkl. `busy timeout`, konkreter Exception-Mapping-Regeln zu 409/503 und Testnachweis mit 20 Parallelrequests). |
| F-03 | **Major** | **Retry-Semantik widerspricht Anforderungen:** FR-4 sagt „nur transiente Fehler retry-fähig“, Blueprint markiert `BOOKING_WARNING_PRECONDITION (428)` als retryable. | Client implementiert uneinheitliche Retry-Strategien; falsche automatische Retries. | FR/Blueprint harmonisieren: entweder 428 als nicht-retrybar markieren oder FR-4 präzisieren („retryable“ vs. „resubmittable mit geänderten Parametern“). |
| F-04 | **Major** | **Idempotency-Key-Fallback nicht vertragsstabil:** Fallback basiert auf begrenztem Parameter-Set (u. a. `ForceWarnings`) ohne Versionierung/Normierung. | Semantische Kollisionen bei zukünftigen Request-Erweiterungen; unklare Replay-Zuordnung. | RequestKey-Spezifikation versionieren (z. B. `key-schema-version`) und kanonische Input-Bildung fixieren; alternativ Header als Pflicht für öffentliche API machen. |
| F-05 | **Minor** | **Operationalisierung lückenhaft:** Cleanup/TTL/Heartbeat vorhanden, aber ohne harte SLOs/Alarmgrenzen. | Späte Erkennung von Stale-Guards und Retry-Staus. | Konkrete Betriebsziele ergänzen (z. B. max. stale age, Konfliktrate, Cleanup-Intervall, Alert-Schwellen). |

---

## 4) Konkrete Verbesserungsmaßnahmen

1. **Transaktions-/Statusmodell finalisieren (vor Code-Start):**
   - Eindeutig festlegen, wann `BookingOperation` persistiert wird und was bei Fehlern garantiert erhalten bleibt.
   - Das Modell als Sequenzdiagramm + Zustandsregeln aktualisieren.

2. **SQLite-Parallelitätsvertrag technisch scharfziehen:**
   - Exakte Acquire-/Takeover-Algorithmen definieren.
   - Fehler-Mapping-Tabelle ergänzen (`SQLite busy`, `timeout`, `deadlock-like` → 409 oder 503).

3. **Retry-Vertrag harmonisieren:**
   - `retryable` ausschließlich für transiente Fehler oder FR-Text entsprechend anpassen.
   - Clientseitige Semantik „erneut senden mit geänderten Parametern“ getrennt von Auto-Retry dokumentieren.

4. **Idempotenzvertrag härten:**
   - Fallback-Key algorithmisch versionieren.
   - Replay-Antwort als unveränderliche Vertragspflicht (`ResponseCode`, Payload-Snapshot) beibehalten.

5. **Betriebssicherheit messbar machen:**
   - Metriken/SLOs für Konflikte, stale Guards, Retry-Erfolg und Replay-Rate festlegen.

---

## 5) Traceability: Findings ↔ FR/NFR

| Finding | Betroffene FR | Betroffene NFR | Begründung |
|---|---|---|---|
| F-01 | FR-1, FR-4 | NFR-1, NFR-6 | Widersprüchliches Fehlermodell verhindert belastbare Aussage zu Rollback- und Retry-Sicherheit. |
| F-02 | FR-3 | NFR-2 | Ohne SQLite-präzisen Lock-Kontrakt ist „genau 1 Erfolg bei 20 Parallelrequests“ nicht verlässlich nachweisbar. |
| F-03 | FR-4, FR-5 | NFR-4, NFR-6 | Inkonsistente Retry-Klassifikation führt zu uneindeutiger API-Semantik. |
| F-04 | FR-2, FR-2.1 | NFR-3 | Unklarer Key-Fallback schwächt Idempotenzgarantie über API-Versionen hinweg. |
| F-05 | FR-5 | NFR-4, NFR-5 | Ohne SLOs ist der Fehlervertrag operativ schwer steuer- und beobachtbar. |

---

## 6) Empfohlene Umsetzungsreihenfolge (risikominimierend)

1. **F-01 schließen:** Transaktions-/Failure-Persistenzmodell final entscheiden und dokumentieren.
2. **F-02 schließen:** SQLite-Locking- und Exception-Mapping-Vertrag fixieren.
3. **F-03 schließen:** Fehler-/Retry-Taxonomie in Requirements + Blueprint konsistent machen.
4. **F-04 schließen:** Idempotency-Key-Vertrag versionieren/härten.
5. Migrationen (`BookingOperation`, `DraftProcessingGuard`, Indizes/Constraints) umsetzen.
6. `BookAsync` entlang finalem Transaktions- und Locking-Vertrag implementieren.
7. ProblemDetails + Header-Vertrag (`Idempotency-Key`, `X-Idempotent-Replay`, `Retry-After`) implementieren.
8. Integrations-/Parallelitäts-/Replay-Tests inkl. 20x-Konkurrenz und Retry-Fehlerklassen.
9. Observability + Cleanup-SLOs produktionsreif schalten.

---

## 7) Abschlussurteil

Der Architekturentwurf ist inhaltlich nah am Ziel, aber derzeit **nicht freigabefähig**.  
Vor Umsetzung müssen **F-01 und F-02 (Blocker)** sowie **F-03 und F-04 (Major)** verbindlich korrigiert werden.
