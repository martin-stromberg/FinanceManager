# EF-Migrationen und DbContext

## Relevante Pfade

- `FinanceManager.Infrastructure/AppDbContext.cs`
- `FinanceManager.Infrastructure/Migrations/`
- `FinanceManager.Infrastructure/Data/Migrations/Identity/`

## DbContext-Modell

`AppDbContext` enthaelt `DbSet<ReportFavorite>` und konfiguriert `ReportFavorite` mit Key, Name-Laenge, Unique Index auf `{ OwnerUserId, Name }`, `PostingKind`, `Interval` und `Take`.

Viele ReportFavorite-Properties werden per EF-Konvention gemappt. In den Model-Snapshots sind z. B. `ComparePrevious`, `CompareYear`, `IncludeDividendRelated` und `UseValutaDate` sichtbar.

## Migrationslage

Es gibt zwei Migration-Verzeichnisse. Neuere fachliche App-Migrationen liegen im Pfad `FinanceManager.Infrastructure/Migrations/`, inklusive `AppDbContextModelSnapshot.cs`. Der Identity-Pfad enthaelt aeltere/identity-bezogene Migrationen und ebenfalls historische `ReportFavorites`-Eintraege.

Letzte sichtbare App-Migrationen:

- `20260703061917_202607030850_AddMassImportDialogPolicy`
- `20260707193327_AddCollectionAccountAndLinkedIbans`
- `20260710090000_AddKnownContactAutoCreateSetting.cs`
- `AppDbContextModelSnapshot.cs`

Letzte sichtbare Identity-Migrationen:

- `20260220181649_ReportCache`
- `20260221144132_Postings_OriginalAmount`

## Konsequenz fuer das neue Flag

Die neue Spalte fuer `ReportFavorites` sollte im aktiven App-Migrationspfad (`FinanceManager.Infrastructure/Migrations`) entstehen und den `AppDbContextModelSnapshot` aktualisieren. Default fuer bestehende Datensaetze sollte `false` sein.

Zu pruefen ist, ob das Projekt zur Migrationserzeugung eine spezifische DbContext-/Startup-Konfiguration erwartet. Falls EF CLI nicht stabil laeuft, ist eine manuelle Migration nach bestehendem Muster moeglich, muss aber Snapshot und Designer konsistent aktualisieren.
