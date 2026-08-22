# Offene Aufgaben

Erstellt am: 2026-08-22
Status: Kern-Implementierung und Tests abgeschlossen; verbleibende Schritte ausstehend.

## Erledigt in diesem Durchlauf

- [x] Vorläufig-Merkmal beim Buchen auf alle erzeugten Posten übertragen
- [x] Automatische Stornierung vorläufiger Posten beim Buchen eines realen Kontoauszugs
- [x] Hinweis bei der Prüfung eines realen Kontoauszugs, sofern vorläufige Buchungen vorhanden sind
- [x] EF-Migrationen für `IsPreliminary` in `StatementDraft` und `Posting` angelegt
- [x] Build erfolgreich
- [x] Dedizierte Unit-Tests für Vorläufig-Merkmal, Stornierung und Nicht-Stornierung bei weiterem Vorläufig-Durchlauf
- [x] Dedizierte E2E-Tests für Vorläufig-Kontoauszüge und Stornierung durch realen Kontoauszug
- [x] Schnellbearbeitungsmodus wird bei `?quickEdit=true` im `StatementDraftCardViewModel` automatisch gestartet

## Noch offen

- [ ] Buchungsdatum-Feld im Schnellbearbeitungsmodus automatisch fokussieren
- [ ] Lokalisationen (Ribbon, Spalte, Hinweise) ergänzen
