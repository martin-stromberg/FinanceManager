# Bestandsaufnahme - Bekannte Kontakte

Hinweis: Der vorgesehene Bestandsaufnahme-Unteragent konnte wegen Usage-Limit nicht ausgefuehrt werden. Diese Bestandsaufnahme wurde lokal erstellt.

## Relevante Bereiche

- `FinanceManager.Infrastructure/Statements/StatementDraftService.Classification.cs`
  - zentrale automatische Klassifikation von Kontoauszugsentwuerfen
  - laedt Benutzerkontakte, Aliasnamen, Sparplaene und Wertpapiere
  - `TryAutoAssignContact` ordnet bestehende Kontakte anhand Name/Alias zu
- `FinanceManager.Domain/Users/User.cs`
  - enthaelt benutzerspezifische Import- und Profileinstellungen
  - Import-Split-Defaults werden in Konstruktoren gesetzt
- `FinanceManager.Web/Controllers/UserSettingsController.cs`
  - GET/PUT fuer Import-Split-Einstellungen unter `/api/user/settings/import-split`
- `FinanceManager.Shared/Dtos/Statements/ImportSplitSettingsDto.cs`
  - DTO fuer Import-Einstellungen
- `FinanceManager.Shared/Dtos/Statements/ImportSplitSettingsRequests.cs`
  - Update-Request fuer Import-Einstellungen
- `FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs`
  - laedt/speichert Import-Einstellungen und berechnet Dirty-State
- `FinanceManager.Web/Components/Pages/Setup/SetupStatementTab.razor`
  - UI fuer Kontoauszugsimport-Einstellungen
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`
  - DI-Registrierung fuer Infrastrukturservices
- `FinanceManager.Infrastructure/AppDbContext.cs`
  - EF-Mapping der User-Einstellungen

## Beobachtungen

- Es gibt bisher keine zentrale Programmdaten-Datei fuer bekannte Kontakte.
- Kontakt-Alias-Matching nutzt Wildcards `*` und `?` sowie Umlautnormalisierung.
- Die Klassifikation resetet offene Eintraege, prueft Dubletten, ordnet Kontakte zu, danach Sparplaene und Wertpapiere.
- Neue Kontakte koennen direkt ueber Domain-Entitaeten `Contact` und `AliasName` im DbContext angelegt werden.
- Die passende Benutzereinstellung kann im bestehenden Import-Einstellungsmodell transportiert werden.

## Detaildokumente

- Keine separaten Detaildokumente erforderlich; die betroffenen Dateien sind oben direkt benannt.
