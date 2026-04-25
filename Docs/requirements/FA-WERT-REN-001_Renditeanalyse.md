# FA-WERT-REN-001: Renditeanalyse für Wertpapiere

> **Bezug im Anforderungskatalog:** FA-WERT-008 · FA-REP-005  
> **Status:** 📋 Geplant  
> **Version:** 0.1 (Entwurf)  
> **Datum:** 2025-01-01  
> **Autor:** (auszufüllen)

---

## 1. Überblick und Projektkontext

### 1.1 Projektbeschreibung

Der FinanceManager ist eine Blazor-Server-Anwendung zur persönlichen Finanzverwaltung. Neben Kontenverwaltung, Kontoauszugsimport und Sparplanung bildet die Wertpapierverwaltung einen Kernbereich: Käufe, Verkäufe, Dividenden und Steuern werden als `Posting`-Einträge erfasst; tägliche Kursdaten werden über AlphaVantage abgerufen und als `SecurityPrice`-Einträge gespeichert.

**Fachliche Lücke:** Obwohl alle relevanten Rohdaten vorhanden sind (Transaktionen, Kurshistorie), fehlt bislang eine **Renditeanalyse**, die dem Nutzer strukturiert aufzeigt, wie sich seine Investitionen entwickeln. FA-WERT-008 im Anforderungskatalog benennt das Ziel, ist jedoch noch nicht umgesetzt.

### 1.2 Geschäftsziele

| # | Ziel |
|---|------|
| G-1 | Nutzer kann die tatsächliche Rendite eines Wertpapiers (Kursgewinne + Dividenden – Steuern) auf einen Blick erfassen. |
| G-2 | Nutzer erhält standardkonforme Finanzkennzahlen (TWR, IRR, CAGR, Sharpe Ratio) ohne manuelle Berechnung. |
| G-3 | Detailseite ermöglicht zeitliche Analyse (Jahres-, Monatsrenditen, Cashflow-Timeline). |
| G-4 | Benchmark-Vergleich unterstützt Entscheidungsfindung (konfigurierbar, optional). |
| G-5 | Berechnungen sind transparent und nachvollziehbar (Formel-Tooltips, Drill-Down). |

### 1.3 Stakeholder

| Rolle | Interesse |
|-------|-----------|
| Endanwender (Privatperson) | Rendite der eigenen Wertpapiere verstehen; steuerliche Transparenz |
| Entwickler / Maintainer | Erweiterbare, testbare Berechnungslogik; klare Schnittstellen |

### 1.4 Abgrenzung

Dieses Dokument beschreibt ausschließlich die Renditeanalyse für Wertpapiere. Auswertungen für Bankkonten, Sparplan-Reporting und allgemeine Export-Funktionen sind in separaten Anforderungsdokumenten beschrieben.

---

## 2. Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Einfache Rendite-Box auf der Wertpapierseite:** Kompakter Abschnitt auf der bestehenden Wertpapier-Detailseite zeigt folgende Kennzahlen: Aktuelle Gesamtrendite (Total Return in % und absolut), Rendite seit Kauf, CAGR (Ø Jahresrendite), Einstandskurs vs. aktueller Kurs, investiertes Kapital, aktueller Marktwert. Farbliches Feedback (grün/rot) für positive/negative Werte. Jede Kennzahl trägt einen Tooltip mit der verwendeten Berechnungsformel. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-1.1** | **Mini-Chart auf der Wertpapierseite:** Eingebettetes Liniendiagramm (Sparkline) zeigt die Wertentwicklung des Portfolios (investiertes Kapital vs. Marktwert) über die gesamte Haltedauer. Zeitraum nicht wählbar (immer seit Erstkauf). | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-2** | **Detaillierte Renditeanalyse (Unterseite):** Eigene navigierbare Unterseite (`/securities/{id}/performance`) mit Tabs: **Übersicht \| Zeitliche Entwicklung \| Cashflows \| Kennzahlen \| Benchmark**. Erreichbar über Link/Button von der Wertpapierseite. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) · [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-2.1** | **Erweiterte Kennzahlen (Tab: Kennzahlen):** Bruttorendite, Nettorendite (nach Steuern), Volatilität (annualisiert), maximaler Drawdown (max. Wertverlust vom Höchststand), Dividendenrendite pro Kalenderjahr, Steuerquote (Steuern / Bruttorendite in %), Realized Gains vs. Unrealized Gains, IRR (persönliche Rendite, auf Basis effektiver Cashflows). Sharpe Ratio nur wenn in den Benutzereinstellungen aktiviert (risikofreier Zinssatz konfigurierbar). → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-2.2** | **Zeitliche Auswertungen (Tab: Zeitliche Entwicklung):** Jahresrenditen als Balkendiagramm (ein Balken pro Kalenderjahr), monatliche Renditen als Heatmap oder tabellarische Übersicht. Zeiträume werden automatisch aus den Transaktionsdaten abgeleitet. | Reporting & Analyse | MUST HAVE | 📋 Geplant |
| **FR-2.3** | **Cashflow-Timeline (Tab: Cashflows):** Chronologische Darstellung aller wertpapierbezogenen Cashflows: Käufe (negativ), Verkäufe (positiv), Dividenden (positiv), Steuern (negativ). Darstellung als Timeline-Liste und optional als gestapeltes Balkendiagramm pro Jahr. | Reporting & Analyse | MUST HAVE | 📋 Geplant |
| **FR-2.4** | **Performance-Chart (Tab: Übersicht):** Liniendiagramm der Portfoliowertentwicklung über die gesamte Haltedauer (bereinigter Einstandswert + Kursentwicklung + Dividenden). Zeitraumauswahl: 1 Monat, 3 Monate, 6 Monate, 1 Jahr, 3 Jahre, Gesamt. | Reporting & Analyse | HIGH | 📋 Geplant |
| **FR-2.5** | **Dividendenverlaufsdiagramm (Tab: Zeitliche Entwicklung):** Balkendiagramm der ausbezahlten Dividenden (brutto und netto) gruppiert nach Kalenderjahr, ergänzt durch kumulative Gesamtdividende. | Reporting & Analyse | HIGH | 📋 Geplant |
| **FR-2.6** | **Kosten-/Steuerverlauf (Tab: Cashflows):** Balkendiagramm der angefallenen Gebühren und Steuern pro Kalenderjahr, inklusive kumulativer Summe. | Reporting & Analyse | MEDIUM | 📋 Geplant |
| **FR-3** | **Berechnungsmodul (TWR):** Implementierung der zeitgewichteten Rendite (Time-Weighted Return) nach Industrie­standard (Modified Dietz oder exakte Perioden­verkettung). TWR ermöglicht fairen Vergleich unabhängig vom Einzahlungszeitpunkt. Dividenden fließen am Ex-Tag ein. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | KI-Integration | MUST HAVE | 📋 Geplant |
| **FR-4** | **Berechnungsmodul (IRR):** Berechnung des internen Zinsfußes (Internal Rate of Return / persönliche Rendite) auf Basis der tatsächlichen Cashflows (Käufe, Verkäufe, Dividenden, Steuern, aktueller Marktwert als Terminal-Cashflow). Numerische Lösung (Newton-Raphson oder Bisection). → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-5** | **Berechnungsmodul (CAGR):** Durchschnittliche jährliche Wachstumsrate (Compound Annual Growth Rate) berechnet aus Einstandswert, aktuellem Marktwert und Haltedauer in Jahren. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-6** | **FIFO-Kostenbasismethode für Teilverkäufe:** Bei Teilverkäufen wird der Einstandspreis (Kostenbasis) nach FIFO (First In, First Out) ermittelt. Realized Gains werden auf Basis der FIFO-Kostenbasis berechnet. → [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-7** | **Benchmark-Vergleich (Tab: Benchmark):** Ein vorhandenes Wertpapier aus dem System dient als Benchmark-Index (Konfiguration in den Benutzereinstellungen). Der Tab zeigt parallele Wertentwicklung von Ziel-Wertpapier und Benchmark im gleichen Chart. Falls kein Benchmark konfiguriert ist, bleibt der Tab ausgeblendet oder zeigt einen Hinweis mit Link zu den Einstellungen. → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Kern-Feature | HIGH | 📋 Geplant |
| **FR-8** | **Sharpe Ratio (optional, per Einstellung):** Berechnung der Sharpe Ratio (Überrendite / Volatilität) sofern in den Benutzereinstellungen aktiviert. Der risikofreie Zinssatz ist als Dezimalzahl (z. B. 0,04 für 4 %) in den Einstellungen konfigurierbar. Ohne Aktivierung wird die Kennzahl nicht berechnet und nicht angezeigt. | Kern-Feature | HIGH | 📋 Geplant |
| **FR-9** | **Benutzereinstellungen für die Renditeanalyse:** Erweiterung des Benutzerprofils / Setup um: Benchmark-Wertpapier (Auswahl aus vorhandenen Securities), Sharpe-Ratio-Aktivierung (Bool), risikofreier Zinssatz (Decimal). Einstellungen sind per User gespeichert. → [ERM](../architecture/entity-relationship-model-renditeanalyse.md) | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-10** | **Robustheit bei fehlenden Kursen:** Fehlen Kursdaten für einzelne Tage (Wochenende, Feiertag, Datenlücke), wird der zuletzt bekannte Kurs (forward-fill) verwendet. Ist kein Kurs vorhanden, wird die Berechnung des betroffenen Zeitraums übersprungen und dem Nutzer ein Hinweis angezeigt (z. B. „Kursdaten ab [Datum] verfügbar"). | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-11** | **Drill-Down / Nachvollziehbarkeit:** Alle aggregierten Kennzahlen auf der Detailseite können auf die zugrundeliegenden Einzeltransaktionen aufgeschlüsselt werden (Klick öffnet gefilterte Postings-Liste oder Detail-Overlay). | UX / Accessibility | MEDIUM | 📋 Geplant |

---

## 3. Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Korrektheit finanzmathematischer Berechnungen:** TWR, IRR, CAGR, Volatilität und Sharpe Ratio müssen Industrie­standards (CFA Institute / GIPS-konform für TWR, XIRR-Definition für IRR) entsprechen. Abweichung < 0,01 % im Vergleich zu Referenzwerten (Excel XIRR / bekannte Vergleichsdaten). → [Architektur-Blueprint](../architecture/architecture-blueprint-renditeanalyse.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Performance / Ladezeiten:** Einfache Rendite-Box (FR-1) lädt vollständig in < 1 Sekunde für ein Wertpapier mit bis zu 10 Jahren Kurshistorie und 500 Transaktionen. Detailseite (FR-2) lädt vollständig in < 3 Sekunden für gleiche Datenmenge. | Performance | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Caching aggregierter Zeitreihen:** Berechnete Jahresrenditen, TWR-Zeitreihen und Chart-Datenpunkte werden server­seitig gecacht (In-Memory, TTL: 1 Stunde). Cache wird invalidiert bei neuen Transaktionen oder neuen Kurseinträgen des jeweiligen Wertpapiers. | Performance | MUST HAVE | 📋 Geplant |
| **NFR-4** | **Transparenz und Formel-Dokumentation:** Alle Berechnungsformeln sind im Code (XML-Dokumentation) sowie per Tooltip in der UI erläutert. Für die Detailseite existiert ein verlinkter Hilfe-Abschnitt / Glossar, der TWR, IRR, CAGR, Drawdown und Sharpe Ratio verständlich erklärt. | Wartbarkeit | HIGH | 📋 Geplant |
| **NFR-5** | **Erweiterbarkeit des Berechnungsmoduls:** Das Renditeberechnungsmodul (`IReturnCalculationService` o. Ä.) ist über eine klare Schnittstelle abstrahiert, sodass neue Kennzahlen ohne Änderung bestehender Berechnungslogik hinzugefügt werden können (Open/Closed Principle). | Wartbarkeit | HIGH | 📋 Geplant |
| **NFR-6** | **Robustheit bei ungültigen Eingaben:** Fehlende Kursdaten, Wertpapiere ohne Transaktionen, Zeiträume < 1 Tag führen nicht zu Ausnahmen in der UI, sondern zu definierten Fallback-Zuständen (Anzeige „–" oder Hinweistext). IRR-Berechnung bricht nach max. 100 Iterationen ab und liefert `null` (keine Lösung konvergiert). | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-7** | **Testabdeckung:** Unit Tests für alle Kernberechnungen (TWR, IRR, CAGR, Drawdown, Volatilität, FIFO-Kostenbasis) mit mindestens 5 Testfällen je Methode inkl. Grenzfällen (0 Transaktionen, nur Käufe, nur Verkäufe, Teilverkauf, Dividenden ohne Kauf). Integration Tests für den Rendite-API-Endpunkt. | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-8** | **Sicherheit / Datenzugriff:** Renditedaten sind strikt user-scopig: Kein Benutzer kann Rendite- oder Transaktionsdaten anderer Nutzer abrufen. Ownership-Check analog zu bestehenden API-Endpunkten. | Sicherheit | MUST HAVE | 📋 Geplant |
| **NFR-9** | **Lokalisierung:** Alle UI-Texte, Formel-Beschreibungen, Fehler- und Hinweistexte der Renditeanalyse sind in Deutsch und Englisch verfügbar (resx-Dateien). Datums- und Zahlenformate folgen der aktiven UI-Kultur (FA-I18N-006). | UX / Accessibility | HIGH | 📋 Geplant |
| **NFR-10** | **Barrierefreiheit (Accessibility):** Charts enthalten ARIA-Labels und alternative Tabellen­ansichten. Farbliche Feedback-Elemente (grün/rot) werden zusätzlich durch Icons oder Textannotationen ergänzt (nicht nur Farbe als einzige Unterscheidung). | UX / Accessibility | MEDIUM | 📋 Geplant |

---

## 4. Akzeptanzkriterien (User Stories)

### US-1 – Schnelle Renditeübersicht auf der Wertpapierseite

**Als** Endanwender  
**möchte ich** auf der Wertpapierseite sofort die wichtigsten Renditekennzahlen sehen,  
**damit ich** ohne Navigation auf eine weitere Seite eine Einschätzung des Investments erhalte.

**Akzeptanzkriterien:**

- [ ] Die Rendite-Box erscheint ohne explizites Nachladen (Lazy Load erlaubt, aber kein separater Klick nötig).
- [ ] Total Return wird in % und absolut (Währung) angezeigt.
- [ ] Positive Werte werden grün, negative Werte rot dargestellt; zusätzlich durch ▲/▼-Icon gekennzeichnet.
- [ ] Jede Kennzahl besitzt einen Tooltip mit der Berechnungsformel (mind. 1 Satz).
- [ ] Der Mini-Chart zeigt die Wertentwicklung korrekt für ein Wertpapier mit ≥ 1 Transaktion und ≥ 30 Kursdatenpunkten.
- [ ] Ladezeit der Box ≤ 1 Sekunde bei normalem Datenbestand (< 500 Transaktionen, < 10 Jahre Kursdaten).
- [ ] Bei fehlendem aktuellem Kurs erscheint ein Hinweis „Kein aktueller Kurs verfügbar" anstelle der Kennzahlen.

---

### US-2 – Detaillierte Renditeanalyse auf eigener Unterseite

**Als** Endanwender  
**möchte ich** eine dedizierte Seite mit vollständiger Renditeanalyse aufrufen,  
**damit ich** tiefgreifende zeitliche Auswertungen und alle Finanzkennzahlen untersuchen kann.

**Akzeptanzkriterien:**

- [ ] Route `/securities/{id}/performance` ist erreichbar und zeigt die Detailseite.
- [ ] Die Seite gliedert sich in Tabs: **Übersicht | Zeitliche Entwicklung | Cashflows | Kennzahlen | Benchmark**.
- [ ] Jeder Tab ist einzeln aufrufbar (URL-Fragment oder State) und zeigt korrekte Inhalte.
- [ ] Performance-Chart (Tab Übersicht) rendert für alle 6 Zeitraum-Optionen ohne Fehler.
- [ ] Jahresrenditen-Balkendiagramm enthält einen Balken pro abgeschlossenem Kalenderjahr und den laufenden Zeitraum YTD.
- [ ] Cashflow-Timeline listet alle Transaktionen in korrekter chronologischer Reihenfolge.
- [ ] Ladezeit der Seite ≤ 3 Sekunden bei normalem Datenbestand.

---

### US-3 – Korrektheit der Kennzahlen

**Als** Endanwender  
**möchte ich** dass die berechneten Kennzahlen finanzmathematisch korrekt sind,  
**damit ich** ihnen vertrauen kann.

**Akzeptanzkriterien:**

- [ ] TWR für einen Testkauf (100 Stück à 10 €, aktueller Kurs 15 €, keine Dividende) beträgt +50,00 %.
- [ ] IRR für einen einmaligen Kauf (1.000 € Investition, nach 1 Jahr 1.100 € Marktwert) beträgt +10,00 % p. a. (± 0,01 %).
- [ ] CAGR für ein 2-Jahres-Investment von 1.000 € auf 1.210 € beträgt +10,00 % p. a. (± 0,01 %).
- [ ] FIFO-Kostenbasis bei Teilverkauf aus 2 Kauflosen (100 Stück à 10 €, 50 Stück à 12 €) und Verkauf von 80 Stück ergibt Einstandswert 800 € (80 × 10 € FIFO).
- [ ] Nettorendite = Bruttorendite – Steuern; Steuern ≥ 0.

---

### US-4 – Benchmark-Vergleich

**Als** Endanwender  
**möchte ich** mein Wertpapier mit einem selbst gewählten Benchmark vergleichen,  
**damit ich** beurteile, ob mein Investment den Markt schlägt.

**Akzeptanzkriterien:**

- [ ] In den Benutzereinstellungen kann ein Wertpapier als Benchmark ausgewählt werden (Freitext-Suche + Auswahl aus Dropdown).
- [ ] Benchmark-Wertpapier kann jederzeit geändert oder entfernt werden.
- [ ] Tab „Benchmark" ist nur sichtbar, wenn ein Benchmark konfiguriert ist.
- [ ] Chart zeigt zwei Linien (Portfoliowertentwicklung normiert + Benchmarkkurs normiert auf Basis 100 am Kaufdatum).
- [ ] Wenn für den Benchmark einzelne Kursdaten fehlen, wird forward-fill angewendet (kein Fehler).

---

### US-5 – Sharpe Ratio (optional)

**Als** Endanwender  
**möchte ich** die Sharpe Ratio nur sehen, wenn ich sie explizit aktiviert habe,  
**damit ich** nicht mit Kennzahlen konfrontiert werde, die ich nicht interpretieren möchte.

**Akzeptanzkriterien:**

- [ ] Sharpe Ratio ist standardmäßig deaktiviert und nicht sichtbar.
- [ ] In den Einstellungen kann die Sharpe Ratio aktiviert werden; risikofreier Zinssatz ist als Dezimalwert eingeb­bar (z. B. „0,04" für 4 %).
- [ ] Nach Aktivierung erscheint die Sharpe Ratio im Tab „Kennzahlen".
- [ ] Eingabe eines negativen risikofreien Zinssatzes wird mit Validierungsmeldung abgewiesen.

---

## 5. Annahmen und Abhängigkeiten

| # | Typ | Beschreibung |
|---|-----|--------------|
| A-1 | Annahme | Für jedes Wertpapier existieren ausreichend `SecurityPrice`-Einträge (tagesgenau). Lücken werden per forward-fill geschlossen. |
| A-2 | Annahme | Die `Posting`-Entität enthält alle notwendigen Felder (Kind, SecurityId, SecuritySubType, Amount, Quantity, BookingDate) in validem Zustand. |
| A-3 | Annahme | Dividenden werden dem Ex-Datum zugeordnet (BookingDate des zugehörigen Postings). |
| A-4 | Annahme | Die FIFO-Reihenfolge basiert auf dem `BookingDate` der Kauf-Postings; bei gleichem Datum auf der Datenbankreihenfolge (Einfügungsreihenfolge). |
| A-5 | Annahme | AlphaVantage-Kurse sind in der Basiswährung des jeweiligen Wertpapiers (CurrencyCode). Währungsumrechnung ist Out of Scope für Phase 1. |
| A-6 | Annahme | Benutzereinstellungen (Benchmark, Sharpe-Konfiguration) werden in der bestehenden `UserPreferences`-Tabelle oder einer neuen `UserSecuritySettings`-Tabelle gespeichert. |
| D-1 | Abhängigkeit | Bestehende `ISecurityReportService`-Implementierung (Dividendenaggregation) kann als Basis für Dividendenberechnungen referenziert werden. |
| D-2 | Abhängigkeit | AlphaVantage-Integration (täglicher Kursabruf, FA-WERT-004) muss stabil funktionieren; ohne Kursdaten ist die Renditeanalyse eingeschränkt. |
| D-3 | Abhängigkeit | Chart-Bibliothek muss Zeitreihen-Liniendiagramme, Balkendiagramme und Heatmaps unterstützen (Anforderungsklärung welche Bibliothek im Architektur-Blueprint). |
| D-4 | Abhängigkeit | Benutzereinstellungen-UI (Setup-Seite) muss um neue Felder erweiterbar sein (bestehende Infrastruktur). |
| D-5 | Abhängigkeit | NFA-REL-003 (Hintergrundaufgaben-Queue) kann für rechenintensive Vorberechnungen (Cache-Rebuild) genutzt werden. |

---

## 6. Scope und Out-of-Scope

### In-Scope ✅

- Einfache Rendite-Box auf der Wertpapier-Detailseite
- Mini-Chart (Sparkline) auf der Wertpapierseite
- Detailseite `/securities/{id}/performance` mit 5 Tabs
- Kennzahlen: Total Return, CAGR, TWR, IRR, Volatilität, Drawdown, Dividendenrendite, Steuerquote, Realized/Unrealized Gains
- Sharpe Ratio (opt-in per Benutzereinstellung, risikofreier Zinssatz konfigurierbar)
- Jahres- und Monatsrenditen (Balkendiagramm / Heatmap)
- Cashflow-Timeline (Käufe, Verkäufe, Dividenden, Steuern)
- Performance-Chart mit Zeitraumauswahl
- Dividendenverlaufsdiagramm
- Kosten-/Steuerverlauf
- Benchmark-Vergleich (ein Wertpapier aus dem System, optional)
- FIFO-Kostenbasismethode für Teilverkäufe
- Forward-Fill bei fehlenden Kursdaten
- Server-seitiges Caching (In-Memory, TTL 1h)
- Benutzereinstellungen: Benchmark-Wertpapier, Sharpe-Aktivierung, risikofreier Zinssatz
- Formel-Tooltips und Drill-Down auf Einzeltransaktionen
- Lokalisierung DE/EN

### Out-of-Scope ❌

- Export der Rendite­daten (CSV, Excel, PDF)
- Push-Benachrichtigungen bei Rendite-Schwellwerten
- Vergleich mehrerer eigener Wertpapiere untereinander (Portfolio-Ebene)
- Steueroptimierungshinweise
- Währungsumrechnung bei Wertpapieren in Fremdwährung (Phase 1)
- Externe Benchmark-Daten (nur interne Wertpapiere aus dem System)
- Persistentes Caching (Datenbank-Caching; nur In-Memory Phase 1)
- Mobile-App-spezifische Renditeansicht (MAUI)
- Automatisierte Steuerberichte / Steuererklärungshilfe

---

## 7. Domänenmodell und Glossar

### 7.1 Schlüsselentitäten und Beziehungen

```
Security (1) ──────────────── (n) SecurityPrice
    │                              (Date, Close)
    │
    └──── (n) Posting
               (Kind, SecuritySubType, Amount, Quantity,
                BookingDate, ValutaDate)
                    │
                    ├── PostingKind.Security
                    └── SecurityPostingSubType
                        ├── Buy       ← Kauf
                        ├── Sell      ← Verkauf
                        ├── Dividend  ← Dividende
                        └── Tax       ← Steuer / Quellensteuer

User (1) ──── (1) UserPreferences / UserSecuritySettings
                  (BenchmarkSecurityId, SharpeEnabled,
                   RiskFreeRate)
```

> Vollständiges ERM: → [entity-relationship-model-renditeanalyse.md](../architecture/entity-relationship-model-renditeanalyse.md)

### 7.2 Neue Konzepte / Berechnungsdomäne

```
ReturnCalculationInput
    Security, DateRange, Postings[], PriceTimeSeries[]

ReturnCalculationResult
    TotalReturn (%), TotalReturnAbsolute (Währung)
    Cagr (%), Twr (%), Irr (%)
    Volatility (%), MaxDrawdown (%)
    SharpeRatio (? | null)
    DividendYieldPerYear [Year → %]
    TaxRate (%)
    RealizedGain, UnrealizedGain (Währung)
    FifoCostBasis [Lot[]]

PriceSeries
    Date → decimal Close (forward-filled)

CashflowItem
    Date, Type (Buy|Sell|Dividend|Tax), Amount

BenchmarkComparison
    NormalizedPortfolioSeries [Date → decimal]
    NormalizedBenchmarkSeries [Date → decimal]
```

### 7.3 Glossar

| Begriff | Definition |
|---------|-----------|
| **TWR** (Time-Weighted Return) | Rendite, die den Einfluss von Zu- und Abflüssen eliminiert; geeignet für Vergleichbarkeit. Berechnung: Verkettung von Sub-Perioden-Renditen zwischen Cashflow-Ereignissen. |
| **IRR** (Internal Rate of Return / XIRR) | Persönliche Rendite auf Basis der tatsächlichen Cashflows und deren Zeitpunkte. Entspricht dem Diskontsatz, bei dem der Kapitalwert der Cashflows = 0. |
| **CAGR** (Compound Annual Growth Rate) | Durchschnittliche jährliche Wachstumsrate. Formel: `(Endwert / Anfangswert)^(1/Jahre) – 1`. |
| **Sharpe Ratio** | `(Portfoliorendite – risikofreier Zinssatz) / Volatilität`. Maß für die risikobereinige Rendite. |
| **Volatilität** | Annualisierte Standardabweichung der täglichen logarithmischen Renditen. |
| **Maximaler Drawdown** | Größter prozentualer Rückgang vom lokalen Höchststand bis zum nachfolgenden Tiefstwert innerhalb einer Zeitreihe. |
| **Realized Gain** | Tatsächlich realisierter Gewinn/Verlust aus vollständig oder teilweise verkauften Positionen, berechnet nach FIFO-Kostenbasis. |
| **Unrealized Gain** | Noch nicht realisierter Gewinn/Verlust der offenen Position (Marktwert – FIFO-Kostenbasis des verbleibenden Bestands). |
| **FIFO** (First In, First Out) | Methode zur Kostenbasisermittlung: Die zuerst gekauften Anteile gelten als zuerst verkauft. |
| **Ex-Tag (Ex-Dividend Date)** | Stichtag, ab dem eine Aktie ohne Anspruch auf die nächste Dividende gehandelt wird. Im System: `BookingDate` des Dividenden-Postings. |
| **Forward-Fill** | Ersetzt fehlende Kursdaten durch den zuletzt bekannten Wert (Carry-Forward). |
| **Benchmark** | Ein Referenz-Wertpapier im System, gegen das die Wertentwicklung des analysierten Wertpapiers verglichen wird. |
| **Einstandskurs** | Durchschnittlicher Kaufpreis pro Aktie auf Basis aller Käufe (mengengewichtet). |
| **Total Return** | Gesamtrendite: Kursgewinn + erhaltene Dividenden – gezahlte Steuern, bezogen auf das eingesetzte Kapital. |

---

## 8. Nutzungsfälle (Use Cases)

### UC-1: Rendite-Schnellübersicht aufrufen

**Akteur:** Endanwender  
**Vorbedingung:** Mindestens eine Transaktion (Kauf) für das Wertpapier vorhanden; Wertpapier-Detailseite geöffnet.  
**Normalablauf:**

1. Nutzer öffnet die Wertpapierseite.
2. System lädt Rendite-Box asynchron (Lazy Load).
3. System berechnet Total Return, CAGR, Einstandskurs, aktueller Kurs, Marktwert, IRR aus gecachten oder frisch berechneten Daten.
4. Rendite-Box zeigt alle Kennzahlen mit farblichem Feedback.
5. Mini-Chart rendert die Wertentwicklung.
6. Nutzer hoverd über eine Kennzahl → Tooltip zeigt Berechnungsformel.

**Alternativer Ablauf (A1) – Kein aktueller Kurs:**

3a. Kein `SecurityPrice` für die letzten 7 Tage vorhanden.  
3b. System zeigt Hinweis „Kein aktueller Kurs verfügbar – zuletzt: [Datum]".  
3c. Kennzahlen, die aktuellen Kurs benötigen (Marktwert, TWR), werden mit „–" angezeigt.

**Alternativer Ablauf (A2) – Keine Transaktionen:**

3a. Keine Kauf-Postings für das Wertpapier vorhanden.  
3b. Rendite-Box zeigt „Noch keine Transaktionen erfasst".

---

### UC-2: Detaillierte Renditeanalyse öffnen

**Akteur:** Endanwender  
**Vorbedingung:** Wertpapier mit mindestens einer Transaktion vorhanden.  
**Normalablauf:**

1. Nutzer klickt auf „Detaillierte Analyse" (Button/Link auf Wertpapierseite) oder navigiert direkt zu `/securities/{id}/performance`.
2. System lädt die Detailseite und wählt standardmäßig Tab „Übersicht".
3. Performance-Chart erscheint mit Zeitraum „Gesamt".
4. Nutzer wechselt Tab zu „Zeitliche Entwicklung" → Jahresrenditen-Balkendiagramm und Dividendenverlauf erscheinen.
5. Nutzer wechselt zu „Cashflows" → Timeline und Steuerverlauf erscheinen.
6. Nutzer wechselt zu „Kennzahlen" → Erweiterte KPIs werden angezeigt.
7. Nutzer wechselt zu „Benchmark" (nur wenn konfiguriert) → Vergleichschart erscheint.

**Alternativer Ablauf (A1) – Kein Benchmark konfiguriert:**

7a. Tab „Benchmark" enthält Hinweis: „Kein Benchmark konfiguriert. [Einstellungen öffnen →]"

---

### UC-3: Benchmark konfigurieren

**Akteur:** Endanwender  
**Vorbedingung:** Mindestens zwei Wertpapiere im System vorhanden.  
**Normalablauf:**

1. Nutzer öffnet Setup → Abschnitt „Wertpapier-Einstellungen" (oder Benutzer­profil).
2. Nutzer wählt im Dropdown „Benchmark-Wertpapier" ein vorhandenes Wertpapier aus.
3. Nutzer aktiviert optional „Sharpe Ratio anzeigen" und gibt risikofreien Zinssatz ein.
4. Nutzer speichert.
5. System persistiert Einstellungen.
6. Beim nächsten Öffnen der Renditedetailseite ist Tab „Benchmark" sichtbar und Chart lädt korrekt.

---

### UC-4: Cashflow-Timeline analysieren

**Akteur:** Endanwender  
**Vorbedingung:** Mehrere Transaktionen (Käufe, Dividenden, Steuern) vorhanden.  
**Normalablauf:**

1. Nutzer öffnet Tab „Cashflows" der Renditedetailseite.
2. System zeigt chronologische Liste aller Cashflows mit Datum, Typ (Icon), Betrag (farblich: Zu-/Abfluss).
3. Nutzer klickt auf einen Cashflow-Eintrag.
4. System öffnet Detail-Overlay oder navigiert zur gefilterten Postings-Ansicht des Wertpapiers.

---

### UC-5: Teilverkauf mit FIFO nachvollziehen

**Akteur:** Endanwender  
**Vorbedingung:** Mindestens zwei Käufe zu unterschiedlichen Preisen, danach ein Teilverkauf vorhanden.  
**Normalablauf:**

1. Nutzer öffnet Tab „Kennzahlen".
2. System zeigt „Realized Gains" mit Wert.
3. Nutzer klickt auf Drill-Down-Link neben „Realized Gains".
4. System öffnet FIFO-Auflösung: Tabelle zeigt alle Kauflose (Datum, Menge, Preis) und welche Lose für den Verkauf verwendet wurden.

---

## 9. Nächste Schritte

| # | Aufgabe | Priorität | Abhängigkeit |
|---|---------|-----------|--------------|
| 1 | Architektur-Blueprint erstellen (`docs/architecture/architecture-blueprint-renditeanalyse.md`): Schichtdiagramm, Service-Schnittstellen (`IReturnCalculationService`), Caching-Strategie, Chart-Bibliothekswahl | MUST HAVE | – |
| 2 | Entity-Relationship-Modell erstellen (`docs/architecture/entity-relationship-model-renditeanalyse.md`): Erweiterung UserPreferences, neues `ReturnCache`-Modell (optional) | MUST HAVE | Schritt 1 |
| 3 | Domain-Modell erweitern: FIFO-Lotberechnung, `ReturnCalculationInput`/`Result`-Records | MUST HAVE | Schritt 2 |
| 4 | `IReturnCalculationService` implementieren: TWR, IRR (Newton-Raphson), CAGR, Volatilität, Drawdown, Sharpe, FIFO | MUST HAVE | Schritt 3 |
| 5 | Unit Tests für alle Berechnungsmethoden (min. 5 Fälle je Methode) | MUST HAVE | Schritt 4 |
| 6 | API-Endpunkt `GET /api/securities/{id}/performance` implementieren | MUST HAVE | Schritt 4 |
| 7 | Caching-Layer implementieren (In-Memory, TTL 1h, Invalidierung bei neuen Postings/Kursen) | HIGH | Schritt 6 |
| 8 | Blazor-Komponente: Rendite-Box + Mini-Chart auf Wertpapierseite | MUST HAVE | Schritt 6 |
| 9 | Blazor-Seite: `/securities/{id}/performance` mit 5 Tabs und Charts | MUST HAVE | Schritt 6 |
| 10 | Benutzereinstellungen (Benchmark, Sharpe, risikofreier Zinssatz) im Setup-Bereich | HIGH | Schritt 6 |
| 11 | Lokalisierungstexte DE/EN für alle neuen UI-Elemente | HIGH | Schritt 8/9 |
| 12 | Integrationstests (API-Endpunkt End-to-End) | HIGH | Schritt 6 |
| 13 | Anforderungsstatus in `docs/Anforderungsstatus.md` aktualisieren | MEDIUM | Schritt 9 |

---

## 10. Approval & Versionierung

| Version | Datum | Autor | Änderung |
|---------|-------|-------|----------|
| 0.1 | 2025-01-01 | (auszufüllen) | Initiale Erstellung |

**Review ausstehend:** Architektur-Blueprint und ERM sind noch zu erstellen; nach deren Erstellung wird dieses Dokument um konkrete Verweise auf Schnittstellen und Datenmodell ergänzt (Version 0.2).

**Offene Fragen:**

| # | Frage | Verantwortlich | Status |
|---|-------|----------------|--------|
| OQ-1 | Welche Chart-Bibliothek wird verwendet (Blazor-kompatibel, Lizenz, Performance)? | Architekt/Entwickler | Offen |
| OQ-2 | Wird `UserPreferences` erweitert oder eine neue `UserSecuritySettings`-Tabelle angelegt? | Architekt/Entwickler | Offen |
| OQ-3 | Soll IRR auch für das Gesamtportfolio (alle Wertpapiere) berechnet werden, oder nur pro Wertpapier? | Product Owner | Offen (Out of Scope Phase 1 vorgeschlagen) |
| OQ-4 | Genügt In-Memory-Caching, oder ist persistentes Caching (DB-Tabelle für aggregierte Zeitreihen) ab Phase 1 nötig? | Architekt/Entwickler | Offen |
| OQ-5 | Wie wird mit Wertpapieren in Fremdwährung umgegangen (Kurs in USD, Konto in EUR)? | Product Owner | Offen (Phase 1: Out of Scope) |

---

*Letzte Aktualisierung: 2025-01-01 (Initiale Erstellung)*
