# Umsetzungsplan: Mobile Ansicht der Kontoauszuege

## Zielbild

Die mobile Kontoauszugsansicht zeigt pro Eintrag alle fachlich relevanten Informationen ohne horizontales Scrollen: Datum und Betrag stehen in einer gemeinsamen zweispaltigen Zeile, lange Datei- und Textwerte brechen innerhalb der verfuegbaren Breite um, gebuchte Eintraege sind sichtbar abgeschwaecht und Kontakt-/Empfaenger-, Sparplan- sowie Wertpapierinformationen werden nach den bestehenden Fachregeln angezeigt.

Die Desktop-Darstellung bleibt funktional unveraendert. Generische Listen werden nur ueber optionale mobile Render-Metadaten erweitert; das neue Verhalten wird ausschliesslich von `StatementDraftEntriesListViewModel` aktiviert.

## Architekturentscheidung

Die Anforderung wird ueber eine gezielte Erweiterung des generischen Listenrenderings umgesetzt:

- `ListRecord` erhaelt optionale mobile Layout-Daten, damit einzelne Listen eine eigene mobile Kartenstruktur liefern koennen.
- `GenericListPage.razor` rendert weiterhin den bisherigen generischen Vier-Zellen-Fallback, wenn keine mobilen Layout-Daten vorhanden sind.
- `StatementDraftEntriesListViewModel` befuellt diese mobilen Layout-Daten fuer Kontoauszugseintraege mit den benoetigten Feldern und Gruppierungen.

Damit bleibt das Risiko fuer andere Listen gering, waehrend die Kontoauszugsansicht die geforderte spezifische mobile Struktur bekommt.

## Arbeitspakete

### 1. Mobile Render-Metadaten einfuehren

Datei: `FinanceManager.Web/ViewModels/Common/ListRendering.cs`

- Neue kleine Rendering-Typen fuer mobile Listenkarten einfuehren, z. B. `ListMobileRow`, `ListMobileRowKind` und optional `ListMobileCell`.
- Unterstuetzte Struktur:
  - normale Label/Wert-Zeile fuer Kontakt/Empfaenger, Betreff, Sparplan, Wertpapier und Status;
  - zweispaltige Label/Wert-Zeile fuer Datum und Betrag;
  - optionale CSS-Klasse pro mobilem Row-Block, damit Kontoauszugs-spezifische Styles moeglich sind.
- `ListRecord` um eine optionale `MobileRows`-Eigenschaft erweitern.
- Konstruktor-/Record-Erweiterung rueckwaertskompatibel gestalten, sodass bestehende `new ListRecord(cells, item, hint)`-Aufrufe weiter kompilieren.

### 2. Generische mobile Liste erweitern

Datei: `FinanceManager.Web/Components/Pages/GenericListPage.razor`

- In der mobilen Kartenansicht zuerst `rec.MobileRows` rendern, falls vorhanden.
- Fuer Records ohne `MobileRows` den bestehenden `GetMobileCells`-Fallback unveraendert verwenden.
- Eine zweispaltige mobile Row-Struktur rendern, deren Werte die vorhandenen `ListCell`-Renderregeln fuer Text, Symbol und Currency wiederverwenden.
- Das bestehende `muted-row`-Signal der Karte beibehalten.
- Keine Aenderung an der Desktop-Tabelle vornehmen.

### 3. Datenfluss fuer Kontaktanzeige ergaenzen

Dateien:

- `FinanceManager.Shared/Dtos/Statements/StatementDraftDetailDtos.cs`
- `FinanceManager.Web/Controllers/StatementDraftsController.cs`
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`

Umsetzung:

- `StatementDraftDetailDto` um optionale Lookup-Daten erweitern:
  - `ContactNames` als Entry-ID-Map;
  - `AccountBankContactId`;
  - `SelfContactId`.
- In `StatementDraftsController.GetAsync` beim bestehenden Symbolaufbau zusaetzlich Kontaktnamen setzen.
- Den Bankkontakt ueber `DetectedAccountId` und `IAccountService.GetAsync` ermitteln.
- Den Self-Kontakt ueber `IContactService.ListAsync` oder eine vorhandene passende Methode robust ermitteln; fehlt er, bleibt `SelfContactId` `null`.
- Bestehende DTO-Konstruktoraufrufe fuer andere Endpunkte mit Defaultwerten kompatibel halten oder gezielt ergaenzen, wenn die Signatur es erfordert.

### 4. Kontoauszugs-ViewModel fachlich erweitern

Datei: `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntriesListViewModel.cs`

- `StatementDraftEntryItem` bzw. die interne Item-Befuellung um folgende Werte erweitern:
  - `ContactId`
  - `SavingsPlanId`
  - `SecurityId`
  - `SecurityTransactionType`
- Draft-Level-Maps beim Laden uebernehmen:
  - `ContactNames`
  - `AccountBankContactId`
  - `SelfContactId`
- Anzeigeentscheidung fuer Kontakt/Empfaenger kapseln:
  - Wenn `ContactId` gesetzt ist und weder `AccountBankContactId` noch `SelfContactId` entspricht, Kontaktname anzeigen.
  - Sonst keinen Kontakt anzeigen.
  - Empfaenger nur anzeigen, wenn kein `ContactId` gesetzt ist und `RecipientName` nicht leer ist.
- Wertpapiertext als `Name (Buchungsart)` formatieren, wenn `SecurityId` und `SecurityTransactionType` vorhanden sind.
- Buchungsart ueber vorhandene Ressourcen `EnumType_SecurityTransactionType_*` lokalisieren; nicht per reinem `ToString()` in der UI ausgeben.
- Mobile Rows pro Record in dieser Reihenfolge liefern:
  1. Symbol, falls vorhanden;
  2. Datum und Betrag als zweispaltiger Block;
  3. Kontakt oder Empfaenger;
  4. Betreff, falls vorhanden;
  5. Sparplan, falls vorhanden;
  6. Wertpapier inklusive Buchungsart, falls vorhanden;
  7. Status, falls sinnvoll fuer die mobile Unterscheidung.
- `Muted` fuer alle fachlichen Zellen bereits gebuchter Eintraege beibehalten.

### 5. Mobile CSS absichern

Dateien:

- `FinanceManager.Web/wwwroot/css/app.css`
- optional `FinanceManager.Web/wwwroot/css/app.StatementDraftDetail.css`

Umsetzung:

- `.generic-list-mobile-card.muted-row` sichtbar abschwaechen, z. B. reduzierte Deckkraft und `color: var(--muted)`, ohne Inhalt unlesbar zu machen.
- Mobile Werte robuster umbrechen lassen:
  - `overflow-wrap: anywhere`;
  - `word-break: break-word`;
  - `min-width: 0` fuer mobile Kartenwerte und zweispaltige Zellen.
- Neue Klassen fuer zweispaltige mobile Rows definieren, z. B. `.generic-list-mobile-row.two-column` oder `.generic-list-mobile-grid`.
- Die zweispaltige Datum/Betrag-Zeile mit `grid-template-columns: minmax(0, 1fr) minmax(0, 1fr)` umsetzen.
- Datei- und Kartenwerte in der Statement-Draft-Detailkarte gegen horizontales Scrollen absichern, insbesondere fuer sehr lange `OriginalFileName`-Werte.

### 6. Tests ergaenzen

Bevorzugte Testdateien:

- `FinanceManager.Tests/ViewModels/StatementDraftsViewModelTests.cs`
- ggf. neue oder vorhandene bUnit-Tests fuer `GenericListPage.razor`
- optional E2E-Test, wenn vorhandene Testdaten und Gateway den Kontoauszugsdetailfluss einfach erreichbar machen

Pflichttests:

- Ein `AlreadyBooked`-Eintrag erzeugt weiterhin gemutete Zellen und eine mobile Karte mit `muted-row`.
- Eintrag mit fremdem Kontakt zeigt den Kontaktname mobil an.
- Eintrag mit Bankkonto-Kontakt zeigt keinen Kontaktname mobil an.
- Eintrag mit Self-Kontakt zeigt keinen Kontaktname mobil an.
- Eintrag ohne Kontakt, aber mit Empfaenger zeigt den Empfaenger mobil an.
- Eintrag mit Kontakt und Empfaenger zeigt den Empfaenger nicht mobil an.
- Eintrag mit Sparplan zeigt den Sparplannamen mobil an.
- Eintrag mit Wertpapier und Buchungsart zeigt `Wertpapier (Buchungsart)` mobil an.
- Mobile Rendering-Struktur enthaelt Datum und Betrag in einem gemeinsamen zweispaltigen Block.

Optionaler E2E-/Playwright-Test:

- Mobile Viewport oeffnen, sehr langen Dateinamen verwenden und pruefen, dass `document.documentElement.scrollWidth <= window.innerWidth`.

## Validierung

- `dotnet build`
- `dotnet test`
- Falls ein E2E-Test ergaenzt wird: relevanten E2E-Test gezielt ausfuehren.
- Manuelle Sichtpruefung im mobilen Viewport fuer:
  - gebuchte vs. offene Eintraege;
  - langer Dateiname;
  - Datum/Betrag-Zweispalte;
  - Kontakt/Empfaenger-Regel;
  - Sparplan;
  - Wertpapier mit Buchungsart.

## Risiken und Gegenmassnahmen

- Risiko: Eine generische Listenanpassung veraendert andere mobile Listen.
  - Gegenmassnahme: Bestehenden Fallback beibehalten und neue mobile Struktur nur verwenden, wenn `ListRecord.MobileRows` gesetzt ist.
- Risiko: DTO-Erweiterungen brechen Konstruktoraufrufe.
  - Gegenmassnahme: Neue Parameter nur optional am Ende des Records einfuegen und Build sofort ausfuehren.
- Risiko: Self-Kontakt fehlt in Sonderdatenbestaenden.
  - Gegenmassnahme: `SelfContactId` nullable behandeln; Kontaktanzeige nur dann unterdruecken, wenn IDs tatsaechlich uebereinstimmen.
- Risiko: Buchungsart wird unlokalisiert angezeigt.
  - Gegenmassnahme: vorhandene `EnumType_SecurityTransactionType_*`-Ressourcen nutzen und Fallback nur bei fehlendem Resource-Key verwenden.
- Risiko: Scrollfreiheit wird durch andere Container wie `card-embedded-list` oder lange Dateinamen gebrochen.
  - Gegenmassnahme: CSS mit `min-width: 0` und `overflow-wrap: anywhere` fuer Kartenwerte und mobile Listenwerte ergaenzen und per mobilem Viewport pruefen.

## Nicht umzusetzen

- Keine Aenderung an der Erkennung `StatementDraftEntryStatus.AlreadyBooked`.
- Keine Aenderung an Kontakt-, Sparplan- oder Wertpapierzuordnungen.
- Keine fachliche Neupriorisierung oder Sortierung der Kontoauszugseintraege.
- Keine breit sichtbare Aenderung der Desktop-Tabelle.

## Offene Punkte

Keine.
