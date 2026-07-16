# Umsetzungsoptionen und Risiken

## Option A: Rollenattribut auf User-Actions

Beispiel:

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
```

Anwendung auf alle sieben User-Management-Actions.

Vorteile:

- Minimal-invasiv.
- Keine neue Policy-Registrierung erforderlich.
- Nutzt vorhandene JWT-Rollenclaims und `User.IsInRole("Admin")`.
- IP-Block-Endpunkte bleiben unveraendert.

Nachteile:

- Wiederholung desselben Attributs auf sieben Actions.
- Keine benannte fachliche Policy, falls spaeter weitere Admin-Bereiche hinzukommen.

## Option B: Zentrale Admin-Policy

Registrierung in `ProgramExtensions.RegisterAppServices`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
```

Verwendung:

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "AdminOnly")]
```

Vorteile:

- Zentral benannte fachliche Autorisierung.
- Gut erweiterbar, falls Admin spaeter nicht nur ueber Rolle abgebildet werden soll.

Nachteile:

- Erfordert neue Konvention im Projekt.
- Policy-Name muss konsistent verwendet und getestet werden.

## Option C: Controller-weite Admin-Autorisierung

Die Admin-Rolle wird auf den ganzen `AdminController` gelegt.

Vorteile:

- Alle aktuellen Admin-Endpunkte sind einheitlich geschuetzt.
- Reduziert doppelte Action-Attribute.

Nachteile:

- Groesserer Behaviour-Change, weil IP-Block-Endpunkte dann bereits vor Action-Ausfuehrung blockiert werden.
- Explizite `_current.IsAdmin`-Pruefungen waeren redundant oder muessten migriert werden.
- Falls zukuenftig nicht-admin-faehige Endpunkte unter `api/admin` geplant sind, wird die Grenze grob.

## Bewertung

Fuer die vorliegende Anforderung ist Option A oder B auf den sieben User-Actions am passendsten. Option A ist die kleinste Aenderung. Option B ist vorzuziehen, wenn im Plan eine zentrale Admin-Policy als Zielstandard festgelegt wird.

## Wichtige Risiken

- Beim Ersetzen des Controller-Level-Attributs darf das JWT-AuthenticationScheme nicht verloren gehen.
- Ein Attribut nur auf Controller-Ebene mit `Roles = "Admin"` veraendert auch die IP-Block-Endpunkte; das muss bewusst entschieden und getestet werden.
- Der Shared `ApiClient` wandelt `403` in Exceptions um. Tests sollten nicht versehentlich nur "wirft Exception" pruefen, sondern den Statuscode.
- Nicht-Admin-Tests muessen echte Nicht-Admin-Tokens verwenden. Wegen des Bootstrap-Admins in der Testfactory ist eine normale Registrierung dafuer geeignet.
- Tests mit nicht existierenden IDs sollten weiterhin `403` erwarten; `404` waere ein Hinweis, dass die Autorisierung nicht vor dem Service greift.
