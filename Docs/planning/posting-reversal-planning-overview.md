# Planungsübersicht: Posting-Stornierung (Reversal)

**Feature:** Posting-Stornierung (Reversal)  
**Status:** 🟠 Planung abgeschlossen - Bereit für Pre-Implementation Review  
**Datum:** 2025-01-27  
**Koordination:** planning-orchestrator

---

## Executive Summary

Die Planung für das Feature "Posting-Stornierung (Reversal)" wurde erfolgreich durch alle vier Planungsphasen geführt:

1. ✅ **Anforderungsanalyse** - Strukturierte Anforderungen mit Use Cases
2. ✅ **Architektur-Blueprint** - Technische Architektur und Implementierungsplan
3. ✅ **Entity-Relationship-Modellierung** - Datenmodell-Erweiterungen
4. ✅ **Architecture-Review** - Kritische Bewertung und Verbesserungsvorschläge

### Gesamtbewertung

**Review-Status:** 🟠 **CONDITIONAL GO**

Die Architektur ist solide, aber es existieren **4 kritische Auflagen**, die vor Implementierungsbeginn addressiert werden müssen:

| Kritischer Punkt | Priorität | Status |
|------------------|-----------|--------|
| **C-1:** Transaktionskonsistenz | 🔴 Blocker | Offen |
| **C-2:** Audit-Trail-Mechanismen | 🔴 Blocker | Offen |
| **C-3:** Cascading Reversals | 🔴 Blocker | Offen |
| **C-4:** Concurrency-Strategie | 🔴 Blocker | Offen |

---

## Planungsdokumente

### 1. Anforderungsdokument

📄 **[FA-POST-001: Posting-Stornierung (Reversal)](../requirements/FA-POST-001_Posting_Reversal.md)**

**Inhalt:**
- Projektkontext und Geschäftsziele (5 Ziele)
- Funktionale Anforderungen (FR-1 bis FR-6)
- Nicht-funktionale Anforderungen (NFR-1 bis NFR-6)
- 4 detaillierte User Stories mit SMART-Akzeptanzkriterien
- Domänenmodell mit Mermaid-ER-Diagramm
- 3 vollständige Use Cases

**Highlights:**
- ✅ Klar definierte Akzeptanzkriterien (messbar)
- ✅ Vollständige Domänenmodellierung
- ✅ Detaillierte Use Cases mit alternativen Abläufen
- ✅ Scope-Abgrenzung (In-Scope / Out-of-Scope)

---

### 2. Architektur-Blueprint

📐 **[Architektur-Blueprint: Posting-Stornierung](../architecture/architecture-blueprint-posting-reversal.md)**

**Inhalt:**
- Systemarchitektur mit Mermaid-Diagramm (Web → Application → Infrastructure → Domain → Database)
- Komponenten und Schnittstellen (IPostingReversalService, DTOs, API-Endpunkte)
- Technologieentscheidungen (EF Core Transactions, Service Layer Pattern)
- UI/UX-Konzept (Button-Anzeige, Bestätigungsdialog, State Management)
- Datenmodell (EF Core Configuration, Migration-Inhalt, DB-Schema)
- Qualitätsziele (Atomarität, Performance, Nachvollziehbarkeit, Testabdeckung)
- Implementierungsplan (5 Phasen, 27 Stunden geschätzt)

**Highlights:**
- ✅ Vollständige Systemarchitektur visualisiert
- ✅ Code-Beispiele für alle kritischen Komponenten
- ✅ Detaillierter Implementierungsplan mit Zeitschätzungen
- ✅ 3 offene Fragen identifiziert (Q-1 bis Q-3)

**Offene Fragen:**
- Q-1: StatementImport-Struktur für Stornierungsbuchungen
- Q-2: Blazor-Komponenten-Namen für Posting-Detail-Seiten
- Q-3: Aggregate Update Scope (UpsertForPostingAsync)

---

### 3. Entity-Relationship-Modell

🗄️ **[Entity-Relationship-Modell: Posting-Reversal](../architecture/entity-relationship-model-posting-reversal.md)**

**Inhalt:**
- Vollständiges ERM-Diagramm mit Mermaid
- Tabellarische Übersicht aller Posting-Felder (21 Attribute)
- Detaillierte Beziehungsübersicht (Externe + Self-References)
- 5 begründete Modellierungsentscheidungen
- Abgleich mit Architektur-Blueprint (100% Konsistenz)
- DB-Migrations-Hinweise (dotnet ef commands, SQL-Statements)

**Neue Datenbankfelder:**
```csharp
public Guid? ReversedByPostingId { get; private set; }  // → Stornierungsbuchung
public Guid? ReversalForPostingId { get; private set; } // → Original-Buchung
```

**Highlights:**
- ✅ Bidirektionale 1:1-Beziehung für Reversal-Tracking
- ✅ Self-Referencing FK mit ON DELETE RESTRICT
- ✅ Indexierung für Query-Performance
- ✅ Check Constraint für Exklusivität (Original XOR Stornierung)
- ✅ SQLite-spezifische Hinweise dokumentiert

---

### 4. Architecture-Review

🔍 **[Architecture-Review: Posting-Reversal](../improvements/review-architecture-posting-reversal.md)**

**Inhalt:**
- Executive Summary mit Gesamtbewertung
- Identifizierte Findings (4 kritisch, 10 major, 3 minor)
- Priorisierte Verbesserungsvorschläge (V-1 bis V-10)
- Empfohlene nächste Schritte (Phase 0 bis 3)
- Go/No-Go-Empfehlung mit Checkliste
- Risikomatrix mit Mitigation-Strategien

**Kritische Findings:**
- **C-1:** Transaktionskonsistenz nicht vollständig spezifiziert
- **C-2:** Audit-Trail-Mechanismen fehlen
- **C-3:** Strategie für Cascading Reversals unklar
- **C-4:** Concurrency-Strategie nicht definiert

**Major Findings:**
- M-1: Idempotenz nicht garantiert
- M-2: Unzureichende Edge Case Coverage
- M-3: Performance-Risiko bei großen Gruppen
- M-4: Fehlendes Error-Recovery-Pattern
- M-5: Validierung nicht exhaustiv
- M-6: Integration mit Statement-Import unklar
- M-7: Fehlende Benachrichtigungsstrategie
- M-8: Migration-Rollback nicht dokumentiert
- M-9: Testbarkeit von Transaktionen unklar
- M-10: Security-Aspekte unvollständig

**Empfehlung:**
🟠 **CONDITIONAL GO** - Implementierung kann nach Phase 0 (Pre-Implementation) beginnen

---

## Nächste Schritte

### Phase 0: Pre-Implementation (MUSS vor Sprint-Start)

**Dauer:** 4-6 Stunden  
**Verantwortlich:** Product Owner + Architect  
**Status:** 🔴 Offen

| Schritt | Beschreibung | Aufwand | Blocker? |
|---------|--------------|---------|----------|
| **S-1** | Offene Frage Q-1 klären (StatementImport) | 1h | 🔴 Ja |
| **S-2** | Offene Frage Q-2 klären (Blazor-Komponenten) | 0.5h | 🟡 Nein |
| **S-3** | Offene Frage Q-3 klären (UpsertForPostingAsync) | 1h | 🔴 Ja |
| **S-4** | Authorization Policy definieren (C-4, V-1) | 1h | 🔴 Ja |
| **S-5** | Audit-Logging-Strategie spezifizieren (C-2, V-2) | 1h | 🔴 Ja |
| **S-6** | "Storno der Stornierung" als Anforderung ergänzen (V-3) | 0.5h | 🟡 Nein |
| **S-7** | Edge Cases spezifizieren (M-2, V-7) | 1h | 🟡 Nein |

**Output:** Aktualisierte Versionen aller drei Dokumente

---

### Phase 1: Implementation (Sprint 1)

**Dauer:** 34 Stunden (Original 27h + 7h für zusätzliche Tasks)  
**Verantwortlich:** Development Team  
**Status:** ⏸️ Warten auf Phase 0

**Tasks:**
1. Domain & Infrastructure (Foundation) - 8.25h
2. API Layer - 2.5h
3. UI Layer - 6h
4. Testing - 11h (separat in Phase 2)
5. Documentation & Deployment - 3h (separat in Phase 3)
6. **Zusätzlich:** Authorization - 2h
7. **Zusätzlich:** Audit-Logging - 2h
8. **Zusätzlich:** "Storno der Stornierung" - 3h

---

### Phase 2: Testing & Validation (Sprint 1-2)

**Dauer:** 8-12 Stunden  
**Verantwortlich:** QA + Development Team  
**Status:** ⏸️ Warten auf Phase 1

| Schritt | Beschreibung | Aufwand |
|---------|--------------|---------|
| **T-1** | Unit Tests (≥85% Coverage) | 4h |
| **T-2** | Integration Tests | 3h |
| **T-3** | Lasttests (V-6) | 3h |
| **T-4** | Authorization Tests | 1h |
| **T-5** | Audit-Logging Validierung | 1h |

---

### Phase 3: Documentation & Review (Sprint 2)

**Dauer:** 2-3 Stunden  
**Verantwortlich:** Tech Lead  
**Status:** ⏸️ Warten auf Phase 2

| Schritt | Beschreibung | Aufwand |
|---------|--------------|---------|
| **D-1** | API-Dokumentation (Swagger) | 1h |
| **D-2** | Benutzer-Dokumentation | 1h |
| **D-3** | Final Architecture Review | 1h |

---

## Gesamtaufwand

| Phase | Geschätzt |
|-------|-----------|
| **Phase 0:** Pre-Implementation | 4-6h |
| **Phase 1:** Implementation | 34h |
| **Phase 2:** Testing & Validation | 8-12h |
| **Phase 3:** Documentation & Review | 2-3h |
| **Gesamt** | **48-55h** |

**Hinweis:** Der Gesamtaufwand ist höher als ursprünglich geschätzt (27h → 48-55h), weil kritische Aspekte (Authorization, Audit-Logging, Testabdeckung) nun explizit berücksichtigt werden.

---

## Risiken

### Kritische Risiken

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|--------|-------------------|--------|------------|
| Transaktionskonsistenz nicht garantiert | Hoch | Kritisch | Explizite Transaktionsdefinition + Integration Tests |
| Fehlender Audit-Trail | Mittel | Kritisch | Audit-Logging-Service implementieren |
| Concurrency-Probleme | Mittel | Hoch | Optimistic Concurrency mit RowVersion |
| Performance bei großen Gruppen | Mittel | Mittel | Lasttests + Async Processing für große Gruppen |

---

## Zusammenfassung

Die Planung für das Posting-Reversal-Feature ist **vollständig und von hoher Qualität**, aber es existieren **kritische Lücken**, die vor Implementierung geschlossen werden müssen.

**Empfehlung:**
1. ✅ Führen Sie **Architecture Decision Meeting** durch (1-2 Stunden)
2. ✅ Klären Sie alle offenen Fragen (Q-1 bis Q-3)
3. ✅ Adressieren Sie kritische Findings (C-1 bis C-4)
4. ✅ Aktualisieren Sie Dokumente basierend auf Review-Feedback
5. ✅ Nach Abschluss von Phase 0: **GO für Implementierung**

---

## Änderungshistorie

| Version | Datum | Autor | Änderung |
|---------|-------|-------|----------|
| 1.0 | 2025-01-27 | planning-orchestrator | Initial planning overview after all 4 phases completed |

---

**Erstellt durch:** planning-orchestrator  
**Beteiligte Agenten:**
- planning-requirements-analysis
- planning-architecture-blueprint
- planning-entity-relationship-modeler
- review-architecture
