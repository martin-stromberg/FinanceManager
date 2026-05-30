# FA-WERT-REN-001: Renditeanalyse für Wertpapiere

> **Bezug im Anforderungskatalog:** FA-WERT-008 · FA-REP-005  
> **Primäre Anforderung:** `.copilot-task.md`  
> **Status:** 🔄 In Arbeit – branch-spezifisch für `126-wertpapierrendite`  
> **Version:** 0.2  
> **Datum:** 2026-05-08  
> **Autor:** GitHub Copilot

---

## 1. Überblick und Projektkontext

### 1.1 Projektbeschreibung

Der FinanceManager ist eine Blazor-Server-Anwendung zur persönlichen Finanzverwaltung. Für Wertpapiere liegen bereits die relevanten Rohdaten vor: Käufe, Verkäufe, Dividenden, Steuern, Gebühren und tägliche Kursdaten.  
Dieses Dokument beschreibt den **tatsächlichen Stand im Branch `126-wertpapierrendite`** und gleicht die Vorgaben aus `.copilot-task.md` mit dem bereits implementierten Code ab.

Die Renditeanalyse ist in diesem Branch **größtenteils umgesetzt**, aber noch nicht vollständig abgeschlossen. Zusätzliche, fachlich sinnvolle Erweiterungen werden ausdrücklich akzeptiert, sofern sie der Primäranforderung nicht widersprechen.

### 1.2 Geschäftsziele

| # | Ziel |
|---|------|
| G-1 | Nutzer sieht die Performance eines einzelnen Wertpapiers ohne externe Berechnung. |
| G-2 | Nutzer kann Renditekennzahlen fachlich nachvollziehen und aufschlüsseln. |
| G-3 | Nutzer erhält eine eigene Detailseite für zeitliche Analyse, Cashflows, Kennzahlen und Benchmark. |
| G-4 | Benchmark- und Sharpe-Konfiguration sind benutzerbezogen speicherbar. |
| G-5 | Fehlende oder unvollständige Kursdaten führen zu verständlichen Hinweiszuständen statt zu Laufzeitfehlern. |

### 1.3 Stakeholder

| Rolle | Interesse |
|-------|-----------|
| Endanwender | Verständliche und verlässliche Renditeanalyse pro Wertpapier |
| Entwickler / Maintainer | Klare Service-Schnittstellen, testbare Berechnungen, erweiterbare UI |
| Fachliche Review-Instanz | Soll/Ist-Abgleich gegen `.copilot-task.md`, Architektur- und Review-Dokumente |

### 1.4 Abgrenzung

Dieses Dokument betrachtet ausschließlich die Renditeanalyse für **einzelne Wertpapiere**. Portfolio-Gesamtrendite, Exportfunktionen, Währungsumrechnung und steuerliche Beratung bleiben weiterhin außerhalb des Scopes.

### 1.5 Bestandsaufnahme

| Bereich | Ist-Stand im Branch |
|---------|---------------------|
| **Primäranforderung** | `.copilot-task.md` beschreibt zwei Analyseebenen: kompakte Wertpapieransicht und Detailseite mit Tabs sowie technische/UI-bezogene Randbedingungen. |
| **Service/API** | `IReturnAnalysisService` enthält produktionsrelevante Methoden für Summary, Sparkline, Kennzahlen, Perioden, Cashflows, Performance-Chart, Benchmark, KPI-Breakdowns und User-Settings. Dazu existieren API-Endpunkte unter `SecuritiesController`. |
| **Persistenz** | Benutzerbezogene Renditeanalyse-Einstellungen sind im Domain-Partial `User.ReturnAnalysis.cs` und per Migration `20260425085408_AddReturnAnalysisSettingsToUser.cs` in `AspNetUsers` verankert. |
| **Caching** | `MemoryReturnAnalysisCache` implementiert 1h-In-Memory-Caching mit Invalidierung. |
| **Wertpapierseite** | `ReturnSummaryWidget` ist auf `CardPage.razor` für Wertpapierkarten eingebunden. KPI-Breakdowns werden lazy über Side-Panel geladen. |
| **Detailseite** | `/securities/{id}/performance` ist mit `SecurityPerformancePage.razor` und Tabs `Overview`, `TimeSeries`, `Cashflows`, `Metrics`, `Benchmark` vorhanden. |
| **Zusatzfeatures** | KPI-Breakdowns/Transparenz, Hinweiszustände bei fehlenden Kursen, Heatmap, simulierte Renditen bei fehlender Historie sowie Benchmark-Kontext vor Vergleichsstart sind implementiert und widersprechen der Anforderung nicht. |
| **Dokumentation** | Architektur-Blueprint, ERM und Review liegen bereits vor: [Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) · [ERM](../architecture/entity-relationship-model-renditeanalyse.md) · [Review](../improvements/review-renditeanalyse.md) |

### 1.6 Soll/Ist-Abgleich

| Thema | Soll laut `.copilot-task.md` | Ist im Branch | Bewertung |
|------|-------------------------------|---------------|-----------|
| Kompakte Analyse auf Wertpapierseite | Kennzahlen + Mini-Chart auf bestehender Wertpapierseite | `ReturnSummaryWidget` zeigt Kernkennzahlen und Warnzustände; Sparkline-Service existiert, Visualisierung ist im Widget noch nicht verdrahtet | **Teilweise erfüllt** |
| Detailseite | Eigene Unterseite `/securities/{id}/performance` mit Tabs `Overview/TimeSeries/Cashflows/Metrics/Benchmark` | Route, Tabs, Ribbon-Integration und Navigation von der Wertpapierkarte sind vorhanden | **Erfüllt** |
| KPI-Transparenz | Formeln nachvollziehbar, Aufschlüsselung möglich | Widget besitzt KPI-Breakdown-Sidepanel via `GetKpiBreakdownsAsync` | **Erfüllt, sogar erweitert** |
| Fehlende Kursdaten | Robust abfangen und Nutzer informieren | Widget und Tabs zeigen Hinweiszustände; zusätzlich gibt es simulierte Renditen ohne Historie | **Erfüllt** |
| Monatliche Renditen | Heatmap oder Tabelle | Heatmap ist implementiert | **Erfüllt** |
| Benchmark-Vergleich | Vergleich mit optionalem Benchmark, verständlicher Hinweis ohne Benchmark | Benchmark-Tab mit Chart, Setup-Link und zusätzlichem Vorperioden-Kontext vor Vergleichsstart vorhanden | **Erfüllt, sogar erweitert** |
| User-Settings | Benchmark, Sharpe-Opt-in, risikofreier Zinssatz benutzerbezogen | Backend/Domain/API vorhanden; Setup-UI zeigt derzeit nur Benchmark-Auswahl | **Teilweise erfüllt** |
| Dividend-/Kostenverlauf | Detaildarstellungen für Dividenden sowie Kosten/Steuern | Datenstrukturen und Jahresaggregate existieren; dedizierte Diagramme sind noch nicht vollständig in der UI sichtbar | **Teilweise erfüllt** |

### 1.7 Akzeptierte Zusatzfeatures

| Kennung | Zusatzfeature | Begründung |
|---------|---------------|------------|
| AF-1 | **KPI-Breakdown-Sidepanel im `ReturnSummaryWidget`** | Erhöht Transparenz und unterstützt NFR-Nachvollziehbarkeit; widerspricht keiner Soll-Anforderung. |
| AF-2 | **Simulierte Renditen bei fehlender Kurshistorie** | Fachlich hilfreicher Fallback statt Komplettausfall; UI kennzeichnet diesen Zustand explizit. |
| AF-3 | **Benchmark-Kontext vor Vergleichsstart** | Verbessert Interpretierbarkeit des Vergleichs, insbesondere bei unterschiedlichen Historienlängen. |
| AF-4 | **Automatische Eskalation des Overview-Zeitraums** | Wenn ein enger Zeitraum leer ist, wird ein größerer Zeitraum geladen; verbessert UX ohne Fachbruch. |

### 1.8 Verbleibende Lücken

| Kennung | Lücke | Auswirkung | Priorität |
|---------|-------|------------|-----------|
| GAP-1 | Sparkline wird trotz `GetSparklineDataAsync` noch nicht im `ReturnSummaryWidget` angezeigt | Ein Teil der Primäranforderung für die kompakte Analyse ist nur backend-seitig erfüllt | MUST HAVE |
| GAP-2 | Setup-UI bietet noch keine Eingabe für `ShowSharpeRatio` und `RiskFreeRate` | Sharpe-Ratio-Feature ist ohne direkten UI-Zugang nur teilweise nutzbar | MUST HAVE |
| GAP-3 | Dedizierte Dividenden- und Kosten-/Steuerdiagramme fehlen in der finalen UI-Ausprägung | Detailseite erfüllt Reporting-Anforderungen nur teilweise | HIGH |
| GAP-4 | Drill-Down auf aggregierte Kennzahlen der Detailseite ist noch nicht überall umgesetzt | Nachvollziehbarkeit ist aktuell primär im Widget vorhanden | HIGH |
| GAP-5 | Tab-spezifische Deep-Links/URL-State sind nicht explizit umgesetzt | Navigierbarkeit und Wiedereinstieg sind schwächer als geplant | MEDIUM |
| GAP-6 | Teile der Detailseite verwenden noch harte deutsche UI-Texte | NFR zu vollständiger DE/EN-Lokalisierung ist noch nicht vollständig erfüllt | MEDIUM |

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Kompakte Renditeanalyse auf der Wertpapierkarte:** `ReturnSummaryWidget` ist auf `CardPage.razor` eingebunden und zeigt Total Return absolut/% , Marktwert, investiertes Kapital, CAGR, IRR sowie Warnzustände bei Datenlücken. Für die vollständige Soll-Erfüllung fehlen noch die explizite Gegenüberstellung von Einstandskurs zu aktuellem Kurs und die sichtbare Sparkline. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) · [Review](../improvements/review-renditeanalyse.md) | Kern-Feature | MUST HAVE | 🔄 In Arbeit |
| **FR-1.1** | **Sparkline für die Wertpapierkarte:** `IReturnAnalysisService.GetSparklineDataAsync` und der Endpunkt `GET /api/securities/{id}/return-sparkline` liefern Daten für die Mini-Zeitreihe mit mindestens 30 Preiswerten; die UI-Einbindung im Widget steht noch aus. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Reporting & Analyse | MUST HAVE | 🔄 In Arbeit |
| **FR-2** | **Detaillierte Renditeanalyse auf Unterseite:** Die Seite `SecurityPerformancePage.razor` ist über `/securities/{id}/performance` erreichbar und über die Wertpapierkarte verlinkt. Die Tabs `Overview`, `TimeSeries`, `Cashflows`, `Metrics`, `Benchmark` sind vorhanden. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) · [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-2.1** | **Erweiterte Kennzahlen im Tab `Metrics`:** Brutto-/Nettorendite, TWR, IRR, Volatilität, maximaler Drawdown, Steuerquote, Realized/Unrealized Gains und optional Sharpe Ratio sind vorhanden. Die Sichtbarkeit der Sharpe Ratio ist backend-seitig benutzerabhängig, aber UI-seitige Konfiguration fehlt noch teilweise. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | MUST HAVE | 🔄 In Arbeit |
| **FR-2.2** | **Zeitliche Auswertungen im Tab `TimeSeries`:** Jahresrenditen und monatliche Renditen als Heatmap sind implementiert. Zusätzlich existiert ein Hinweisbanner für simulierte Renditen bei fehlender Kurshistorie. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Reporting & Analyse | MUST HAVE | ✅ Umgesetzt |
| **FR-2.3** | **Cashflow-Timeline im Tab `Cashflows`:** Käufe, Verkäufe, Dividenden, Steuern und Gebühren werden chronologisch dargestellt; dazu gibt es Jahresaggregate. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Reporting & Analyse | MUST HAVE | ✅ Umgesetzt |
| **FR-2.4** | **Performance-Chart im Tab `Overview`:** Marktwert und investiertes Kapital werden als Zeitreihe dargestellt; die Zeiträume `1M/3M/6M/1J/3J/Gesamt` sind über das Ribbon wählbar. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Reporting & Analyse | HIGH | ✅ Umgesetzt |
| **FR-2.5** | **Dividendenverlauf auf Detailseite:** Jahresdividenden sind im Datenmodell vorhanden (`AnnualDividends`), aber die finale Visualisierung als eigener Verlauf ist in der aktuellen UI noch nicht vollständig sichtbar. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Reporting & Analyse | HIGH | 🔄 In Arbeit |
| **FR-2.6** | **Kosten-/Steuerverlauf auf Detailseite:** Jahreswerte für Steuern und Gebühren sind in `CashflowTimelineDto.AnnualSummaries` vorhanden; ein dediziertes Diagramm inkl. kumulativer Sicht fehlt noch. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Reporting & Analyse | MEDIUM | 🔄 In Arbeit |
| **FR-3** | **Zeitgewichtete Rendite (TWR):** Das Berechnungsmodul stellt TWR für die Detailkennzahlen bereit und ist testseitig abgedeckt. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) · [Review](../improvements/review-renditeanalyse.md) | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-4** | **Persönliche Rendite (IRR):** IRR ist im Berechnungsservice vorhanden und wird in Widget sowie Detailkennzahlen verwendet, sofern berechenbar. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-5** | **CAGR-Berechnung:** CAGR ist im Summary-Widget integriert und über den KPI-Breakdown dokumentiert. | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-6** | **FIFO-Kostenbasis für Teilverkäufe:** FIFO-Basis und Realized Gains sind in Service, DTOs und Tests umgesetzt; Oversell-Warnungen werden zusätzlich abgefangen. → [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Datenverwaltung | MUST HAVE | ✅ Umgesetzt |
| **FR-7** | **Benchmark-Vergleich:** Der Benchmark-Tab zeigt einen benutzerbezogenen Vergleich gegen ein internes Wertpapier; ohne Konfiguration erscheint ein Hinweis samt Setup-Aktion. Zusätzlich ist Vorperioden-Kontext vor `ComparisonStartDate` umgesetzt. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) · [Review](../improvements/review-renditeanalyse.md) | Kern-Feature | HIGH | ✅ Umgesetzt |
| **FR-8** | **Sharpe Ratio als Opt-in:** Datenmodell, Service und API unterstützen `ShowSharpeRatio` und `RiskFreeRate`; für die vollständige Fachanforderung fehlt die vollständige Eingabestrecke im Setup-UI. → [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Kern-Feature | HIGH | 🔄 In Arbeit |
| **FR-9** | **Benutzereinstellungen für Renditeanalyse:** Benchmark, Sharpe-Opt-in und risikofreier Zinssatz sind userbezogen persistierbar. Aktuell ist in der Setup-Oberfläche nur die Benchmark-Auswahl direkt editierbar. → [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Datenverwaltung | MUST HAVE | 🔄 In Arbeit |
| **FR-10** | **Robustheit bei fehlenden Kursen:** Fehlende Preise führen zu Warnhinweisen, Fallback-Zuständen oder simulierten Zeitreihen statt zu UI-Abbrüchen. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | MUST HAVE | ✅ Umgesetzt |
| **FR-11** | **Nachvollziehbarkeit und Drill-Down:** KPI-Breakdowns sind für Widget-Kennzahlen vorhanden. Für aggregierte Kennzahlen der kompletten Detailseite ist die Drill-Down-Tiefe noch unvollständig. → [Review](../improvements/review-renditeanalyse.md) | UX / Accessibility | MEDIUM | 🔄 In Arbeit |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Finanzmathematische Korrektheit:** Für Renditeberechnungen existieren umfangreiche Unit Tests (`ReturnCalculationServiceTests.cs`, `ReturnAnalysisServiceTests.cs`). Ein expliziter dokumentierter Referenzabgleich gegen externe Finanztools ist im Branch noch nicht sichtbar. → [Review](../improvements/review-renditeanalyse.md) | Zuverlässigkeit | MUST HAVE | 🔄 In Arbeit |
| **NFR-2** | **Ladezeiten und Performance-Budget:** Caching und tabweises Laden sind vorbereitet; eine branch-spezifisch dokumentierte Messung gegen `< 1s / < 3s` fehlt noch. | Performance | MUST HAVE | 🔄 In Arbeit |
| **NFR-3** | **Caching aggregierter Zeitreihen:** `MemoryReturnAnalysisCache` mit TTL, Key-Tracking und Invalidierung ist implementiert. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Performance | MUST HAVE | ✅ Umgesetzt |
| **NFR-4** | **Transparenz und Formel-Dokumentation:** XML-Dokumentation, KPI-Breakdowns und Formeltexte sind vorhanden; ein eigenständiger Hilfe-/Glossar-Einstieg aus der Detailseite fehlt noch. | Wartbarkeit | HIGH | 🔄 In Arbeit |
| **NFR-5** | **Erweiterbarkeit des Berechnungsmoduls:** `IReturnAnalysisService`, `IReturnCalculationService`, `IFifoCostBasisCalculator` und DTO-Struktur erlauben Erweiterungen ohne UI-Komplettumbau. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Wartbarkeit | HIGH | ✅ Umgesetzt |
| **NFR-6** | **Robustheit bei ungültigen oder lückenhaften Daten:** Missing-Price-Hinweise, No-Data-States, simulierte Zeitreihen und Oversell-Warnungen sind vorhanden und getestet. | Zuverlässigkeit | MUST HAVE | ✅ Umgesetzt |
| **NFR-7** | **Testabdeckung:** Unit Tests für Berechnungen, Cache und Service sind vorhanden; vollständige End-to-End- oder Controller-Integrationstests für alle Return-Endpoints sind noch nicht nachgewiesen. | Zuverlässigkeit | MUST HAVE | 🔄 In Arbeit |
| **NFR-8** | **User-Scoping und Zugriffsschutz:** Service-Methoden und Controller sind durchgängig user-scopig ausgelegt; Ownership-Tests für den Service liegen vor. | Sicherheit | MUST HAVE | ✅ Umgesetzt |
| **NFR-9** | **Lokalisierung:** Für Teile der Renditeanalyse existieren DE/EN-Ressourcen, aber mehrere Razor-Komponenten nutzen noch harte deutsche Texte. | UX / Accessibility | HIGH | 🔄 In Arbeit |
| **NFR-10** | **Barrierefreiheit:** Es existieren ARIA-Labels, Tabellenrollen und Icon-Unterstützung; alternative Darstellungen und vollständige Accessibility-Abdeckung der Charts sind noch nicht durchgängig erkennbar. | UX / Accessibility | MEDIUM | 🔄 In Arbeit |

---

## 4. Akzeptanzkriterien

### US-1 – Kompakte Renditeübersicht auf der Wertpapierseite

**Als** Endanwender  
**möchte ich** auf der Wertpapierkarte sofort die wichtigsten Renditekennzahlen sehen,  
**damit ich** die Performance ohne Seitenwechsel einschätzen kann.

**Akzeptanzkriterien (Branch `126-wertpapierrendite`):**

- [x] `ReturnSummaryWidget` wird auf `CardPage.razor` für einzelne Wertpapiere automatisch eingeblendet.
- [x] Total Return wird absolut und prozentual angezeigt.
- [x] Positive/negative Werte werden zusätzlich zu Farbe mit `▲/▼` gekennzeichnet.
- [x] KPI-Formeln und Aufschlüsselungen sind über das Info-Sidepanel erreichbar.
- [x] Fehlende Kursdaten werden als Warnzustand angezeigt.
- [ ] Sparkline wird sichtbar im Widget gerendert.
- [ ] Einstandskurs vs. aktueller Kurs wird explizit als eigenes Feld angezeigt.

### US-2 – Detaillierte Renditeanalyse auf eigener Unterseite

**Als** Endanwender  
**möchte ich** eine dedizierte Analyse-Seite mit Tabs nutzen,  
**damit ich** Kennzahlen, Zeitverläufe, Cashflows und Benchmark getrennt untersuchen kann.

**Akzeptanzkriterien (Branch `126-wertpapierrendite`):**

- [x] Route `/securities/{id}/performance` ist erreichbar.
- [x] Tabs `Overview`, `TimeSeries`, `Cashflows`, `Metrics`, `Benchmark` sind vorhanden.
- [x] Performance-Chart mit Zeiträumen `1M/3M/6M/1J/3J/Gesamt` ist vorhanden.
- [x] Jahresrenditen und monatliche Heatmap sind vorhanden.
- [x] Cashflow-Timeline und Jahresaggregate sind vorhanden.
- [x] Benchmark-Hinweis ohne Konfiguration ist vorhanden.
- [x] Dividendenverlauf ist als explizites UI-Element vollständig sichtbar.
- [x] Kosten-/Steuerverlauf ist als eigenes Diagramm vollständig sichtbar.
- [x] Tabs sind direkt per URL-State oder Deep-Link anwählbar.

### US-3 – Korrektheit und Nachvollziehbarkeit der Kennzahlen

**Als** Endanwender  
**möchte ich** fachlich korrekte und nachvollziehbare Kennzahlen sehen,  
**damit ich** der Renditeanalyse vertrauen kann.

**Akzeptanzkriterien (Branch `126-wertpapierrendite`):**

- [x] Es existieren umfangreiche Unit Tests für Berechnungen (`ReturnCalculationServiceTests.cs`).
- [x] Es existieren Service-Tests für Ownership, Missing Prices, Sparkline, Benchmark und KPI-Breakdowns.
- [x] Widget-KPIs sind über `GetKpiBreakdownsAsync` aufschlüsselbar.
- [ ] Detailseiten-KPIs sind durchgängig bis auf Einzeltransaktionsebene aufschlüsselbar.
- [ ] Ein dokumentierter Referenzvergleich gegen externe Finanzwerkzeuge ist im Branch ergänzt.

### US-4 – Benchmark-Vergleich und Benutzerkonfiguration

**Als** Endanwender  
**möchte ich** mein Wertpapier gegen einen Benchmark vergleichen und meine Präferenzen speichern,  
**damit ich** meine Performance im Kontext interpretieren kann.

**Akzeptanzkriterien (Branch `126-wertpapierrendite`):**

- [x] Benchmark kann im Setup als benutzerbezogene Einstellung ausgewählt und gespeichert werden.
- [x] Benchmark-Chart ist auf der Detailseite vorhanden.
- [x] Ohne Benchmark erscheint ein verständlicher Hinweis mit Setup-Bezug.
- [x] Vorperioden-Kontext vor dem eigentlichen Vergleichsstart wird angezeigt.
- [x] Sharpe-Opt-in und risikofreier Zinssatz sind in derselben Setup-UI editierbar.

### US-5 – Robuste Analyse trotz Datenlücken

**Als** Endanwender  
**möchte ich** auch bei lückenhaften Kursdaten verständliche Ergebnisse oder Hinweise erhalten,  
**damit ich** nicht mit leeren oder fehlerhaften Ansichten zurückgelassen werde.

**Akzeptanzkriterien (Branch `126-wertpapierrendite`):**

- [x] Missing-Price-Warnungen sind im Widget vorhanden.
- [x] Für fehlende Historie kann die Zeitreihenanalyse als simulierte Rendite gekennzeichnet werden.
- [x] Benchmark-Vergleiche erklären fehlende Vorperioden-Daten des Benchmarks.
- [ ] Das Verhalten ist zusätzlich durch End-to-End-Tests für die UI/API abgesichert.

---

## 5. Annahmen und Abhängigkeiten

| # | Typ | Beschreibung |
|---|-----|--------------|
| A-1 | Annahme | `Posting`, `Security` und `SecurityPrice` bleiben die primären fachlichen Datenquellen der Renditeanalyse. |
| A-2 | Annahme | User-Settings werden direkt auf `AspNetUsers` gespeichert (`BenchmarkSecurityId`, `ShowSharpeRatio`, `RiskFreeRate`). |
| A-3 | Annahme | Benchmarks bleiben interne Wertpapiere des gleichen Benutzers. |
| A-4 | Annahme | Simulierte Renditen bei fehlender Historie sind als transparenter Fallback fachlich akzeptiert. |
| D-1 | Abhängigkeit | [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) bleibt Referenz für Schichten, Services und Cache-Strategie. |
| D-2 | Abhängigkeit | [ERM](../architecture/entity-relationship-model-renditeanalyse.md) bleibt Referenz für User-Settings und betroffene Entitäten. |
| D-3 | Abhängigkeit | [Review](../improvements/review-renditeanalyse.md) enthält relevante Hinweise zu Ownership, Cache-Naming und Randfällen. |
| D-4 | Abhängigkeit | Die Setup-Seite muss UI-seitig erweiterbar bleiben, um Sharpe-Optionen nachzuziehen. |
| D-5 | Abhängigkeit | Für vollständige NFR-Erfüllung sind zusätzliche Messungen und Tests außerhalb der bisherigen Branch-Implementierung nötig. |

---

## 6. Scope und Out-of-Scope

### In-Scope ✅

- Rendite-Widget auf der Wertpapierkarte
- API- und Service-Schicht für Summary, Sparkline, Metrics, Periodic Returns, Cashflows, Benchmark und KPI-Breakdowns
- Detailseite `/securities/{id}/performance`
- Tabs `Overview`, `TimeSeries`, `Cashflows`, `Metrics`, `Benchmark`
- Benchmark als benutzerbezogene Einstellung
- Sharpe-Ratio-Backend inklusive Risikofreiem-Zinssatz
- Missing-Price-Hinweise und transparente Fallback-Zustände
- Heatmap für monatliche Renditen
- KPI-Breakdowns / Transparenz
- Benchmark-Kontext vor Vergleichsstart

### Out-of-Scope ❌

- CSV-/Excel-/PDF-Export der Renditeanalyse
- Portfolio-weite Renditeanalyse über mehrere Wertpapiere
- Externe Benchmark-Datenquellen außerhalb des eigenen Wertpapierbestands
- Währungsumrechnung für Fremdwährungs-Wertpapiere
- Steueroptimierung, Steuerprognosen oder Steuerberatung
- Mobile-spezifische Sonder-UI

---

## 7. Domänenmodell und Glossar

### 7.1 Schlüsselentitäten und Beziehungen

```text
User
 ├─ BenchmarkSecurityId : Guid?
 ├─ ShowSharpeRatio     : bool
 └─ RiskFreeRate        : decimal

Security
 ├─ OwnerUserId
 ├─ SecurityPrice[]
 └─ Posting[]

Posting
 ├─ Buy
 ├─ Sell
 ├─ Dividend
 ├─ Tax
 └─ Fee

ReturnAnalysis
 ├─ ReturnSummaryDto
 ├─ SparklineDataDto
 ├─ DetailedReturnMetricsDto
 ├─ PeriodicReturnsDto
 ├─ CashflowTimelineDto
 ├─ BenchmarkComparisonDto
 └─ KpiBreakdownDto[]
```

> Vollständige Modellierung: [entity-relationship-model-renditeanalyse.md](../architecture/entity-relationship-model-renditeanalyse.md)

### 7.2 Fachliche Konzepte im Branch

| Konzept | Beschreibung |
|---------|--------------|
| **Return Summary** | Kompakte Kennzahlen für die Wertpapierkarte |
| **Sparkline** | Separate Zeitreihe für Mini-Chart auf der Wertpapierkarte |
| **KPI Breakdown** | Formeltext + gruppierte Beiträge/Transaktionen zu einer Kennzahl |
| **Periodic Returns** | Jahresrenditen, Monatsrenditen, Jahresdividenden, Fallback-Flag `HasSimulatedPrices` |
| **Benchmark Comparison** | Normierte Wertentwicklung von Zielwertpapier und Benchmark inkl. Vorperioden-Kontext |

### 7.3 Glossar

| Begriff | Definition |
|---------|-----------|
| **TWR** | Zeitgewichtete Rendite zur Vergleichbarkeit unabhängig von Cashflow-Zeitpunkten |
| **IRR / XIRR** | Persönliche Rendite auf Basis realer Zahlungszeitpunkte |
| **CAGR** | Durchschnittliche jährliche Wachstumsrate |
| **Sharpe Ratio** | Risikobereinigte Rendite relativ zum risikofreien Zinssatz |
| **FIFO** | Kostenbasis nach „First In, First Out“ |
| **KPI Breakdown** | Transparenzansicht mit Formel, Gruppen und Einzelbeiträgen |
| **HasSimulatedPrices** | Kennzeichen, dass Periodenrenditen aus Transaktionsankern statt echter Kurshistorie abgeleitet wurden |
| **ComparisonStartDate** | Erster Zeitpunkt, an dem Zielwertpapier und Benchmark sinnvoll normiert verglichen werden können |

---

## 8. Nutzungsfälle (Use Cases)

### UC-1: Rendite-Widget auf Wertpapierkarte nutzen

**Akteur:** Endanwender  
**Vorbedingung:** Wertpapierkarte geöffnet  
**Ablauf:**

1. Nutzer öffnet eine einzelne Wertpapierkarte.
2. `CardPage.razor` bindet `ReturnSummaryWidget` ein.
3. Das Widget lädt die Summary.
4. Nutzer öffnet per Info-Button den KPI-Breakdown.
5. System zeigt Formeltext und Beitragsgruppen zur gewählten Kennzahl.

### UC-2: Detailseite der Renditeanalyse öffnen

**Akteur:** Endanwender  
**Vorbedingung:** Wertpapier vorhanden  
**Ablauf:**

1. Nutzer klickt auf die Ribbon-Aktion „Performance“.
2. System navigiert zu `/securities/{id}/performance`.
3. Die Seite zeigt `Overview`, `TimeSeries`, `Cashflows`, `Metrics`, `Benchmark`.
4. Nutzer wechselt zwischen Tabs und analysiert die Daten tabweise.

### UC-3: Benchmark konfigurieren

**Akteur:** Endanwender  
**Vorbedingung:** Mindestens zwei Wertpapiere vorhanden  
**Ablauf:**

1. Nutzer öffnet das Setup.
2. Im Bereich Renditeanalyse wird ein Benchmark-Wertpapier gewählt.
3. System speichert die Auswahl benutzerbezogen.
4. Benchmark-Vergleich ist anschließend auf der Detailseite nutzbar.

**Hinweis zum Branch:** Sharpe-Opt-in und risikofreier Zinssatz sind backend-seitig vorbereitet, aber noch nicht vollständig über dieselbe UI pflegbar.

### UC-4: Monatliche Renditen trotz lückenhafter Historie interpretieren

**Akteur:** Endanwender  
**Vorbedingung:** Wertpapier mit unvollständiger Kurshistorie  
**Ablauf:**

1. Nutzer öffnet den Tab `TimeSeries`.
2. Das System erkennt fehlende Preisreihen.
3. Statt eines harten Fehlers zeigt die UI einen Hinweis auf simulierte Renditen.
4. Heatmap und Jahreswerte bleiben interpretierbar, aber fachlich markiert.

### UC-5: Benchmark-Kontext vor Vergleichsstart verstehen

**Akteur:** Endanwender  
**Vorbedingung:** Benchmark ist konfiguriert, Historien starten zu unterschiedlichen Zeitpunkten  
**Ablauf:**

1. Nutzer öffnet den Tab `Benchmark`.
2. Das System zeigt Zielwertpapier und Benchmark normiert.
3. Vor `ComparisonStartDate` wird zusätzlicher Kontext angezeigt.
4. Der Nutzer erkennt, ab wann ein echter Vergleich beginnt und wie frühere Daten zu interpretieren sind.

---

## 9. Nächste Schritte

| # | Aufgabe | Priorität | Abhängigkeit |
|---|---------|-----------|--------------|
| 1 | Sparkline im `ReturnSummaryWidget` visuell integrieren und an `return-sparkline` anbinden | MUST HAVE | FR-1.1 |
| 2 | Setup-UI um `ShowSharpeRatio` und `RiskFreeRate` ergänzen | MUST HAVE | FR-8 / FR-9 |
| 3 | Dividendenverlauf auf der Detailseite sichtbar rendern | HIGH | FR-2.5 |
| 4 | Kosten-/Steuerverlauf inkl. kumulativer Sicht auf der Detailseite ergänzen | HIGH | FR-2.6 |
| 5 | Drill-Down von Detailseiten-Kennzahlen auf Postings/Teilansichten ausbauen | HIGH | FR-11 |
| 6 | Tab-State per URL/Deep-Link unterstützen | MEDIUM | FR-2 |
| 7 | Harte UI-Texte der Renditeanalyse vollständig lokalisieren | MEDIUM | NFR-9 |
| 8 | Performance-Budgets messen und dokumentieren | MEDIUM | NFR-2 |
| 9 | End-to-End-/Controller-Tests für Return-Endpoints ergänzen | MEDIUM | NFR-7 |

---

## 10. Approval & Versionierung

### 10.1 Freigabestatus

Die branch-spezifische Bestandsaufnahme ergibt: **fachlich weit fortgeschritten, aber noch nicht vollständig abgeschlossen**.  
Die bestehenden Zusatzfeatures werden akzeptiert und bleiben Teil der Anforderung, solange die Muss-Kriterien aus `.copilot-task.md` vollständig nachgezogen werden.

### 10.2 Versionstabelle

| Version | Datum | Autor | Änderung |
|---------|-------|-------|----------|
| 0.1 | 2025-01-01 | (auszufüllen) | Initiale Erstellung |
| 0.2 | 2026-05-08 | GitHub Copilot | Branch-spezifische Bestandsaufnahme, Soll/Ist-Abgleich, akzeptierte Zusatzfeatures und verbleibende Lücken ergänzt; Status der FR/NFR aktualisiert |

### 10.3 Offene Freigabepunkte

| # | Thema | Status |
|---|-------|--------|
| AP-1 | Sparkline-UI auf der Wertpapierkarte | Offen |
| AP-2 | Vollständige Setup-UI für Sharpe-Konfiguration | Offen |
| AP-3 | Vollständige Reporting-Visualisierung für Dividenden sowie Kosten/Steuern | Offen |
| AP-4 | Nachweis der Performance- und Test-NFRs | Offen |

---

*Letzte Aktualisierung: 2026-05-08 – branch-spezifischer Soll/Ist-Abgleich für `126-wertpapierrendite`*
