# Offene Aufgaben

Erstellt am: 2026-08-22
Status: Kern-Implementierung abgeschlossen; verbleibende Schritte ausstehend.

## Erledigt in diesem Durchlauf

- [x] Vorläufig-Merkmal beim Buchen auf alle erzeugten Posten übertragen
- [x] Automatische Stornierung vorläufiger Posten beim Buchen eines realen Kontoauszugs
- [x] Hinweis bei der Prüfung eines realen Kontoauszugs, sofern vorläufige Buchungen vorhanden sind
- [x] EF-Migrationen für `IsPreliminary` in `StatementDraft` und `Posting` angelegt
- [x] Build und bestehende Statement-Draft-Buchungs-Tests erfolgreich

## Noch offen

- [ ] Lokalisationen (Ribbon, Spalte, Hinweise) ergänzen
- [ ] Schnellbearbeitungsmodus und Fokus auf Buchungsdatum in `StatementDraftCardViewModel`
- [ ] Dedizierte Unit-/Integrationstests für Vorläufig-Merkmal und Stornierung ergänzen
