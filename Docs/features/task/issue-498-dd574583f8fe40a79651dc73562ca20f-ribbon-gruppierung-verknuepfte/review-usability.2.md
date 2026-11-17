# Usability-Review

## Ergebnis

**Status:** Keine Befunde

Die Änderung besteht ausschließlich aus der Umordnung der Ribbon-Gruppe „Verknüpfte Informationen" (`Ribbon_Group_Linked`) ans Ende der Gruppenleiste auf drei Karten-Detailseiten. Aus Sicht einer nicht-technischen Anwenderin ist die geforderte Wirkung erreicht:

- **Sparplan-Karte** (`/card/savings-plans/{id}`): Gruppenreihenfolge Navigation → Verwalten → Analyse → Buchungen → Verknüpfte Informationen (`SavingsPlanCardViewModel.cs:895`).
- **Bankkonto-Karte** (`/card/accounts/{id}`): Navigation → Verwalten → Buchungen → Verknüpfte Informationen (`BankAccountCardViewModel.cs:302–308`).
- **Posten-Karte** (`/card/postings/{id}`): Navigation → Aktionen → Verknüpfte Informationen (`PostingsCardViewModel.cs:154`).

Die sichtbare Reihenfolge ist gesichert: `Ribbon.razor` (`BuildTabsToRender`, Zeilen 173–253) bildet jede `UiRibbonTab`-Position in First-Seen-Reihenfolge auf eine Gruppe ab; das abschließende `Groups.OrderBy(g => g.Sort)` ist stabil und alle Tabs tragen den Default `Sort = 0` — die Listenreihenfolge im ViewModel entspricht damit exakt der gerenderten Reihenfolge (desktop: horizontal von links nach rechts; mobil: vertikal gestapelt über dieselbe `CurrentGroups`-Sequenz, Zeilen 60–144).

Weitere Prüfpunkte ohne Befund:

- **Keine internen Kennungen:** Die Anforderung verlangt keine Eingabe; alle Aktionen werden über beschriftete Buttons mit resx-Labels ausgelöst.
- **Keine Auswahl-/Identifikationsaufgabe** betroffen.
- **Etabliertes Muster:** Identischer `UiRibbonTab`/`UiRibbonRegister`-Mechanismus wie in den konformen Referenz-ViewModels (`SecurityCardViewModel`, `ContactCardViewModel` u. a.); keine abweichende Lösung.
- **Aktionswirksamkeit:** Kein Callback, Label, Icon, `Disabled`- oder `Tooltip`-Wert wurde geändert — die Umordnung betrifft nur die Listenelement-Positionen. Blockierte Aktionen bleiben wie bisher deaktiviert und teilweise per Tooltip begründet (z. B. `ActualReport_Disabled_Linked` bei verknüpften Konten, `SavingsPlanCardViewModel.cs:889–890`).
- **Keine Sackgassen durch leere Gruppen:** `Ribbon.razor:183` überspringt Tabs ohne Items; alle drei `Linked`-Tabs enthalten in jedem Zustand Items (Buttons werden deaktiviert statt entfernt), sodass die Gruppe stets sichtbar an letzter Stelle erscheint.
- **Kein Nachträgliches Anhängen fremder Gruppen:** `base.GetRibbonRegisterDefinition` liefert über `BaseViewModel` (`BaseViewModel.cs:651`) `null`, und keines der drei ViewModels registriert Child-ViewModels, deren Register hinter der `Linked`-Gruppe gerendert würden (`GetRibbonRegisters` aggregiert Child-Register erst nach den eigenen, `BaseViewModel.cs:628–644`).

## Befunde

Keine.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Sparplan-Detailseite öffnen → Ribbon zeigt „Verknüpfte Informationen" als letzte Gruppe (Navigation, Verwalten, Analyse, Buchungen, Verknüpfte Informationen) → unauffällig
- Bankkonto-Detailseite öffnen → Ribbon zeigt „Verknüpfte Informationen" als letzte Gruppe (Navigation, Verwalten, Buchungen, Verknüpfte Informationen) → unauffällig
- Posten-Detailseite öffnen → Ribbon zeigt „Verknüpfte Informationen" als letzte Gruppe (Navigation, Aktionen, Verknüpfte Informationen) → unauffällig
- Bedienung der Gruppenaktionen (Zurück, Speichern, Löschen, Posten öffnen, Bankkontakt öffnen, Anhänge, Kategorie, Stornieren, Istmeldung, Vorläufige Buchungen) nach der Umordnung → unverändert verdrahtet, unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` (geändert)
- `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` (geändert)
- `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` (geändert)
- `FinanceManager.Web/Components/Shared/Ribbon.razor` (Render-Pipeline, unverändert — zur Wirkungsprüfung gelesen)
- `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (`UiRibbonTab`-Sort-Semantik, unverändert — zur Wirkungsprüfung gelesen)
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Register-Aggregation, unverändert — zur Wirkungsprüfung gelesen)
- `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs` (neu — E2E-Absicherung der sichtbaren Gruppenreihenfolge: erste Gruppe enthält `#Back`, letzte Gruppe `#OpenAttachments`)
- `AGENT.md` (geändert — Dokumentation der Konvention „Navigation an Position 0", keine UI-Wirkung)
