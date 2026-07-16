### Fachliche Zusammenfassung

Die Admin-User-Endpunkte unter `api/admin/users` muessen serverseitig strikt auf Administratoren beschraenkt werden. Aktuell reicht eine gueltige Authentifizierung aus, weil `FinanceManager.Web/Controllers/AdminController.cs` nur `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]` auf Controller-Ebene setzt und die User-Management-Actions keine sichtbare Admin-Pruefung ausfuehren. Dadurch koennen authentifizierte Nicht-Admin-Benutzer Benutzer auflisten, abrufen, anlegen, aktualisieren, Passwoerter zuruecksetzen, Benutzer entsperren und Benutzer loeschen.

Ziel ist eine zentrale, konsistente Autorisierung fuer alle administrativen Benutzerverwaltungsfunktionen. Nicht-Admins muessen fuer alle betroffenen User-Endpunkte mit `403 Forbidden` abgewiesen werden, waehrend bestehende Admin-Benutzer die Funktionen weiterhin nutzen koennen. Die bestehende Admin-Pruefung der IP-Block-Endpunkte dient als Vergleichsverhalten und darf nicht abgeschwaecht werden.

---

### Betroffene Klassen und Komponenten

#### API-Controller

- **`AdminController`** (`FinanceManager.Web/Controllers/AdminController.cs`)
  - Betroffene User-Endpunkte:
    - `GET /api/admin/users` (`ListUsersAsync`)
    - `GET /api/admin/users/{id}` (`GetUserAsync`)
    - `POST /api/admin/users` (`CreateUserAsync`)
    - `PUT /api/admin/users/{id}` (`UpdateUserAsync`)
    - `POST /api/admin/users/{id}/reset-password` (`ResetPasswordAsync`)
    - `POST /api/admin/users/{id}/unlock` (`UnlockUserAsync`)
    - `DELETE /api/admin/users/{id}` (`DeleteUserAsync`)
  - Diese Endpunkte muessen eine Admin-Rollen- oder Admin-Policy-Autorisierung erhalten.
  - Die bestehende Authentifizierung per JWT bleibt erhalten.
  - Die IP-Block-Endpunkte mit expliziter `_current.IsAdmin`-Pruefung bleiben funktional unveraendert.

#### Authentifizierung und Autorisierung

- **Admin-Rolle `Admin`**
  - Bestehende Rollenmitgliedschaft wird bereits ueber Identity und Domain-Flag `IsAdmin` synchronisiert.
  - Die Autorisierung soll diese vorhandene Rolle bzw. eine zentrale Admin-Policy verwenden.

- **`CurrentUserService`** (`FinanceManager.Web/Services/CurrentUserService.cs`)
  - Liefert weiterhin `IsAdmin` ueber `User.IsInRole("Admin")`.
  - Kann als bestehende technische Grundlage dienen, falls die Umsetzung aus Konsistenzgruenden bei expliziten Pruefungen bleibt.

- **`ProgramExtensions`** (`FinanceManager.Web/ProgramExtensions.cs`)
  - Falls eine zentrale Policy statt direkter Rollenattribute verwendet wird, muss die Admin-Policy hier bzw. in der bestehenden Authorization-Konfiguration registriert werden.

#### Services

- **`IUserAdminService` / `UserAdminService`**
  - Fachliche User-Admin-Operationen bleiben unveraendert.
  - Die Zugriffskontrolle soll vor dem Service-Aufruf auf Controller- bzw. Policy-Ebene greifen.

#### Shared API Client und ViewModels

- **Admin-API-Client-Methoden** (`Admin_ListUsersAsync`, `Admin_CreateUserAsync`, `Admin_UpdateUserAsync`, `Admin_ResetPasswordAsync`, `Admin_UnlockUserAsync`, `Admin_DeleteUserAsync`)
  - Keine fachliche Signaturaenderung erforderlich.
  - Nicht-Admin-Aufrufe muessen Fehlerstatus korrekt propagieren.

- **Setup/User-ViewModels**
  - Keine fachliche UI-Erweiterung erforderlich, sofern die UI bereits nur fuer Admins sichtbar ist.
  - Die serverseitige Autorisierung ist verbindlich und darf nicht von UI-Sichtbarkeit abhaengen.

#### Tests

- **Integrationstests fuer Admin-User-Endpunkte** (`FinanceManager.Tests.Integration/ApiClient/ApiClientUsersAdminTests.cs` oder neue passende Testklasse)
  - Bestehender Positivtest fuer Bootstrap-Admin bleibt erhalten.
  - Neue Negativtests fuer authentifizierte Nicht-Admins muessen alle betroffenen User-Endpunkte abdecken.

- **Test-Infrastruktur** (`FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`)
  - Kann genutzt werden, um Bootstrap-Admin und regulaere Testbenutzer anzulegen bzw. anzumelden.

---

### Implementierungsansatz

1. **Admin-Autorisierung zentral festlegen**
   - Bevorzugt wird eine zentrale Admin-Policy, z. B. `RequireRole("Admin")`, oder ein direkter Rollencheck ueber `[Authorize(Roles = "Admin")]`.
   - Die Entscheidung soll sich an vorhandenen Konventionen im Projekt orientieren.

2. **User-Management-Endpunkte absichern**
   - Die Autorisierung muss mindestens fuer die Route-Gruppe `api/admin/users` gelten.
   - Moegliche Varianten:
     - `[Authorize(Roles = "Admin")]` auf dem gesamten `AdminController`, wenn dadurch alle Admin-Endpunkte korrekt abgesichert werden.
     - Spezifisches Rollen-/Policy-Attribut auf jeder User-Management-Action.
     - Separate Controller-Aufteilung fuer User-Admin und IP-Block-Admin, falls das die Autorisierungsgrenzen klarer macht.
   - Eine blosse UI-Sperre oder clientseitige Pruefung reicht nicht aus.

3. **Bestehendes Verhalten fuer Admins erhalten**
   - Admins muessen weiterhin Benutzer listen, abrufen, anlegen, aktualisieren, Passwoerter zuruecksetzen, entsperren und loeschen koennen.
   - Bestehende Response-Codes fuer fachliche Fehler (`400`, `404`, `409`, `500`) bleiben unveraendert, wenn der Benutzer autorisiert ist.

4. **Nicht-Admin-Zugriffe blockieren**
   - Authentifizierte Nicht-Admins muessen fuer alle betroffenen Endpunkte `403 Forbidden` erhalten.
   - Nicht authentifizierte Benutzer muessen weiterhin `401 Unauthorized` erhalten.
   - User-Service-Methoden duerfen bei blockierten Nicht-Admin-Zugriffen nicht ausgefuehrt werden.

5. **Regression fuer IP-Block-Endpunkte vermeiden**
   - Die bereits vorhandene Admin-Pruefung der IP-Block-Endpunkte bleibt erhalten oder wird auf dieselbe zentrale Admin-Policy migriert.
   - Falls eine Controller-weite Admin-Autorisierung eingefuehrt wird, darf sie das erwartete Verhalten der IP-Block-Endpunkte nicht verschlechtern.

---

### Sicherheitsanforderungen

- Die Zugriffskontrolle muss serverseitig erfolgen.
- Alle administrativen Benutzerverwaltungsaktionen muessen Administratorrechte erfordern.
- Das Domain-Flag `IsAdmin`, Identity-Rollen und JWT-Rollenclaims muessen weiterhin konsistent ausgewertet werden.
- Ein normaler authentifizierter Benutzer darf keine fremden Benutzer sehen, anlegen, aendern, entsperren, loeschen oder Passwoerter zuruecksetzen.
- Die Umsetzung adressiert OWASP Top 10 A01 Broken Access Control und OWASP ASVS Access Control.

---

### Testanforderungen

#### Positive Tests

- Ein angemeldeter Admin kann weiterhin:
  - Benutzer anlegen
  - Benutzer listen
  - Einzelnen Benutzer abrufen
  - Benutzer aktualisieren
  - Passwort eines Benutzers zuruecksetzen
  - Benutzer entsperren
  - Benutzer loeschen

#### Negative Integrationstests

- Ein angemeldeter Nicht-Admin erhaelt `403 Forbidden` fuer:
  - `GET /api/admin/users`
  - `GET /api/admin/users/{id}`
  - `POST /api/admin/users`
  - `PUT /api/admin/users/{id}`
  - `POST /api/admin/users/{id}/reset-password`
  - `POST /api/admin/users/{id}/unlock`
  - `DELETE /api/admin/users/{id}`

#### Service-Call-Schutz

- Wenn sinnvoll testbar, soll geprueft werden, dass bei einem Nicht-Admin-Aufruf keine User-Admin-Serviceoperation ausgefuehrt wird.

#### Regressionstests

- Bestehende Admin-Tests fuer IP-Block-Endpunkte bleiben gruen.
- Nicht authentifizierte Zugriffe auf geschuetzte Admin-Endpunkte liefern weiterhin `401 Unauthorized`.

---

### Abnahmekriterien

1. Alle `api/admin/users`-Endpunkte sind nur noch fuer Administratoren erreichbar.
2. Authentifizierte Nicht-Admins erhalten auf allen betroffenen User-Management-Endpunkten `403 Forbidden`.
3. Admin-Benutzer koennen die bestehenden User-Management-Funktionen ohne funktionale Regression nutzen.
4. Nicht authentifizierte Benutzer erhalten weiterhin `401 Unauthorized`.
5. Negative Integrationstests fuer Nicht-Admins decken alle betroffenen Endpunkte ab.
6. Bestehende Tests fuer Admin-User-Verwaltung und IP-Block-Verwaltung bleiben erfolgreich.

---

### Offene Fragen

1. Soll die Umsetzung bevorzugt per direktem `[Authorize(Roles = "Admin")]` erfolgen oder soll eine benannte zentrale Admin-Policy eingefuehrt werden?
2. Soll die Admin-Autorisierung auf den gesamten `AdminController` angewendet werden oder nur auf die User-Management-Actions, um das bestehende IP-Block-Verhalten minimal-invasiv zu belassen?
