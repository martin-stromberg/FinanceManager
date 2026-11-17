# Übersetzte Anforderung – Issue #510: Posteneinsicht im Bericht

## Kundenanforderung (Original, Issue #510)

> „In den berichten werden aktuell nur die Summen der jeweils betrachteten Entitäten gezeigt. Der Anwender soll nun auch die zu den Werten zugehrigen Posten aufgelistet bekommen. Dafür soll es ein Symbolbutton an den Beträgen geben. (Betrag, Vorperiode, Vorjahr). Betätigt der Anwender diesen neuen Button, so soll sich ein Overlay-Panel mit einer Auflistung der Posten zeigen, aus denen sich der Betrag ergibt."

## Ziel

Der Anwender kann zu jedem aggregierten Betragswert in der Berichtstabelle die zugrunde liegenden einzelnen Posten (Buchungen) einsehen, ohne den Bericht zu verlassen. Die bisher reine Summenanzeige wird um eine Drill-down-Einsicht auf Postenebene erweitert.

## Ist-Zustand

- Das Report-Dashboard (`FinanceManager.Web/Components/Pages/ReportDashboard.razor`) zeigt pro Zeile (Gruppierungsentität, ggf. mit Kategorie- und Kindzeilen sowie Summenzeilen) ausschließlich aggregierte Werte:
  - `Betrag` (aktueller Zeitraum, resx `Th_Amount`),
  - optional `Vorperiode` (resx `Th_Prev`, inkl. Delta-Spalte `Th_DeltaPrev`),
  - optional `Vorjahr` (resx `Th_YearAgo`, inkl. Delta-Spalte `Th_DeltaYear`),
  - optional `Hochrechnung` (resx `Th_Projection`, nur reine Wertpapierberichte).
- Es existiert keine Möglichkeit, die hinter einer Summe stehenden Posten aus dem Bericht heraus anzuzeigen.
- Referenz für ein vergleichbares Bedienmuster existiert bereits: Der Budget-Report verfügt über ein Posten-Overlay (siehe `BudgetReportViewModel` mit `PostingsOverlayKind`, `PurposePostings` und der Posten-Tabelle in `BudgetReport.razor` mit den Spalten Datum, Herkunft, Beschreibung, Betrag).

## Fachlicher Scope / Funktionale Anforderungen

1. **Symbolbutton an Betragszellen**
   - Jede Zelle mit einem aggregierten Betragswert in den Spalten `Betrag`, `Vorperiode` und `Vorjahr` erhält einen Symbolbutton (Icon aus `/icons/sprite.svg`).
   - Der Button ist an allen Zeilen sichtbar bzw. bedienbar, die einen anzeigbaren Betrag liefern — Gruppenzeilen, Kind-/Detailzeilen und Summenzeilen (Total/Grand Total).
   - Zellen ohne Wert (Anzeige „-" für fehlende Vorperioden-/Vorjahreswerte) erhalten keinen bzw. einen deaktivierten Button.
   - Die Delta-Spalten (`Delta Vorperiode`, `Delta Vorjahr`) erhalten **keinen** Button, da Differenzen keine eigenständige Postenmenge besitzen.

2. **Overlay-Panel mit Postenliste**
   - Bei Betätigung des Buttons öffnet sich ein Overlay-Panel (nicht-modale Abfrage im bestehenden UI-Muster der Anwendung), das die Posten auflistet, aus denen sich der angezeigte Betrag zusammensetzt.
   - Das Panel macht den Kontext erkennbar (welche Zeile/Entität und welche Spalte/Periode eingesehen wird), z. B. über einen Titel.
   - Die Postenliste zeigt mindestens die für eine Einordnung notwendigen Felder je Posten — analog zur bestehenden Posten-Darstellung: Buchungsdatum, Beschreibung/Empfänger (Herkunft) und Betrag.
   - Die aufgelisteten Posten entsprechen exakt der Filter- und Vergleichslogik des Berichts: gleiche Entitäts-/Kategorieeinschränkung, gleiche Zeitraumgrenzen (Betrag = aktueller Zeitraum, Vorperiode/Vorjahr = jeweilige Vergleichsperiode) und gleiche aktive Filter (inkl. Textfilter, Buchungsarten, Untertypen).
   - Die Summe der angezeigten Posten entspricht dem in der Zelle angezeigten Wert.
   - Das Panel kann vom Anwender wieder geschlossen werden (Schließen-Button, ggf. Klick außerhalb/Escape, konform zum üblichen Overlay-Verhalten der Anwendung).
   - Ladezustand: Während die Posten abgerufen werden, ist ein Ladehinweis sichtbar; bei leerem Ergebnis wird eine entsprechende Leeranzeige gezeigt.

3. **Datenbezug**
   - Die Posten werden benutzer- und mandantensicher serverseitig ermittelt; der Besitzerfilter (`OwnerUserId`) gilt wie bei der Berichtsberechnung.
   - Der Abruf erfolgt bei Bedarf (Lazy), nicht zusammen mit der initialen Summenberechnung.

## Akzeptanzkriterien

1. Im Report-Dashboard weist jede Betragszelle der Spalten `Betrag`, `Vorperiode` und `Vorjahr` (sofern ein Wert angezeigt wird) einen Symbolbutton auf.
2. Ein Klick auf den Button öffnet ein Overlay-Panel, das die Posten der betreffenden Zeile und Periode auflistet.
3. Die Summe der Posten im Overlay stimmt mit dem angezeigten Zellwert überein (unter Berücksichtigung der anzuzeigenden Rundung).
4. Für Vorperiode und Vorjahr werden die Posten der jeweiligen Vergleichsperiode geladen, nicht die des aktuellen Zeitraums.
5. Aktive Berichtsfilter (Entitäten, Kategorien, Buchungsarten/-untertypen, Textfilter) wirken identisch auf die aufgelisteten Posten.
6. Delta-Spalten und leere Zellen („-") bieten keinen Posten-Abruf.
7. Neue UI-Texte (Button-Tooltip, Overlay-Titel, Leeranzeige) sind in den resx-Ressourcen für Deutsch und Englisch gepflegt.
8. Die Funktion ist für eingeloggte Anwender auf ihren eigenen Daten nutzbar; fremde Posten sind nicht abrufbar.

## Abgrenzungen / Scope-Ausschlüsse

- **Keine Änderung der Aggregatberechnung:** Die Summenbildung, Vergleichsperioden, Intervalle und Filterlogik des Berichts bleiben unverändert; das Feature liefert ausschließlich eine zusätzliche Einsicht.
- **Hochrechnungsspalte:** Für die Spalte `Hochrechnung` (Projection) ist im Kundentext kein Button gefordert („(Betrag, Vorperiode, Vorjahr)"). Ob dort ebenfalls eine Posteneinsicht sinnvoll ist (Mischung aus gebuchten und erwarteten Dividenden), bleibt offen (siehe Offene Punkte).
- **Kein Editing im Overlay:** Das Panel ist reine Einsicht; Bearbeiten, Löschen oder Verknüpfungsaktionen einzelner Posten sind nicht Teil dieser Anforderung.
- **Budget-Report:** Der Budget-Report besitzt bereits ein Posten-Overlay mit eigenem Spaltenlayout (Budget/Ist/Delta); die Anforderung richtet sich primär an das allgemeine Report-Dashboard. Eine Ausweitung auf weitere Berichtsansichten (Startseiten-KPIs, Sparplan-Ist-Report, Portfolio-Analyse) ist nicht Teil des Kundentexts (siehe Offene Punkte).
- **Kein Export:** Ein Export der Postenliste aus dem Overlay wird nicht gefordert.

## Betroffene Komponenten (Hinweise, nicht abschließend)

- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` — Ergebnistabelle mit den Betragszellen (`Th_Amount`, `Th_Prev`/`Th_YearAgo`), Top-/Child-/Summenzeilen.
- Zugehöriges Report-ViewModel — Auslösung des Overlays, Laden der Posten pro Zelle/Periode, Overlay-State.
- `FinanceManager.Application/Reports/IReportAggregationService.cs` bzw. bestehender Postenabruf — serverseitige Ermittlung der Posten je Aggregatzelle unter Berücksichtigung der Filter.
- Referenzmuster: `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs` (`PostingsOverlayKind`, `PurposePostings`) und `BudgetReport.razor` (Posten-Overlay); `OverlayHost`/`UiOverlaySpec`-Mechanik auf `CardPage.razor`.
- `FinanceManager.Web/Resources/**/*.resx` (de + en) — neue Texte für Tooltip, Overlay-Titel, Spaltenköpfe, Leeranzeige.
- Icons aus `/icons/sprite.svg` für den Symbolbutton.

## Offene Punkte

1. **Umfang „die Berichte":** Der Kundentext spricht von „den berichten" generell. Primäre Auslegung: das Report-Dashboard, da dort die genannten Spalten (Betrag, Vorperiode, Vorjahr) existieren. Soll die Posteneinsicht zusätzlich auch für KPI-Kacheln der Startseite oder andere Berichtsansichten gelten?
2. **Hochrechnungsspalte:** Soll auch an der Spalte `Hochrechnung` ein Posten-Overlay angeboten werden (und welche Posten — nur gebuchte oder auch erwartete Dividenden)?
3. **Summenzeilen:** Soll der Button auch an den Total-/Grand-Total-Zeilen alle Posten der gesamten Berichtsperiode bzw. Spalte liefern, oder nur an den Entitätszeilen?
4. **Felder der Postenliste:** Sind die Minimal-Felder (Datum, Herkunft/Beschreibung, Betrag) ausreichend oder werden weitere Felder (Konto, Kategorie, Untertyp) verlangt?
5. **Detailnavigation:** Soll aus dem Overlay per Klick auf einen Posten die Posten-Detailkarte geöffnet werden können, oder bleibt es bei reiner Listenansicht?
6. **Mobile-Darstellung:** Wie verhält sich der Symbolbutton auf kleinen Viewports (immer sichtbar vs. nur bei Hover/Fokus)?
