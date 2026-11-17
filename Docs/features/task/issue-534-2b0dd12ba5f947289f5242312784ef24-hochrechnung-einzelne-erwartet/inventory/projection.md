# Inventar — Projektionspfad

## `FinanceManager.Infrastructure/Reports/ReportProjectionCalculator.cs`

| Element | Beschreibung |
|---------|--------------|
| `ApplyProjectionAmounts` | Statisch. Parameter: `IReadOnlyList<ReportAggregatePointDto> targetPoints`, `IReadOnlyList<ProjectionEvent> events`, `ReportProjectionContext context`, `Func<...,string> nameResolver`, `bool hasActiveSecurity`, `bool securityDividendLeafOnly`. |
| `ProjectionCandidate` | `Kind`, `EntityId`, `PriorYearDate`, `ExpectedDate`, `Amount`, `EntityName` — die Suppression-Identität `(Kind, EntityId, PriorYearDate)` liegt hier vor. |
| `FindExpectedItems` | Bestätigungsprüfung pro Muster (Monthly/Quarterly/Annual/Irregular); unbestätigte Kandidaten → `ReportProjectionExpectedItemDto`. |
| Ergebnis | `ProjectionAmount` + `ProjectionExpectedItems` werden je Zeilenpunkt gesetzt; Kategorie-/Summenzeilen aggregieren. |

**Eingriffspunkt:** Kandidatenfilter vor `FindExpectedItems` — neuer Parameter `IReadOnlySet<(PostingKind Kind, Guid EntityId, DateTime PriorYearDate)>? suppressions = null`; Kandidaten mit Treffer im Set werden verworfen (Datumvergleich auf `.Date` normalisiert).

## `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`

| Stelle | Inhalt |
|--------|--------|
| `QueryAsync` Z. 44 ff. | Query-Validierung, `projectionEnabled = query.CompareProjection && query.Interval != AllHistory`, `securityProjectionEnabled`, `comparedProjection` (Z. 62–65). |
| ~Z. 823 | `ApplyProjectionAmounts(points, projectionEvents, projectionContext, ResolveProjectionEntityName, hasActiveSecurity, securityDividendLeafOnly: true)` — Standardpfad. |
| `QuerySecurityDividendsNetAsync` ~Z. 1200 + ~Z. 1225 | `ApplyProjectionAmounts` im Netto-Dividenden-Pfad. |
| `ResolveProjectionEntityName` | Namensauflösung je `Kind` (Accounts/Contacts/SavingsPlans/Securities). |

**Eingriffspunkt:** `query.ReportFavoriteId.HasValue` → `ReportSuppressedExpectations` für `(OwnerUserId, ReportFavoriteId)` laden → Schlüsselmenge bauen → an beide Calculator-Aufrufe übergeben.

## `ReportPostingsQueryService.cs`

Projection-Pfad ist Security-only (Postings-Drilldown) — kein Favoriten-Kontext, bleibt unverändert.
