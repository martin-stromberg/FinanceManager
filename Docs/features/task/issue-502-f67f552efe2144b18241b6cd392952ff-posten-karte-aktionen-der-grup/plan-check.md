# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `OpenAccount` → Navigation zu `/card/accounts/{Posting.AccountId}` | Designentscheidung (Variante 1, generischer `case "OpenPostings"`), Hilfsmethode `RequestOpenLinkedCard("accounts", …)`, Umsetzungsschritt 1, Programmablauf 1 | Unit `OpenAccount_ShouldFireOpenPostings_WithAccountCardUrl` + E2E `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` (Pflicht) | Abgedeckt |
| `OpenContact` → Navigation zu `/card/contacts/{Posting.ContactId}` | `RequestOpenLinkedCard("contacts", …)`, Umsetzungsschritt 1 | Unit `OpenContact_ShouldFireOpenPostings_WithContactCardUrl` + E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` (Pflicht, mit erweitertem `SeededPosting`, Schritt 3) | Abgedeckt |
| `OpenSavingsPlan` → Navigation zu `/card/savings-plans/{Posting.SavingsPlanId}` | `RequestOpenLinkedCard("savings-plans", …)`, Umsetzungsschritt 1 | Unit `OpenSavingsPlan_ShouldFireOpenPostings_WithSavingsPlanCardUrl` + E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` (Pflicht) | Abgedeckt |
| `OpenSecurity` → Navigation zu `/card/securities/{Posting.SecurityId}` | `RequestOpenLinkedCard("securities", …)`, Umsetzungsschritt 1 | Unit `OpenSecurity_ShouldFireOpenPostings_WithSecurityCardUrl` + E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` (Pflicht) | Abgedeckt |
| `OpenAttachments` → `AttachmentsPanel`-Overlay für `AttachmentEntityKind.Posting`, `ParentId = Id` | `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)`, Umsetzungsschritt 1, Programmablauf 2 | Unit `OpenAttachments_ShouldFireOverlaySpec_ForPosting` (ComponentType, ParentKind, ParentId) + E2E `PostingsCard_OpenAttachments_OpensOverlay` (Pflicht) | Abgedeckt |
| `back`-Parameter an Zielkarte (Zurück-Kette zur Posten-Karte) | URL-Aufbau mit `?back={EscapeDataString(nav.Uri)}` aus `ServiceProvider.GetService<NavigationManager>()` inkl. try/catch-Fallback (Designentscheidung, Programmablauf 1 Schritt 4, Akzeptanzszenario 6) | E2E `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` (Rückweg über `#Back`); Unit-Tests tolerant bzgl. `?back=`-Suffix | Abgedeckt |
| Deaktivierung: Nav-Aktionen nur aktiv bei gesetzter Verknüpfung (`AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId != null`) | `Disabled`-Flags bleiben `Posting?.X == null` (Scope-Klärung, Umsetzungsschritt 1, Akzeptanzszenario 3) | Unit `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` + Guard-Test `LinkedNavigationCallback_ShouldNotFire_WhenLinkRemoved` + E2E `PostingsCard_LinkedActions_Disabled_WhenNoLink` (Pflicht) | Abgedeckt |
| `OpenAttachments` nur aktiv bei geladenem Posten (`Disabled` hart `false` → `Posting == null`); alle Linked-Aktionen deaktiviert wenn Posten nicht geladen | `Disabled`-Flag geändert (Scope-Klärung, Umsetzungsschritt 1, Akzeptanzszenario 4) | Unit `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` + **neuer Pflicht-E2E `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound`** (alle fünf Buttons `disabled`/`aria-disabled` bei nicht ladbarem Posten) | Abgedeckt |
| Ziel-Entität zur Laufzeit nicht mehr vorhanden → NotFound-/Error-Handling der Zielkarte | Explizit als unverändertes Bestandsverhalten ohne Zusatzlogik festgelegt (Scope-Klärung, Akzeptanzszenario 5) | Nicht erforderlich (kein Verhaltenszusatz auf der Posten-Karte) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — beide Befunde aus `plan-check.1.md` sind geschlossen: `PostingsCard_OpenLinkedEntities_NavigateToCards` ist jetzt Pflicht (statt „Empfohlen"), und der neue Pflicht-E2E `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` deckt den anwendersichtbaren Fehlerfall „Posten nicht ladbar" inkl. der neuen `OpenAttachments`-`Disabled`-Bedingung ab.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Klick `#OpenAccount` → Konto-Karte inkl. `?back=`-Rückweg | `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` (Pflicht) | Abgedeckt |
| Klick `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` → jeweilige Zielkarte `/card/contacts/…`, `/card/savings-plans/…`, `/card/securities/…` | `PostingsCard_OpenLinkedEntities_NavigateToCards` (Pflicht; benötigt `SeededPosting`-Erweiterung aus Schritt 3 und Ziel-Seeds via `CreateBankContactAsync`/Savings-Plans-/Securities-API) | Abgedeckt |
| Klick `#OpenAttachments` → `AttachmentsPanel`-Modal | `PostingsCard_OpenAttachments_OpensOverlay` (Pflicht) | Abgedeckt |
| `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` deaktiviert bei fehlender Verknüpfung | `PostingsCard_LinkedActions_Disabled_WhenNoLink` (Pflicht) | Abgedeckt |
| Posten nicht ladbar (`Posting == null`) → alle fünf Linked-Buttons deaktiviert, kein Overlay mit `ParentId = Guid.Empty` | `PostingsCard_LinkedActions_AllDisabled_WhenPostingNotFound` (Pflicht) — stichprobenartig verifiziert: Ribbon rendert auch im Fehlerzustand (`CardPage.razor` Zeile 36, `else`-Zweig bei `_vm != null`) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine. Scope-Klärung ist vollständig (Verarbeitungseinheit, auslösende Ereignisse, erwartete Wirkung, Umgang mit fehlenden Objekten inkl. `NavigationManager`-Fallback); alle sechs Scope-Ausschlüsse tragen Begründung und Status `bestätigt`; `Akzeptanzszenarien` enthalten einen vollständigen Happy Path vom Nutzerklick bis zur sichtbaren Wirkung (Szenario 1 und 2) sowie Ausgangszustände für fehlende Objekte (Szenarien 3, 4, 6); Umsetzungsreihenfolge, Voraussetzungen, Seiteneffekte/Risiken, betroffene bestehende Tests (keine Anpassung nötig, begründet) und Test-Hilfsmethoden (`FindLinkedAction`, `CaptureUiActions`, `BuildPostingDto`-Erweiterung, `SeededPosting`-Erweiterung, E2E-Helpers) sind vollständig benannt. Die offene Frage der Verdrahtungsvariante ist entschieden (Variante 1, `CardPage.razor` bleibt unverändert); alle drei offenen Fragen der `requirement.md` sind im Abschnitt „Offene Punkte" geschlossen.

## Hinweise

- Der Requirement-Ausschluss „Keine neuen Ribbon-Aktionen und keine Änderung an `Back`/`Reverse`/`NavigateToPosting`" ist im Plan nicht als eigene Ausschluss-Zeile geführt, folgt aber eindeutig aus der beschränkten Änderungsliste (nur die fünf `Linked`-Callbacks plus ein `Disabled`-Flag; „Keine Änderung an Ids, Labels, Icons, `MobileShortcut`, Tabs oder Reihenfolge"). Optional kann eine siebte Ausschluss-Zeile ergänzt werden — keine Lücke.
- `AccountsApiSeedHelper`-Erweiterung (Schritt 3): Beschreibung verifiziert — `SeededPosting` ist ein Record (Zeile 172) und `CreateAccountWithBalanceAsync` übergibt fix `contactId: null, savingsPlanId: null, securityId: null` (Zeilen 108–117); die optionalen neuen Felder müssen mit Default-Werten ergänzt werden, damit bestehende Aufrufer kompilierfähig bleiben (Plan sagt „optional" — korrekt).
- Der dokumentierte preexisting E2E-Fehlschlag (`AuthenticatedUser_ChangesPassword_ViaUi`) ist korrekt als nicht-regressionsrelevant gekennzeichnet.
