# Umsetzungsplan: Posten-Karte — Handler für die Ribbon-Aktionen der Gruppe „Verknüpfte Informationen" (Issue #502)

## Übersicht

Die fünf Ribbon-Aktionen der `Linked`-Gruppe auf der Posten-Detailkarte (`PostingsCardViewModel`, Route `/card/postings/{id}`) werden funktionsfähig verdrahtet: Die vier Navigationsaktionen `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity` feuern künftig einen URL-Payload über den vorhandenen generischen `case "OpenPostings"` in `CardPage.razor`, und `OpenAttachments` nutzt `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` für das Anhangs-Overlay. Betroffen ist ausschließlich `PostingsCardViewModel.GetRibbonRegisterDefinition` (Zeilen 137–144); `CardPage.razor`, `OverlayHost`, `AttachmentsPanel` und die Handler-Infrastruktur bleiben unverändert.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Eine `UiRibbonAction` der `Linked`-Gruppe in `PostingsCardViewModel.GetRibbonRegisterDefinition` (Zeilen 137–144) samt ihrem `Callback` |
| Auslösende Ereignisse | Nutzerklick auf einen der fünf Ribbon-Buttons `OpenAccount` / `OpenContact` / `OpenSavingsPlan` / `OpenSecurity` / `OpenAttachments` auf der Posten-Karte (`/card/postings/{id}`); `Ribbon.razor` ruft `Callback` nur bei `Disabled == false` |
| Erwartete Wirkung | Navigation zur Karte des verknüpften Datensatzes (`/card/accounts/{AccountId}`, `/card/contacts/{ContactId}`, `/card/savings-plans/{SavingsPlanId}`, `/card/securities/{SecurityId}`, jeweils mit `?back=`) bzw. Öffnen des `AttachmentsPanel`-Overlays für `AttachmentEntityKind.Posting` mit `ParentId = Id` |
| Umgang mit fehlenden Objekten | Verknüpfungs-Id nicht gesetzt (`AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId == null`): Button bleibt deaktiviert — wiederverwenden, kein Erzeugen. Posten nicht geladen (`Posting == null`, z. B. `Id == Guid.Empty` oder Ladefehler): alle fünf Linked-Aktionen deaktiviert — `OpenAttachments`-`Disabled` wird von hart `false` auf `Posting == null` gezogen (Geschwistermuster `Account == null` in `BankAccountCardViewModel`). Ziel-Entität zur Laufzeit nicht mehr vorhanden: NotFound-/Error-Handling der Zielkarte greift — kein Zusatzverhalten auf der Posten-Karte. `NavigationManager` in Callbacks nicht auflösbar (Unit-Test-Kontext): Fallback-URL ohne `?back=` feuern — verwerfen statt Fehler. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Entfernung der fünf Aktionen — sie bleiben erhalten und werden verdrahtet | Verbindliche Anwenderentscheidung im Issue-#502-Kommentar | bestätigt (Issue-Kommentar) |
| 2 | Keine Änderung an Labels, Icons, `MobileShortcut`-Flags, Gruppenzuordnung oder Reihenfolge des Ribbons | `requirement.md` Scope; Gruppenreihenfolge wurde mit #498 korrigiert (`Linked` bereits letzte Gruppe) | bestätigt (requirement.md) |
| 3 | `OpenContact`/`OpenSavingsPlan`/`OpenSecurity` bleiben deaktiviert, wenn die jeweilige Id am Posten `null` ist — es erfolgt keine Auflösung über `LinkedPosting*`-Felder oder `Postings_GetGroupLinksAsync` | Anforderung verlangt nur das Öffnen der direkt am Posten gesetzten Verknüpfung; `GroupLinksDto` wird vom ViewModel bewusst nicht verwendet | bestätigt (Issue-Kommentar 2026-11-11) |
| 4 | Keine Datenmodell-/Persistenzänderung und keine resx-Änderung | `AttachmentEntityKind.Posting = 7` existiert, Attachments-API ist art-agnostisch, alle `Ribbon_*`-Labels vorhanden | bestätigt (requirement.md) |
| 5 | Keine Veränderung der Handler-Infrastruktur (`UiActionEventArgs`, `UiOverlaySpec`, `OverlayHost`, `Ribbon`) sowie kein neuer `case` in `CardPage.razor` | Generischer `case "OpenPostings"` (Zeilen 192–194) deckt die URL-Navigation ab; `case "OpenAttachments"` bleibt bewusst No-Op (Kommentar Zeilen 253–255) | bestätigt (requirement.md) |
| 6 | Kein Upload-/Verwaltungszusatz für Posten-Anhänge außerhalb des vorhandenen `AttachmentsPanel` | Das generische Panel deckt Liste/Upload/Download bereits ab | bestätigt (Issue-Kommentar 2026-11-11) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Verdrahtung der vier Navigationsaktionen | **Variante 1 — URL-Payload über den generischen `case "OpenPostings"`** (Muster `StatementDraftEntryCardViewModel` Zeilen 997–1053): Jeder Callback baut die Ziel-URL `/card/{route}/{id}` inkl. `?back={EscapeDataString(nav.Uri)}` und feuert `RaiseUiActionRequested("OpenPostings", url)` | Erzeugt projektkonform die wenigste Sonderlogik: `CardPage.razor` bleibt vollständig unverändert — Variante 2 würde vier neue, nahezu identische typspezifische `case`s in den bereits großen `OnUiActionRequested`-Switch einführen. Das fachlich identische Geschwistermuster ist die `Linked`-Gruppe des `StatementDraftEntryCardViewModel`, die exakt dieselbe Aufgabe (Karte des verknüpften Kontakts/Sparplans/Wertpapiers öffnen, inkl. `?back=`) bereits auf diesem Weg löst. Der semantisch generische Aktionsname `OpenPostings` ist dabei bereits etablierte Konvention (dort feuert er ebenfalls URLs für contacts/savings-plans/securities). |
| Quelle des `back`-Parameters | `ServiceProvider.GetService<NavigationManager>()?.Uri` mit try/catch-Fallback auf die URL ohne `?back=` (exaktes Geschwistermuster) | Hält den Callback robust gegenüber Kontexten ohne registrierten `NavigationManager` (Unit-Tests) und entspricht Zeile für Zeile der Referenzimplementierung; `BuildCardPath` der `CardPage` steht nur Variante 2 zur Verfügung. |
| `OpenAttachments`-Verdrahtung | `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` (geerbte Basismethode, `BaseViewModel` Zeilen 325–334) | Einheitliches Overlay-Muster aller Karten (`BankAccountCardViewModel` Z. 279, `SavingsPlanCardViewModel` Z. 862, `SecurityCardViewModel` Z. 383, `ContactCardViewModel` Z. 301); erzeugte `UiOverlaySpec` wird vom `OverlayHost` gerendert, `CardPage`-`case "OpenAttachments"` bleibt bewusst No-Op. |
| URL-Aufbau in den vier Callbacks | Private Hilfsmethode `RequestOpenLinkedCard(string route, Guid? linkedId)` im `PostingsCardViewModel` | Vermeidet das Vierfach-Kopieren des ~15-zeiligen try/catch-URL-Blocks des Geschwistermusters; kapselt Guard (`linkedId != null`), `back`-Anhang und `RaiseUiActionRequested("OpenPostings", url)` an einer Stelle. |

## Programmabläufe

### Navigation zur Karte eines verknüpften Datensatzes (`OpenAccount`/`OpenContact`/`OpenSavingsPlan`/`OpenSecurity`)

1. `Ribbon.razor` (`OnItemClick`) prüft `item.Disabled` — bei `Posting?.X == null` ist der Button deaktiviert, der Klick bricht ab (bestehendes Verhalten, bleibt).
2. Der `Callback` der `UiRibbonAction` ruft die neue private Hilfsmethode `RequestOpenLinkedCard(route, linkedId)` auf (z. B. `RequestOpenLinkedCard("accounts", Posting?.AccountId)`).
3. `RequestOpenLinkedCard` prüft `linkedId != null` (Guard gegen Race bei entfernter Verknüpfung) und baut `url = $"/card/{route}/{linkedId}"`.
4. In `try`: `NavigationManager` via `ServiceProvider.GetService<NavigationManager>()` auflösen; ist `nav?.Uri` nicht leer, `url += "?back=" + Uri.EscapeDataString(nav.Uri)`. Im `catch` (oder ohne `nav`): URL ohne `back` verwenden.
5. `RaiseUiActionRequested("OpenPostings", url)` feuert `UiActionRequested` mit dem URL-String-Payload.
6. `CardPage.razor` `OnUiActionRequested` → vorhandener `case "OpenPostings"` (Zeilen 192–194): `NavigationManager.NavigateTo(payload)` navigiert zur Zielkarte.
7. Die Zielkarte (`BankAccountCardViewModel`, `ContactCardViewModel`, `SavingsPlanCardViewModel`, `SecurityCardViewModel`) initialisiert sich über `CardViewModelResolver`/`ICardInitializable`; ihr `?back=`-Parameter führt per `SetBackNavigation` dazu, dass deren `Back`-Aktion zur Posten-Karte zurückkehrt.

Beteiligte Klassen/Komponenten: `PostingsCardViewModel`, `Ribbon.razor`, `CardPage.razor` (unveränderter `case "OpenPostings"`), `CardViewModelResolver`, Ziel-Card-ViewModels.

### Öffnen des Anhangs-Overlays (`OpenAttachments`)

1. `Ribbon.razor` prüft `item.Disabled` — neu `Posting == null` (statt hart `false`), sodass nach Ladefehler/`Guid.Empty` kein Klick möglich ist.
2. Der `Callback` ruft `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` (Basismethode) auf.
3. `RequestOpenAttachments` baut `UiOverlaySpec(typeof(AttachmentsPanel), { ["ParentKind"] = AttachmentEntityKind.Posting, ["ParentId"] = Id })` und feuert `RaiseUiActionRequested("OpenAttachments", spec)`.
4. `CardPage.razor` `case "OpenAttachments"` ist bewusst No-Op (Zeilen 253–255) — bleibt unverändert.
5. `OverlayHost.razor` (`OnUiActionRequested`, Zeilen 86–94) erkennt `e.PayloadObject is UiOverlaySpec` und rendert `AttachmentsPanel` als Modal-Dialog mit `ParentKind`/`ParentId` sowie Titel `Attachments_Title`.
6. `AttachmentsPanel` lädt Posten-Anhänge art-agnostisch über `Attachments_ListAsync((short)AttachmentEntityKind.Posting, Id, …)`; serverseitig erzeugte Posten-Anhänge (aus `StatementDraftService.PropagateEntryAttachmentsAsync`, Zeilen 58–63) werden sichtbar.

Beteiligte Klassen/Komponenten: `PostingsCardViewModel`, `BaseViewModel.RequestOpenAttachments`, `OverlayHost.razor`, `AttachmentsPanel.razor`, `IApiClient` (Attachments-API).

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `PostingsCardViewModel` (`FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`)

- **Neue Methoden:** `RequestOpenLinkedCard(string route, Guid? linkedId)` (`private`) — baut `/card/{route}/{linkedId}` inkl. optionalem `?back=` aus `ServiceProvider.GetService<NavigationManager>()?.Uri` (try/catch-Fallback ohne `back`), feuert `RaiseUiActionRequested("OpenPostings", url)`; No-Op bei `linkedId == null`.
- **Geänderte Methoden:** `GetRibbonRegisterDefinition(IStringLocalizer)` (Zeilen 122–155) — im `linked`-Tab (Zeilen 137–144):
  - `OpenAccount`: `Callback` → `RequestOpenLinkedCard("accounts", Posting?.AccountId)`; `Disabled` bleibt `Posting?.AccountId == null`.
  - `OpenContact`: `Callback` → `RequestOpenLinkedCard("contacts", Posting?.ContactId)`; `Disabled` bleibt `Posting?.ContactId == null`.
  - `OpenSavingsPlan`: `Callback` → `RequestOpenLinkedCard("savings-plans", Posting?.SavingsPlanId)`; `Disabled` bleibt `Posting?.SavingsPlanId == null`.
  - `OpenSecurity`: `Callback` → `RequestOpenLinkedCard("securities", Posting?.SecurityId)`; `Disabled` bleibt `Posting?.SecurityId == null`.
  - `OpenAttachments`: `Callback` → `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)`; `Disabled` von hart `false` → `Posting == null`.
  - Keine Änderung an Ids, Labels, Icons, `MobileShortcut`, Tabs oder Reihenfolge.
- **Neue Event-Handler:** Keine.

### `AccountsApiSeedHelper` (`FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`) — nur E2E-Testinfrastruktur

- **Geänderte Methoden/Typen:** `SeededPosting`-Record um optionale Felder `ContactId`/`SavingsPlanId`/`SecurityId`/`Kind` erweitern; `CreateAccountWithBalanceAsync` übergibt diese Werte an den `Posting`-Konstruktor (statt fixer `contactId: null, savingsPlanId: null, securityId: null`, Zeilen 108–116). Zweck: E2E-Seed eines Postens mit gesetzten Verknüpfungs-Ids zur Abdeckung von `OpenContact`/`OpenSavingsPlan`/`OpenSecurity`.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine. (Die Deaktivierungslogik der `UiRibbonAction`s verhindert ungültige Auslösungen bereits deklarativ; `Ribbon.razor` bricht bei `Disabled` ab, und `RequestOpenLinkedCard` guardet `linkedId == null` zusätzlich.)

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`case "OpenPostings"` in `CardPage.razor`:** Wird nun zusätzlich von der Posten-Karte genutzt; der Handler ist generisch (`NavigateTo(payload)`) und unverändert — kein Risiko für bestehende Aufrufer (`StatementDraftEntryCardViewModel`, `BankAccountCardViewModel`-`OpenPostings`).
- **`OpenAttachments`-`Disabled`-Flag:** Wechsel von hart `false` auf `Posting == null` — bei Ladefehler oder `Id == Guid.Empty` ist der Button neu deaktiviert; das verhindert ein Overlay mit `ParentId = Guid.Empty`. Gewünschte Verhaltensänderung, kein Risiko.
- **`UiOverlaySpec`-Payload `OpenAttachments`:** `OverlayHost` reagiert auf `PayloadObject is UiOverlaySpec` unabhängig vom Provider — identischer Pfad wie auf den vier Schwester-Karten, keine Auswirkung auf andere Seiten.
- **Preexisting E2E-Fehlschlag:** `ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` schlägt in der Baseline fehl (Login-Navigation, `ERR_ABORTED`) — ohne Bezug zu dieser Änderung; beim E2E-Lauf als bekanntes Fehlerbild zu erwarten.

## Umsetzungsreihenfolge

1. **`PostingsCardViewModel`: Linked-Aktionen verdrahten**
   - Voraussetzungen: Keine — alle benötigten Elemente existieren im Repo (`RequestOpenAttachments` in `BaseViewModel`, `AttachmentEntityKind.Posting`, generischer `case "OpenPostings"` in `CardPage.razor`, `NavigationManager` in der App-DI, resx-Labels).
   - Beschreibung: Private Hilfsmethode `RequestOpenLinkedCard` anlegen; die vier Navigations-Callbacks auf `RaiseUiActionRequested("OpenPostings", url)` mit Ziel-URL inkl. `?back=` umstellen; `OpenAttachments`-Callback auf `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` umstellen und `Disabled` auf `Posting == null` setzen.

2. **Unit-Tests: neue Testklasse `PostingsCardViewModelLinkedTests`**
   - Voraussetzungen: Schritt 1; vorhandene Testinfrastruktur (`ServiceCollection`, `Mock<IApiClient>`, `DummyLocalizer`, `TestCurrentUserService` — Muster aus `PostingsCardViewModelReversalTests`).
   - Beschreibung: Tests für Action-Id + URL-Payload der vier Navigationsaktionen, `UiOverlaySpec` (`AttachmentsPanel`, `ParentKind = Posting`, `ParentId = Id`) von `OpenAttachments` sowie alle `Disabled`-Konstellationen.

3. **E2E-Infrastruktur: `AccountsApiSeedHelper.SeededPosting` erweitern**
   - Voraussetzungen: Keine (reine Testcode-Erweiterung; `Posting`-Konstruktor akzeptiert die Ids bereits).
   - Beschreibung: Optionale `ContactId`/`SavingsPlanId`/`SecurityId`/`Kind`-Felder am Seed-Record und deren Weitergabe an den `Posting`-Konstruktor.

4. **E2E-Tests: `PostingsCardLinkedActionsPlaywrightTests`**
   - Voraussetzungen: Schritte 1 und 3; vorhandene Helpers `BrowserApiHelper`, `TestUserSeeder`, `AuthGateway`, `PlaywrightWebAppFixture`.
   - Beschreibung: Playwright-Tests für Klick-Navigation (`OpenAccount` inkl. `?back=`-Rückweg), `OpenAttachments`-Overlay, deaktivierte Buttons bei fehlender Verknüpfung, deaktivierte Buttons bei nicht ladbarem Posten (nicht existierende Id → alle fünf Linked-Buttons `disabled`) sowie — mit geseedeten Verknüpfungs-Ids — `OpenContact`/`OpenSavingsPlan`/`OpenSecurity`.

5. **Vollständige Verifikation**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet test` für `FinanceManager.Tests` (Filter `--filter-not-trait "Category=OsInterface"`), `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E`; dokumentierter preexisting Fehlschlag (`AuthenticatedUser_ChangesPassword_ViaUi`) nicht als Regression werten.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `OpenAccount_ShouldFireOpenPostings_WithAccountCardUrl` | `PostingsCardViewModelLinkedTests` (`FinanceManager.Tests/ViewModels/`) | Nach `InitializeAsync` mit gesetztem `AccountId`: `Callback` feuert `UiActionRequested` mit `Action == "OpenPostings"` und `Payload` beginnend mit `/card/accounts/{AccountId}` (optional `?back=`-Suffix tolerant prüfen, da im Unit-Test kein `NavigationManager` registriert ist) |
| `OpenContact_ShouldFireOpenPostings_WithContactCardUrl` | `PostingsCardViewModelLinkedTests` | Analog für `/card/contacts/{ContactId}` |
| `OpenSavingsPlan_ShouldFireOpenPostings_WithSavingsPlanCardUrl` | `PostingsCardViewModelLinkedTests` | Analog für `/card/savings-plans/{SavingsPlanId}` |
| `OpenSecurity_ShouldFireOpenPostings_WithSecurityCardUrl` | `PostingsCardViewModelLinkedTests` | Analog für `/card/securities/{SecurityId}` |
| `OpenAttachments_ShouldFireOverlaySpec_ForPosting` | `PostingsCardViewModelLinkedTests` | `Callback` feuert `Action == "OpenAttachments"` mit `PayloadObject is UiOverlaySpec`: `ComponentType == typeof(AttachmentsPanel)`, `Parameters["ParentKind"] == AttachmentEntityKind.Posting`, `Parameters["ParentId"] == Id` |
| `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` | `PostingsCardViewModelLinkedTests` | Pro Verknüpfung (`ContactId`/`SavingsPlanId`/`SecurityId` `null` bzw. gesetzt): korrektes `Disabled`-Flag; gesetzte Verknüpfung → `Disabled == false` |
| `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` | `PostingsCardViewModelLinkedTests` | `Posting == null` (z. B. `InitializeAsync(Guid.Empty)` oder API liefert `null`) → `Disabled == true` |
| `LinkedNavigationCallback_ShouldNotFire_WhenLinkRemoved` (Guard) | `PostingsCardViewModelLinkedTests` | Direkter Callback-Aufruf mit `linkedId == null` feuert kein `UiActionRequested` (Guard in `RequestOpenLinkedCard`) |
| `FindLinkedAction(vm, id)` / `CaptureUiActions(vm)` | Hilfsmethoden in `PostingsCardViewModelLinkedTests` | Abflachen von `GetRibbonRegisters(localizer)` zu `UiRibbonAction` per `Id` (Muster `FindReverseAction`); Event-Recorder für `Action`/`Payload`/`PayloadObject` (Muster Zeilen 189–191 der Reversal-Tests) |
| `BuildPostingDto` um optionale Link-Ids erweitert | Hilfsmethode in `PostingsCardViewModelLinkedTests` | Parametrisierbares `PostingServiceDto` mit belegbaren `AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` (Kopie des Musters aus `PostingsCardViewModelReversalTests`) |
| `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` | `PostingsCardLinkedActionsPlaywrightTests` (`FinanceManager.Tests.E2E/Tests/Navigation/` bzw. neuem Ordner `Tests/Postings/`) | Bankposten via `AccountsApiSeedHelper` seeden → `/card/postings/{id}` → Klick `#OpenAccount` → URL `/card/accounts/{AccountId}` mit `?back=` → Klick `#Back` → zurück auf Posten-Karte |
| `PostingsCard_OpenAttachments_OpensOverlay` | `PostingsCardLinkedActionsPlaywrightTests` | Klick `#OpenAttachments` → `AttachmentsPanel`-Modal sichtbar (Overlay-Host rendert Dialog mit Anhangs-Liste) |
| `PostingsCard_LinkedActions_Disabled_WhenNoLink` | `PostingsCardLinkedActionsPlaywrightTests` | Auf reinem Bankposten: `#OpenContact`, `#OpenSavingsPlan`, `#OpenSecurity` mit `disabled`-Attribut |
| `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` | `PostingsCardLinkedActionsPlaywrightTests` | `/card/postings/{nicht-existierende Guid}` aufrufen → Karte rendert Fehlerzustand (`Posting == null`, Ribbon wird weiterhin gerendert, `CardPage.razor` Zeile 36); alle fünf Linked-Buttons (`#OpenAccount`, `#OpenContact`, `#OpenSavingsPlan`, `#OpenSecurity`, `#OpenAttachments`) tragen `disabled`/`aria-disabled` — UI-Nachweis der neuen `OpenAttachments`-`Disabled`-Bedingung und dass kein Overlay mit `ParentId = Guid.Empty` ausgelöst werden kann |
| `PostingsCard_OpenLinkedEntities_NavigateToCards` | `PostingsCardLinkedActionsPlaywrightTests` | Posten mit geseedeten `ContactId`/`SavingsPlanId`/`SecurityId` (plus geseedete Ziel-Entities: Kontakt via `CreateBankContactAsync`, Sparplan via `POST /api/savings-plans`, Wertpapier via Securities-API) → die drei Buttons aktiv, Klick navigiert zur jeweiligen `/card/{route}/{id}` (Ziel-URL inkl. `?back=`-Parameter assertierbar) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine. `PostingsCardViewModelReversalTests` assertiert nur `Reverse`-/`GroupOrder`-Verhalten (unverändert); `RibbonGroupOrderPlaywrightTests.PostingsCard_LinkedGroup_IsLastRibbonGroup` prüft lediglich Gruppenreihenfolge und das Vorhandensein von `#OpenAttachments` — beides bleibt bestehen. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Klick `#OpenAccount` auf Bankposten navigiert zur Konto-Karte, `?back=` führt zurück zur Posten-Karte | `FinanceManager.Tests.E2E/Tests/Navigation/PostingsCardLinkedActionsPlaywrightTests.cs` | Navigationsaktion zur verknüpften Karte funktioniert inkl. Zurück-Kette | Kernbefund des Issues sind tote Buttons — nur ein echter Browser-Klick über `Ribbon` → `Callback` → `UiActionRequested` → `CardPage`-`case "OpenPostings"` → `NavigateTo` beweist die Wirkung |
| Pflicht | Klick `#OpenAttachments` öffnet das Anhangs-Overlay (`AttachmentsPanel`-Modal) | `PostingsCardLinkedActionsPlaywrightTests` | `UiOverlaySpec`-Payload erreicht `OverlayHost` und rendert das Panel | Unit-Tests assertieren nur das Event; Overlay-Rendering (`OverlayHost`, `DynamicComponent`) ist ein UI-Fluss |
| Pflicht | `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` sind auf einem Posten ohne diese Verknüpfungen deaktiviert | `PostingsCardLinkedActionsPlaywrightTests` | Fehlende Verknüpfung → deaktivierter Button (kein wirkungslos-klickbarer Button) | Deaktivierungslogik ist anwendersichtbar; ein scheinbar aktiver toter Button wäre unzulässig |
| Pflicht | Auf Posten mit geseedeten Verknüpfungs-Ids navigieren `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` zu `/card/contacts/…`, `/card/savings-plans/…`, `/card/securities/…` | `PostingsCardLinkedActionsPlaywrightTests` (mit erweitertem `SeededPosting`, Schritt 3) | Jede der vier Navigationsaktionen ist ein eigenes UI-erreichbares Akzeptanzkriterium — alle verifiziert | Vierfach-Nachweis der Routen-Mapping-Korrektheit über den kompletten UI-Pfad |
| Pflicht | `/card/postings/{nicht-existierende Id}` aufrufen → alle fünf Linked-Buttons inkl. `#OpenAttachments` deaktiviert | `PostingsCardLinkedActionsPlaywrightTests` | `Posting == null` → `Disabled == true` für alle Linked-Aktionen; neue `OpenAttachments`-`Disabled`-Bedingung (`Posting == null` statt hart `false`) | Fehlerfall ist anwendersichtbar — das Ribbon rendert auch im Fehlerzustand (`CardPage.razor` Zeile 36); Unit-Tests prüfen nur die ViewModel-Flags, nicht das gerenderte `disabled`-Attribut |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine. `RibbonGroupOrderPlaywrightTests` assertiert nur Gruppenreihenfolge/Vorhandensein und ist unverändert lauffähig. |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Konto-Karte öffnen (inkl. Zurück-Kette) | Klick `#OpenAccount` | Bankposten mit `AccountId` geladen; Posten-Karte `/card/postings/{id}` geöffnet | `Ribbon.OnItemClick` → `Callback` → `RequestOpenLinkedCard("accounts", AccountId)` → `RaiseUiActionRequested("OpenPostings", "/card/accounts/{id}?back=…")` → `CardPage.case "OpenPostings"` → `NavigateTo` | Konto-Karte `/card/accounts/{AccountId}` wird angezeigt; deren `Back`-Aktion führt zurück zur Posten-Karte | E2E `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` |
| 2 | Happy Path: Anhangs-Overlay öffnen | Klick `#OpenAttachments` | Bankposten geladen (`Posting != null`); Posten besitzt keine oder beliebige Anhänge | `Callback` → `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)` → `UiOverlaySpec` → `OverlayHost` rendert `AttachmentsPanel` | Modal-Dialog mit Anhangs-Liste (Titel `Attachments_Title`) sichtbar; vorhandene Posten-Anhänge gelistet, leere Liste als Leerzustand | E2E `PostingsCard_OpenAttachments_OpensOverlay` + Unit-Test `OpenAttachments_ShouldFireOverlaySpec_ForPosting` |
| 3 | Fehlende Verknüpfung: Button deaktiviert | Posten-Karte öffnen | Bankposten ohne `ContactId`/`SavingsPlanId`/`SecurityId` | `GetRibbonRegisterDefinition` liefert `Disabled == true` für die drei Buttons | Buttons gerendert, aber `disabled`/`aria-disabled` — kein Klick möglich, kein toter aktiver Button | E2E `PostingsCard_LinkedActions_Disabled_WhenNoLink` + Unit-Test `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` |
| 4 | Posten nicht geladen | Karte mit nicht existierender/`Guid.Empty`-Id öffnen | `Posting == null` (Ladefehler oder leere Id); Ribbon rendert weiterhin (`CardPage.razor` Zeile 36) | `Disabled` aller fünf Linked-Aktionen `== true` (inkl. `OpenAttachments` neu) | Alle Linked-Buttons gerendert, aber `disabled`/`aria-disabled`; kein Overlay mit `ParentId = Guid.Empty` möglich | E2E `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` + Unit-Test `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` |
| 5 | Verknüpfte Karten aller übrigen Arten öffnen | Klick `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` | Posten mit gesetzten `ContactId`/`SavingsPlanId`/`SecurityId` | Wie Szenario 1, Routen `contacts`/`savings-plans`/`securities` | Jeweilige Zielkarte mit `?back=` geöffnet; bei gelöschter Ziel-Entität greift deren NotFound-Handling (kein Zusatzverhalten) | E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` + Unit-Tests pro Aktion |
| 6 | Fehlende Laufzeitvoraussetzung: `NavigationManager` nicht auflösbar | Callback-Aufruf ohne registrierten `NavigationManager` | Unit-Test-Kontext (`ServiceProvider` ohne `NavigationManager`) | `try` schlägt fehl / `nav` ist `null` → `catch`-Fallback feuert URL ohne `?back=` | `UiActionRequested` mit gültiger Basis-URL — kein Exception-Abbruch, kein verlorener Klick | Unit-Tests (Payload-Assert tolerant bzgl. `?back=`-Suffix) |

## Offene Punkte

Keine. Die drei offenen Fragen der `requirement.md` sind entschieden: Verdrahtungsvariante = generischer `OpenPostings`-URL-Payload (Designentscheidung oben), `OpenAttachments`-`Disabled` = `Posting == null` (Scope-Klärung), `back`-Parameter = ja (Scope-Klärung, beide Referenzmuster übergeben `?back=`).
