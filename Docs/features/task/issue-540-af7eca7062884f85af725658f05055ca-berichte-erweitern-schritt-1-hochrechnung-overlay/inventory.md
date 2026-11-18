# Bestandsaufnahme – Schritt 1

- `ProjectionEvent` enthält bisher keine Posting-ID; `ReportProjectionEventLoader` erstellt Events aus `Posting.Id`-Informationen und kann diese erweitern.
- `ReportProjectionExpectedItemDto`, `ReportSuppressExpectationRequest`, `ReportSuppressedExpectation` und `ReportSuppressedExpectationDto` bilden die API-/Persistenzkette.
- `LoadSuppressionKeysAsync` lädt nur `(Kind, EntityId, PriorYearDate)`; daraus muss eine postingbezogene Menge plus Kompatibilitätsschlüssel werden.
- Das Overlay hält `_suppressedExpectations` aktuell global pro Favorit. Der geöffnete Scope muss aus den `ReportProjectionExpectedItemDto`-Zeilen abgeleitet und beim Filtern berücksichtigt werden.
- `UnsuppressExpectationAsync` aktualisiert nur die suppressed-Liste. Nach erfolgreichem Reload können die Erwartungsdetails aus den neuen Punktdaten wieder aufgebaut werden.
- `ReportPostingDto` trägt `Subject`, `RecipientName` und `Description`; die Razor-Anzeige wählt aktuell `Subject ?? RecipientName ?? Description`.
- Betroffene Tests: `ReportSuppressedExpectationServiceTests`, `ReportAggregationSuppressionTests`, `ReportDashboardViewModelTests`, Backup-/Restore-Tests.
