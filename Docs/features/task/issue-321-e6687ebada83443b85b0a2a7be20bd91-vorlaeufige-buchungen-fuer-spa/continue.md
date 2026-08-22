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
- [x] Ribbon-Button auf Bankkonto legt vorläufigen Kontoauszug an und öffnet ihn mit `?quickEdit=true`
- [x] E2E-Test für Ribbon-Anlage + Öffnen vorhanden

## Noch offen

- [x] Buchungsdatum-Feld im Schnellbearbeitungsmodus automatisch fokussieren
- [x] Lokalisationen (Ribbon, Spalte, Hinweise) ergänzen

## Nach dem Review festgestellte offene Punkte

- [x] Draft-Beschreibung `Vorl. Buchungen vom {dateText}` in `StatementDraftService.Preliminary.cs` lokalisiert einbauen (Ressource `StatementDraft_Description_Preliminary`)
- [x] Validierungswarnung für vorläufige Stornierung als klickbaren Link zur Buchungsübersicht darstellen

## Während des Test-Runs aufgefallen

- [x] `PostingsListReversalColumnTests.BuildRecords_StornoCell_ShouldShowDash_ForReversedPosting` / `BuildRecords_StornoCell_ShouldShowCheckmark_ForReversalPosting` schlugen initial fehl, weil `BasePostingsListViewModel` die neue `preliminary` Spalte nach der `storno` Spalte angehängt hatte. `Columns` und `BuildRecords` wurden korrigiert (Reihenfolge jetzt `preliminary`, dann `storno`), beide Tests laufen jetzt grün.
- [ ] `HelpSecurityMiddlewareTests.HelpAssetHttpRequest_IsBlockedWhenManifestedFileIsManipulated(relativeAssetPath: "de/search-index.json", requestPath: "/api/help/search-index/de.json", ...)` schlägt fehl: Expected `NotFound`, Actual `OK`. Ursache: `de/search-index.json` existiert nicht unter `wwwroot/help/de`; `HelpController.GetSearchIndex` generiert den Index on-the-fly und gibt 200 zurück. Nicht direkt vom vorläufigen-Buchungen-Feature verursacht, aber im aktuellen Branch sichtbar.

- [x] Auf der Kontoauszugansicht muss erkennbar sein, dass es sich um einen Vorläufigen Auszug handelt (StatementDraftCardViewModel / CardRecord)
