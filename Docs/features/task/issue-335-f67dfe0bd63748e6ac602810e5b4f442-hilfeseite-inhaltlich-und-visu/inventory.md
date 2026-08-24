# Bestandsaufnahme: Hilfeseite

## Umfang

Untersucht wurden die Help-Routen, die Inhaltsauswahl, die Markdown-Quellen unter `Docs/help`, die Help-Assets, die Build-/Manifest-Integration sowie vorhandene Unit- und E2E-Tests.

## Relevante Bereiche

| Bereich | Bestand | Relevanz |
|---|---|---|
| Help-Hub | `FinanceManager.Web/Components/Pages/HelpHub.razor` | `/help`, Uebersicht, Suche und Themenlinks |
| Help-Detailseite | `FinanceManager.Web/Components/Pages/HelpPageView.razor` | `/help/view/{HelpPath}`, Markdown-Auswahl und Darstellung |
| Layout | `FinanceManager.Web/Components/Layout/HelpLayout.razor` | Gemeinsamer Seitenrahmen |
| Pfadauflosung | `FinanceManager.Web/Services/Help/HelpDocumentPathResolver.cs` | Normalisierung und Auswahl von Markdown-Dateien |
| Suchindex | `FinanceManager.Web/Services/Help/HelpSearchIndexBuilder.cs` | Themenliste, Titel, Excerpt und Keywords |
| Rendering | `FinanceManager.Web/Services/Help/HelpContentRenderer.cs` | Markdown-Rendering, Linkumschreibung und HTML-Sanitizing |
| Sichtbare Assets | `FinanceManager.Web/wwwroot/help/css/help-page.css`, `js/help-search.js` | Layout, Karten, Suche und Navigation |
| Inhaltsquellen | `Docs/help/` | Themenverzeichnisse und technische bzw. anwenderorientierte Dokumente |
| Tests | `FinanceManager.Tests`, `FinanceManager.Tests.E2E` | Renderer-, Integritaets-, Sicherheits- und UI-Abdeckung |

## Aktuelle Zuordnung der Themen

Der Hub erzeugt aus jedem Unterverzeichnis von `Docs/help` einen Eintrag. Die aktuelle primaere Datei ist fuer jedes Thema `index.md`, weil der Resolver bei einem einteiligen Themenpfad zuerst `index.md` waehlt.

| Thema | Aktuell angezeigte Primaerdatei | Vorhandene anwenderorientierte Dateien | Technische/interne Dateien, die getrennt werden muessen |
|---|---|---|---|
| Anhange | `Docs/help/anhaenge/index.md` | `beschreibung.md` | `api.md`, `datenmodell.md`, `business-rules.md` |
| Benutzeroberflaeche | `Docs/help/benutzeroberflaeche/index.md` | `beschreibung.md`, `ablauf-anwender.md`, `installation.md` | `ablauf-technisch.md` |
| Berichtswesen | `Docs/help/berichtswesen/index.md` | `beschreibung.md` | `api.md`, `datenmodell.md`, `business-rules.md` |
| Budgetplanung | `Docs/help/budgetplanung/index.md` | `beschreibung.md` | `api.md`, `datenmodell.md`, `business-rules.md` |
| Kontakte | `Docs/help/kontakte/index.md` | `beschreibung.md` | `api.md`, `datenmodell.md`, `business-rules.md` |
| Konten und Buchungen | `Docs/help/konten-und-buchungen/index.md` | `beschreibung.md`, `vorlaeufige-buchungen.md` | `api.md`, `datenmodell.md`, `business-rules.md` |
| Kontoauszuege und Import | `Docs/help/kontoauszuege-und-import/index.md` | `beschreibung.md`, `ablauf-anwender.md` | `ablauf-technisch.md`, `api.md`, `datenmodell.md`, `business-rules.md` |
| Programminformationen | `Docs/help/programminformationen/index.md` | `beschreibung.md`, `ablauf-anwender.md` | `ablauf-technisch.md` |
| Sparplaene | `Docs/help/sparplaene/index.md` | `beschreibung.md` | `api.md`, `datenmodell.md`, `business-rules.md` |
| Systemverwaltung und Setup | `Docs/help/systemverwaltung-und-setup/index.md` | `beschreibung.md`, `ablauf-anwender.md`, `installation.md`, `troubleshooting.md`, `sicherheit-help.md` | `ablauf-technisch.md`, `api.md`, `datenmodell.md`, `business-rules.md`, `bereitstellung.md` |
| Automatische Updates | `Docs/help/updates/index.md` | `beschreibung.md`, `installation.md`, `troubleshooting.md` | `ablauf-technisch.md`, `api.md` |
| Wertpapiermanagement | `Docs/help/wertpapiermanagement/index.md` | `beschreibung.md`, `ablauf-anwender.md` | `ablauf-technisch.md`, `api.md`, `datenmodell.md`, `business-rules.md` |

Die `index.md`-Dateien verlinken aktuell vielfach direkt auf technische Dokumenttypen. Besonders deutlich ist das bei Updates, Systemverwaltung und Setup sowie Wertpapiermanagement. Eine spaetere Umsetzung muss daher die sichtbare Themenstruktur und die Detailpfade explizit auf freigegebene Anwenderdokumente begrenzen.

## Festgestellte Luecken und Risiken

- Die Detailroute akzeptiert neben `index` beliebige Markdown-Dateinamen im Themenverzeichnis. Eine reine UI-Ausblendung reicht deshalb nicht als Inhaltsgrenze.
- Der Suchindex nimmt pro Themenverzeichnis automatisch die vom Resolver ausgewaehlte Datei. Es gibt keinen separaten redaktionellen Katalog fuer sichtbare Anwenderdokumente.
- `HelpHub.razor` und `help-search.js` rendern die Themenliste parallel. Diese beiden Darstellungen koennen auseinanderlaufen.
- Die UI verwendet generische Bootstrap-Karten und eine breite Dokumentationssprache; die bestehende CSS-Datei enthaelt zudem eigene, teils widerspruechliche Hell-/Dunkel-Farbregeln.
- Die bestehende Abgrenzung aus Issue #325 fuer Asset-Sicherheit und Suchindex bleibt ausserhalb des Scopes; vorhandene Integritaetspruefungen sollen dennoch als Vertrag erhalten bleiben.

## Detailinventar

- [Laufzeit und Inhaltsauswahl](inventory/runtime-and-content-selection.md)
- [Help-Quellen und redaktionelle Zuordnung](inventory/help-content-catalog.md)
- [UI, Styling und Navigation](inventory/help-ui-assets.md)
- [Build, Sicherheit und Tests](inventory/build-security-and-tests.md)

## Empfohlene naechste Schnittstellen

Die Planung sollte einen expliziten, zentralen Katalog fuer sichtbare Anwenderdokumente vorsehen. Hub, Suchindex und Detailroute sollten denselben Katalog verwenden. Technische Dokumente koennen unter `Docs/help` verbleiben, duerfen aber weder als Primaerdatei noch ueber die Anwendernavigation aufloesbar sein.
