# Anforderungen – Feature: Budget Impact Visibility während Buchung

## Überblick und Projektkontext

Das Feature erweitert den Buchungsprozess im FinanceManager um eine transparente Budget-Wirkungsanalyse in Echtzeit. Während der Erfassung und nach dem Abschluss einer Buchung soll sofort ersichtlich sein, ob Budgetzwecke überschritten, nahezu ausgeschöpft oder in der Zielerreichung deutlich verändert werden.

**Geschäftsziele**
- Früherkennung von Budgetüberschreitungen direkt im Buchungsprozess
- Transparenz über betroffene Budgetzwecke und Zielerreichungsänderungen
- Schnellere, fundierte Entscheidungen des Nutzers während der Buchung

**Stakeholder**
- Endnutzer (Privatanwender im Buchungsprozess)
- Produktverantwortliche
- Entwicklungsteam (Backend, Frontend, Regel-Engine)
- QA/Testing

**Abgrenzung**
Im Scope liegt die Bewertung von Budgetauswirkungen während und unmittelbar nach einer Buchung inkl. Hinweis- und Zusammenfassungslogik. Außerhalb liegt eine automatische Budgetanpassung oder autonome Neuplanung von Budgetzielen.

## Funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **FR-1** | **Automatische Budget-Impact-Prüfung:** Sobald eine Buchung einem Kontakt oder Sparplan zugeordnet ist, prüft das System Budgetüberschreitung und Veränderung der Zielerreichung auf Basis von `BudgetPurpose.SourceType`, `BudgetRules`, dynamischen Zielwerten und Ist-Werten inkl. aktueller Buchung. → [Architektur-Blueprint Budget Impact Booking](../architecture/architecture-blueprint-budget-impact-booking.md) · [ERM Budget Impact Booking](../architecture/entity-relationship-model-budget-impact-booking.md) · [Architecture Review Budget Impact Booking](../improvements/review-architecture-budget-impact-booking.md) | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-1.1** | **Trigger bei Zuordnungsereignis:** Die Prüfung startet automatisch, sobald Kontakt oder Sparplan im Buchungsdialog eindeutig bestimmt ist. | Datenverwaltung | MUST HAVE | 📋 Geplant |
| **FR-1.2** | **Mehrzweck-Bewertung:** Bei einer Buchung mit Wirkung auf mehrere Budgetzwecke werden alle betroffenen Zwecke einzeln bewertet und konsolidiert angezeigt. | Reporting & Analyse | HIGH | 📋 Geplant |
| **FR-2** | **Sofort-Hinweis bei kritischem Budgetzustand:** Bei Überschreitung, fast ausgeschöpftem Budget oder stark veränderter Zielerreichung wird unmittelbar ein kategorisierter Hinweis angezeigt. → [Architektur-Blueprint Budget Impact Booking](../architecture/architecture-blueprint-budget-impact-booking.md) | UX / Accessibility | MUST HAVE | 📋 Geplant |
| **FR-2.1** | **Hinweiskategorien:** Das System unterscheidet mindestens die Kategorien „Budget überschritten“, „Budget fast ausgeschöpft“ und „Zielerreichung stark verändert“. | Kern-Feature | MUST HAVE | 📋 Geplant |
| **FR-3** | **Zusammenfassung nach Buchungsabschluss:** Nach dem Speichern zeigt das System eine Summary mit Zielerreichung vorher/nachher, Delta sowie allen betroffenen Budgetzwecken. → [Architektur-Blueprint Budget Impact Booking](../architecture/architecture-blueprint-budget-impact-booking.md) · [ERM Budget Impact Booking](../architecture/entity-relationship-model-budget-impact-booking.md) | Reporting & Analyse | MUST HAVE | 📋 Geplant |
| **FR-4** | **Transparente Betroffenheitsdarstellung:** Nutzer sieht je betroffenem Budgetzweck den Status, die neue Auslastung und den Grund der Auswirkung nachvollziehbar im Buchungsfluss. | Reporting & Analyse | HIGH | 📋 Geplant |

## Nicht-funktionale Anforderungen

| Kennung | Beschreibung | Kategorie | Priorität | Status |
|---------|--------------|-----------|-----------|--------|
| **NFR-1** | **Echtzeit-Reaktionszeit Hinweise:** Nach relevanter Änderung im Buchungsdialog erscheint ein Budget-Hinweis in **< 500 ms** (95. Perzentil). → [Architektur-Blueprint Budget Impact Booking](../architecture/architecture-blueprint-budget-impact-booking.md) | Performance | MUST HAVE | 📋 Geplant |
| **NFR-2** | **Konsistente Ergebnisbildung:** Berechnete Vorher/Nachher/Delta-Werte in Sofort-Hinweis und Abschluss-Summary müssen in **100 %** der Fälle identisch sein. → [Architecture Review Budget Impact Booking](../improvements/review-architecture-budget-impact-booking.md) | Zuverlässigkeit | MUST HAVE | 📋 Geplant |
| **NFR-3** | **Nachvollziehbarkeit und Verständlichkeit:** Hinweise und Summary müssen ohne Domänenvorwissen verständlich sein; in Usability-Tests erreichen mindestens **85 %** der Nutzer korrekte Interpretation. | UX / Accessibility | HIGH | 📋 Geplant |
| **NFR-4** | **Regelbasierte Wartbarkeit:** Schwellenwerte und Kategorien sind zentral über BudgetRules konfigurierbar, ohne UI-Codeänderung in mehr als **1** Komponente. → [Architektur-Blueprint Budget Impact Booking](../architecture/architecture-blueprint-budget-impact-booking.md) · [ERM Budget Impact Booking](../architecture/entity-relationship-model-budget-impact-booking.md) | Wartbarkeit | HIGH | 📋 Geplant |
| **NFR-5** | **Fehlertoleranz bei unvollständigen Daten:** Ist eine Prüfung mangels Zuordnung noch nicht möglich, wird ein neutraler Status angezeigt; das System erzeugt keine irreführenden Warnungen (Fehlwarnrate **< 2 %** in Testdatensatz). | Zuverlässigkeit | MEDIUM | 📋 Geplant |

## Akzeptanzkriterien

### User Story 1 – Sofortige Budgetwarnung während Buchung
**Als** Nutzer  
**möchte ich** bei einer budgetkritischen Buchung sofort einen klaren Hinweis sehen,  
**damit** ich vor dem Speichern reagieren kann.

**Akzeptanzkriterien (SMART)**
1. Sobald Kontakt oder Sparplan bestimmt ist, startet die Budgetprüfung automatisch ohne manuelle Aktion.
2. Wird ein Budget überschritten, erscheint innerhalb von 500 ms ein Hinweis der Kategorie „Budget überschritten“.
3. Bei „fast ausgeschöpft“ und „stark verändert“ werden die jeweiligen Kategorien korrekt und eindeutig angezeigt.

### User Story 2 – Transparente Auswirkung auf Budgetzwecke
**Als** Nutzer  
**möchte ich** die betroffenen Budgetzwecke inkl. Veränderung sehen,  
**damit** ich die Auswirkungen der Buchung nachvollziehen kann.

**Akzeptanzkriterien (SMART)**
1. Für jeden betroffenen Budgetzweck werden Name, Status und Zielerreichungsänderung angezeigt.
2. Bei Buchungen mit mehreren betroffenen Budgetzwecken listet das System alle betroffenen Zwecke vollständig auf.
3. In einem Testkatalog werden mindestens 95 % der erwarteten betroffenen Zwecke korrekt angezeigt.

### User Story 3 – Abschluss-Summary mit Vorher/Nachher/Delta
**Als** Nutzer  
**möchte ich** nach dem Speichern eine verständliche Summary sehen,  
**damit** ich die finale Budgetwirkung bewerten kann.

**Akzeptanzkriterien (SMART)**
1. Die Summary enthält für betroffene Budgetzwecke jeweils Vorher-, Nachher- und Delta-Wert der Zielerreichung.
2. Die Werte der Summary stimmen in 100 % der Testfälle mit den zuvor angezeigten Sofort-Hinweisen überein.
3. Der Nutzer kann anhand der Summary klar erkennen, ob Handlungsbedarf besteht.

## Annahmen und Abhängigkeiten

| Typ | Beschreibung |
|-----|--------------|
| Annahme | `BudgetPurpose.SourceType`, `BudgetRules` und aktuelle Ist-Werte sind zur Buchungszeit konsistent verfügbar. |
| Annahme | Dynamische Zielwerte können synchron zur Buchung berechnet oder gelesen werden. |
| Abhängigkeit | Fachliche und technische Ausgestaltung erfolgt im [Architektur-Blueprint Budget Impact Booking](../architecture/architecture-blueprint-budget-impact-booking.md). |
| Abhängigkeit | Datenmodellierung und Beziehungen werden im [ERM Budget Impact Booking](../architecture/entity-relationship-model-budget-impact-booking.md) präzisiert. |
| Abhängigkeit | Risiko-/Qualitätsprüfung der Architektur erfolgt über [Architecture Review Budget Impact Booking](../improvements/review-architecture-budget-impact-booking.md). |

## Scope und Out-of-Scope

**In-Scope ✅**
- Automatische Budget-Impact-Prüfung bei bestimmtem Kontakt/Sparplan
- Sofort-Hinweise mit definierten Hinweis-Kategorien
- Abschluss-Summary mit Vorher/Nachher/Delta und betroffenen Budgetzwecken
- Transparente Darstellung aller betroffenen Budgetzwecke im Buchungsfluss

**Out-of-Scope ❌**
- Automatische Anpassung von Budgetlimits oder Zielwerten
- Vollautomatische Korrektur von Buchungen ohne Nutzerentscheidung
- Strategische Budgetplanung über den einzelnen Buchungsvorgang hinaus

## Domänenmodell und Glossar

**Schlüsselentitäten**
- **Booking**: Zu speichernde Transaktion im Buchungsprozess
- **BudgetPurpose**: Budgetzweck mit Quelle (`SourceType`) und Zieldefinition
- **BudgetRule**: Regelwerk für Schwellenwerte und Bewertungskategorien
- **BudgetImpactEvaluation**: Ergebnis der Impact-Prüfung (Status, Scores, Delta)
- **BookingImpactSummary**: Abschlussdarstellung mit aggregierten Ergebnisdaten

**Beziehungen (vereinfacht)**
- Booking 1..n BudgetImpactEvaluation
- BudgetImpactEvaluation n..1 BudgetPurpose
- BudgetPurpose 1..n BudgetRule
- Booking 1..1 BookingImpactSummary

**Glossar**
- **Zielerreichung**: Verhältnis aus Ist-Wert zu dynamischem Zielwert eines Budgetzwecks
- **Delta**: Differenz zwischen Zielerreichung vor und nach Buchung
- **Budget fast ausgeschöpft**: Schwellwertnahe Auslastung gemäß BudgetRules
- **Stark verändert**: Signifikante Zielerreichungsänderung gemäß Regeldefinition

## Nutzungsfälle

### UC-1: Echtzeitbewertung während Buchung
**Akteure:** Nutzer, FinanceManager UI, Regel-Engine  
**Vorbedingungen:** Buchung in Bearbeitung, Kontakt oder Sparplan bestimmt  
**Ablauf:**
1. Nutzer wählt/ändert Kontakt oder Sparplan.
2. System lädt relevante Budgetzwecke und Regeln.
3. System berechnet Budgetwirkung inkl. Zielerreichungsänderung.
4. Bei Bedarf wird ein kategorisierter Sofort-Hinweis eingeblendet.  
**Nachbedingungen:** Aktueller Budgetstatus zur Buchung ist transparent sichtbar.

### UC-2: Hinweise interpretieren und Buchung anpassen
**Akteure:** Nutzer, FinanceManager UI  
**Vorbedingungen:** Mindestens ein Hinweis wurde erzeugt  
**Ablauf:**
1. Nutzer sieht Hinweis inkl. Kategorie.
2. Nutzer prüft betroffene Budgetzwecke.
3. Nutzer entscheidet, Buchung anzupassen oder fortzufahren.  
**Nachbedingungen:** Buchung wurde bewusst bestätigt oder angepasst.

### UC-3: Abschluss-Summary nach Speichern
**Akteure:** Nutzer, FinanceManager Backend/UI  
**Vorbedingungen:** Buchung erfolgreich gespeichert  
**Ablauf:**
1. System erstellt Summary aus finalen Impact-Daten.
2. System zeigt Vorher/Nachher/Delta je betroffenem Budgetzweck.
3. Nutzer bestätigt Kenntnisnahme der Auswirkungen.  
**Nachbedingungen:** Budgetwirkung der Buchung ist nachvollziehbar dokumentiert.

## Nächste Schritte

1. Architektur-Blueprint für Budget-Impact-Bewertung konkretisieren.
2. ERM für BudgetImpactEvaluation und BookingImpactSummary finalisieren.
3. Schwellwerte und Kategorieregeln fachlich abstimmen und in BudgetRules überführen.
4. UX-Entwurf für Sofort-Hinweise und Abschluss-Summary spezifizieren.
5. Akzeptanztests für Echtzeitverhalten, Korrektheit und Verständlichkeit definieren.

## Approval & Versionierung

| Version | Datum | Autor | Änderungen | Freigabe |
|---------|-------|-------|-----------|----------|
| 0.1 | 2026-05-31 | Copilot (planning-requirements-analysis) | Initiale Erstellung der Anforderungen für „Budget Impact Visibility während Buchung“ | Ausstehend |
