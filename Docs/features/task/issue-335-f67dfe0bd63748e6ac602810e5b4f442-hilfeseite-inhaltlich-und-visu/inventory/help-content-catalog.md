# Help-Quellen und redaktionelle Zuordnung

## Verzeichnisstruktur

`Docs/help` enthaelt aktuell 12 Themenverzeichnisse plus `index.md` und `bestandsaufnahme.md` auf der obersten Ebene. Die Themenverzeichnisse kombinieren anwenderorientierte Dateien (`beschreibung.md`, `ablauf-anwender.md`, `installation.md`, `troubleshooting.md`) mit technischen Dateien (`api.md`, `datenmodell.md`, `business-rules.md`, `ablauf-technisch.md`, `bereitstellung.md`).

## Aktuelle Dokumenttypen

| Dokumenttyp | Rolle | Sichtbarkeit fuer Endanwender |
|---|---|---|
| `index.md` | Themenuebersicht, Titel und Links | derzeit primaer sichtbar; Inhalte/Links sind redaktionell gemischt |
| `beschreibung.md` | fachliche Beschreibung | geeignet, nach redaktioneller Pruefung |
| `ablauf-anwender.md` | Bedienablauf | geeignet |
| `installation.md` | Installation/Konfiguration | nur fuer passende Zielgruppe geeignet |
| `troubleshooting.md` | Anwender-Fehlerbehebung | geeignet, nach Pruefung |
| `sicherheit-help.md` | anwendernahe Sicherheitshinweise | geeignet, nach Pruefung |
| `api.md` | technische Schnittstelle | nicht als primaere Anwenderhilfe |
| `datenmodell.md` | interne Datenstruktur | nicht als primaere Anwenderhilfe |
| `business-rules.md` | interne Fachlogik | nicht als primaere Anwenderhilfe |
| `ablauf-technisch.md` | Implementierungsablauf | nicht als primaere Anwenderhilfe |
| `bereitstellung.md` | Deployment/Release | nicht als primaere Anwenderhilfe |

## Redaktionsrisiko

Die Indexdateien sind nicht durchgehend reine Anwenderuebersichten. `updates/index.md` nennt beispielsweise interne Typen wie `IUpdateOrchestrator` und `UpdateExecutor`; `systemverwaltung-und-setup/index.md` verweist auf RFC- und security.txt-Details; andere Indexdateien verlinken API-, Datenmodell- und Business-Rule-Dateien.

## Erforderliche Zielentscheidung fuer die Planung

Pro Thema muss eine sichtbare Dokumentdatei oder eine explizite Liste sichtbarer Dokumente festgelegt werden. Der Katalog sollte neben Route und Titel auch die erlaubten Detaildokumente enthalten. Nicht gelistete Dateien bleiben im Repository und koennen fuer Entwickler- oder Wartungsdokumentation verwendet werden.
