← [Zurück zur Übersicht](index.md)

# Sparpläne — Business Rules

## Wiederkehrende Ziele werden automatisch fortgeschrieben

**Beschreibung:** Bei wiederkehrenden Sparplänen wird ein überfälliges Zieldatum automatisch in die Zukunft verschoben.

**Bedingungen:**
- `Type = Recurring`
- `Interval` und `TargetDate` sind gesetzt.

**Verhalten:**
- Zieltermin wird in Intervallschritten erhöht, bis er nach dem Stichtag liegt.

**Umsetzung:** `SavingsPlan.AdvanceTargetDateIfDue`.

## Monatsende bleibt Monatsende

**Beschreibung:** Intervallfortschreibung bewahrt Monatsend-Semantik.

**Bedingungen:**
- Ursprungsdatum liegt am Monatsende.

**Verhalten:**
- Neues Datum wird ebenfalls auf das Monatsende des Zielmonats gesetzt.

**Umsetzung:** `SavingsPlan.AddIntervalWithMonthEndRule`.

## Detailansicht zeigt Sparfortschritt

**Beschreibung:** Die Detailansicht eines bestehenden Sparplans zeigt aktuelle Fortschrittskennzahlen an.

**Bedingungen:**
- Ein bestehender Sparplan wurde geladen.

**Verhalten:**
- `CurrentAmount` wird als aktueller Saldo angezeigt.
- `RemainingAmount` wird als Restbetrag angezeigt.
- Beide Felder sind nicht editierbar.

**Umsetzung:** `SavingsPlanCardViewModel.BuildCardRecordAsync`.

## Benötigter Monatsbetrag nur für offene Einmalziele

**Beschreibung:** Der benötigte Monatsbetrag wird nur angezeigt, wenn er für die Planung eines einmaligen Sparziels relevant ist.

**Bedingungen:**
- `Type = OneTime`
- `RemainingAmount > 0`
- `TargetDate` liegt nach dem heutigen Datum.
- Die Analyse liefert `RequiredMonthly > 0`.

**Verhalten:**
- In der Detailansicht erscheint das nicht editierbare Feld `Card_Caption_SavingsPlan_RequiredMonthly`.
- Bei wiederkehrenden Sparplänen, vollständig angesparten Zielen oder Zieldatum heute beziehungsweise in der Vergangenheit wird das Feld ausgeblendet.

**Umsetzung:** `SavingsPlanCardViewModel.BuildCardRecordAsync` mit Daten aus `SavingsPlanService.AnalyzeAsync`.

## Kontoverknüpfung steuert Saldo und Analyse

**Beschreibung:** Ein Sparplan kann mit mehreren eigenen Konten verknüpft werden; ein Konto kann mehrere Pläne bedienen.

**Bedingungen:**
- Plan und Konto gehören demselben Eigentümer; doppelte Verknüpfungen werden abgewiesen.
- Es sind ausschließlich Konten vom Typ `Savings` (Sparkonto) verknüpfbar — die Auswahl und der Service lehnen andere Kontotypen ab.

**Verhalten:**
- Ab dem ersten Link ist `CurrentAmount`/`accumulatedAmount` die Summe der verknüpften Kontosalden (additive Summe, keine Aufteilung bei Sammelkonten).
- Die Rhythmus-/Defizitanalyse nutzt den Konto-Inflow (alle Zuflüsse inkl. Einzüge, VL, Zinsen).
- Plan-Buchungen mit Funding-Buchungsgruppe (Bank-Gegenposting) werden als „informell" gekennzeichnet und zählen nicht in `accumulated`/`expectedToDate`.
- Nach Entfernen des letzten Links gilt wieder die buchungsbasierte Auswertung.
- Bei verknüpften Plänen wird zusätzlich der „Eigenanteil" (Summe der Plan-eigenen Buchungen) in den Kopfdaten ausgewiesen (`SavingsPlanDto.OwnAmount`).
- Kontoauszugszeilen eines verknüpften Kontos können keinem Sparplan zugeordnet werden (manuell und per Auto-Klassifizierung); die Zuordnung wird mit `SAVINGSPLAN_ACCOUNT_LINKED` abgelehnt.

**Umsetzung:** `SavingsPlanAccountLinkService`, `SavingsPlanLinkAnalysis`, Endpunkte `…/linked-accounts` sowie `GET /api/accounts/{id}/savings-plan-links`, UI-Abschnitt `SavingsPlanLinkedAccountsPanel`.

## Istmeldung korrigiert den Plan-Saldo ohne Kontobewegung

**Beschreibung:** Für Pläne ohne verknüpfte Konten kann der Ist-Saldo per Istmeldung gesetzt werden.

**Bedingungen:**
- Der Plan hat keine verknüpften Konten (bei Links ist der Kontostand die Ist-Quelle).

**Verhalten:**
- Es entsteht eine Plan-Buchung in Höhe `gemeldeter Saldo − accumulated` mit `SavingsPlanSubType = ActualReport` und ohne `AccountId` — keine Kontobewegung.
- Istmeldungen sind in Liste und Detail als Korrektur gekennzeichnet, gelten nicht als informell und fließen nicht in Budget-Actuals.

**Umsetzung:** `SavingsPlanService.ReportActualBalanceAsync`, Endpunkt `…/actual-reports`, Ribbon-Aktion „Istmeldung" + Overlay `SavingsPlanActualReportPanel`.
