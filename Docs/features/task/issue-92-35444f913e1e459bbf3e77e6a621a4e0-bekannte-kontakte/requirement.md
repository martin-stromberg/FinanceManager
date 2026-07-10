### Fachliche Zusammenfassung

Das Feature erweitert die automatische Kontaktzuordnung beim Import von Kontoauszügen um eine zentrale Liste bekannter Unternehmen. Wenn für einen Kontoauszugseintrag über die vorhandenen Kontakte und deren Aliasse kein Kontakt gefunden wird, soll die Anwendung zusätzlich diese mitgelieferte Liste durchsuchen. Wird dort ein eindeutiger bekannter Kontakt erkannt, legt die Anwendung im Konto des angemeldeten Benutzers automatisch einen entsprechenden Kontakt an und weist den Kontoauszugseintrag diesem Kontakt zu.

Die zentrale Liste bekannter Kontakte wird als leicht erweiterbare Datei im Programmverzeichnis ausgeliefert. Sie enthält je bekannten Kontakt den Anzeigenamen sowie mögliche Alias-/Suchmuster. Die Funktion ist benutzerspezifisch über die Einstellungen deaktivierbar.

---

### Betroffene Klassen und Komponenten

#### Datenmodellklassen

- **`User`** (`FinanceManager.Domain.Users.User`)
  - Neues benutzerspezifisches Flag, z. B. `KnownContactAutoCreateEnabled`
  - Neue Setter-Methode für die Einstellung
  - Standardwert: aktiviert, sofern fachlich keine abweichende Vorgabe besteht

- **`Contact`** (`FinanceManager.Domain.Contacts.Contact`)
  - Keine neue fachliche Entität erforderlich
  - Automatisch erzeugte Kontakte werden als normale Benutzerkontakte angelegt
  - Kontakt-Typ voraussichtlich `ContactType.Organization`, sofern die Datei keinen Typ vorgibt

- **`AliasName`** (`FinanceManager.Domain.Contacts.AliasName`)
  - Aliasse aus der bekannten-Kontakte-Datei sollen beim Erstellen des Kontakts als Aliasnamen übernommen werden, soweit sie nicht dem Kontaktnamen entsprechen

#### Konfigurationsdatei / Programmdaten

- **Neue Datei im Programmverzeichnis**
  - Möglicher Pfad: `FinanceManager.Web/KnownContacts.json` oder `FinanceManager.Web/Data/KnownContacts.json`
  - Inhalt: Liste bekannter Kontakte mit Anzeigename, optionalem Typ und Alias-/Suchmustern
  - Die Datei wird mit der Anwendung ausgeliefert und kann ohne Datenbankmigration der bekannten Kontakte erweitert werden

- **Neue DTO-/Optionsklasse**
  - Beispiel: `KnownContactDefinition`
  - Felder:
    - `string Name`
    - `ContactType? Type` oder `string? Type`
    - `IReadOnlyList<string> Aliases`

#### Services

- **Neuer Service: `IKnownContactCatalog` / `KnownContactCatalog`**
  - Lädt die bekannte-Kontakte-Datei aus dem Programmverzeichnis
  - Validiert und normalisiert Name und Aliasse
  - Stellt eine Match-Methode bereit, z. B. `FindMatch(string? recipientName, string? subject)`
  - Behandelt Mehrdeutigkeiten defensiv: kein automatisches Anlegen bei mehreren Treffern
  - Optional: Caching der Datei für die Laufzeit oder Reload bei Dateiänderung

- **`IContactService` / `ContactService`**
  - Neue interne oder öffentliche Methode zum Anlegen eines Kontakts inklusive Aliasnamen in einem Vorgang, z. B. `CreateWithAliasesAsync`
  - Alternativ kann der neue Klassifizierungsservice direkt `CreateAsync` und `AddAliasAsync` verwenden
  - Muss bestehende Dublettenregeln respektieren und keine mehrfachen Kontakte für denselben bekannten Eintrag erzeugen

- **`StatementDraftService`** (`FinanceManager.Infrastructure.Statements.StatementDraftService.Classification.cs`)
  - Erweiterung der Kontaktklassifikation:
    1. Bestehende Kontakt-/Alias-Zuordnung ausführen
    2. Nur wenn kein Kontakt gefunden wurde und die Benutzereinstellung aktiv ist, bekannte-Kontakte-Katalog durchsuchen
    3. Bei eindeutigem Treffer Kontakt für den Benutzer anlegen
    4. Aliasse aus der Definition speichern
    5. Kontoauszugseintrag mit dem neuen Kontakt markieren bzw. verbuchen
  - Relevanter Einstiegspunkt: `TryAutoAssignContact` bzw. der umgebende Ablauf in `ClassifyInternalAsync`, da dort Benutzerkontakte und Aliasnamen geladen werden

#### Einstellungen / API / Shared

- **`UserProfileSettingsDto`** oder eigener Settings-DTO
  - Neues Feld: `bool KnownContactAutoCreateEnabled`

- **`UserProfileSettingsUpdateRequest`** oder eigener Settings-Request
  - Neues Feld zum Aktualisieren der Einstellung

- **`UserSettingsController`** (`FinanceManager.Web.Controllers.UserSettingsController`)
  - Erweiterung des bestehenden Settings-Endpunkts oder neuer Endpunkt für Import-/Kontakt-Automatisierung
  - GET liefert die aktuelle Einstellung
  - PUT speichert die Einstellung am Benutzer

- **`IApiClient` / `ApiClient`**
  - Erweiterung der passenden User-Settings-Methoden um das neue Feld

#### UI-Komponenten

- **Einstellungen-Seite / Settings-ViewModel**
  - Neuer Schalter zum Aktivieren/Deaktivieren der automatischen Anlage bekannter Kontakte
  - Beschriftung sinngemäß: "Bekannte Kontakte automatisch anlegen"

- **Ressourcen**
  - Lokalisierte Texte in den deutschen und englischen `.resx`-Dateien der betroffenen Einstellungsseite

#### Datenbankschicht

- **`AppDbContext` / Identity- oder App-Migrationen**
  - Neue Migration für das Benutzer-Flag
  - Mapping des neuen Felds an der bestehenden User-Entität

#### Tests

- **Unit-Tests für `KnownContactCatalog`**
  - Lädt gültige Datei
  - Ignoriert ungültige/leere Einträge
  - Match über Name und Alias
  - Kein Treffer bei Mehrdeutigkeit

- **Tests für `StatementDraftService`**
  - Bestehender Benutzerkontakt hat Vorrang vor bekanntem Kontakt
  - Fehlender Kontakt wird aus bekanntem Kontakt automatisch angelegt
  - Aliasse werden beim neuen Kontakt gespeichert
  - Einstellung deaktiviert verhindert Suche und Anlage
  - Kein automatisches Anlegen bei mehreren Treffern

- **Controller-/ApiClient-Tests für Einstellungen**
  - Einstellung wird gelesen und gespeichert
  - Defaultwert ist korrekt

---

### Implementierungsansatz

1. **Bekannte-Kontakte-Datei definieren**: JSON-Datei im Programmverzeichnis anlegen, z. B. mit Struktur `{ "contacts": [ { "name": "...", "type": "Organization", "aliases": ["..."] } ] }`. Die Datei wird nicht pro Benutzer gespeichert, sondern als auslieferbares Programmdatum behandelt.

2. **Katalog-Service einführen**: `KnownContactCatalog` lädt die Datei über `IHostEnvironment.ContentRootPath` oder eine per Options konfigurierte Pfadangabe. Der Service normalisiert Suchtexte analog zur bestehenden Kontaktklassifikation (`NormalizeUmlauts`, Whitespace-Behandlung, Wildcard-Muster) und liefert nur bei genau einem Treffer eine Definition zurück.

3. **Benutzereinstellung persistieren**: Die User-Entität erhält ein boolesches Flag zur Aktivierung der Funktion. Settings-DTOs, Controller, ApiClient und UI werden um diesen Wert erweitert.

4. **Klassifikation erweitern**: In `StatementDraftService.Classification.cs` wird nach der bestehenden Kontakt-/Alias-Suche geprüft, ob der Eintrag weiterhin keinen Kontakt hat. Ist die Einstellung aktiv, wird der Katalog mit `RecipientName`, ggf. ergänzend `Subject` oder `BookingDescription`, durchsucht.

5. **Kontakt automatisch anlegen**: Bei eindeutigem Katalogtreffer wird ein normaler Kontakt für den Benutzer erstellt. Danach werden die zugehörigen Aliasnamen gespeichert. Anschließend wird der aktuelle Kontoauszugseintrag so behandelt, als wäre der Kontakt regulär gefunden worden.

6. **Dubletten vermeiden**: Vor dem Anlegen wird erneut gegen vorhandene Benutzerkontakte und Aliasnamen geprüft. Dadurch wird verhindert, dass bei mehrfacher Klassifikation oder parallelen Imports derselbe bekannte Kontakt mehrfach entsteht.

7. **Tests ergänzen**: Die Tests decken sowohl den reinen Katalog-Matcher als auch die Integration in die Kontoauszugsklassifikation und die neue Einstellung ab.

---

### Konfiguration

- Die Liste bekannter Kontakte liegt als Datei im Programmverzeichnis und ist dadurch ohne Codeänderung erweiterbar.
- Die automatische Verwendung dieser Liste ist pro Benutzer in den Einstellungen ein- und ausschaltbar.
- Es ist keine benutzerspezifische Pflege der bekannten-Kontakte-Liste vorgesehen; Benutzer erhalten nur die automatisch angelegten normalen Kontakte in ihrem eigenen Konto.

---

### Offene Fragen

1. **Dateiformat und Pfad**: Soll die bekannte-Kontakte-Liste zwingend JSON sein, und welcher genaue Pfad im Programmverzeichnis ist gewünscht?
2. **Standardwert der Einstellung**: Soll die Funktion für bestehende und neue Benutzer standardmäßig aktiviert oder deaktiviert sein?
3. **Kontakt-Typen**: Sollen bekannte Kontakte immer als `Organization` angelegt werden, oder soll die Datei den `ContactType` je Eintrag festlegen können?
4. **Alias-Übernahme**: Sollen alle Aliasse aus der Datei beim automatisch erzeugten Kontakt gespeichert werden, oder nur der konkret erkannte Alias?
5. **Matching-Felder**: Soll nur der Empfängername durchsucht werden, oder zusätzlich Betreff/Buchungstext, wenn der Empfängername leer oder unbrauchbar ist?
6. **Mehrdeutige Treffer**: Soll bei mehreren passenden bekannten Kontakten gar nichts passieren, oder soll der Eintrag zur manuellen Prüfung markiert werden?
