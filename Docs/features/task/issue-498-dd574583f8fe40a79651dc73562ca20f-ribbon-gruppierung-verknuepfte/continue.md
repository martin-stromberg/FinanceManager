# Offene Aufgaben

Erstellt am: 2026-11-09
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iterationszähler 3 erreicht; verbleibende Punkte sind Altbestände außerhalb des bestätigten Scopes)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt`.

## Code-Review-Befunde

Keine — `review-code.md` trägt den Status `Keine Befunde`.

## Usability-Befunde

- [ ] `PostingsCardViewModel.cs` — die Ribbon-Aktionen `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity` (Z. 139–142) und `OpenAttachments` (Z. 143) der `Linked`-Gruppe haben keinen Handler: `CardPage.razor` enthält keinen passenden `case`, `OpenAttachments` feuert ohne `UiOverlaySpec`-Payload (statt `RequestOpenAttachments(AttachmentEntityKind.Posting, Id)`). Klicks laufen ins Leere. **Altbestand auf `staging`, nicht durch diesen Change verursacht** — triagiert als Folge-Issue https://github.com/martin-stromberg/FinanceManager/issues/502 (Behebung erfordert fachliche Entscheidungen zu Navigationszielen und ist außerhalb des bestätigten Scopes dieser Anforderung).

## Fehlgeschlagene Tests

- [ ] `ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` — `net::ERR_ABORTED` bei Navigation zu `/login` in `AuthGateway.LoginAsync` nach `LogoutAsync`. **Preexisting auf `staging`** (Baseline-TRX `inventory/test-results/baseline-e2e-tests.trx` weist identisches Fehlerbild nach), Blazor/Playwright-Navigations-Flake ohne Change-Bezug — triagiert als Folge-Issue https://github.com/martin-stromberg/FinanceManager/issues/503.
