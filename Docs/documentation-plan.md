# Dokumentationsplan: Renditeanalyse (FA-WERT-REN-001)

> Erstellt: 2025-07  
> Status: 🟡 In Bearbeitung  
> Feature: Renditeanalyse – TWR, IRR, CAGR, Volatilität, MaxDrawdown, Sharpe Ratio

---

## Phase 1 – Analyse-Ergebnisse (Agentenschwarm)

### API-Dokumentation

| Befund | Details |
|--------|---------|
| **Bestehende Datei** | `docs/api/SecuritiesController.md` – vorhanden, aber veraltet |
| **Fehlende Endpunkte** | 10 neue Renditeanalyse-Endpunkte komplett undokumentiert |
| **Priorität** | 🔴 Hoch |

**Lücken (alle 10 neuen Endpunkte fehlen):**

| HTTP | Route | Status |
|------|-------|--------|
| GET | `/api/securities/{id}/return-summary` | ❌ Fehlt |
| GET | `/api/securities/{id}/return-sparkline` | ❌ Fehlt |
| GET | `/api/securities/{id}/return-metrics` | ❌ Fehlt |
| GET | `/api/securities/{id}/return-periodic` | ❌ Fehlt |
| GET | `/api/securities/{id}/return-cashflows` | ❌ Fehlt |
| GET | `/api/securities/{id}/return-chart` | ❌ Fehlt |
| GET | `/api/securities/{id}/return-benchmark` | ❌ Fehlt |
| GET | `/api/securities/return-analysis/settings` | ❌ Fehlt |
| PUT | `/api/securities/return-analysis/settings` | ❌ Fehlt |
| DELETE | `/api/securities/{id}/return-cache` | ❌ Fehlt |

---

### Flow-Dokumentation

| Befund | Details |
|--------|---------|
| **Bestehende Flows** | 4 Flows vorhanden (`import-classification`, `posting-aggregates`, `split-uploadgroup`, `statement-draft-booking`) |
| **Neue Komponenten ohne Flow** | 4 Komponenten |
| **Priorität** | 🔴 Sehr hoch |

**Lücken:**

| Komponente | Datei | Status |
|-----------|-------|--------|
| ReturnAnalysisService (Orchestrierung) | `docs/flows/return-analysis-service.md` | ❌ Fehlt |
| ReturnCalculationService (Mathematik) | `docs/flows/return-calculation.md` | ❌ Fehlt |
| FifoCostBasisCalculator | `docs/flows/fifo-cost-basis.md` | ❌ Fehlt |
| MemoryReturnAnalysisCache | _(in return-analysis-service.md integriert)_ | ❌ Fehlt |

---

### Business-Dokumentation

| Befund | Details |
|--------|---------|
| **Bestehende Docs** | F001–F016 vorhanden, F017 komplett fehlend |
| **Renditeanalyse** | 0% in `docs/business/` dokumentiert |
| **Priorität** | 🔴 Hoch |

**Lücken:**

| Datei | Inhalt | Status |
|-------|--------|--------|
| `docs/business/features/F017-renditeanalyse.md` | Endanwender-Perspektive: Widget, 5-Tab-Seite, Use Cases, Erklärungen | ❌ Fehlt |
| `docs/business/features/F017-renditeanalyse-domain.md` | Geschäftsregeln: FIFO, Kennzahlen-Definitionen, Benchmark-Konfiguration | ❌ Fehlt |
| `docs/business/features.md` | Eintrag für F017 fehlt in Übersichtstabelle | ❌ Lücke |
| `docs/business/overview.md` | Security-Abschnitt um Renditeanalyse erweitern | ⚠️ Unvollständig |

---

### README-Analyse

| Befund | Details |
|--------|---------|
| **Vollständigkeit** | 5/9 Standard-Abschnitte vorhanden (56%) |
| **Renditeanalyse** | Komplett fehlend in README |
| **Priorität** | 🟡 Mittel |

**Lücken:**

| Abschnitt | Status |
|-----------|--------|
| Renditeanalyse in Feature-Liste | ❌ Fehlt |
| Architektur-Übersicht | ❌ Fehlt |
| Deployment-Anleitung | ❌ Fehlt |
| Changelog | ❌ Fehlt |
| Links zu neuen Planungsdokumenten | ❌ Fehlt |

---

## Phase 2 – Ausführungsplan

### Neue Dateien erstellen

| Datei | Agent | Priorität |
|-------|-------|-----------|
| `docs/api/SecuritiesController.md` (Update) | documentation-api | ✅ Erledigt |
| `docs/flows/return-analysis-service.md` | documentation-flow | 🔴 Hoch |
| `docs/flows/return-calculation.md` | documentation-flow | 🔴 Hoch |
| `docs/flows/fifo-cost-basis.md` | documentation-flow | 🔴 Hoch |
| `docs/business/features/F017-renditeanalyse.md` | documentation-business | 🔴 Hoch |
| `docs/business/features/F017-renditeanalyse-domain.md` | documentation-business | 🔴 Hoch |

### Bestehende Dateien aktualisieren

| Datei | Agent | Änderung |
|-------|-------|----------|
| `docs/business/features.md` | documentation-business | F017-Eintrag ergänzen |
| `docs/business/overview.md` | documentation-business | Security-Abschnitt: Renditeanalyse-Link |
| `README.md` | documentation-readme-writer | Features, Architektur-Abschnitt |

### Bekannte offene Punkte (nicht dokumentieren, sondern hinweisen)

| ID | Problem | Hinweis |
|----|---------|---------|
| BUG-1 | `BuildTwrPeriods` nutzt `start` statt `end` → TWR fehlerhaft | In Flow-Doc als bekannter Bug vermerken |
| Prio-2 | Periodische Renditen, Benchmark-Normalisierung Tests fehlen | In Business-Doc als ausstehend vermerken |
| Prio-3 | bUnit UI-Tests nicht implementiert | In Business-Doc als ausstehend vermerken |

---

## Ergebnis (nach Phase 2)

> Abgeschlossen: 2025-07  
> Status: ✅ Vollständig

### Erstellt / Aktualisiert

| Datei | Aktion | Größe | Inhalt |
|-------|--------|-------|--------|
| `docs/api/SecuritiesController.md` | ✅ Aktualisiert | 35.691 B | 24 Endpunkte vollständig dokumentiert, alle 10 neuen Return-Analysis-Endpunkte mit Parametern, DTOs, Statuscodes, curl-Beispielen |
| `docs/flows/return-analysis-service.md` | ✅ Neu erstellt | 10.894 B | Sequenz- & Flowchart-Diagramme, Methodentabelle, Cache-Keys, Sicherheitshinweise S-1/S-3, BUG-1-Hinweis |
| `docs/flows/return-calculation.md` | ✅ Neu erstellt | 7.242 B | TWR- und IRR-Flowcharts (Modified Dietz, Newton-Raphson + Bisection-Fallback), alle 9 Berechnungsmethoden |
| `docs/flows/fifo-cost-basis.md` | ✅ Neu erstellt | 7.781 B | Buy/Sell/Fee-Ablaufdiagramme, Lot-Verwaltung, GroupId-Mechanismus, Oversell-Handling |
| `docs/flows/README.md` | ✅ Neu erstellt | 1.366 B | Index aller 7 Flow-Dokumentationen |
| `docs/business/features/F017-renditeanalyse.md` | ✅ Neu erstellt | 14.556 B | Endanwender-Doku: Rendite-Box, 5 Tabs, Kennzahl-Erklärungen in Alltagssprache, FAQ |
| `docs/business/features/F017-renditeanalyse-domain.md` | ✅ Neu erstellt | 14.332 B | Domain-Doku: Formeln, FIFO-Regeln, Sicherheitsregeln, Caching-Strategie, DB-Felder |
| `docs/business/features.md` | ✅ Aktualisiert | 4.790 B | F017-Eintrag ergänzt, Gesamtfortschritt aktualisiert |
| `docs/business/overview.md` | ✅ Aktualisiert | 3.572 B | Security-Abschnitt um Renditeanalyse-Links erweitert |
| `README.md` | ✅ Aktualisiert | 5.019 B | Feature-Liste, Architektur-Abschnitt, neue Doku-Links, offene Punkte |

### Bekannte offene Punkte (dokumentiert, aber nicht behoben)

| ID | Problem | Dokumentiert in |
|----|---------|-----------------|
| **BUG-1** | `BuildTwrPeriods` nutzt `start` statt `end` für SharesAtEnd → TWR-Ergebnisse fehlerhaft bei mehreren gleichzeitigen Transaktionen | `docs/flows/return-analysis-service.md`, `docs/api/SecuritiesController.md`, `docs/business/features/F017-renditeanalyse.md` |
| **Prio-2** | Periodische Renditen & Benchmark-Normalisierung Tests fehlen | `docs/business/features/F017-renditeanalyse.md` |
| **Prio-3** | bUnit UI-Tests für Tab-Komponenten fehlen | `docs/business/features/F017-renditeanalyse.md` |

---

# Dokumentationsplan: Budgetwirkung bei Buchung (2026-05-31)

> Lauf: Vollständige Doku-Aktualisierung für vorhandene Code-Änderungen im Branch  
> Status: ✅ Abgeschlossen

## Phase 1 – Analyse-Ergebnisse

### API-Dokumentation

| Befund | Ergebnis |
|---|---|
| Vorhandene API-Doku | `Docs/api/` mit breiter Controller-Abdeckung vorhanden |
| Lücken | `HelpController` und `StatementDraftEntriesController` ohne eigene Doku-Datei |
| Veraltete Datei | `Docs/api/StatementDraftsController.md` zu knapp, ohne Budget-Impact-Erweiterung |
| Priorität | 🔴 Hoch |

### Flow-Dokumentation

| Befund | Ergebnis |
|---|---|
| Vorhandene Flows | Import, Booking, Aggregates, Split, Renditeanalyse dokumentiert |
| Lücke | Kein dedizierter Ablauf für `BudgetImpactEvaluationService` |
| Veraltet | `Docs/flows/statement-draft-booking.md` ohne Budget-Impact-Schritt |
| Priorität | 🔴 Hoch |

### Business-Dokumentation

| Befund | Ergebnis |
|---|---|
| Vorhandene Business-Doku | Features F001–F017 vorhanden |
| Lücke | Kein eigenes Feature-Dokument zur Budgetwirkung während Buchung |
| Veraltet | `Docs/business/features.md` und `Docs/business/overview.md` ohne neues Verhalten |
| Priorität | 🔴 Hoch |

### README-Analyse

| Befund | Ergebnis |
|---|---|
| Bestand | Projektübersicht vorhanden, aber Struktur uneinheitlich |
| Lücken | Kein expliziter Abschnitt „Konfiguration“, „Deployment“, „Changelog“ |
| Veraltet | Budget-Impact-Verhalten in Featureliste fehlt |
| Priorität | 🟡 Mittel |

## Phase 2 – Umsetzungsplan

### Neu zu erstellen

- `Docs/api/HelpController.md`
- `Docs/api/StatementDraftEntriesController.md`
- `Docs/flows/budget-impact-evaluation.md`
- `Docs/business/features/F018-budgetwirkung-buchung.md`

### Zu aktualisieren

- `Docs/api/StatementDraftsController.md`
- `Docs/api/README.md`
- `Docs/flows/statement-draft-booking.md`
- `Docs/flows/README.md`
- `Docs/business/features.md`
- `Docs/business/overview.md`
- `README.md`

## Ergebnis (Anhang)

### Geprüfte implementierte Änderungen (Code & Tests)

- Neue Services/Verträge:
  - `FinanceManager.Application/Statements/IBudgetImpactEvaluationService.cs`
  - `FinanceManager.Infrastructure/Statements/BudgetImpactEvaluationService.cs`
- Dependency Injection:
  - Registrierung in `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`
- DTO-Erweiterungen:
  - `FinanceManager.Shared/Dtos/Statements/BudgetImpactDtos.cs`
  - `FinanceManager.Shared/Dtos/Statements/BudgetImpactHintType.cs`
  - `StatementDraftEntryDto.BudgetImpact`
  - `BookingResult.BudgetImpactSummary`
- API-Verhalten:
  - `StatementDraftsController` liefert bei Kontakt-/Sparplan-/Save-All-Änderungen nun Entry-Impact
  - `StatementDraftService.BookAsync(...)` liefert Abschluss-Summary in `BookingResult`
- Tests:
  - `FinanceManager.Tests/Statements/BudgetImpactEvaluationServiceTests.cs` deckt Überschreitung + Neutralfall ab

### Dokumentationsstatus nach Umsetzung

- ✅ API, Flow, Business und README für Budget-Impact-Funktion ergänzt/aktualisiert
- ✅ Dokumentation auf tatsächlich implementiertes Verhalten beschränkt
- ⚠️ Keine separaten automatischen Doku-Lint- oder Build-Checks im Repository gefunden

### Abschlussprüfung (Orchestrator-Lauf)

- ✅ Alle geplanten Ziel-Dateien existieren und sind nicht leer.
- ✅ Code-Änderungen und Testklasse zur Budget-Impact-Funktion wurden in der Dokumentation referenziert.
- ⚠️ `dotnet test` konnte in dieser Umgebung nicht bis zum Abschluss beobachtet werden (Restore/Warnungen sichtbar, danach kein finaler Exit im Tool-Stream).

---

# Dokumentationsplan: Transaktionssichere Kontoauszug-Buchung

> Status: ✅ Aktualisiert
> Feature: Transaktionssichere Buchung von Statement-Drafts mit Guard, Wiederholungsstrategie und 409-Fehlervertrag

## Aktualisierte Dokumente

- `Docs/flows/statement-draft-booking.md`
- `Docs/api/StatementDraftsController.md`
- `Docs/business/features/F004b-kontoauszug-verwaltung.md`
- `Docs/business/overview.md`
- `Docs/api/README.md`
- `README.md`

## Abgedeckte Inhalte

- transaktionssichere Buchung mit vollständigem Rollback
- Single-Flight-/Locking-Guard gegen parallele Buchungen
- Idempotenz bei wiederholten Buchungsversuchen
- 409-ProblemDetails mit `code`, `retryable` und `traceId`
- Benutzerhinweise zur sicheren Wiederholung der Buchung

## Ergebnis

- Die technische und fachliche Dokumentation spiegelt jetzt den implementierten Stand der Statement-Buchung wider.
- Offene Doku-Lücken für dieses Feature sind nicht bekannt.
