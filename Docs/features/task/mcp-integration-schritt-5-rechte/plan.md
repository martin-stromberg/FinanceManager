# Plan – EP-05 Individuelle Rechte & Filter

## Ansatz
Serverseitig existierte `PUT …/scopes` bereits vollständig (Owner-Isolation,
Individuell-Gate, F3-Validierung, Delta-Audit) — nachgezogen wurde nur die
Implikationsregel „kein Recht ohne Lesen" (`McpAccessService.ValidateScopes`),
die das UI-Konzept der API zuschreibt. Der UI-Teil ist neu: `McpRightsEditor.razor`
(Matrix + Filter + Genehmigungsmodus-Klartext) in der Rechte-Section der
Zugangskarte; alle Serveraufrufe ausschließlich via `IApiClient` über neue
Methoden des `McpAccessCardViewModel`.

## Architekturentscheidungen

1. **Implikationsregel serverseitig** (`ValidateScopes`): für jedes Tupel mit
   Rechteart ≠ `Lesen` muss im selben Profil eine `Lesen`-Zeile desselben
   Datentyps existieren, sonst `Err_Mcp_InvalidScope`. Gilt für Create und
   PUT scopes gleichermaßen; Standardprofile sind davon unberührt (enthalten
   ohnehin `Lesen` überall). Der Editor selektiert `Lesen` automatisch mit
   (Vorschlag wird direkt umgesetzt statt nur gewarnt) und entfernt beim
   Abwählen von `Lesen` die übrigen Rechte der Zeile (Kaskade) — so kann die
   UI keine unsinnige Kombination erzeugen.

2. **„Von Vorlage anpassen" als Zwei-Schritt** (`StartIndividualEditAsync`):
   PUT access mit `Level=Individuell` (pending Feldwerte werden mitgeführt,
   damit halbeditierte Kartendaten nicht verloren gehen), danach PUT scopes
   mit dem bisher effektiven Profil als Seed. Der Rechte-Stand ist nach dem
   Wechsel identisch; schlägt das Seeding fehl, bleibt der Zugang
   einschränkend (kein Recht), die Karte lädt neu und zeigt den Fehler —
   Retry ist idempotent.

3. **Matrix-Editor-Modell = 1 Checkbox je (Datentyp × Rechteart).** Beim
   Speichern werden bestehende Zeilen eines weiterhin gesetzten Tupels
   verbatim (inkl. Filter-CSVs) übernommen und neue Tupel als ungefilterte
   Zeile angelegt — ein Matrix-Save kann nie still Filter verwerfen und nie
   Mehrfachzeilen (OR-Varianten) einkassieren. Abgewählte Tupel entfernen
   alle ihre Zeilen (einschränkend).

4. **Rechtserweiterung lokal bestätigt** (`SaveScopesAsync`): Diff der
   (Datentyp:Recht)-Tupelmengen; bei hinzugefügten Tupeln
   `IConfirmationService` (Warning). Erweiterung kann nie über F3/Owner-Rechte
   hinaus — die Bestätigung macht sie sichtbar, nicht möglich.

5. **Filterverwaltung je persistierter Zeile** (`SaveScopeFiltersAsync` →
   `PUT …/filters/{scopeId}`): 5 Dimensionen als Checkbox-Listen;
   `AccountTypes` aus dem Enum (lokalisiert via `EnumType_AccountType_*`),
   Kategoriedimensionen aus `Options.*Categories` (Namen→Id-Auflösungshilfe —
   die Options-Route liefert bereits die Owner-Listen via `IApiClient`).
   Gesetzte, nicht mehr auflösbare Ids (z. B. gelöschte Kategorien) werden
   muted nachgeführt und bleiben entfernbar. Zeilen-Ids ändern sich bei
   jedem scopes-PUT (Replace), daher sind Filter-Edits bei unsaved Matrix
   gesperrt (`Mcp_Filters_PendingMatrix`).

6. **Genehmigungsmodus** bleibt über das Karten-Feld editierbar (persistiert
   via Update + AccessUpdated-Delta); neu: `PreApproved` + Profil mit
   `Buchung`/`Löschung` → Critical-Bestätigung; Klartext + Kill-Switch-
   Hinweis in der Rechte-Section und im Wizard.

## Betroffene Dateien
- `FinanceManager.Infrastructure/Mcp/McpAccessService.cs` (ValidateScopes + Implikation)
- `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs` (+3 Methoden, PreApproved-Confirm, Pending-Resolver)
- `FinanceManager.Web/Components/Shared/McpRightsEditor.razor` (NEU)
- `FinanceManager.Web/Components/Shared/McpAccessExtrasPanel.razor` (Rechte-Section → Editor)
- `FinanceManager.Web/Components/Shared/McpAccessCreateWizard.razor` (Kill-Switch-Hinweis)
- `FinanceManager.Web/Resources/Pages{,.de,.en}.resx` (24 neue Keys × 3)
- `FinanceManager.Tests/Mcp/McpAccessServiceTests.cs` (5 neue Fälle + 1 angepasster)
- `FinanceManager.Web/Mcp/Tools/FmStatementDraftPrepareBookingTool.cs` (Whitespace-Fix, pre-push-Gate)
