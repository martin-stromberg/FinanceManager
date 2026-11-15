# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweiter Prüflauf: Alle vier im ersten Lauf (`plan-check.1.md`) gemeldeten Testlücken sind im nachgeschärften Plan geschlossen, und die Nachschärfung hat keine neuen Lücken erzeugt. Die neuen Testfälle wurden gegen den Ist-Code verifiziert und sind fachlich wie technisch korrekt angesetzt:

1. **`QueryAsync_BankProjection_NoPriorYearEvents_ProducesNoExpectation`** (plan.md Z. 228) deckt die Verwerfen-Regel „kein Muster, keine Erwartung" ab. Die Assertion `ProjectionExpectedItems == null && ProjectionAmount == Amount` ist mit der extrahierten Semantik konsistent — die Leaf-Zuordnung setzt `ProjectionAmount = Amount + expected` unbedingt (`ReportAggregationService.cs` Z. 1524), sodass `expected = 0` zu `ProjectionAmount == Amount` führt.
2. **`QueryAsync_BankProjection_ComparePrevious_KeepsProjectionOnlyLatestRow`** (Z. 229) aktiviert mit `ComparePrevious = true` korrekt die Leer-Latest-Bereinigung (Gate `!allHistoryMode && (query.ComparePrevious || query.CompareYear)`, verifiziert Z. 754) und belegt den `hasProjectionData`-Term fail-before/pass-after.
3. **`QueryAsync_MultiKindProjection_TypeRollupCountsCategoryChildrenOnce`** (Z. 235) prüft exakt die Design-Regel `ParentGroupKey ∈ {null, "Type:{K}"}` gegen Doppelzählung — verifiziert, dass das heutige Rollup nur parent-lose Leafs und `Category:`-Zeilen summiert (Z. 1555–1572), die Designentscheidung also nötig und der Test aussagekräftig ist.
4. **`QueryAsync_MultiKindProjection_FullySoldSecurity_ProducesNoExpectation`** (Z. 237) belegt die `HasPositiveHolding`-Delegat-Verdrahtung im Standardpfad (nur `Kind == Security`), fail-before-fähig.

Das Zusatzartefakt `…-tasks.md` (Task 20) listet alle vier neuen Fälle konsistent zum Plan.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Hochrechnung für `PostingKind.Bank` (Mustererkennung, +1J-Projektion, `ProjectionAmount`) | `ReportProjectionEventLoader` (Bank-Events `Amount != 0`) + `ReportProjectionCalculator`; Programmablauf Standardpfad Schritte 3–7 | `…_AddsUnconfirmedPriorYearPosting`, `…_TreatsCurrentPostingAsConfirmed`, `…_MonthlyPatternExpectsOpenMonth`, `…_YtdAndYearIntervals_ForBank` | Abgedeckt |
| Hochrechnung für `PostingKind.Contact` | Analoger Contact-Strang im Loader | `QueryAsync_ContactProjection_QuarterlyPatternExpectsOpenQuarter` | Abgedeckt |
| Hochrechnung für `PostingKind.SavingsPlan` (inkl. `LinkedAccountPostings`) | Loader: Plan-Postings bzw. Bank-Buchungen verknüpfter Konten (`SavingsPlanAccountLinks` existiert in `AppDbContext`), EntityId = PlanId | `…_AnnualPatternExpectsPosting`, `…_LinkedAccountSource_UsesLinkedBankPostings` | Abgedeckt |
| Security-Semantik unverändert (Netto-Dividende, `GroupId`-Netting, `HasPositiveHolding`) | Extraktion statt Neuimplementierung; `QuerySecurityDividendsNetAsync` ruft Loader + Calculator; Delegat nur für `Kind == Security` | Alle 25 bestehenden `ReportAggregationProjectionTests` unverändert grün (Refactor-Anker) + E2E `…SecurityFavorite_StillWorks` + **neu:** `…_FullySoldSecurity_ProducesNoExpectation` (Standardpfad-Bestandsprüfung) | Abgedeckt |
| Eine einzige artübergreifende Funktionalität, keine artspezifischen Sonderlogiken | Geteilter Calculator + Loader; Designentscheidungen begründen Security-Sonderfälle als Bestandsschutz | `QueryAsync_MultiKindProjection_ProjectsEachKind` | Abgedeckt |
| Auslöser Dashboard-Checkbox (`Label_CompareProjection`) | UI-Gate `CanCompareProjection` = `SelectedKinds.Count > 0 && Interval != AllHistory` an beiden Stellen (razor Z. 594–595, VM Z. 217/223–229 — verifiziert) | E2E `ProjectionCheckbox_EnabledForBankKind_TogglesColumn`; VM-Tests `CanCompareProjection_True_…` / `_False_ForAllHistory` | Abgedeckt |
| Auslöser gespeicherte Favoriten (`ReportFavorite.CompareProjection`) | `ApplyFavorite`/`ReloadAsync` mechanismusgleich; Favorit aktiviert Option nun wirksam | Akzeptanzszenario 7; E2E-Happy-Path nutzt `?favoriteId=…`; betroffene VM-Tests werden invertiert | Abgedeckt |
| Auslöser KPI-Kacheln (`HomeKpiGrid`) | Unverändert — reicht Flag durch, rendert nur `Amount` | Bestehender Test `ReportFavoriteKpi_ForwardsCompareProjectionToAggregateRequest` | Abgedeckt |
| Auslöser MCP (`fm_reporting_aggregate`, `fm_reports_configuration_manage`) | Output `expectedItems` (`kind`/`entityId`/`untrusted_data.entityName`/…), Schema-/Description-Texte artneutral; Ist-Code `expectedDividends`/`securityName` Z. 195–202 verifiziert | `FmReportingAggregateTool`-Tests: Output-Shape-Assertions anpassen | Abgedeckt |
| `ComparedProjection = true`, `ProjectionAmount` = gebuchter Betrag + unbestätigte Vorjahreserwartungen | Rückgabe `projectionEnabled && aktiveProjektionsArten.Any()`; Calculator-Leaf-Zuordnung `ProjectionAmount = Amount + expected` | Happy-Path-Tests je Art + `ContradictingSecuritySubTypes_SuppressesOnlySecurity` | Abgedeckt |
| Detail-Overlay mit erwarteten Einzelereignissen, artneutrale Entitätsspalte | DTO `ReportProjectionExpectedItemDto`, resx `ProjectionDetails_Entity`, Overlay-/Sortierumbau | E2E-Happy-Path assertet Overlay mit Kontoname, Erwartungsdatum, Betrag | Abgedeckt |
| Kategorie- und `Type:`-Summenzeilen aggregieren Projektionswerte der Kinder | Generisches Category-Rollup + verallgemeinertes `Type:`-Rollup inkl. `ParentGroupKey ∈ {null, "Type:{K}"}`-Regel | `…_AggregatesCategoryRows_ForContactAndSavingsPlan`, `…_ProjectsEachKind` + **neu:** `…_TypeRollupCountsCategoryChildrenOnce` (Multi-Kind + `IncludeCategory` — `IncludeCategory` am Request verifiziert) | Abgedeckt |
| `ReportInterval.AllHistory` bleibt wirkungslos | `projectionEnabled` enthält `Interval != AllHistory`; UI-Gate deaktiviert Checkbox | `QueryAsync_Projection_IsIgnored_ForAllHistory` + E2E `ProjectionCheckbox_DisabledForAllHistory` | Abgedeckt |
| Entitäten ohne aktuelle Buchungen, aber mit unerfüllten Vorjahres-Erwartungen → Erwartung fließt ein | Latest-Period-Injektion + Calculator-Aufruf danach + `hasProjectionData` in Leer-Latest-Bereinigung | **Neu:** `…_ComparePrevious_KeepsProjectionOnlyLatestRow` (aktiviert die Bereinigung gezielt) | Abgedeckt |
| Entitäten ohne Vorjahres-Events → kein Muster, keine Erwartung (verwerfen) | Scope-Klärung dokumentiert | **Neu:** `…_NoPriorYearEvents_ProducesNoExpectation` (dedizierter Negativtest, Assertion codeverifiziert) | Abgedeckt |
| Besitzerisolation (UserId aus Auth-Kontext, Filter über Entitäts-FKs) | Owner-Entity-Ids je `OwnerUserId` im Loader, analog Textfilter-Block Z. 184–191 | `QueryAsync_BankProjection_IgnoresOtherUsersPostings`; Bestandstest `…_IgnoresOtherUsersHoldings` bleibt | Abgedeckt |
| Text-/Entitätsfilter binden Erwartungen (Offene Frage 6) | Loader wendet `*TextFilters`/`AccountIds` analog `LoadTextFilteredRowsAsync` an | `…_RespectsBankTextFilters`, `…_RespectsAccountIdsFilter` | Abgedeckt |
| `UseValutaDate` für alle Arten | `ApplyDateWindow`-Semantik im Loader | `…_UsesValutaDateAndBookingFallback` | Abgedeckt |
| Security-Erwartungen nur auf Dividenden-Zeilen (Multi-Kind, subtyp-gesplittete Keys) | Designentscheidung Leaf-Zuordnung: nur `Security:{id}:Dividend`-Suffix im Standardpfad | `…_SecurityExpectationsOnlyOnDividendRows` | Abgedeckt |
| Keine neue Konfiguration/Persistenz (Backup-Regel AGENT.md) | Keine Migrationen, keine DTO-Persistenzänderung; `ReportFavorite.CompareProjection` bereits im Backup-DTO | Bestehende `ReportFavoriteBackupTests`/`BackupRestoreCoverageTests` unverändert gültig | Abgedeckt |
| resx-Pflicht de+en (AGENT.md) | `ProjectionDetails_Security` → `ProjectionDetails_Entity` in beiden resx (beide Vorkommen Z. 387 verifiziert) | — | Abgedeckt |
| `EnumCoverageTests`-Pflicht (AGENT.md) | `ProjectionPattern_Values` per Reflection (Muster `ReportGroupKeyTargetKind_Values`, Z. 481 verifiziert) | Geplanter Test | Abgedeckt |
| Doku-Update `Docs/help/berichtswesen/*` | `business-rules.md` (Regel Z. 30–47), `beschreibung.md`, `api.md`, `datenmodell.md` | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — alle vier im ersten Lauf benannten Testlücken wurden mit konkreten, fail-before-fähigen Testfällen geschlossen (siehe Ergebnis).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Bank-Bericht: Favorit/Checkbox → `Th_Projection`-Spalte → `ProjectionAmount` → Detail-Overlay | `ProjectionColumn_BankFavorite_ShowsExpectedAmountAndOverlay` (Pflicht) | Abgedeckt |
| Checkbox-Gate: für Bank-Auswahl aktivierbar, Toggle blendet Spalte ein | `ProjectionCheckbox_EnabledForBankKind_TogglesColumn` (Pflicht) | Abgedeckt |
| `AllHistory`: Checkbox deaktiviert, keine Spalte | `ProjectionCheckbox_DisabledForAllHistory` (Pflicht) | Abgedeckt |
| Multi-Kind-Favorit (Bank+Contact): Spalte + `Type:`-Zeilen mit Werten | `ProjectionColumn_MultiKind_ShowsPerKindValues` (empfohlen) | Abgedeckt |
| Security-Regression inkl. umbenanntem Overlay-Label | `ProjectionColumn_SecurityFavorite_StillWorks` (empfohlen) | Abgedeckt |
| KPI-Kachel `HomeKpiGrid` | Kein E2E geplant | Nicht erforderlich — Kachel rendert bewusst nur `Amount` (kein sichtbarer Projektions-Effekt); Flag-Durchreichung per Unit-Test abgesichert |
| MCP-Tools | Kein E2E geplant | Nicht erforderlich — kein UI-Fluss; Output-Shape über Tool-Tests |
| Gespeicherter Favorit auf Nicht-Security-Auswahl wird wirksam (Szenario 7) | Indirekt über `?favoriteId=…` im Bank-Happy-Path + VM-Tests | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine — Scope-Klärung vollständig gefüllt; alle vier Scope-Ausschlüsse tragen Begründung und Status `bestätigt`; die acht Akzeptanzszenarien enthalten einen vollständigen Happy Path vom Nutzer-Auslöser bis zur sichtbaren Wirkung inkl. Ausgangszustand ohne aktuelle Buchungen; Warte-/Asynchron-Situationen existieren nicht (synchrone Aggregation); Seiteneffekte und Risiken (Serialisierungsbruch, Mehr-DB-Reads, Multi-Kind-/Favoriten-Verhaltensänderung, Type-Rollup-Lücke, Subtyp-Split-Leafs) sind benannt; betroffene bestehende Tests, Seed-Helfer und reale Abnahme (Schritt 10) sind vollständig erfasst. Die Hinweise des ersten Laufs (YTD-Fallback-Erhaltung, Filter-Abdeckung je Art, MCP-Output-Positivtest, fail-before/pass-after) wurden entweder in den Plantext übernommen (YTD-Fallback jetzt explizit in Programmablauf Schritt 1, Z. 46) oder bleiben als nicht blockierende optionale Ausbaupunkte bestehen.

## Hinweise

- **YTD-Fallback im Plantext verankert:** Der Hinweis aus Lauf 1 wurde aufgegriffen — Programmablauf Schritt 1 (Z. 46) benennt den zweiten Routing-Block `onlySecurityKind && ytdMode && dividendSelected` (Z. 85–88) jetzt explizit als zu erhaltenden Early-Return.
- **`IsSecurityOnlySelection` ist `public` am ViewModel:** Die Entfernung ist mit „sofern keine weitere Verwendung" konditioniert — im Repo verifiziert nur in den beiden Gates verwendet (razor Z. 594, VM Z. 227); bei der Umsetzung auf Repo-weite Verwendung prüfen, sonst Property stehen lassen.
- **Verwerfen-Test-Assertion bewusst `ProjectionAmount == Amount`:** Die Leaf-Zuordnung setzt `ProjectionAmount` unbedingt (`Amount + expected`); die Assertion `== null` aus dem ersten Prüfbericht wäre falsch gewesen — der Plan formuliert sie nun korrekt als `== Amount` bei `ProjectionExpectedItems == null`.
- **`ComparePrevious_KeepsProjectionOnlyLatestRow` abhängig von Perioden-Mapping:** Der geseedete Vorjahres-Event muss so gewählt werden, dass die Erwartung in die injizierte Latest-Periode fällt (z. B. Annual-Muster, gleicher Monat des Vorjahres) — sonst wird die Zeile nicht projiziert und der Test prüft am Szenario vorbei. Beim Schreiben des Tests verifizieren.
- **Filter-Abdeckung je Art weiterhin optional:** Nur Bank hat dedizierte Filtertests (`BankTextFilters`, `AccountIds`); `ContactIds`/`SavingsPlanIds`/`ContactTextFilters`/`SavingsPlanTextFilters` sind mechanismusgleich parametrisiert — optionaler Ausbau, kein Blocker (Bewertung aus Lauf 1 unverändert).
- **Implizite Scope-Bestätigungen:** Die Ausschlüsse 1–4 sind mit „bestätigt durch Anforderung" (Konsistenzschluss, 2026-11-15) markiert — keine explizite Nutzerbestätigung. Da die Anforderung die fachliche Evaluation explizit dem Auftraggeber vorbehält, vertretbar; der Orchestrator kann die Annahmen bei der Abnahme spiegeln.
