# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK-1: Infokachel zusätzlich zur bestehenden Tabelle | Kontenspezifische Einbindung in `ListPage.razor`, eigene Statistikkomponente sowie Desktop- und Mobile-Layout sind geplant. Andere Listentypen bleiben unverändert. | bUnit für kontenspezifisches Rendering sowie Desktop- und Mobile-E2E mit gleichzeitig sichtbarer Liste und Statistik. | Abgedeckt |
| AK-2: Summe aller Bankkontosalden | Gemeinsames Statistik-DTO und serverseitige Aggregation über die vollständige, ungepagte Kontenmenge sind geplant; die UI soll die bestehende Währungsformatierung verwenden. | Service-/Integrationstest, Leerfalltest und E2E mit bekannten Kontenwerten und sichtbarer Währungsformatierung. | Abgedeckt |
| AK-3: Veränderung im aktuellen Kalenderjahr | Differenz zum Periodenbeginn auf Basis von `BookingDate` ist festgelegt. Die konkrete Quelle für aktuelle Zeit und Anwendungszeitzone sowie ein deterministisch testbarer Zeitgeber fehlen jedoch. | Ein Grenztest ist genannt, deckt die in der Bestandsaufnahme geforderte Zeitzonenbehandlung und einen fest gesetzten Jahreswechsel aber nicht konkret ab. | Lücke |
| AK-4: Veränderung im aktuellen Kalendermonat | Differenz zum Monatsbeginn auf Basis von `BookingDate` ist festgelegt. Die konkrete Quelle für aktuelle Zeit und Anwendungszeitzone sowie ein deterministisch testbarer Zeitgeber fehlen jedoch. | Ein Grenztest ist genannt, deckt die in der Bestandsaufnahme geforderte Zeitzonenbehandlung und einen fest gesetzten Monatswechsel aber nicht konkret ab. | Lücke |
| AK-5: Tortendiagramm nach Kontoart | Gruppierung und lokalisierte Kontoartbezeichnungen sind geplant. Negative Salden werden jedoch auf `0,0 %` reduziert und damit nicht als tatsächlicher Saldo in der Verteilung dargestellt oder verständlich erläutert. | Happy-Path-, Negativ-/Nullsaldo-, bUnit- und E2E-Tests sind genannt; ein sichtbarer Nachweis für eine fachlich verständliche Darstellung gemischter positiver und negativer Salden fehlt. | Lücke |
| AK-6: Tortendiagramm nach Bankkontakt | Gruppierung, Kontaktauflösung und Fallback-Label sind geplant. Negative Salden werden jedoch auf `0,0 %` reduziert und damit nicht als tatsächlicher Saldo in der Verteilung dargestellt oder verständlich erläutert. | Happy-Path-, Negativ-/Nullsaldo-, bUnit- und E2E-Tests sind genannt; Fallback-Kontakt, lange Labels und die sichtbare Darstellung gemischter Vorzeichen sind nicht konkret abgedeckt. | Lücke |
| AK-7: Werte stimmen mit den in der Tabelle berücksichtigten Konten und Salden überein | Gemeinsame Owner-/Suchfilterlogik, ungepagte Statistik und serverseitiges `q` sind geplant. Die bisherige Suche nach Name oder IBAN wird im neuen Suchvertrag nicht konkret festgeschrieben. | Integration für Owner, Suche und mehr als 50 Konten sowie E2E für Suche und Infinite Scroll sind geplant. Es fehlen ein E2E-Nachweis der Benutzerisolation, der Such-Reset und eine Abstimmung der vorzeichenbehafteten Gruppensummen mit Tabellen- und Gesamtsaldo. | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] Für AK-3 und AK-4 einen deterministischen Unit-/Integrationstest mit festem Zeitgeber und expliziter Anwendungszeitzone planen, der Buchungen exakt vor, auf und nach Monats- und Jahresbeginn prüft.
- [ ] Für AK-5 und AK-6 einen Komponenten- oder E2E-Test mit gemischten positiven, negativen und null Salden planen, der die sichtbaren Beträge, die erklärte Prozentbasis und die Übereinstimmung mit dem Gesamtsaldo nachweist.
- [ ] Für AK-6 einen konkreten Test für einen nicht auflösbaren Bankkontakt sowie einen responsiven Test mit sehr langem Bankkontaktnamen planen.
- [ ] Für AK-7 die bestehende Suche nach Name und IBAN einschließlich Groß-/Kleinschreibung und leerem beziehungsweise zurückgesetztem Suchbegriff auf Service-/API-Ebene absichern.
- [ ] Für AK-7 einen E2E-Test `AccountsOverview_ClearSearch_RestoresTableAndStatistics` mit Suche, Ribbon-Aktion `ClearSearch` und sichtbar wiederhergestellten Tabellen- und Statistikwerten planen.
- [ ] Für AK-7 einen E2E-Sichtbarkeitstest mit Konten zweier Benutzer planen, der in Tabelle, KPI-Werten und Diagrammlegenden ausschließlich die Daten des angemeldeten Benutzers nachweist.
- [ ] Für den neuen Statistikendpunkt einen Autorisierungstest für einen nicht angemeldeten Aufruf sowie die bereits geplante Benutzerisolation konkret als Controller-/Integrationstest benennen.
- [ ] Für die Statistikkomponente konkrete bUnit-Tests für Lade-, Leer- und Fehlerzustand planen; im Fehlerfall muss die bestehende Kontenliste weiterhin sichtbar und bedienbar bleiben.
- [ ] Einen E2E-Test mit fehlgeschlagenem Statistikaufruf planen, der den sichtbaren Fehlerzustand und die weiterhin nutzbare Tabelle prüft.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kontenübersicht öffnen und Statistik neben der Tabelle sehen (AK-1) | `AccountsOverview_ShowsStatisticsAlongsideTable` | Abgedeckt |
| Kontenübersicht auf mobilem Viewport verwenden (AK-1, nicht-funktionale Anforderung) | `AccountsOverview_Mobile_RendersStatisticsWithoutHidingList` | Abgedeckt |
| Gesamtsaldo sowie Jahres- und Monatsveränderung ablesen (AK-2 bis AK-4) | `AccountsOverview_ShowsTotalYearChangeAndMonthChange` | Abgedeckt |
| Verteilungen nach Kontoart und Bankkontakt ablesen (AK-5, AK-6) | `AccountsOverview_ShowsAccountTypeAndBankContactDonuts` | Abgedeckt |
| Gemischte positive, negative und null Salden in beiden Diagrammen verständlich auswerten (AK-5, AK-6) | Kein konkreter E2E-Test vorgesehen. | Lücke |
| Suche anwenden und Tabelle sowie Statistik gemeinsam aktualisieren (AK-7) | `AccountsOverview_Search_UpdatesTableAndStatisticsTogether` | Abgedeckt |
| Suche über `ClearSearch` zurücksetzen und beide Darstellungen wiederherstellen (AK-7) | Kein konkreter E2E-Test vorgesehen. | Lücke |
| Weitere Tabellenzeilen nachladen, ohne den Statistikwert zu verändern (AK-7) | `AccountsOverview_InfiniteScroll_DoesNotChangeStatisticsTotal` | Abgedeckt |
| Daten eines anderen Benutzers bleiben in Tabelle und Statistik verborgen (AK-7) | Nur ein Service-/Integrationstest ist vorgesehen; ein E2E-Sichtbarkeitstest fehlt. | Lücke |
| Leere Kontenübersicht mit Nullwerten und leeren Diagrammen öffnen | Kein konkreter E2E-Test vorgesehen. | Lücke |
| Fehlgeschlagene Statistikabfrage bei weiterhin nutzbarer Kontenliste beobachten | Kein konkreter E2E-Test vorgesehen. | Lücke |
| Bestehende Navigation über Kontenzeile oder mobile Karte verwenden | `AccountsOverview_ClickingAccountRow_StillNavigatesToCard` | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Für negative Salden eine fachlich verlustfreie und eindeutig beschriftete Diagrammsemantik festlegen. `0,0 %` stellt einen negativen Kontosaldo nicht dar und kann bei gemischten Vorzeichen eine Verteilung suggerieren, die nicht mit Gesamt- und Tabellensaldo übereinstimmt.
- [ ] Die konkrete Quelle für aktuelle Zeit und Anwendungszeitzone sowie einen injizierbaren Zeitgeber festlegen, damit Monats-/Jahresgrenzen reproduzierbar implementiert und getestet werden können.
- [ ] Den gemeinsamen `q`-Suchvertrag vollständig definieren: mindestens Name und IBAN wie im Bestand, einschließlich Normalisierung, Groß-/Kleinschreibung und Verhalten bei leerem Suchtext.
- [ ] Lade-, Leer- und Fehlerverhalten der Statistikkomponente konkret beschreiben, einschließlich sichtbarer Fehlermeldung und der Zusicherung, dass ein Statistikfehler Suche, Paging, Zeilenklick und Ribbon der Tabelle nicht blockiert.

## Hinweise

Der Happy Path und die grundsätzliche Testpyramide sind bereits detailliert geplant. Nach Ergänzung der genannten Randfälle und Festlegung der negativen Salden- sowie Zeitsemantik kann der Plan erneut geprüft werden.
