# Code-Review

**Status:** Keine Befunde

- `SavingsPlanAccountLinkService.AddLinkAsync`: Owner-Scope über Account-Query (existiert + `Type == Savings`); ArgumentException → 400 im Controller; Kommentare erhalten.
- `ReportAggregationService`: Umattribution nur bei `LinkedAccountPostings` + SavingsPlan-Kind; Owner-gefilterte Links; gleiche `sourcePeriod`/`DateKind`; unverknüpfte Pläne behalten Planposten; Bankposten anderer Owner werden nie einbezogen.
- `ReportFavorite`-Backup: `ReportFavoriteBackupDto` + `AssignBackupDto`/`ToBackupDto` erweitert — Alte Backups ohne Feld erhalten Default `PlanPostings`.
- `SavingsPlanService.GetAsync`: `OwnAmount` nur bei Links; Summe der SavingsPlan-Posten bis heute — konsistent zu bisheriger `accumulated`-Berechnung.
- `DemoDataService`: Salden-Tracking über `accountByDraft`/`balanceByAccount`; Zinsbuchungen nur in gebuchten Dezember-Monaten; Informal-Planposten via `SavingsPlanId`-Zuweisung am Draft-Eintrag (selbe Konvention wie Rücklagen/Urlaub).
- Migration `AddReportFavoriteSavingsPlanPostingSource`: rein additiv (int NOT NULL DEFAULT 0), `Down` entfernt Spalte, kein Datenverlust.
- Rückwärtskompatibilität: alle neuen DTO-Parameter optional mit Default `PlanPostings`; bestehende API-Konsumenten unverändert.
- Keine Kommentar-Entfernungen; XML-Docs vollständig; keine neuen Warnungen (nur vorhandene NU1510/xUnit-Warnungen).

Bekannte Einschränkung: ein Konto, das mehreren Plänen zugeordnet ist, liefert seine Bankposten in den Report jeder verknüpften Plan-Ausgabe (konsistent zur additiven Saldo-Regel).
