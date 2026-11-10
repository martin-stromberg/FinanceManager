# Übersetzte Anforderung – Issue #498: Ribbon-Gruppierung „Verknüpfte Informationen" nicht an letzter Position (3 ViewModels)

## Fachliche Zusammenfassung

Drei Card-ViewModels liefern über `IRibbonProvider.GetRibbonRegisters(...)` ihre Ribbon-Tabs in einer Reihenfolge, die der in `AGENT.md` festgelegten Gruppierungsregel widerspricht: Die Gruppe „Verknüpfte Informationen" (Tab-Titel aus `Ribbon_Group_Linked`) steht vor Aktionsgruppen statt an letzter Position. Die `UiRibbonTab`-Listen in `SavingsPlanCardViewModel`, `BankAccountCardViewModel` und `PostingsCardViewModel` sind so umzuordnen, dass der `Linked`-Tab jeweils die letzte Gruppe des Ribbons bildet.

## Fachlicher Scope

- **Verarbeitungseinheit:** Das Ribbon-Tab-Layout einer Detail-/Kartenansicht, d. h. die geordnete `UiRibbonTab`-Liste innerhalb des von `GetRibbonRegisters` zurückgegebenen `UiRibbonRegister` (Kind `UiRibbonRegisterKind.Actions`).
- **Auslösende Ereignisse:** Kein eigenes Nutzer- oder Systemereignis — die Reihenfolge wirkt bei jedem Aufbau des Ribbons beim Rendern der betroffenen Detailseiten (Sparplan-Karte, Bankkonto-Karte, Posten-Karte).
- **Erwartete Wirkung:** In der Ribbon-Leiste der drei Detailseiten erscheint die Gruppe „Verknüpfte Informationen" sichtbar als letzte Gruppe. Zielreihenfolgen:
  - `SavingsPlanCardViewModel`: Navigation → Manage → Analysis → Bookings → **Linked** (zuvor: Navigation → Manage → Linked → Analysis → Bookings)
  - `BankAccountCardViewModel`: Navigation → Manage → Bookings → **Linked** (zuvor: Navigation → Manage → Linked → Bookings)
  - `PostingsCardViewModel`: Navigation → Actions → **Linked** (zuvor: Navigation → Linked → Actions)
- **Umgang mit fehlenden Objekten:** Nicht zutreffend — alle betroffenen Gruppen und Aktionen existieren bereits; es werden keine neuen Ribbon-Elemente erzeugt.
- **Scope-Ausschlüsse:**
  - Keine Änderung an Inhalt, Aktionen, Labels, Icons oder `MobileShortcut`-Flags der betroffenen Gruppen — ausschließlich die Position des `Linked`-Tabs ändert sich.
  - Keine Umbenennung von Ressourcen-Keys (`Ribbon_Group_Linked` etc.) und keine resx-Änderungen.
  - Keine Veränderung der `Navigation`-Gruppe; sie bleibt projektkonform an Position 0.
  - Kein Refactoring zu einer generischen/zentralen Sortierlogik und keine Anpassung weiterer, bereits konformer ViewModels.
  - Die im Issue als Hinweis genannte Dokumentation der Meta-Konvention „Navigation an Position 0" in `AGENT.md` ist optional formuliert („könnte ergänzt werden") und kein zwingender Teil des Scope.

## Betroffene Klassen und Komponenten

- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` — Tab-Liste in `GetRibbonRegisters` (akt. `var tabs = new List<UiRibbonTab> { nav, manage, linked, analysis, bookings };`, ca. Zeile 895)
- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` — Tab-Liste (Navigation, Manage, Linked, Bookings; ca. Zeilen 302–308)
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` — Register `UiRibbonRegisterKind.Actions` mit Tabs `{ nav, linked, actions }` (ca. Zeile 154)
- `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` — `UiRibbonTab(Title, Items, Sort = 0)` (nur referenziert, keine Änderung)
- `FinanceManager.Web/Components/Shared/Ribbon.razor` — rendert Gruppen via `Groups.OrderBy(g => g.Sort)` unter Beibehaltung der First-Seen-Reihenfolge bei gleichem `Sort` (nur referenziert, keine Änderung)
- Tests: bestehende ViewModel-/Ribbon-Tests in `FinanceManager.Tests` (u. a. `ViewModels/PostingsCardViewModelReversalTests.cs`, `ViewModels/SavingsPlanCardLinkTests.cs`, `Components/RibbonTests.cs`); ggf. neue Assertions zur Tab-Reihenfolge; UI-/E2E-Abnahme der sichtbaren Gruppenreihenfolge gemäß Projektregeln
- Referenzimplementierungen (konform, unverändert): `SecurityCardViewModel`, `ContactCardViewModel`, `StatementDraftCardViewModel`, `StatementDraftEntryCardViewModel`
- Optional: `AGENT.md` — Ergänzung der Konvention „`Navigation`-Gruppe an Position 0"

## Implementierungsansatz

- Reine Umordnung: In den drei ViewModels wird der `linked`-Tab ans Ende der `UiRibbonTab`-Liste verschoben. In `BankAccountCardViewModel` ist die Liste inline aufgebaut — der `Linked`-Eintrag wandert hinter den `Bookings`-Eintrag.
- Die Render-Komponente `Ribbon.razor` sortiert Gruppen per `Groups.OrderBy(g => g.Sort)`; da alle Tabs `Sort = 0` (Default) haben, entscheidet die First-Seen-Reihenfolge der Liste — die Umordnung der Listeneinträge genügt daher. Alternativ oder ergänzend können explizite, aufsteigende `Sort`-Werte gesetzt werden (im Issue als „bzw. `Sort` anpassen" genannt); die Entscheidung obliegt der Planung.
- Keine neuen Klassen, Interfaces, Events oder Hooks erforderlich; keine Abhängigkeiten zu anderen Komponenten.
- Absicherung über Unit-Tests auf ViewModel-Ebene (Reihenfolge der von `GetRibbonRegisters` gelieferten Tabs) sowie E2E-/UI-Abnahme der sichtbaren Gruppenreihenfolge auf den drei Detailseiten gemäß Projektregeln.

## Konfiguration

Keine — das Verhalten ist fest durch die Projektregel in `AGENT.md` vorgegeben und nicht konfigurierbar.

## Offene Fragen

1. Soll die im Issue vorgeschlagene Ergänzung von `AGENT.md` (Meta-Konvention „`Navigation`-Gruppe an Position 0") im gleichen Change umgesetzt werden oder separat erfolgen?
2. Soll die Reihenfolge über die reine Listenposition festgelegt werden oder sollen zur expliziten Absicherung aufsteigende `Sort`-Werte auf den `UiRibbonTab`-Einträgen gesetzt werden?
