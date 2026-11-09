# Tasks: Ribbon-Gruppierung „Verknüpfte Informationen" an letzte Position (Issue #498)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI | `SavingsPlanCardViewModel`: `linked`-Tab in `GetRibbonRegisterDefinition` (Zeile 895) ans Ende der Tab-Liste verschieben → `{ nav, manage, analysis, bookings, linked }` | Offen | — |
| 2 | UI | `BankAccountCardViewModel`: `Ribbon_Group_Linked`-Tab in `GetRibbonRegisterDefinition` (Zeilen 302–308) hinter den `Ribbon_Group_Bookings`-Eintrag verschieben | Offen | — |
| 3 | UI | `PostingsCardViewModel`: Tab-Liste in `GetRibbonRegisterDefinition` (Zeile 154) auf `{ nav, actions, linked }` umordnen | Offen | — |
| 4 | Dokumentation | `AGENT.md`: Abschnitt „Gruppierung des Ribbon-Menüs" um die Konvention „`Navigation`-Gruppe an Position 0" ergänzen | Offen | — |
| 5 | Tests | `AccountDetailViewModelTests`: Test `GetRibbon_GroupOrder_PlacesLinkedLast` hinzufügen (Sequenz Navigation → Manage → Bookings → Linked; `Linked` letzter Tab) | Offen | — |
| 6 | Tests | `PostingsCardViewModelReversalTests`: Test `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` hinzufügen (Sequenz Navigation → Actions → Linked) | Offen | — |
| 7 | Tests | `SavingsPlanCardLinkTests`: Test `Ribbon_GroupOrder_PlacesLinkedLast` hinzufügen (Sequenz Navigation → Manage → Analysis → Bookings → Linked) | Offen | — |
| 8 | Tests | `RibbonTests` (bUnit): Test `Groups_RenderInTabListOrder` hinzufügen — DOM-Reihenfolge der `.fm-ribbon-group-footer`-Elemente folgt der Tab-Listenreihenfolge | Offen | — |
| 9 | E2E-Tests | Neue Klasse `RibbonGroupOrderPlaywrightTests` in `FinanceManager.Tests.E2E/Tests/Navigation/`: drei `[Fact]`s (Sparplan-, Bankkonto-, Posten-Karte) — letzte `.fm-ribbon-group` enthält Linked-Aktion (`#OpenAttachments`), erste `#Back` | Offen | — |
| 10 | Tests | Unit-/Komponententests ausführen (`FinanceManager.Tests`, Filter `Category=OsInterface` ausgeschlossen) — alle grün | Offen | — |
| 11 | E2E-Tests | Neuen E2E-Test ausführen (`--filter "FullyQualifiedName~RibbonGroupOrder"`) — grün; preexisting Fehlschläge lt. `inventory/tests.md` beachten | Offen | — |
| 12 | Abnahme | Reale End-to-End-Abnahme: App starten, drei Detailseiten öffnen, sichtbare Gruppenreihenfolge prüfen; Ergebnis dokumentieren | Offen | — |
