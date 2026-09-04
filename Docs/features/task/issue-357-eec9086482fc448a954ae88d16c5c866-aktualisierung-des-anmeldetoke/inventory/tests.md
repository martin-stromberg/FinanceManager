## Testklassen

### `JwtRefreshServiceTests`
Datei: `FinanceManager.Tests/Infrastructure/Auth/JwtRefreshServiceTests.cs`
- `RefreshAsync_ShouldRejectInactiveUser` — Refresh wird für deaktivierten Benutzer abgelehnt.
- `RefreshAsync_ShouldRejectSecurityStampMismatch` — Refresh wird bei abweichendem `security_stamp` abgelehnt.
- `RefreshAsync_ShouldRejectOldAdminPrincipal_AfterRoleRevocationChangedSecurityStamp` — alter Admin-Principal wird nach Sicherheitsstempelwechsel verworfen.
- `RefreshAsync_ShouldCreateTokenWithCurrentAdminRoleAndSecurityStamp` — Refresh nutzt aktuelle Rollen-/Stamp-Werte und erstellt neues Token.

### `JwtCookieAuthTokenProviderTests`
Datei: `FinanceManager.Tests/Infrastructure/Auth/JwtCookieAuthTokenProviderTests.cs`
- `GetAccessTokenAsync_ShouldPreferRequestCookie_WhenCacheContainsDifferentToken` — Request-Cookie hat Vorrang vor Cache.
- `GetAccessTokenAsync_ShouldReturnCachedToken_WhenHttpContextIsUnavailable` — Fallback auf Cache ohne `HttpContext`.
- `GetAccessTokenAsync_ShouldReturnNull_WhenIssuerIsInvalid` — Token mit falschem Issuer wird verworfen.
- `GetAccessTokenAsync_ShouldReturnNull_WhenAudienceIsInvalid` — Token mit falscher Audience wird verworfen.
- `GetAccessTokenAsync_ShouldUseRefreshService_WhenTokenNearExpiry` — near-expiry Token wird über `IJwtRefreshService` erneuert.
- `GetAccessTokenAsync_ShouldReturnNull_WhenRefreshIsRejected` — abgelehnter Refresh liefert kein Token.

### `ApiClientAuthTests`
Datei: `FinanceManager.Tests.Integration/ApiClient/ApiClientAuthTests.cs`
- `Keepalive_WithBearerNearExpiry_ShouldRefreshCookieAndReturnNoContent` — `/api/auth/keepalive` liefert `204`, setzt Refresh-Header und erneuertes `FinanceManager.Auth`-Cookie.
- `Keepalive_WithInvalidSecurityStamp_ShouldReturnUnauthorizedWithoutRefreshLoop` — ungültiger Sicherheitsstempel ergibt `401` ohne Refresh-Header.
- `Bearer_ShouldRejectToken_WhenSecurityStampChanged` — normaler geschützter Endpoint lehnt veraltetes Token nach Stamp-Änderung ab.
- `Bearer_ShouldRejectAdminClaim_WhenCurrentAdminRoleWasRevokedWithoutSecurityStampChange` — veralteter Admin-Claim wird abgelehnt.
- `Bearer_ShouldRejectExistingToken_WhenUserWasDeactivated` — Token wird nach Benutzerdeaktivierung ungültig.
- `ApiClient_ShouldRaiseAuthenticationRequired_OnUnauthorized` — API-Client signalisiert Auth-Verlust bei `401`.
- `ApiClient_ShouldRaiseAuthenticationRequired_OnForbiddenWithAuthenticationCode` — API-Client signalisiert Auth-Verlust bei `403` mit `authentication_required`.

### `AuthenticationFlowPlaywrightTests`
Datei: `FinanceManager.Tests.E2E/Tests/Auth/AuthenticationFlowPlaywrightTests.cs`
- `ActiveNavigationAndInteraction_ShouldRefreshNearExpirySessionWithoutLoginRedirect` — Navigation + Interaktion lösen erfolgreichen Keepalive aus, Session bleibt aktiv.
- `InvalidatedSession_KeepaliveFailure_ShouldNotRedirectUntilProtectedActionRedirectsOnce` — Keepalive-Fehler (`401`) löst allein keinen Redirect aus; geschützte Folgeaktion führt einmalig zu Login-Redirect.
- `MultipleAuthenticationFailures_ShouldNavigateToLoginOnlyOnce` — mehrere gleichzeitige Auth-Fehler deduplizieren Login-Navigation.
- `ExpiredSession_OnProtectedRoute_ShouldRedirectToLoginWithReturnUrl_AndReturnAfterLogin` — Return-URL-Redirect-Verhalten bei abgelaufener/invalidierter Session.

### `StatementDraftQuickEditValueTakeoverE2ETests`
Datei: `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftQuickEditValueTakeoverE2ETests.cs`
- `QuickEdit_Blur_ShouldSendKeepaliveAndKeepLocalInputValue` — Blur auf Quick-Edit-Feld triggert Keepalive, lokaler Input bleibt erhalten, Session bleibt gültig.
- `QuickEdit_MultipleFastBlurs_ShouldCoalesceKeepaliveRequests` — schnelle Mehrfach-Blur-Ereignisse werden zu einem Keepalive-Request zusammengeführt.
- `QuickEdit`-Navigation-/Takeover-Tests (z. B. `CtrlArrow...`, `F8`, `CtrlF8`) bestätigen Quick-Edit-Bedienabläufe im selben Komponentenbereich, in dem Keepalive-Attribute gesetzt sind.

## Hilfsmethoden

### `TestAuthCookieHelper`
Datei: `FinanceManager.Tests.E2E/Helpers/TestAuthCookieHelper.cs`
- `SetNearExpiryCookieAsync` — injiziert ein near-expiry `FinanceManager.Auth`-Cookie in den Browserkontext.
- `CreateTokenAsync` — erzeugt signiertes JWT inkl. `security_stamp` und optionaler Admin-Rolle auf Basis realer DB-Benutzerdaten.
- `CreateContext` — erstellt `AppDbContext` für das Lesen von User-/Rolleninformationen bei Token-Erzeugung.

### Interne Test-Hilfsmethoden in Testklassen
- `AuthenticationFlowPlaywrightTests.WaitForKeepaliveResponseAsync` — wartet gezielt auf `/api/auth/keepalive`-Response.
- `AuthenticationFlowPlaywrightTests.WaitForForcedKeepaliveThrottleAsync` — berücksichtigt erzwungenes Keepalive-Throttling.
- `StatementDraftQuickEditValueTakeoverE2ETests.IsKeepaliveRequest` / `IsKeepaliveResponse` — filtert Keepalive-Requests/-Responses.
- `JwtRefreshServiceTests.Create(...)` / `CreatePrincipal(...)` — baut `JwtRefreshService`-SUT und ClaimsPrincipal für Security-Stamp-/Rollen-Szenarien.
- `JwtCookieAuthTokenProviderTests.CreateProvider(...)` / `CreateToken(...)` / `CreateHttpContextWithCookie(...)` — erstellt Provider, Test-JWTs und Cookie-konfigurierten `HttpContext`.

