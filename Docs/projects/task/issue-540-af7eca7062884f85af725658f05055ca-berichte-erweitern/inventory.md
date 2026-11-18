# Bestandsaufnahme – Berichte erweitern

## Relevanter Ist-Stand
- `ReportDashboard.razor` rendert Einstellungen, Mini-Periodenchart oberhalb der Tabelle, Ergebnis-Tabelle, Posten-Overlay und Hochrechnungs-Overlay.
- `ReportDashboardViewModel` baut `ReportAggregatesFiltersRequest`, lädt Aggregationen und Posten, verwaltet Favoriten, Filterdialog, Sicherheitstyp-Filter und Textfilter.
- `ReportProjectionCalculator` erzeugt `ReportProjectionExpectedItemDto` aus `ProjectionEvent` mit `Kind`, `EntityId`, `PriorYearDate` und `Amount`; eine Posting-Id wird bisher nicht übertragen.
- `ReportSuppressedExpectation` speichert Unterdrückungen bisher mit dem Schlüssel `(OwnerUserId, ReportFavoriteId, Kind, EntityId, PriorYearDate)`. Dadurch werden alle gleich datierten Kandidaten derselben Entität gemeinsam unterdrückt.
- `ReportSuppressedExpectationService` listet alle Unterdrückungen eines Favoriten; `ReportDashboard.razor` zeigt sie ohne Kontextfilterung in jedem geöffneten Hochrechnungs-Overlay.
- `UnsuppressExpectationAsync` entfernt nur den Listeneintrag und startet `LoadAsync()`; die im Overlay gehaltene `_projectionDetails`-Liste wird nicht wieder ergänzt.
- `ReportPostingDto` enthält `Subject`, `RecipientName` und `Description`; die UI zeigt bisher nur das erste verfügbare Feld.
- `ReportFavorite` persistiert Einstellungen und Filter als Felder/CSV/JSON; neue Felder benötigen EF-Migration und Backup-DTO-Defaults.
- `BankPostingType` existiert bereits (`Unspecified`, `Inflow`, `Outflow`, `Interest`, `Tax`) und wird auf `Posting.PostingType` gespeichert. Ein Report-Filter existiert bisher nur für `SecuritySubTypes`.
- EF-Migrationen werden ausschließlich über `dotnet ef migrations add` erzeugt; neue Enum-Mitglieder sind in `EnumCoverageTests` zu ergänzen.

## Abweichung vom Skill-Ablauf
Die Umgebung stellt keine frei verwendbaren Unteragenten für diese Sitzung bereit. Anforderungsaufbereitung, Bestandsaufnahme, Planung, Implementierung, Reviews und Tests werden daher direkt, aber phasengetrennt im selben Workflow ausgeführt.
