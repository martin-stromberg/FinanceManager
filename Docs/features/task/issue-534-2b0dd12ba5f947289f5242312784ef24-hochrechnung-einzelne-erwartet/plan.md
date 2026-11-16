# Umsetzungsplan — Issue #534: Einzelne erwartete Posten pro Favorit ausblenden

## Entscheidungen

| Punkt | Entscheidung | Begründung |
|-------|--------------|------------|
| Identität | `(ReportFavoriteId, PostingKind, EntityId, PriorYearDate)`; Unique-Index inkl. `OwnerUserId` | Issue-Vorgabe; Betrag ausgeschlossen, damit Betragskorrekturen den Vermerk nicht lösen |
| Favoriten-Kontext | `ReportFavoriteId` (nullable) in `ReportAggregationQuery` + `ReportAggregatesQueryRequest` | Dashboard setzt `ActiveFavoriteId`; HomeKpi setzt gebundenen Favorit; MCP setzt ihn nur bei `favoriteId`-Param → ad-hoc unbeeinflusst |
| Filterpunkt | Kandidatenfilter in `ApplyProjectionAmounts` vor `FindExpectedItems` | Issue: „vor der Bestätigungsprüfung verworfen" |
| Schlüsselmenge | `HashSet<(PostingKind, Guid, DateTime)>` mit `.Date`-Normalisierung | Zeitanteile in Posting-Daten vermeiden Fehlvergleiche |
| Suppression-Liste fürs Overlay | Eigener Endpunkt `GET report-favorites/{id}/suppressed-expectations` liefert DTOs inkl. aufgelöstem `EntityName` + `ExpectedDate = PriorYearDate+1J` | Overlay soll Vermerke zeigen, ohne sie der Kalkulation zu entnehmen |
| Endpunkte | GET/POST `report-favorites/{favoriteId}/suppressed-expectations`, DELETE `.../{suppressionId}` am `ReportsController` | REST-konform zum bestehenden Favoriten-CRUD |
| Suppress | Idempotent: bestehender Vermerk wird zurückgegeben statt Fehler | Doppelklick/Race im Overlay |
| Unsuppress | Löschung per Suppression-`Id` (aus GET-Liste) | Einfacher, eindeutiger Bezug aus UI |
| Favorit-Löschung | FK `Cascade` auf `ReportFavoriteId` | Vermerke ohne Favoriten sind sinnlos |
| Migration | `dotnet ef migrations add AddReportSuppressedExpectations` | AGENT.md |
| Backup | Optionale Sektion `ReportSuppressedExpectations`, `TryGetProperty`-Guard, `reportMap`-Remapping | AGENT.md; kein `RequiredDataProperties`-Eintrag |
| Jahreswechsel | Kein Löschjob — `PriorYearDate` rolliert natürlich aus dem Referenzfenster | Issue-Vorgabe |

## Schritte

1. **Domain**: `FinanceManager.Domain/Reports/ReportSuppressedExpectation.cs` (Entity, Guards, `ReportSuppressedExpectationBackupDto`, `ToBackupDto`/`AssignBackupDto`)
2. **Persistenz**: `AppDbContext` — DbSet, Modell (Unique-Index + Cascade-FK), `ClearUserDataAsync`-Schritt (total 27→28); EF-Migration generieren
3. **Shared**: `ReportSuppressedExpectationDto` + `ReportSuppressedExpectationRequest` (Shared/Dtos/Reports); `ReportFavoriteId` in `ReportAggregationQuery` + `ReportAggregatesQueryRequest` anhängen (positional kompatibel)
4. **Service**: `IReportSuppressedExpectationService` (Application/Reports) + `ReportSuppressedExpectationService` (Infrastructure/Reports) — Owner-Filter, Favoriten-Ownership-Prüfung, EntityName-Auflösung; DI-Registrierung
5. **Controller**: `ReportsController` — `ReportFavoriteId` ins Query-Mapping; drei Suppression-Endpunkte mit `_current.UserId`
6. **ApiClient**: `IApiClient` + `ApiClient.Reports.cs` — drei Methoden
7. **Calculator**: `ApplyProjectionAmounts` + optionaler `suppressions`-Parameter; Kandidatenfilter vor `FindExpectedItems`
8. **Aggregation**: `ReportAggregationService` — `LoadSuppressionKeysAsync` (nur wenn `ReportFavoriteId` gesetzt), an beide `ApplyProjectionAmounts`-Aufrufe übergeben
9. **Caller**: `ReportDashboardViewModel` (`ActiveFavoriteId`), `HomeKpiGrid` (`fav.Id`), `FmReportingAggregateTool` (`favorite.Id` bei favoriteId-Param)
10. **UI**: `ReportDashboard.razor` — „Nicht mehr erwarten"-Button je erwartetem Posten (nur mit `ActiveFavoriteId`), Abschnitt „Ausgeblendete Erwartungen" mit „Wieder einbeziehen", `_suppressedExpectations`-State, Reload nach Mutation
11. **Resx**: de + en Keys
12. **Backup**: Export in `BuildBackupDataAsync`; Import in `SetupImportService` nach `ReportFavorites` mit `reportMap`
13. **Tests**:
    - `ReportSuppressedExpectationServiceTests` — Suppress/List/Unsuppress, Idempotenz, Owner-Isolation, Favoriten-Fremdbezug
    - `ReportAggregationSuppressionTests` — Service-Level: Vermerk entfernt erwartetes Item + senkt `ProjectionAmount`; ohne Favorit unverändert; fremder Favorit unverändert; nach Unsuppress wieder da
    - `ReportSuppressedExpectationBackupRestoreTests` — Export/Import, Favorite-Remapping, fehlende Sektion (altes Backup)
    - E2E `ReportSuppressedExpectationPlaywrightTests` — Favorit aktiv → Overlay: ausblenden → versteckte Liste → wieder einbeziehen
14. **Docs**: `Docs/help/berichtswesen/` (beschreibung, business-rules, datenmodell, ablauf-technisch ggf. api.md), index.md

## Risiken

- `ReportAggregatePointDto`-Mutationen müssen unberührt bleiben — nur Kandidaten filtern, nicht die Roll-up-Logik ändern.
- `ClearUserDataAsync`-Total korrekt hochzählen (Fortschrittsanzeige).
- Beide `ApplyProjectionAmounts`-Aufrufe im Security-Net-Pfad müssen die Menge erhalten.
- Alte `ReportAggregatesQueryRequest`-Clients ohne `ReportFavoriteId` → null → kein Suppression-Verhalten (gewollt).
