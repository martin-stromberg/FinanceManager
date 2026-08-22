# Offene Aufgaben

Erstellt am: 2026-08-22
Abbruchgrund: Komplexität der Buchungs- und Stornierungslogik erfordert weiteren Bearbeitungsschritt.

Der aktuelle Durchlauf hat das Grundgerüst für vorläufige Buchungen umgesetzt. Folgende inhaltliche Punkte sind noch offen.

## Offene Implementierungsschritte

- [ ] Vorläufig-Merkmal beim Buchen auf alle erzeugten Posten übertragen (`FinanceManager.Infrastructure/Statements/StatementDraftService.cs`)
- [ ] Automatische Stornierung vorläufiger Posten beim Buchen eines realen Kontoauszugs (`FinanceManager.Infrastructure/Postings/PostingReversalService.cs` erweitern)
- [ ] Hinweis bei der Prüfung eines realen Kontoauszugs, sofern vorläufige Buchungen vorhanden sind
- [ ] EF-Migrationen für `IsPreliminary` in `StatementDraft` und `Posting` anlegen
- [ ] Schnellbearbeitungsmodus und Fokus auf Buchungsdatum in `StatementDraftCardViewModel`
- [ ] Lokalisationen (Ribbon, Spalte, Hinweise) ergänzen
- [ ] Unit- und Integrationstests ergänzen
