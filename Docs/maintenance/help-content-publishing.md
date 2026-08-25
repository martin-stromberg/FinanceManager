# Help-Inhalte veroeffentlichen

Die sichtbare Hilfe unter `/help` verwendet ausschliesslich Dokumente, die im
redaktionellen Katalog `FinanceManager.Web/Services/Help/HelpContentCatalog.cs`
freigegeben sind. Der Katalog ist die gemeinsame Quelle fuer Help-Uebersicht,
Suche und Detailnavigation.

## Redaktionsregeln

- Anwenderhilfe muss ohne technische Vorkenntnisse verstaendlich, kurz und
  scanbar formuliert sein.
- Technische Dokumente bleiben unter `Docs/help/` erhalten, werden aber nicht
  automatisch aus Verzeichnisinhalten oder `index.md`-Dateien veroeffentlicht.
- Dateien wie `api.md`, `ablauf-technisch.md`, `bereitstellung.md`,
  `business-rules.md`, `datenmodell.md` und `index.md` gelten als technische
  Dokumentation und sind nicht primaere Anwenderhilfe.
- Neue oder geaenderte Anwenderdokumente muessen vor der Aufnahme in den
  Katalog redaktionell geprueft werden. Ein Eintrag im Katalog legt dabei
  Route, Dateiname und sichtbare Bezeichnung explizit fest.
- Die erste Datei eines Themas ist die primaere Quelle fuer
  `/help/view/{thema}`. Weitere freigegebene Dateien erscheinen in der
  Detailnavigation.

## Veroeffentlichte Zuordnung

| Thema | Primaere Datei | Weitere freigegebene Dateien |
|---|---|---|
| Anhaenge | `anhaenge/beschreibung.md` | - |
| Benutzeroberflaeche | `benutzeroberflaeche/beschreibung.md` | `ablauf-anwender.md`, `installation.md` |
| Berichtswesen | `berichtswesen/beschreibung.md` | - |
| Budgetplanung | `budgetplanung/beschreibung.md` | - |
| Kontakte | `kontakte/beschreibung.md` | - |
| Konten und Buchungen | `konten-und-buchungen/beschreibung.md` | `vorlaeufige-buchungen.md` |
| Kontoauszuege und Import | `kontoauszuege-und-import/beschreibung.md` | `ablauf-anwender.md` |
| Programminformationen | `programminformationen/beschreibung.md` | `ablauf-anwender.md` |
| Sparplaene | `sparplaene/beschreibung.md` | - |
| Systemverwaltung und Setup | `systemverwaltung-und-setup/beschreibung.md` | `ablauf-anwender.md`, `einrichtung-anwender.md`, `troubleshooting.md`, `sicherheit-help.md` |
| Automatische Updates | `updates/beschreibung.md` | `einrichtung-anwender.md`, `fehlerbehebung-anwender.md` |
| Wertpapiermanagement | `wertpapiermanagement/beschreibung.md` | `ablauf-anwender.md` |

Die aufgelisteten Pfade werden durch `HelpContentCatalog.TryResolveDocument`
auf vorhandene Dateien aufgeloest. Nicht katalogisierte Pfade werden nicht als
Anwenderhilfe geladen. Technische Dateien sind deshalb weiterhin im Repository
verfuegbar, aber weder primaere Quelle noch ueber die Help-Navigation
erreichbar.

## Aenderungsablauf

1. Anwendertext unter dem passenden Verzeichnis in `Docs/help/` erstellen oder
   ueberarbeiten.
2. Redaktionell pruefen, dass der Text keine Implementierungsdetails oder
   internen Betriebsinformationen als Bedienhilfe enthaelt.
3. Den Pfad in `HelpContentCatalog.cs` aufnehmen oder entfernen. Die erste
   Position der Dokumentliste ist die primaere Datei.
4. Katalogtests sowie den Help-E2E-Test ausfuehren.
5. Bei Aenderungen unter `Docs/help/` den Help-Index und das Asset-Manifest
   durch einen neuen Build aktualisieren, bevor die Anwendung ausgeliefert
   wird.
