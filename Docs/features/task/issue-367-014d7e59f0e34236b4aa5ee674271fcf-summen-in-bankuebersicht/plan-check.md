# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK-1: Infokachel zusätzlich zur bestehenden Tabelle | Kontenspezifische Einbindung von `AccountsStatisticsTile.razor` in `ListPage.razor`, getrennte Listen- und Statistikzustände sowie responsive Desktop- und Mobile-Komposition sind konkret geplant. Andere Listentypen bleiben unverändert. | bUnit für die kontenspezifische Einbindung; E2E für gleichzeitige Sichtbarkeit von Statistik und Tabelle, Mobile-Darstellung, Leer- und Fehlerzustand sowie bestehende Zeilennavigation. | Abgedeckt |
| AK-2: Summe aller Bankkontosalden | `TotalBalance` wird serverseitig als Summe von `Account.CurrentBalance` über die vollständige, ungepagte Owner- und Suchfiltermenge berechnet und mit der bestehenden Währungsformatierung angezeigt. | Service- und Integrationstests für Gesamtsumme, mehr als 50 Konten, gemischte Salden und Leerfall; E2E mit bekannten Beträgen und Währungsformatierung. | Abgedeckt |
| AK-3: Veränderung im aktuellen Kalenderjahr | Period-to-date-Semantik, `BookingDate`, inklusive Jahresgrenze, exklusives Tagesende, Benutzerzeitzone, UTC-Fallback und injizierbarer Zeitgeber sind verbindlich festgelegt. | Deterministische Unit- und Integrationstests mit festem Zeitgeber, Benutzerzeitzone und Buchungen vor, auf und nach der Jahresgrenze; E2E prüft den sichtbaren Jahresbetrag. | Abgedeckt |
| AK-4: Veränderung im aktuellen Kalendermonat | Period-to-date-Semantik, `BookingDate`, inklusive Monatsgrenze, exklusives Tagesende, Benutzerzeitzone, UTC-Fallback und injizierbarer Zeitgeber sind verbindlich festgelegt. | Deterministische Unit- und Integrationstests mit festem Zeitgeber, Benutzerzeitzone und Buchungen vor, auf und nach der Monatsgrenze; E2E prüft den sichtbaren Monatsbetrag. | Abgedeckt |
| AK-5: Tortendiagramm nach Kontoart | Die Gruppierung nach technischem Kontoartschlüssel, UI-Lokalisierung und verlustfreie Bruttovolumen-Semantik für positive, negative und Nullsalden sind vollständig beschrieben. | Service- und Integrationstests für Gruppeninvarianten; bUnit und E2E prüfen Titel, Gruppen, signierte Beträge, Prozentbasis, Nullsalden und ARIA-Texte. | Abgedeckt |
| AK-6: Tortendiagramm nach Bankkontakt | Kontaktauflösung, stabiler Fallback für unbekannte Kontakte, lange Bezeichnungen und dieselbe verlustfreie Diagrammsemantik wie bei Kontoarten sind geplant. | Service- und Integrationstests für Gruppierung und Fallback; bUnit und E2E prüfen Bankkontaktgruppen, unbekannte und lange Namen sowie Desktop- und Mobile-Lesbarkeit. | Abgedeckt |
| AK-7: Übereinstimmung mit den in der Tabelle berücksichtigten Konten | Liste und Statistik verwenden dieselbe zentrale Owner- und `q`-Filterkomposition; nur die Tabelle wird paginiert. Suchnormalisierung, `ClearSearch`, Request-Generation und unveränderte Statistik beim Infinite Scroll sind festgelegt. | Unit-, Controller- und Integrationstests prüfen Owner-Scope, Suchvertrag und mehr als 50 Konten; E2E deckt Suche nach Name und IBAN, `ClearSearch`, Infinite Scroll und die Unsichtbarkeit fremder Konten in Tabelle, KPIs und Legenden ab. | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kontenübersicht öffnen und Statistik zusammen mit der bestehenden Tabelle sehen (AK-1) | `AccountsOverview_ShowsStatisticsAlongsideTable` | Abgedeckt |
| Kontenübersicht auf einem mobilen Viewport ohne verdeckte oder horizontal überlaufende Inhalte verwenden (AK-1, nicht-funktionale Anforderungen) | `AccountsOverview_Mobile_RendersStatisticsWithoutHidingList` | Abgedeckt |
| Gesamtsaldo sowie Jahres- und Monatsveränderung mit korrekter Beschriftung und Währungsformatierung ablesen (AK-2 bis AK-4) | `AccountsOverview_ShowsTotalYearChangeAndMonthChange` | Abgedeckt |
| Verteilungen nach Kontoart und Bankkontakt ablesen (AK-5, AK-6) | `AccountsOverview_ShowsAccountTypeAndBankContactDonuts` | Abgedeckt |
| Gemischte positive, negative und null Salden in beiden Diagrammen verlustfrei auswerten (AK-5, AK-6) | `AccountsOverview_MixedBalances_ShowsLosslessSignedDistribution` | Abgedeckt |
| Unbekannte und lange Bankkontaktbezeichnungen auf Desktop und Mobile lesen (AK-6, nicht-funktionale Anforderungen) | `AccountsOverview_UnknownAndLongBankContact_RemainsReadable` | Abgedeckt |
| Suche nach Name und formatierter IBAN anwenden und Tabelle sowie Statistik gemeinsam aktualisieren (AK-7) | `AccountsOverview_Search_UpdatesTableAndStatisticsTogether` | Abgedeckt |
| Suche über die Ribbon-Aktion `ClearSearch` zurücksetzen und beide Darstellungen wiederherstellen (AK-7) | `AccountsOverview_ClearSearch_RestoresTableAndStatistics` | Abgedeckt |
| Weitere Tabellenzeilen nachladen, ohne die Statistik zu verändern (AK-7) | `AccountsOverview_InfiniteScroll_DoesNotChangeStatisticsTotal` | Abgedeckt |
| Daten anderer Benutzer in Tabelle, KPIs und Diagrammlegenden verborgen halten (AK-7) | `AccountsOverview_OtherUsersAccountsRemainInvisibleEverywhere` | Abgedeckt |
| Leere Kontenübersicht mit Null-KPIs, leeren Diagrammen und nutzbarem Ribbon anzeigen | `AccountsOverview_Empty_ShowsZeroKpisEmptyChartsAndUsableRibbon` | Abgedeckt |
| Sichtbaren Statistikfehler behandeln, während Tabelle, Suche und Navigation nutzbar bleiben, und anschließend erneut laden | `AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable` | Abgedeckt |
| Bestehende Navigation von der Kontenzeile zur Detailansicht weiterverwenden | `AccountsOverview_ClickingAccountRow_StillNavigatesToCard` | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

Der Plan schließt die in der vorherigen Gegenprüfung dokumentierten Lücken. Insbesondere sind Zeitquelle und Zeitzone, Suchvertrag, negative und null Salden, Lade-/Leer-/Fehlerzustände, Benutzerisolation sowie die erforderlichen E2E-Szenarien jetzt verbindlich festgelegt.
