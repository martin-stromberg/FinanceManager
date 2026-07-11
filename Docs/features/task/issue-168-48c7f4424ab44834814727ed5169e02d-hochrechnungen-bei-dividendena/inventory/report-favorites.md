# ReportFavorite-Persistenz und Service-Mapping

## Relevante Dateien

- `FinanceManager.Domain/Reports/ReportFavorite.cs`
- `FinanceManager.Infrastructure/Reports/ReportFavoriteService.cs`
- `FinanceManager.Application/Reports/IReportFavoriteService.cs`
- `FinanceManager.Infrastructure/AppDbContext.cs`

## Entity

`ReportFavorite` speichert die bestehenden Dashboard-Flags direkt:

- `IncludeCategory`
- `ComparePrevious`
- `CompareYear`
- `ShowChart`
- `Expandable`
- `UseValutaDate`

Weitere Auswahlwerte werden als CSV persistiert (`PostingKindsCsv`, Filter-CSV-Felder, `SecuritySubTypesCsv`). `IncludeDividendRelated` ist ein nullable Filter-Flag, kein Top-Level-Favoritenflag.

Das neue Hochrechnungsflag passt fachlich zu `ComparePrevious` und `CompareYear` und sollte als normales `bool` auf der Entity gespeichert werden. Betroffene Stellen in `ReportFavorite`:

- Konstruktor
- `Update(...)`
- Property-Liste
- `ReportFavoriteBackupDto`
- `ToBackupDto()`
- `AssignBackupDto(...)`

## Service-Mapping

`ReportFavoriteService` mappt Entity und DTO manuell. Betroffene Pfade:

- `ListAsync(...)`: anonyme Select-Projektion, temporaere Entity-Rekonstruktion, `ReportFavoriteDto`-Konstruktor.
- `GetAsync(...)`: analog zu `ListAsync`.
- `CreateAsync(...)`: Entity-Konstruktor, `Update(...)` fuer `UseValutaDate`, Rueckgabe-DTO.
- `UpdateAsync(...)`: `entity.Update(...)`, Rueckgabe-DTO.

Die Duplikatspruefung basiert nur auf `{ OwnerUserId, Name }`. Es gibt keine kriteriumsbasierte Konfliktpruefung, obwohl die Anforderung diese Moeglichkeit erwaehnt.

## Backup/Restore-Relevanz

`ReportFavoriteBackupDto` ist ein verschachtelter Record in der Entity und wird in Backup-Tests verwendet. Ein neues persistiertes Flag muss dort mit Default-kompatibler Restore-Strategie aufgenommen werden. Da Records positionsbasiert serialisiert/deserialisiert werden koennen, sollte die Aenderung mit Backup-Kompatibilitaet getestet werden.

## DbContext

`AppDbContext` deklariert `DbSet<ReportFavorite>` und konfiguriert Key, Name, Unique Index, `PostingKind`, `Interval` und `Take`. Viele bool-Spalten sind nicht explizit konfiguriert und werden per EF-Konvention gemappt. Das neue Flag kann per Konvention gemappt werden; fuer Klarheit und Default kann eine explizite Property-Konfiguration sinnvoll sein.
