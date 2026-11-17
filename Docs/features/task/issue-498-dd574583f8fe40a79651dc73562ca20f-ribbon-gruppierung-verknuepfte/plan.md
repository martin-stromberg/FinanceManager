# Umsetzungsplan: Ribbon-Gruppierung „Verknüpfte Informationen" an letzte Position (Issue #498)

## Übersicht

Drei Card-ViewModels (`SavingsPlanCardViewModel`, `BankAccountCardViewModel`, `PostingsCardViewModel`) liefern ihre `UiRibbonTab`-Listen in einer Reihenfolge, die der Gruppierungsregel aus `AGENT.md` widerspricht: Die Gruppe `Ribbon_Group_Linked` steht vor Aktionsgruppen statt an letzter Position. Umgesetzt wird eine reine Umordnung der Listeneinträge — `Linked` wird jeweils ans Ende verschoben — sowie die im Issue vorgeschlagene Ergänzung von `AGENT.md` um die Meta-Konvention „`Navigation`-Gruppe an Position 0". Abgesichert wird die Korrektur durch neue Reihenfolge-Assertions in Unit-Tests (ViewModel-Ebene), einen bUnit-Komponententest (DOM-Reihenfolge) und einen neuen Playwright-E2E-Test für alle drei Detailseiten; ergänzend erfolgt die projektregelkonforme reale End-to-End-Abnahme.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die geordnete `UiRibbonTab`-Liste innerhalb des von `GetRibbonRegisters` zurückgegebenen `UiRibbonRegister` (Kind `UiRibbonRegisterKind.Actions`) je Detail-/Kartenansicht |
| Auslösende Ereignisse | Kein eigenes Nutzer- oder Systemereignis — die Reihenfolge wirkt bei jedem Ribbon-Aufbau beim Rendern der drei Detailseiten (Sparplan-Karte `/card/savings-plans/{id}`, Bankkonto-Karte `/card/accounts/{id}`, Posten-Karte `/card/postings/{id}`) |
| Erwartete Wirkung | In der Ribbon-Leiste der drei Detailseiten erscheint die Gruppe „Verknüpfte Informationen" sichtbar als letzte Gruppe. Zielreihenfolgen: `SavingsPlanCardViewModel`: Navigation → Manage → Analysis → Bookings → Linked; `BankAccountCardViewModel`: Navigation → Manage → Bookings → Linked; `PostingsCardViewModel`: Navigation → Actions → Linked |
| Umgang mit fehlenden Objekten | Nicht zutreffend — alle betroffenen Gruppen und Aktionen existieren bereits; es werden keine Ribbon-Elemente erzeugt, wiederverwendet, aktiviert oder verworfen |

### Scope-Ausschlüsse

Alle folgenden Ausschlüsse sind bereits im Issue bzw. in der `requirement.md` selbst festgelegt und damit durch den Anforderungssteller bestätigt — es verbleibt kein `offener` Punkt:

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Änderung an Inhalt, Aktionen, Labels, Icons oder `MobileShortcut`-Flags der betroffenen Gruppen — ausschließlich die Position des `Linked`-Tabs ändert sich | Issue-Scope: reine Umordnung | bestätigt (im Issue festgelegt) |
| 2 | Keine Umbenennung von Ressourcen-Keys und keine resx-Änderungen | Issue-Scope | bestätigt (im Issue festgelegt) |
| 3 | Keine Veränderung der `Navigation`-Gruppe; sie bleibt projektkonform an Position 0 | Issue-Scope; Konvention wird zusätzlich in `AGENT.md` dokumentiert (siehe Designentscheidungen) | bestätigt (im Issue festgelegt) |
| 4 | Kein Refactoring zu einer generischen/zentralen Sortierlogik und keine Anpassung weiterer, bereits konformer ViewModels (`SecurityCardViewModel`, `ContactCardViewModel`, `StatementDraftCardViewModel`, `StatementDraftEntryCardViewModel`) | Issue-Scope: minimalinvasive Korrektur nur der drei Befund-Stellen | bestätigt (im Issue festgelegt) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Festlegung der Gruppenreihenfolge | Reine Listenreihenfolge der `UiRibbonTab`-Einträge; `Sort` bleibt überall auf dem Default `0` | Alle vier konformen Referenzimplementierungen steuern die Reihenfolge ausschließlich über die Listenposition. `Ribbon.razor` sortiert per stabilem `OrderBy(g => g.Sort)` — bei gleichem `Sort` entscheidet die First-Seen-Reihenfolge. Explizite `Sort`-Werte wären ein größerer Diff ohne funktionalen Mehrwert, würden einen zweiten Ordnungsmechanismus etablieren und vom Projekt Muster abweichen. Minimalinvasive, issue-konforme Variante. |
| `AGENT.md`-Ergänzung „`Navigation`-Gruppe an Position 0" | Wird im gleichen Change umgesetzt: ein Satz im Abschnitt „Gruppierung des Ribbon-Menüs", der festhält, dass eine vorhandene `Navigation`-Gruppe (Zurück) stets an Position 0 — vor den drei kategorisierten Gruppen — steht | Im Issue explizit als sinnvolle Ergänzung benannt („könnte ergänzt werden"); dokumentiert eine projektweit bereits konsistent gelebte Konvention genau dort, wo die von diesem Issue durchgesetzte Regel steht. Aufwand und Risiko praktisch null. Das Weglassen wäre ein unbestätigter Scope-Ausschluss gegenüber der Issue-Formulierung. |

## Programmabläufe

### Ribbon-Aufbau auf den drei Karten-Detailseiten

Der Ablauf ist unverändert — es ändert sich ausschließlich die Reihenfolge der von den ViewModels gelieferten Tab-Liste:

1. `CardPage.razor` rendert die `Ribbon`-Komponente mit dem ViewModel als `IRibbonProvider`.
2. `Ribbon.BuildTabsToRender()` ruft `Provider.GetRibbonRegisters(Localizer)` auf → `BaseViewModel.GetRibbonRegisters` ruft das überschriebene `GetRibbonRegisterDefinition` des jeweiligen Card-ViewModels auf und aggregiert Register aktiver Kind-ViewModels.
3. Das ViewModel liefert ein `UiRibbonRegister` (`UiRibbonRegisterKind.Actions`) mit der **korrigierten** Tab-Liste — `Ribbon_Group_Linked` als letzter Eintrag.
4. `BuildTabsToRender()` merged Gruppen registerübergreifend nach Titel in `groupMap` und hält die First-Seen-Reihenfolge in `groupOrder` fest.
5. `Groups.OrderBy(g => g.Sort)` sortiert stabil; da alle Tabs `Sort = 0` haben, wird die First-Seen-Reihenfolge zur sichtbaren DOM-Reihenfolge der `.fm-ribbon-group`-Elemente — `Linked` erscheint als letzte Gruppe.

Beteiligte Klassen/Komponenten: `SavingsPlanCardViewModel`, `BankAccountCardViewModel`, `PostingsCardViewModel`, `BaseViewModel` (unverändert), `Ribbon.razor` (unverändert), `CardPage.razor` (unverändert).

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `SavingsPlanCardViewModel` (`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`)

- **Geänderte Methoden:** `GetRibbonRegisterDefinition(IStringLocalizer)` — Zeile 895: `var tabs = new List<UiRibbonTab> { nav, manage, linked, analysis, bookings };` wird zu `{ nav, manage, analysis, bookings, linked }`. Gruppeninhalte, `baseRegs`-Anhängung und Register-Kind bleiben unverändert.

### `BankAccountCardViewModel` (`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`)

- **Geänderte Methoden:** `GetRibbonRegisterDefinition(IStringLocalizer)` — Zeilen 302–308: der `UiRibbonTab` mit `Ribbon_Group_Linked` (Zeile 306) wandert hinter den `Ribbon_Group_Bookings`-Eintrag (Zeile 307). Zielreihenfolge der inline aufgebauten Liste: Navigation → Manage → Bookings → Linked. Gruppeninhalte bleiben unverändert.

### `PostingsCardViewModel` (`FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`)

- **Geänderte Methoden:** `GetRibbonRegisterDefinition(IStringLocalizer)` — Zeile 154: `new List<UiRibbonTab> { nav, linked, actions }` wird zu `{ nav, actions, linked }`. Gruppeninhalte bleiben unverändert.

### `AGENT.md` (Dokumentation, keine Klasse)

- Im Abschnitt „Gruppierung des Ribbon-Menüs" wird ergänzt, dass eine vorhandene `Navigation`-Gruppe (Zurück-Aktion, `Ribbon_Group_Navigation`) stets an Position 0 steht — vor den drei kategorisierten Gruppen. Ein Satz, keine strukturelle Änderung der Regel.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Kompatibilitäts-Hilfsproperties `UiRibbonRegister.Title`/`Items`:** Sie liefern Titel/Items des **ersten** Tabs. In allen drei ViewModels bleibt `Navigation` der erste Tab — die Hilfsproperties sind unverändert.
- **Registerübergreifendes Gruppen-Merging in `Ribbon.razor`:** Gruppen werden nach Titel gemerged. Da die Umordnung innerhalb eines einzigen Registers erfolgt und `Linked` in keinem anderen Register derselben Seiten vorkommt, ändert sich am Merge-Ergebnis nichts — lediglich die First-Seen-Position des `Linked`-Titels verschiebt sich wie gewünscht ans Ende.
- **Kind-ViewModel-Register:** `BaseViewModel.GetRibbonRegisters` hängt Register aktiver Kind-ViewModels an. Keines der drei ViewModels trägt Kind-ViewModel-Register bei, die eine `Linked`-Gruppe enthielten — die sichtbare Endposition ist garantiert. Die geplanten Unit-Tests assertieren die Reihenfolge auf dem `Actions`-Register und zusätzlich, dass `Linked` der letzte Tab aller gelieferten Register ist.
- **Keine bekannten weiteren Seiteneffekte:** Keine API-, Persistenz-, resx- oder Verhaltensänderung; bestehende Tests, die Vorhandensein von Gruppen/Aktionen prüfen, bleiben gültig.

## Umsetzungsreihenfolge

1. **`Linked`-Tab in den drei ViewModels ans Ende der Tab-Liste verschieben**
   - Voraussetzungen: Keine — reine Umordnung vorhandener Listeneinträge.
   - Beschreibung: `SavingsPlanCardViewModel.cs` Zeile 895 → `{ nav, manage, analysis, bookings, linked }`; `BankAccountCardViewModel.cs` Zeilen 302–308 → `Linked`-Eintrag hinter `Bookings`; `PostingsCardViewModel.cs` Zeile 154 → `{ nav, actions, linked }`.

2. **`AGENT.md` um die `Navigation`-Positionskonvention ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: Im Abschnitt „Gruppierung des Ribbon-Menüs" einen Satz ergänzen, der die Meta-Konvention „`Navigation`-Gruppe (Zurück) an Position 0" festhält (gemäß Designentscheidung).

3. **Neue Unit-Tests für die Tab-Reihenfolge schreiben**
   - Voraussetzungen: Schritt 1 abgeschlossen; vorhandene Testinfrastruktur (`DummyLocalizer` gibt den resx-Key als Titel zurück, `CreateVm`-Fabriken in den drei Testklassen — alles bereits im Repo).
   - Beschreibung: Je ein `[Fact]` in `AccountDetailViewModelTests`, `PostingsCardViewModelReversalTests` und `SavingsPlanCardLinkTests`, der die exakte Titel-Sequenz der Tabs des `Actions`-Registers assertiert und zusätzlich prüft, dass `Ribbon_Group_Linked` der letzte Tab über alle gelieferten Register ist. Die Tests müssen vor der Korrektur rot sein (erwartete Reihenfolge ≠ Ist-Reihenfolge) und danach grün — da Schritt 1 zuerst erfolgt, wird der Rot-Zustand beim Schreiben gegen den alten Stand verifiziert oder die Assertions werden einmalig gegen den Ausgangszustand gegengeprüft.

4. **bUnit-Komponententest für die DOM-Gruppenreihenfolge schreiben**
   - Voraussetzungen: Vorhandene `RibbonTests`-Infrastruktur (`RenderRibbon(registers)` / `CreateRegisters(actions)`-Helfer, `IRibbonProvider`-Mock).
   - Beschreibung: Neuer Test in `FinanceManager.Tests/Components/RibbonTests.cs`, der ein Register mit mehreren Gruppen (inkl. absichtlich „falscher" Reihenfolge und gleichem `Sort`) rendert und die Textreihenfolge der `.fm-ribbon-group-footer`-Elemente gegen die Listenreihenfolge assertiert — Nachweis, dass die Komponente die First-Seen-Reihenfolge stabil rendert.

5. **Playwright-E2E-Test für die sichtbare Gruppenreihenfolge schreiben**
   - Voraussetzungen: Schritt 1; vorhandene E2E-Infrastruktur (`PlaywrightWebAppFixture`, `TestUserSeeder`, `AuthGateway`, `AccountsApiSeedHelper` inkl. `CreateAccountWithBalanceAsync` mit Posting-Seeding, `BrowserApiHelper`, API `/api/postings/account/{id}`, Card-Routen `/card/accounts/{id}`, `/card/savings-plans/{id}`, `/card/postings/{id}`) — alles bereits im Repo.
   - Beschreibung: Neue Testklasse `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs` mit drei `[Fact]`s (je Detailseite): Benutzer anlegen und einloggen, Entität seeden, Kartenseite öffnen, assertieren, dass die letzte `.fm-ribbon-group`-Gruppe eine Linked-Aktion enthält (sprachunabhängig über die Aktions-`id`, z. B. `#OpenAttachments`; zusätzlich Position der `Back`-Aktion in der ersten Gruppe).

6. **Unit- und Komponententests ausführen**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj -- --filter-not-trait "Category=OsInterface"` — alle 2148 bestehenden Tests plus die neuen müssen grün sein.

7. **E2E-Test ausführen**
   - Voraussetzungen: Schritt 5; lokal installierte Playwright-Browser (vorhanden).
   - Beschreibung: `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj -- --filter "FullyQualifiedName~RibbonGroupOrder"` — der neue Test muss grün sein. Hinweis: Zwei preexisting E2E-Fehlschläge (`ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi`, `SecurityTrendReversalE2ETests.SetupNotificationsTab_TrendReversalCheckbox_PersistsToggle`) sind in `inventory/tests.md` nachgewiesen und nicht Teil dieses Changes.

8. **Reale End-to-End-Abnahme (manuell, Pflicht gemäß Projektregeln)**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Anwendung tatsächlich starten, als Benutzer anmelden und die drei Akzeptanzszenarien durchgehen: Sparplan-Karte, Bankkonto-Karte und Posten-Karte öffnen und die sichtbare Ribbon-Gruppenreihenfolge prüfen (`Linked` jeweils letzte Gruppe, `Navigation` erste). Ergebnis dokumentieren; bei Nichtdurchführbarkeit an den Anwender eskalieren (manuelle Abnahme oder ausdrücklicher Verzicht).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetRibbon_GroupOrder_PlacesLinkedLast` | `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs` | `BankAccountCardViewModel.GetRibbonRegisters` liefert im `Actions`-Register exakt die Titel-Sequenz `Ribbon_Group_Navigation` → `Ribbon_Group_Manage` → `Ribbon_Group_Bookings` → `Ribbon_Group_Linked`; `Linked` ist letzter Tab über alle Register. Nutzt die vorhandene `CreateVm()`-Fabrik und `DummyLocalizer`. |
| `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` | `FinanceManager.Tests/ViewModels/PostingsCardViewModelReversalTests.cs` | `PostingsCardViewModel` liefert exakt `Ribbon_Group_Navigation` → `Ribbon_Group_Actions` → `Ribbon_Group_Linked`; `Linked` ist letzter Tab. Nutzt `CreateVm()` + `DummyLocalizer` (analog `FindReverseAction`-Muster). |
| `Ribbon_GroupOrder_PlacesLinkedLast` | `FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs` | `SavingsPlanCardViewModel` liefert exakt `Ribbon_Group_Navigation` → `Ribbon_Group_Manage` → `Ribbon_Group_Analysis` → `Ribbon_Group_Bookings` → `Ribbon_Group_Linked`; `Linked` ist letzter Tab. Nutzt `CreateVm()` + `DummyLocalizer` (analog `FindActualReportAction`-Muster). |
| `Groups_RenderInTabListOrder` | `FinanceManager.Tests/Components/RibbonTests.cs` (bUnit) | Ein Register mit mehreren `UiRibbonTab`s (gleicher `Sort`, absichtlich unsortierte Beispielreihenfolge) wird gerendert; die Texte der `.fm-ribbon-group-footer`-Elemente erscheinen im DOM exakt in der Listenreihenfolge — Nachweis der stabilen First-Seen-Sortierung auf Komponentenebene. |
| `RibbonGroupOrder_*` (3 `[Fact]`s: SavingsPlanCard / BankAccountCard / PostingsCard) | `FinanceManager.Tests.E2E/Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs` (neu) | Je Detailseite: Letzte `.fm-ribbon-group` enthält eine Linked-Aktion (`#OpenAttachments`); erste `.fm-ribbon-group` enthält `#Back`. Seeding: Sparplan via `POST /api/savings-plans`; Konto via `AccountsApiSeedHelper.CreateAccountAsync`; Posten via `CreateAccountWithBalanceAsync` mit `SeededPosting` + `GET /api/postings/account/{id}` für die Posting-Id, dann `/card/postings/{id}`. |

### Betroffene bestehende Tests

Keine — vorhandene Tests assertieren nur das Vorhandensein von Gruppen/Aktionen (z. B. `AccountDetailViewModelTests.GetRibbon_ContainsExpectedGroups`) und bleiben gültig.

### E2E-Tests (primärer Funktionsnachweis)

Die Änderung erzeugt zwar keine neue Benutzerinteraktion, aber eine sichtbare Wirkung in der UI (Gruppenreihenfolge) — die Projektregeln verlangen daher konkrete E2E-/UI-Abdeckung plus reale Abnahme:

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Bankkonto-Karte: `Linked` ist die sichtbar letzte Ribbon-Gruppe, `Navigation` die erste | `Tests/Navigation/RibbonGroupOrderPlaywrightTests.cs` — `BankAccountCard_LinkedGroup_IsLastRibbonGroup` | Zielreihenfolge Navigation → Manage → Bookings → Linked | Nur ein echter Browser-Rendervorgang beweist die sichtbare Gruppenreihenfolge inkl. `OrderBy`-Sortierung, Gruppen-Merging und CSS-Layout |
| Pflicht | Sparplan-Karte: `Linked` ist die sichtbar letzte Ribbon-Gruppe | `RibbonGroupOrderPlaywrightTests` — `SavingsPlanCard_LinkedGroup_IsLastRibbonGroup` | Navigation → Manage → Analysis → Bookings → Linked | Größte der drei Tab-Listen (5 Gruppen) — hier wäre ein Merging-/Sortierungsfehler am ehesten sichtbar |
| Pflicht | Posten-Karte: `Linked` ist die sichtbar letzte Ribbon-Gruppe | `RibbonGroupOrderPlaywrightTests` — `PostingsCard_LinkedGroup_IsLastRibbonGroup` | Navigation → Actions → Linked | Hier verletzt `Linked` aktuell sogar die Entitätsaktion `Reverse` (Stufe 1 der Regel) — klarster fachlicher Befund |
| Pflicht | Reale Abnahme: Anwendung starten, drei Detailseiten im Browser öffnen, Gruppenreihenfolge sichtbar prüfen | Manuell (Schritt 8 der Umsetzungsreihenfolge), dokumentiert in `test-results.md` | Alle drei Zielreihenfolgen am lebenden System | Projektregel: zusätzlich zu automatisierten Tests ist eine reale End-to-End-Abnahme Pflicht |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — kein bestehender E2E-Test assertiert die Gruppenreihenfolge.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path Sparplan-Karte | Nutzer öffnet die Detailseite eines bestehenden Sparplans | Angemeldeter Benutzer; mindestens ein Sparplan existiert (wird im Test via API geseedet) | `/card/savings-plans/{id}` aufrufen → Ribbon wird aufgebaut | Ribbon-Gruppen in Reihenfolge Navigation → Verwalten → Analyse → Buchungen → **Verknüpfte Informationen** (letzte Gruppe) | E2E `SavingsPlanCard_LinkedGroup_IsLastRibbonGroup` + reale Abnahme |
| 2 | Happy Path Bankkonto-Karte | Nutzer öffnet die Detailseite eines bestehenden Bankkontos | Angemeldeter Benutzer; mindestens ein Konto existiert (geseedet) | `/card/accounts/{id}` aufrufen → Ribbon wird aufgebaut | Ribbon-Gruppen in Reihenfolge Navigation → Verwalten → Buchungen → **Verknüpfte Informationen** (letzte Gruppe) | E2E `BankAccountCard_LinkedGroup_IsLastRibbonGroup` + reale Abnahme |
| 3 | Happy Path Posten-Karte | Nutzer öffnet die Detailseite eines bestehenden Postens | Angemeldeter Benutzer; Konto mit mindestens einem Posten existiert (geseedet) | `/card/postings/{id}` aufrufen → Ribbon wird aufgebaut | Ribbon-Gruppen in Reihenfolge Navigation → Aktionen → **Verknüpfte Informationen** (letzte Gruppe); `Reverse` steht in der Aktionsgruppe vor `Linked` | E2E `PostingsCard_LinkedGroup_IsLastRibbonGroup` + reale Abnahme |
| 4 | Neuanlage-Karte (Randzustand) | Nutzer öffnet eine Karte im Neuanlage-Modus (z. B. `/card/accounts/new`) | Angemeldeter Benutzer, noch keine Entität gespeichert | Karte im New-Modus öffnen | Gruppenreihenfolge identisch — `Linked` bleibt letzte Gruppe (einzelne Aktionen darin ggf. deaktiviert, Gruppe selbst wird weiterhin zuletzt gerendert) | Unit-Tests (reihenfolgeunabhängig vom Ladezustand; die drei ViewModels geben die Tab-Liste unabhängig von `IsEdit` aus) |

Keine Wartesituationen, asynchronen Prozesse oder externen Abhängigkeiten — die Reihenfolge wirkt synchron beim Rendern.

## Offene Punkte

Keine — die beiden offenen Fragen aus `requirement.md` wurden als Designentscheidungen getroffen: (1) Die `AGENT.md`-Ergänzung wird im gleichen Change umgesetzt. (2) Die Reihenfolge wird über die reine Listenposition festgelegt, ohne explizite `Sort`-Werte.
