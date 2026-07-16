← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Installation und Konfiguration

## Voraussetzungen

- Laufende Anwendung mit Datenbankmigrationen.
- Konfigurierte Authentifizierung und lokalisierte Ressourcen.

## Installationsschritte

1. Anwendung starten und initiale Migration/Seeding ausführen.
2. Administratorbenutzer anlegen.
3. Setup-Bereiche über die Seite `setup` prüfen.

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Jwt:Key` | string | leer | HMAC-Signaturschluessel fuer JWTs. In produktionsnahen Umgebungen muss der Wert extern bereitgestellt werden, z. B. ueber `Jwt__Key`, und mindestens 32 UTF-8-Bytes Schluesselmaterial enthalten. |
| `Jwt:Issuer` | string | `financemanager` | Erwarteter Token-Issuer fuer Ausstellung und Validierung. Bereitstellung per `Jwt__Issuer` ist moeglich. |
| `Jwt:Audience` | string | `financemanager` | Erwartete Token-Audience fuer Ausstellung und Validierung. Bereitstellung per `Jwt__Audience` ist moeglich. |
| `Jwt:LifetimeMinutes` | int | `30` | JWT- und Cookie-Lebensdauer in Minuten. In produktionsnahen Umgebungen muss der Wert groesser als `0` sein und darf maximal `1440` betragen. Bereitstellung per `Jwt__LifetimeMinutes` ist moeglich. |
| `ImportSplitMode` | Enum | `MonthlyOrFixed` | Strategie für Import-Splitting |
| `ImportMaxEntriesPerDraft` | int | `250` | Max. Entwurfszeilen pro Draft |
| `ImportMonthlySplitThreshold` | int? | `250` | Schwellwert für Monats-Split |
| `ImportMinEntriesPerDraft` | int | `8` | Min. Entwurfszeilen pro Draft |
| `MassImportDialogPolicy` | Enum | `OnMissingInformation` | Dialogverhalten im Massenimport |

## JWT-Konfiguration

Die Anwendung bindet die JWT-Einstellungen aus dem Abschnitt `Jwt`. In
produktionsnahen Umgebungen, also allen Umgebungen ausser `Development`, wird
die Konfiguration beim Start validiert. Der Start bricht ab, wenn
Pflichtwerte fehlen oder unsicher sind. Dadurch werden Deployments mit leerem
Secret, Platzhalterwerten oder zu langer Token-Lebensdauer nicht gestartet.

Fuer produktionsnahe Starts muessen mindestens diese Werte gesetzt sein:

```powershell
$env:Jwt__Key = "<mindestens-32-utf8-bytes-random-secret>"
$env:Jwt__Issuer = "financemanager"
$env:Jwt__Audience = "financemanager"
$env:Jwt__LifetimeMinutes = "30"
```

`Jwt__Key` darf nicht in `appsettings.Production.json` oder anderen
Repository-Dateien hinterlegt werden. Nutze eine Environment-Variable, ein
Container-Secret oder einen Secret Store der Zielplattform. Bekannte
Platzhalter wie `PLEASE_REPLACE_WITH_LONG_RANDOM_256BIT_SECRET_BASE64`,
`CHANGE_ME`, `REPLACE_ME` und `TODO` werden in produktionsnahen Umgebungen
abgelehnt.

`Jwt__Issuer` und `Jwt__Audience` werden sowohl beim Ausstellen als auch beim
Validieren von Bearer- und Cookie-JWTs verwendet. Aendere diese Werte nur
koordiniert, weil Tokens mit abweichendem Issuer oder abweichender Audience
als ungueltig gelten.

## Überprüfung

- Login/Logout funktioniert.
- Der produktionsnahe Start bricht ohne `Jwt__Key` oder mit unsicherem
  `Jwt__Key` ab.
- Geschuetzte API-Aufrufe akzeptieren nur Tokens mit passendem Issuer,
  passender Audience, gueltiger Lebensdauer und gueltiger Signatur.
- Benutzerprofil und Benachrichtigungseinstellungen sind speicherbar.
- Backup kann erstellt und Restore-Status abgefragt werden.
