# Umsetzungsplan: Hilfeseite inhaltlich und visuell ueberarbeiten

## Zielbild

Die Anwenderhilfe unter `/help` verwendet ausschliesslich redaktionell freigegebene, leicht verstaendliche Inhalte. Uebersicht, Suche und Detailansicht beziehen ihre Themen und Dokumentpfade aus einem gemeinsamen Katalog. Technische Dokumente bleiben unter `Docs/help` erhalten, erscheinen aber weder als primaere Themenquelle noch als Link in der Anwendernavigation.

Die Oberflaeche wird als klarer Hilfe-Hub mit sichtbarer Themenstruktur, Suche, Rueckweg zur Uebersicht und gut scanbaren Detailinhalten umgesetzt. Die bestehende Help-Asset-Integritaet, Pfadvalidierung, Linkumschreibung und Sanitizing-Logik bleiben erhalten.

## Umsetzungsschritte

### 1. Redaktionskatalog und Inhaltsauswahl zentralisieren

- Einen zentralen Katalog im Help-Servicebereich einfuehren, zum Beispiel `FinanceManager.Web/Services/Help/HelpContentCatalog.cs`.
- Pro Thema mindestens ID, Titel, Kurzbeschreibung, primaeres Dokument und erlaubte Detaildokumente hinterlegen.
- Nur anwendergeeignete Dokumenttypen aufnehmen, insbesondere `beschreibung.md`, `ablauf-anwender.md`, `installation.md`, `troubleshooting.md` und `sicherheit-help.md`, jeweils nur nach redaktioneller Pruefung fuer das konkrete Thema.
- `index.md` nicht mehr automatisch als sichtbare primaere Quelle verwenden, wenn es technische Links oder interne Begriffe enthaelt. Falls eine Themenuebersicht benoetigt wird, deren sichtbare Links aus dem Katalog erzeugen.
- Hub, Suchindex und Detailroute auf denselben Katalog umstellen. Dadurch entstehen keine abweichenden Themenlisten oder Primaerdateien.
- Bei nicht freigegebenen Dokumentpfaden eine normale Nicht-gefunden-/Fehlerbehandlung verwenden, ohne technische Pfade oder interne Implementierungsdetails als Anwenderinhalt auszugeben.

### 2. Help-Hub und Detailnavigation ueberarbeiten

- `FinanceManager.Web/Components/Pages/HelpHub.razor` auf den Katalog umstellen.
- Themenkarten mit Titel, kurzer anwenderorientierter Beschreibung und nachvollziehbarem Link zur Detailansicht rendern.
- Suchergebnisse aus dem gleichen Katalog bzw. dem daraus erzeugten Index beziehen; den parallelen clientseitigen Renderpfad in `wwwroot/help/js/help-search.js` angleichen oder auf den zentralen Datenvertrag begrenzen.
- `FinanceManager.Web/Components/Pages/HelpPageView.razor` so anpassen, dass nur katalogisierte Dokumente geladen werden.
- Auf der Detailseite einen sichtbaren Rueckweg zum Help-Hub und, falls mehrere Dokumente fuer ein Thema freigegeben sind, eine klare Detailnavigation bereitstellen.
- Fehler-, Lade- und leere Zustaende in anwenderfreundlicher Sprache formulieren; keine technischen Help-Pfade als primaeren Fehlertext anzeigen.

### 3. Inhalte redaktionell pruefen und zuordnen

- Die Zuordnung jedes der 12 Themen im Katalog dokumentieren und auf vorhandene Dateien beschraenken.
- Anwenderorientierte Dokumente sprachlich auf klare, kurze und scanbare Aussagen pruefen.
- Technische Links aus sichtbaren `index.md`-Inhalten entfernen oder durch anwenderorientierte Links ersetzen, sofern dies fuer die sichtbare Hilfe erforderlich ist.
- Keine neuen Fachfunktionen oder Help-Themen erfinden. Nicht freigegebene technische Dokumente bleiben im Repository und werden nur aus der UI-Auswahl ausgeschlossen.

### 4. Darstellung und responsive Verhalten verbessern

- `FinanceManager.Web/Components/Layout/HelpLayout.razor` und `FinanceManager.Web/wwwroot/help/css/help-page.css` auf einen konsistenten Help-Rahmen abstimmen.
- Uebersicht, Suche, Themenkarten, Detailnavigation und Markdown-Inhalt visuell klar trennen.
- Bestehende Produktgestaltung und Bootstrap-Konventionen weiterverwenden, mit gut lesbarer Typografie, ausreichenden Abstaenden, sichtbaren Fokuszustaenden und klaren Linkzustaenden.
- Responsive Layouts fuer Desktop und schmale Viewports pruefen; Tabellen, lange Ueberschriften, Suchergebnisse und Navigation duerfen nicht ueberlaufen.
- Dark-Mode-Regeln und globale Selektoren auf Konflikte mit den Help-Komponenten pruefen und nur Help-spezifisch kapseln.

### 5. Dokumentierte Katalogzuordnung

- Eine kurze technische Dokumentation der Katalogstruktur und der pro Thema veroeffentlichten Dokumenttypen im Feature-Artefakt bzw. an der zugehoerigen Serviceklasse hinterlegen.
- Festhalten, dass technische Dokumente weiterhin vorhanden sind, aber nicht als primaere Anwenderhilfe oder ueber die Help-Navigation auslieferbar sind.

### 6. Tests ergaenzen

- Unit-/Integrationstests fuer den Katalog: vollstaendige Themenliste, erlaubte Primaerdatei und Ausschluss technischer-only Dokumente.
- Tests fuer die Detailroute: freigegebene Dokumente sind erreichbar, nicht freigegebene Dokumente werden nicht angezeigt.
- Tests fuer Hub und Suchindex: identische Themen- und Dokumentauswahl aus dem zentralen Katalog.
- Bestehende Renderer-, Integritaets-, Sicherheits- und Pfadvalidierungstests unveraendert ausfuehren.
- Den vorhandenen Playwright-Test fuer `/help` um die Navigation von Uebersicht zu freigegebenem Detailinhalt und zurueck sowie um einen Nachweis gegen technische-only Inhalte erweitern.
- Responsive Darstellung mindestens fuer einen Desktop- und einen schmalen Viewport pruefen.

## Betroffene Bereiche

- `FinanceManager.Web/Services/Help/HelpContentCatalog.cs` (neu oder bestehendes Help-Service-Muster)
- `FinanceManager.Web/Services/Help/HelpDocumentPathResolver.cs`
- `FinanceManager.Web/Services/Help/HelpSearchIndexBuilder.cs`
- `FinanceManager.Web/Components/Pages/HelpHub.razor`
- `FinanceManager.Web/Components/Pages/HelpPageView.razor`
- `FinanceManager.Web/Components/Layout/HelpLayout.razor`
- `FinanceManager.Web/wwwroot/help/css/help-page.css`
- `FinanceManager.Web/wwwroot/help/js/help-search.js`
- zugehoerige Tests in `FinanceManager.Tests` und `FinanceManager.Tests.E2E`
- redaktionell betroffene Dateien unter `Docs/help/<thema>/`

## Nicht im Umfang

- Keine Aenderung der Help-Asset-Sicherheits- und Hashmechanismen aus Issue #325.
- Keine Aenderung an HTML-Sanitizing, Traversal-Schutz oder allgemeiner Linkvalidierung ausserhalb der notwendigen Katalogintegration.
- Keine neuen Anwenderfunktionen ausser der verbesserten Hilfeauswahl, Navigation und Darstellung.
- Keine Entfernung technischer Dokumente aus `Docs/help`.

## Abnahmekriterien

- `/help` zeigt ausschliesslich katalogisierte Anwenderinhalte.
- Fuer jedes sichtbare Thema ist Primaerdatei bzw. Dokumenttyp im Katalog nachvollziehbar festgelegt.
- Technische-only Dateien sind weder primaere Quelle noch ueber die Anwendernavigation erreichbar.
- Hub, Suche und Detailseite zeigen dieselben freigegebenen Themen.
- Uebersicht und Detailansicht sind klar strukturiert, tastaturbedienbar und auf unterstuetzten Bildschirmgroessen nutzbar.
- Bestehende Help-Sicherheits- und Integritaetstests bleiben erfolgreich; neue Inhaltsauswahl- und Navigations-Tests decken die Abgrenzung ab.

## Offene Punkte

Keine. Die konkrete Ablage des Katalogs darf sich an einem bereits vorhandenen Help-Service-Muster orientieren, solange Hub, Suchindex und Detailroute denselben Datenbestand verwenden.
