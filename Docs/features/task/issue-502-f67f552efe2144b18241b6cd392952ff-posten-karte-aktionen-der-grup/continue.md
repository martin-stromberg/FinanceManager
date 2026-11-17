# Offene Aufgaben

Erstellt am: 2026-11-11
Abbruchgrund: Maximale Iterationsanzahl erreicht (Iterationszähler 3); der verbleibende Punkt ist ein preexisting Testinfrastruktur-Flake außerhalb des bestätigten Scopes dieses Branches.

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt`.

## Code-Review-Befunde

Keine — `review-code.md` trägt den Status `Keine Befunde`.

## Usability-Befunde

Keine — `review-usability.md` trägt den Status `Keine Befunde`.

## Fehlgeschlagene Tests

- [ ] `ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` — `net::ERR_ABORTED` bei Navigation zu `/login` in `AuthGateway.LogoutAsync` (`AuthGateway.cs:138`, `ChangePasswordWellKnownPlaywrightTests.cs:108`). **Preexisting auf `staging`** (Baseline-TRX `inventory/test-results/e2e/test-results-e2e.trx` weist identisches Fehlerbild vor jeder Änderung nach; Iteration-3-Rerun erneut identische Signatur, nur dynamischer Port abweichend). Blazor/Playwright-Navigations-Flake ohne Change-Bezug — bereits triagiert als Folge-Issue https://github.com/martin-stromberg/FinanceManager/issues/503 (Status: OPEN).
