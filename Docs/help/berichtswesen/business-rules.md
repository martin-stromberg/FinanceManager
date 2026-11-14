← [Zurück zur Übersicht](index.md)

# Berichtswesen — Business Rules

## Favoriten begrenzen Zeitfenster

**Beschreibung:** Favoriten begrenzen die Anzahl betrachteter Perioden über `Take`.

**Bedingungen:**
- Eingabewert kann frei übermittelt werden.

**Verhalten:**
- Wert wird auf einen sinnvollen Bereich geklemmt.

**Umsetzung:** `ReportFavorite.SetTake` (1 bis 120).

## Mehrfachfilter werden als CSV persistiert

**Beschreibung:** Mehrfachselektionen werden dauerhaft in CSV-Feldern gespeichert.

**Bedingungen:**
- Filterlisten können leer oder befüllt sein.

**Verhalten:**
- Beim Speichern werden Listen serialisiert.
- Beim Laden werden Listen typisiert zurückgegeben.

**Umsetzung:** `ReportFavorite.SetFilters` und `ReportFavorite.GetFilters`.

## Dividendenhochrechnung nur für Wertpapierberichte

**Beschreibung:** Die Vergleichsoption `Hochrechnung` berechnet erwartete Dividenden nur für reine Wertpapierberichte.

**Bedingungen:**
- Die effektive Buchungsartenauswahl enthält ausschließlich `PostingKind.Security`.
- Das Intervall ist nicht `AllHistory`.
- Die Auswertung nutzt den Netto-Dividendenpfad mit Dividenden, Gebühren und Steuern.

**Verhalten:**
- Bei gültiger Auswahl liefert die Aggregation `ComparedProjection = true` und pro Berichtspunkt ein optionales `ProjectionAmount`.
- `ProjectionAmount` besteht aus den gebuchten Netto-Dividenden des aktuellen Betrachtungszeitraums plus erwarteten Netto-Dividenden aus der gleichen Vorjahresperiode.
- Eine Vorjahresdividende gilt als bestätigt, wenn im korrespondierenden aktuellen Periodenbucket mindestens eine Dividende desselben Wertpapiers vorhanden ist.
- Erwartete Dividenden werden nur erzeugt, wenn das Wertpapier zum erwarteten Dividendentag einen positiven Bestand hat. Käufe und Verkäufe mit Menge bilden den Bestand; Stornobuchungen wirken mit ihrer gespeicherten Gegenmenge.
- Bei anderen Buchungsarten, gemischten Auswahlen oder `AllHistory` bleibt die Hochrechnung wirkungslos; `ComparedProjection = false` und `ProjectionAmount` bleibt leer.
- Kategorie- und Summenzeilen aggregieren die Hochrechnungswerte ihrer Kindzeilen.

**Umsetzung:** `ReportAggregationQuery.CompareProjection`, `ReportAggregationResult.ComparedProjection`, `ReportAggregatePointDto.ProjectionAmount`.

## Hochrechnung wird in Favoriten gespeichert

**Beschreibung:** Die Hochrechnungsoption ist Teil der benutzerspezifischen Favoritenkonfiguration.

**Bedingungen:**
- Ein Anwender speichert oder aktualisiert einen Report-Favoriten im Dashboard.

**Verhalten:**
- `CompareProjection` wird zusammen mit den übrigen Favoritenoptionen persistiert.
- Beim Laden eines Favoriten wird die Option wiederhergestellt.
- Wechselt die Auswahl im Dashboard auf eine nicht gültige Buchungsartenauswahl, wird die Option deaktiviert und nicht an die Aggregation übergeben.

**Umsetzung:** `ReportFavorite.CompareProjection` und die Favoriten-DTOs.

## Textfilter matchen Verwendungszweck und Buchungsbeschreibung

**Beschreibung:** Pro Buchungsart können beliebig viele freie Textfilter gesetzt werden, die Buchungen anhand ihres Textes auswählen.

**Bedingungen:**
- Ein Eintrag ist getrimmt nicht leer und maximal `TextFilterList.MaxEntryLength` = 500 Zeichen lang.
- Leere Einträge werden verworfen; Duplikate werden `OrdinalIgnoreCase` dedupliziert.

**Verhalten:**
- Ein Eintrag matcht, wenn er als Substring (`OrdinalIgnoreCase`) im kombinierten Text aus `Subject` (Verwendungszweck) und `Description` (Buchungsbeschreibung) vorkommt; `RecipientName` wird nicht durchsucht.
- Mehrere Einträge einer Buchungsart sind ODER-verknüpft — ein Treffer genügt.
- Textfilter gelten kumulativ zu den übrigen Filtern (Entitäten, Kategorien, `SecuritySubTypes`), auch im `IncludeCategory`-Modus.
- `null` oder eine leere Liste bedeutet „keine Filterung" für diese Buchungsart.
- Kein Regex-Modus — die Einträge sind reine Suchbegriffe.

**Umsetzung:** `BudgetRulePatternMatcher.MatchesPosting(subject, description, pattern, useRegex: false)`; Normalisierung/Validierung in `TextFilterList` (`Normalize`, `ToJson` wirft `ArgumentException` bei >500 Zeichen).

## Textgefilterte Buchungsarten werden aus Rohbuchungen verdichtet

**Beschreibung:** Da `PostingAggregates` keine Textfelder enthalten, werden Buchungsarten mit gesetztem Textfilter aus den Rohpostings aggregiert — ungefilterte Arten bleiben auf dem schnellen Aggregatpfad.

**Bedingungen:**
- `textFiltersByKind` enthält nur Kinds aus der aktuellen Buchungsartenauswahl mit nicht-leeren Filterlisten.

**Verhalten:**
- Buchungsarten mit Textfilter: `_db.Postings` wird auf die kind-eigene Fremdschlüssel-Ids der Eigentümer-Entitäten eingeschränkt (Server-seitige Besitzerfilterung, da `Posting` kein `OwnerUserId` trägt), auf das Effektivdatum-Fenster begrenzt (nur Obergrenze `postingWindowEnd`) und im Speicher gematcht.
- Die Verdichtung folgt denselben Regeln wie `PostingAggregateService`: `Amount == 0` wird übersprungen, Effektivdatum `BookingDate` bzw. `ValutaDate` mit `MinValue`-Fallback bei `UseValutaDate`, `PeriodStart` über `PostingAggregateService.GetPeriodStart`, keine Sonderbehandlung von Stornos oder vorläufigen Buchungen — ein „Alles-matcht"-Filter liefert daher exakt die Aggregat-Summen.
- Es existiert keine Untergrenze des Lade-Fensters: `Take` begrenzt die letzten N Perioden *mit Daten*, was aus einem Kalenderfenster nicht ableitbar ist.

**Umsetzung:** `ReportAggregationService.LoadTextFilteredRowsAsync`, `ReportAggregationService.ApplyDateWindow`.

## Sonderfälle der Textfilter-Aggregation

**Beschreibung:** Zwei Sonderpfade wenden Textfilter abweichend vom Standard an.

**Bedingungen und Verhalten:**
- **`SavingsPlanPostingSource.LinkedAccountPostings`:** Werden Sparplan-Reihen aus den Bank-Buchungen verknüpfter Konten abgeleitet, matcht `SavingsPlanTextFilters` gegen diese Quell-Bankbuchungen (die emittierten Zeilen tragen `Kind = SavingsPlan`); `BankTextFilters` greifen auf diesem Pfad nicht.
- **Netto-Dividendenpfad (`QuerySecurityDividendsNetAsync`):** `SecurityTextFilters` werden auf die Anker-Buchung (`SecuritySubType == Dividend`) je `GroupId` angewendet. Matcht die Anker-Buchung, bleibt die gesamte Dividendengruppe inklusive Fee-/Tax-Netting erhalten; sonst entfällt die Gruppe komplett.

**Umsetzung:** `ReportAggregationService.QueryAsync` (Linked-Account-Block), `ReportAggregationService.QuerySecurityDividendsNetAsync` (`dividendGroupIds`).

## Posteneinsicht listet exakt die Selektion der Betragszelle

**Beschreibung:** Der Symbolbutton an einer Betragszelle öffnet ein Overlay mit den Einzelposten, aus denen sich der Zellwert zusammensetzt.

**Bedingungen:**
- Der Button steht an den Spalten `Betrag` (immer) sowie `Vorperiode` und `Vorjahr` (nur bei vorhandenem Wert) — an Entitäts-, Kategorie-, Detail- und Summenzeilen.
- Delta-Spalten, die Hochrechnungsspalte und Zellen ohne Wert („-") besitzen keinen Button.

**Verhalten:**
- Die Postenmenge entspricht der Aggregationsselektion der Zelle: gleiche Entitäts-, Kategorie-, Untertyp- und Textfilter, gleiche Besitzereinschränkung sowie das der Spalte zugeordnete Fenster — `Vorperiode` und `Vorjahr` liefern die Posten der jeweiligen Vergleichsperiode.
- Die Summenzeile liefert die Posten der Spaltenperiode über alle angefragten Buchungsarten (`GroupKey = null`).
- Bei `AllHistory` ist nur die `Betrag`-Spalte abfragbar; Vergleichsspalten werden serverseitig mit 400 abgelehnt.
- Die Fußzeilensumme entspricht dem Zellwert; `TotalAmount` ist die exakte Serversumme aller Treffer.

**Umsetzung:** `ReportPostingsQueryService.QueryAsync` (Selektion identisch zu `ReportAggregationService`; gemeinsame Semantik in `ReportQuerySemantics`), `ReportDashboardViewModel.ShowPostingsAsync`, `ReportDashboard.razor`.

## Postenliste ist serverseitig auf 5000 Einträge begrenzt

**Beschreibung:** Große Zellen (Summenzeilen, Typ-Zeilen, `AllHistory`) werden gekürzt übertragen.

**Bedingungen:**
- `MaxPostings = 5000` ist serverseitig fixiert und wird nicht vom Client beeinflusst.

**Verhalten:**
- `TotalCount` und `TotalAmount` bleiben exakt; `Truncated` signalisiert die Kürzung.
- Das Overlay blendet bei `Truncated` den Hinweis `Postings_Truncated` ein; die Fußzeilensumme bleibt korrekt.

**Umsetzung:** `ReportPostingsQueryService` (`MaxPostings`, `PostingSelection`), `ReportPostingsResult.Truncated`, `ReportDashboard.razor`.

## KPI-Cache ist über die Favoriten-Version abgesichert

**Beschreibung:** KPI-Kacheln der Startseite cachen Favoriten-Auswertungen im LocalStorage; ohne Versionsprüfung würden sie nach einer Filteränderung am Favoriten veraltete Werte zeigen.

**Bedingungen:**
- `HomeKpiGrid` schreibt `FavKpiData` inklusive `FavoriteModifiedUtc` in den Cache-Schlüssel `home.kpi.favorite-{k.Id}`.

**Verhalten:**
- `RestoreFavoriteCacheAsync` übernimmt einen Cache-Eintrag nur, wenn `FavoriteModifiedUtc` mit `fav.ModifiedUtc` übereinstimmt; `SetFilters`, `Update` und `Rename` am Favoriten rufen `Touch()` auf und ändern den Stempel zuverlässig.
- Bei Abweichung oder fehlendem Favoriten wird der Eintrag verworfen (`RemoveAsync`) und frisch geladen.
- Alte Cache-Einträge ohne das Feld deserialisieren mit `default(DateTime)` und werden einmalig automatisch verworfen.
- Bei einem Ladefehler der Favoritenliste bleiben die Cache-Einträge erhalten (kein Verlust bei transienten Fehlern).

**Umsetzung:** `HomeKpiGrid.FavKpiData.FavoriteModifiedUtc`, `HomeKpiGrid.RestoreFavoriteCacheAsync`, `ReportFavorite.Touch`.
