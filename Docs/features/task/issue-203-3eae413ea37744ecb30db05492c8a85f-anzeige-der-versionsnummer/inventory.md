# Bestandsaufnahme: Anzeige der Versionsnummer im Programmmenü

Diese Bestandsaufnahme analysiert die bestehende Codebasis bezogen auf die Anforderung, die Benutzer-ID in der `LoginStatus.razor` Komponente durch die aktuelle Versionsnummer des Programms zu ersetzen.

## Zusammenfassung

- **LoginStatus.razor Komponente** existiert bereits und zeigt derzeit die Benutzer-ID (`CurrentUser.UserId`) an.
- **ICurrentUserService** ist als Scoped Service registriert und wird bereits von LoginStatus.razor injiziert.
- **IInstalledReleaseMetadataProvider** ist bereits implementiert und als Singleton Service registriert; die Implementierung liest `release-metadata.json` asynchron ein.
- **InstalledReleaseMetadataDto** existiert und enthält alle erforderlichen Felder (Version, PublishedAt, CommitSha, Repository, RuntimeIdentifier).
- **Tests** für LoginStatus.razor existieren derzeit nicht; Tests für die Update-Services (inklusive Metadaten) sind vorhanden.
- Die DI-Infrastruktur in `ProgramExtensions.cs` ist vorbereitet; der Service muss nur in der Komponente injiziert werden.

## Details

- [Interfaces](inventory/interfaces.md)
- [Datenmodelle](inventory/models.md)
- [Logik-Services](inventory/logic.md)
- [Tests](inventory/tests.md)
