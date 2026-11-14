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

## Verlauf

- **2026-11-09 — Fortsetzungslauf (erneuter Versuch der offenen Punkte):** Beide Einträge wurden erneut gegen den Code- und Teststand verifiziert und bleiben offen:
  - Usability-Befund: `git diff staging...HEAD` an `PostingsCardViewModel.cs` zeigt ausschließlich die Tab-Umordnung (`{ nav, linked, actions }` → `{ nav, actions, linked }`); die Handler-losen Aktionsdefinitionen (Z. 139–143) sind byteidentischer Altbestand. Eine Behebung erfordert Navigationsziele, Payloads und neue `case`-Handler in `CardPage.razor` — das ist eine inhaltliche Änderung der Gruppen-Aktionen und damit durch Scope-Ausschluss 1 der `plan.md` (bestätigt im Issue) verboten. Umsetzung erfolgt im Folge-Issue #502 (existiert, Status OPEN).
  - Fehlgeschlagener Test: Baseline-Nachweis erneut geprüft (`baseline-e2e-tests.trx`: `outcome="Failed"`, identisches `net::ERR_ABORTED` auf `staging`). Testinfrastruktur-Flaw ohne Change-Bezug; Behebung im Folge-Issue #503 (existiert, Status OPEN).
  - Ergebnis: Keine im Scope dieses Branches umsetzbare Änderung — die offenen Punkte erfordern die separat triagierten Folge-Issues bzw. eine manuelle Entscheidung des Anwenders. Kein Code-Delta in diesem Lauf; Reviews und Testergebnisse aus Iteration 2 bleiben gültig (Produktivcode unverändert).
