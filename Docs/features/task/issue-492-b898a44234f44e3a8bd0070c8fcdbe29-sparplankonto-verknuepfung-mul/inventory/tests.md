## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt: 2026-11-08 (lokal, UTC+1)
- Branch und Commit: `task/issue-492-...-sparplankonto-verknuepfung-mul` @ e5f99234 (+ untracked Docs/features)
- Uncommittete Änderungen im getesteten Stand: nur untracked Lifecycle-Artefakte unter `Docs/features/` (kein Code)
- Testumgebung: .NET SDK 10.0.403, Windows/Git-Bash
- Testsuiten: `FinanceManager.Tests` (Unit), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E`

### Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet build FinanceManager.sln` | Repo-Root | 0 | – | – | – | Build 0 Fehler |
| Baseline | `dotnet test FinanceManager.Tests --no-build` | Repo-Root | (läuft, Ergebnis in test-results.md nachgetragen) | | | | |

### Nachgewiesene bestehende Testfehler

Siehe Baseline-Ergebnis in `test-results.md` bzw. Konsolenausgabe des Laufs.

### Testlücken und Ausführungsprobleme

- `FinanceManager.Tests.Integration` und `FinanceManager.Tests.E2E` wurden im Baseline-Lauf nicht vollständig ausgeführt (Laufzeit); relevante Integrationstests werden im Rahmen von Schritt 10 ausgeführt.

## Testklassen (relevant)

- `SavingsPlanAccountLinkServiceTests_AddLink` / `_Queries` / `_RemoveLink` – Link-Service
- `SavingsPlanLinkedAnalysisTests` – Saldo-/Informal-Logik
- `SavingsPlanServiceTests`, `SavingsPlanServiceActualReportTests` – Service-Verhalten
- `SavingsPlanCardLinkTests` (ViewModels) – Link-UI-ViewModel
- `ReportAggregationService*Tests`, `ReportFavoriteServiceTests` – Reports
- `ApiClientSavingsPlanLinksTests`, `ApiClientDemoDataTests` – Integration
