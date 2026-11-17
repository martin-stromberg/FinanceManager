# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| `OpenAccount` → Navigation zu `/card/accounts/{Posting.AccountId}` | Designentscheidung (Variante 1, generischer `case "OpenPostings"`), Hilfsmethode `RequestOpenLinkedCard("accounts", …)`, Umsetzungsschritt 1, Programmablauf 1 | Unit `OpenAccount_ShouldFireOpenPostings_WithAccountCardUrl` + E2E `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` | Abgedeckt |
| `OpenContact` → Navigation zu `/card/contacts/{Posting.ContactId}` | `RequestOpenLinkedCard("contacts", …)`, Umsetzungsschritt 1 | Unit `OpenContact_ShouldFireOpenPostings_WithContactCardUrl` + E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` | Abgedeckt |
| `OpenSavingsPlan` → Navigation zu `/card/savings-plans/{Posting.SavingsPlanId}` | `RequestOpenLinkedCard("savings-plans", …)`, Umsetzungsschritt 1 | Unit `OpenSavingsPlan_ShouldFireOpenPostings_WithSavingsPlanCardUrl` + E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` | Abgedeckt |
| `OpenSecurity` → Navigation zu `/card/securities/{Posting.SecurityId}` | `RequestOpenLinkedCard("securities", …)`, Umsetzungsschritt 1 | Unit `OpenSecurity_ShouldFireOpenPostings_WithSecurityCardUrl` + E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` | Abgedeckt |
| `OpenAttachments` → `AttachmentsPanel`-Overlay für `AttachmentEntityKind.Posting`, `ParentId = Id` | `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)`, Umsetzungsschritt 1, Programmablauf 2 | Unit `OpenAttachments_ShouldFireOverlaySpec_ForPosting` (ComponentType, ParentKind, ParentId) + E2E `PostingsCard_OpenAttachments_OpensOverlay` | Abgedeckt |
| `back`-Parameter an Zielkarte (Zurück-Kette zur Posten-Karte) | URL-Aufbau mit `?back={EscapeDataString(nav.Uri)}` aus `ServiceProvider.GetService<NavigationManager>()` inkl. try/catch-Fallback (Designentscheidung, Programmablauf 1, Schritt 4) | E2E `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` (Rückweg über `#Back`); Unit-Tests tolerant bzgl. `?back=`-Suffix (Fallback-Szenario 6) | Abgedeckt |
| Deaktivierung: Nav-Aktionen nur aktiv bei gesetzter Verknüpfung (`AccountId`/`ContactId`/`SavingsPlanId`/`SecurityId` != null) | `Disabled`-Flags bleiben `Posting?.X == null` (Scope-Klärung, Schritt 1) | Unit `LinkedNavigationActions_ShouldBeDisabled_WhenLinkMissing` + Guard-Test `LinkedNavigationCallback_ShouldNotFire_WhenLinkRemoved` + E2E `PostingsCard_LinkedActions_Disabled_WhenNoLink` | Abgedeckt |
| `OpenAttachments` nur aktiv bei geladenem Posten (`Disabled` von hart `false` → `Posting == null`); alle Linked-Aktionen deaktiviert wenn Posten nicht geladen | `Disabled`-Flag geändert (Scope-Klärung, Umsetzungsschritt 1, Akzeptanzszenario 4) | Nur Unit `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` — **kein E2E** für den UI-beobachtbaren Fehlerfall | Lücke |
| Ziel-Entität zur Laufzeit nicht mehr vorhanden → NotFound-/Error-Handling der Zielkarte | Explizit als Bestandsverhalten ohne Zusatzlogik festgelegt (Scope-Klärung, Szenario 5) | Nicht erforderlich (unverändertes Bestandsverhalten der Zielkarten) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] E2E-Test für den Fehlerfall „Posten nicht geladen" fehlt: Aufruf von `/card/postings/{ungültige bzw. nicht ladbare Id}` muss anwendersichtbar alle fünf Linked-Buttons deaktiviert zeigen — insbesondere den neu eingeführten `OpenAttachments`-`Disabled`-Zustand bei `Posting == null` (Anforderung „Umgang mit fehlenden Objekten", Akzeptanzszenario 4 des Plans mit „Sichtbarer Wirkung: Alle Linked-Buttons deaktiviert"). Der Plan sieht hierfür nur den Unit-Test `OpenAttachments_ShouldBeDisabled_WhenPostingNotLoaded` vor; die bisherigen Disabled-E2Es (`PostingsCard_LinkedActions_Disabled_WhenNoLink`) prüfen den Zustand nur auf einem erfolgreich geladenen Posten und decken die neue `Disabled`-Bedingung von `OpenAttachments` nicht ab.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Klick `#OpenAccount` → Konto-Karte inkl. `?back=`-Rückweg | `PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` | Abgedeckt |
| Klick `#OpenAttachments` → `AttachmentsPanel`-Modal | `PostingsCard_OpenAttachments_OpensOverlay` | Abgedeckt |
| `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` deaktiviert bei fehlender Verknüpfung | `PostingsCard_LinkedActions_Disabled_WhenNoLink` | Abgedeckt |
| Klick `#OpenContact`/`#OpenSavingsPlan`/`#OpenSecurity` → jeweilige Zielkarte | `PostingsCard_OpenLinkedEntities_NavigateToCards` (mit erweitertem `SeededPosting`, Schritt 3) | Abgedeckt (Hinweis: im Plan nur „Empfohlen" priorisiert, per Prüfkriterien aber Pflicht — s. Hinweise) |
| Posten nicht ladbar (`Posting == null`) → alle fünf Linked-Buttons deaktiviert, kein Overlay mit leerer `ParentId` | Nur Unit-Test geplant | Lücke |

## Fehlende oder unvollständige Planbestandteile

Keine weiteren — Umsetzungsschritte, Scope-Klärung, Scope-Ausschlüsse (alle mit Status `bestätigt` und Begründung), Akzeptanzszenarien inkl. vollständigem Happy Path sowie Seiteneffekte/Risiken sind vollständig. Die einzige Lücke betrifft den oben genannten fehlenden E2E-Nachweis.

## Hinweise

- Die Lücke lässt sich kostengünstig schließen: Im bestehenden E2E `PostingsCard_LinkedActions_Disabled_WhenNoLink` (oder einem separaten Szenario) eine nicht existierende Posten-Id aufrufen und `disabled`/`aria-disabled` auf allen fünf Buttons inkl. `#OpenAttachments` prüfen.
- Der E2E `PostingsCard_OpenLinkedEntities_NavigateToCards` ist im Plan mit Priorität „Empfohlen" markiert. Da jede der vier Navigationsaktionen ein UI-erreichbares Akzeptanzkriterium ist, sollte der Test nicht weggelassen werden; optional kann dort auch der `?back=`-Parameter der Ziel-URLs assertiert werden.
- Der dokumentierte preexisting E2E-Fehlschlag (`AuthenticatedUser_ChangesPassword_ViaUi`) ist korrekt als nicht-regressionsrelevant gekennzeichnet.
