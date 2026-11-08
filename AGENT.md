# AGENT.md

Diese Datei beschreibt Prinzipien, die bei der Implementierung neuer Features in diesem Projekt zwingend zu beachten sind. Sie ergänzt die allgemeinen Programmierrichtlinien in `.github/copilot-instructions.md`.

## Backup-Vollständigkeit bei neuen Datenbankfeldern

Jedes neue persistierte Feld muss im Benutzer-Backup enthalten sein, und alte Backups müssen weiterhin eingelesen werden können:

- **Neues Feld an einer bestehenden Entität:** In den `*BackupDto`-Record der Entität (z. B. `Account.AccountBackupDto`) sowie in `ToBackupDto()` und `AssignBackupDto()` aufnehmen. Neue DTO-Parameter erhalten immer einen Default-Wert (z. B. `bool SecurityProcessingEnabled = true`), damit die Deserialisierung alter Backups ohne dieses Feld nicht fehlschlägt.
- **Neue Entität bzw. Collection:** In `BackupService.BuildBackupDataAsync` exportieren und in `SetupImportService` mit `TryGetProperty`-Guard importieren (fehlende Sektion = leere Liste). Neue Sektionen werden **nicht** in `RequiredDataProperties` aufgenommen, da die Schema-Validierung alter Backups sonst fehlschlägt.
- **Absicherung:** Änderungen durch Backup/Restore-Tests belegen (siehe `FinanceManager.Tests/Infrastructure/*Backup*Tests.cs`).

## Benutzerbezug der Datensätze

- Jeder benutzereigene Datensatz trägt `OwnerUserId`, der im Konstruktor gesetzt wird (`Guards.NotEmpty`).
- Alle Abfragen filtern serverseitig auf den Besitzer — direkt (`OwnerUserId == userId`) oder über den Besitzerpfad (z. B. `StatementEntries` über Import → Account). Die UserId stammt aus dem Auth-Kontext und niemals aus Client-Parametern.
- Ein globaler, benutzerübergreifender Datenpool ist nur zulässig, wenn er explizit gefordert ist; er ist dann in der Entität (XML-Dokumentation) und unter `Docs/` entsprechend dokumentiert.

## Gruppierung des Ribbon-Menüs

Ribbon-Inhalte liefert `IRibbonProvider.GetRibbonRegisters(...)`; jeder `UiRibbonTab` wird zu einer Gruppe, deren Reihenfolge über die Tab-Liste bzw. `Sort` gesteuert wird. Die Gruppen sind stets in dieser Reihenfolge zu platzieren:

1. **Aktionen, die die aktuelle Entität direkt betreffen** — Neu, Speichern, Löschen, Archivieren (Gruppe `Ribbon_Group_Manage`).
2. **Größere Aktionen mit dem Datensatz** — z. B. Buchungen vorbereiten, Kurse aktualisieren (Gruppen wie `Ribbon_Group_Actions` / `Ribbon_Group_Bookings`).
3. **Aufrufe verknüpfter Informationen** — z. B. Posten, zugehöriger Kontakt (Gruppe `Ribbon_Group_Linked`, Register-Kind `LinkedInfo`).

Labels stammen aus den resx-Ressourcen (de + en), Icons aus `/icons/sprite.svg`, und die primäre Aktion erhält `MobileShortcut = true`.

## Weitere projektspezifische Regeln

- Aktionen für einen einzelnen Eintrag (Bearbeiten, Archivieren, Löschen) sind ausschließlich auf der Detailseite verfügbar; Übersichtsseiten bieten nur Neuanlage und Massenverarbeitung.
- `@rendermode InteractiveServer` ist auf jeder Blazor-Seite explizit anzugeben.
- Neue UI-Texte werden immer in `Resources/*.resx` für de und en gepflegt — keine hartkodierten Strings.
- EF-Migrationen werden nur per `dotnet ef migrations add <Name>` erzeugt (Developer PowerShell), niemals manuell geschrieben.
- Neue Enum-Mitglieder sind in `EnumCoverageTests` zu ergänzen — der Test assertiert die vollständige Memberliste und schlägt sonst fehl.
