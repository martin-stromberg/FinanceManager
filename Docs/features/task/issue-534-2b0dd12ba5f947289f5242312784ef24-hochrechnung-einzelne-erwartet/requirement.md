# Anforderung — Issue #534: Hochrechnung: Einzelne erwartete Posten pro Favorit ausblenden

## Ausgangslage

Die Hochrechnung (Issue #530) projiziert wiederkehrende Vorjahresposten in den Betrachtungszeitraum. Einmalige Vorjahresereignisse — z. B. die Zinsgutschrift eines abgelaufenen Festgelds — werden fälschlich als wiederkehrende Erwartung projeziert und bleiben sichtbar überfällig.

## Anforderung (übersetzt in Projektkonzepte)

### Postenspezifisches Ausblenden erwarteter Posten, Geltungsbereich pro Berichtsfavorit

1. Im Overlay der erwarteten Posten (`ReportDashboard.razor`, `_projectionDetails`) kann jeder einzelne Vorschlag per Aktion „Nicht mehr erwarten" ausgeblendet werden.
2. Die Unterdrückung gilt **nur**, wenn der Bericht über diesen Favoriten gerendert wird — inkl. an den Favoriten gebundener Home-KPI-Kacheln (`HomeKpiGrid`, `ReportFavoriteId`). Ad-hoc-Abfragen ohne Favoritenbezug (MCP/API) bleiben unbeeinflusst.
3. Ausgeblendete Erwartungen werden im Overlay in einer separaten Liste („Ausgeblendete Erwartungen") angezeigt und können dort per Aktion wieder freigegeben werden.
4. Der Unterdrückungsvermerk wird dauerhaft pro Benutzer und Favorit gespeichert (Persistenz, `OwnerUserId`).
5. Bei der Projektionsberechnung werden unterdrückte Erwartungskandidaten vor der Bestätigungsprüfung ausgeschlossen; der Projektionsbetrag sinkt entsprechend. Ist-Posten bleiben unverändert.
6. Das Ausblenden erfolgt ohne Betragsbindung: Ändert sich der Betrag des Vorjahrespostens, bleibt die Unterdrückung bestehen (Schlüssel ohne Betrag).
7. Beim Jahreswechsel rolliert die Referenzbasis (Vorjahresdatum), Unterdrückungen erledigen sich von selbst — kein automatischer Löschlauf erforderlich.
8. Wiedereinbeziehung stellt den ursprünglichen Zustand vollständig wieder her.

## Technische Ausgestaltung (Vorgabe aus dem Issue)

- Persistente Entität `ReportSuppressedExpectation`:
  - `OwnerUserId`
  - `ReportFavoriteId`
  - `PostingKind`
  - `EntityId`
  - `PriorYearDate`
  - `SuppressedAtUtc`
- Eindeutigkeitsschlüssel `(ReportFavoriteId, PostingKind, EntityId, PriorYearDate)` — Betrag explizit **nicht** Teil des Schlüssels.
- `ReportProjectionCalculator.ApplyProjectionAmounts` erhält die Unterdrückungsliste als zusätzlichen Eingabeparameter; gefiltert wird vor der Bestätigungsprüfung.
- Favoriten-Kontext wird über die Aggregationsanfrage geführt (neues Feld in der Query / Request).
- UI: zwei Listen im Overlay (aktive Erwartungen mit „Nicht mehr erwarten"-Aktion, ausgeblendete Erwartungen mit „Wieder einbeziehen"-Aktion).
- Resx-Einträge deutsch + englisch.

## Verbindlichkeiten aus AGENT.md

- Neue Entität → Backup-Export in `BackupService.BuildBackupDataAsync`, Import in `SetupImportService` mit `TryGetProperty`-Guard; **nicht** in `RequiredDataProperties`.
- `OwnerUserId` per Konstruktor + `Guards.NotEmpty`; alle Abfragen serverseitig auf Besitzer gefiltert.
- EF-Migration nur per `dotnet ef migrations add <Name>`.
- Neue UI-Texte in `*.resx` für de + en.
- Backup/Restore-Tests verpflichtend.
- Neue Enum-Mitglieder → `EnumCoverageTests` (hier nicht nötig — `PostingKind` wird wiederverwendet).

## Abnahmekriterien

| # | Kriterium |
|---|-----------|
| A1 | Einzelner erwarteter Posten kann im Overlay per Aktion ausgeblendet werden; die Hochrechnung des Punktes sinkt. |
| A2 | Unterdrückung wirkt nur im Kontext des betreffenden Favoriten (Dashboard + favoriten-gebundene Home-KPI); ad-hoc Queries unverändert. |
| A3 | Ausgeblendete Erwartungen erscheinen in eigener Liste und können wieder freigegeben werden; danach Originalzustand. |
| A4 | Persistenz + Backup/Restore inkl. Rückwärtskompatibilität alter Backups. |
| A5 | Besitzerfilterung aller Abfragen/Endpunkte. |
| A6 | Tests: Einheit (Calculator-Filterung, Schlüssel-Semantik), Integration (Persistenz/Backup), E2E (Overlay-Interaktion). |
