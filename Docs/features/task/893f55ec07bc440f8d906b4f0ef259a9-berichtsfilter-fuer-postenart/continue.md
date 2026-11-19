# Offene Aufgaben

Erstellt am: 2026-11-19 (aktualisiert nach Fortsetzungs-Lauf)
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (7 offene Punkte ≥ 7 der Voriteration); die ursprünglichen 4 Befunde der ersten `continue.md` wurden vollständig abgearbeitet.

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt`.

## Code-Review-Befunde

- [ ] `ReportAggregationService.QueryAsync` (Z. 117–131, 182–196) — Grenzfall Ladegrenze: `postingWindowEnd` wird nur bei `textFiltersByKind.Count > 0` berechnet; rutscht `PostingKind.Bank` allein über `bankPostingTypes != null` in den Raw-Pfad (keine Textfilter), bleibt die obere Grenze `null` und `LoadTextFilteredRowsAsync` materialisiert die gesamte Posting-Historie. Ergebnis korrekt, Last unbegrenzt. Empfehlung: Bedingung um `|| bankPostingTypes != null` (oder `rawKinds.Count > 0`) erweitern.
- [ ] `ReportDashboardViewModel.SubmitFavoriteDialogAsync` (Z. 1469–1475) — `catch (Exception)` verwirft die Exception vollständig (kein Logging, kein `_api.LastError`) und schluckt `OperationCanceledException` trotz `ct`-Parameter. Empfehlung: Muster der Schwesterstelle (Z. 652–657, OCE rethrow) übernehmen.
- [ ] `ReportProjectionEventLoader.LoadEntityEventsAsync` (Z. 42–51) — 9 positionale Parameter; `bankPostingTypes` ist kind-spezifisch und wird von zwei Aufrufstellen mit `null` durchgereicht — inkonsistent zur Bündel-Refaktorierung (`TextFilteredRowFilters`, `PostingLoadFilters`). Empfehlung: Parameter analog bündeln.
- [ ] `ReportDashboardViewModel.ClearKindFilters` (Z. 373–401) — zwei parallel zu pflegende `PostingKind`→Collection-Switches (`hadFilters` + Clear); Divergenzrisiko. Empfehlung: zu einem Switch zusammenführen.

## Usability-Befunde

- [ ] Unsichtbar wirksame Entity-/Kategorie-Filter (Restproblem): `ClearKindFilters` verwirft nur Subtyp-/Textfilter — die Entity-/Kategorie-Auswahl des abgewählten Kinds (`SelectedContacts` etc.) bleibt unsichtbar aktiv, wird in `BuildFiltersPayload()` gesendet und in Favoriten persistiert. Pfad: `OnToggleKind` in `ReportDashboard.razor` (Z. ~1369–1398), Multi-Kind → Einzelkind.
- [ ] Wirkungslose Aktion mit stillem Filterverlust: Beim Abwählen des letzten/einzigen Kinds wird dieses sofort wieder hinzugefügt (Checkbox bleibt gesetzt), `ClearKindFilters` verwirft aber trotzdem dessen Filter und lädt neu — der Klick sieht wirkungslos aus, zerstört aber Filter.
- [ ] (geringfügig) `GetSelectedFiltersCount`/`GetSelectedTempFiltersCount` zählen `SelectedBankPostingTypes`/`SelectedSecuritySubTypes` kontextlos (nicht an `SelectedKinds` gebunden) — „Filter löschen" kann aktiviert sein, ohne sichtbaren Filter (nur über Favoriten-/API-Daten erreichbar).

## Fehlgeschlagene Tests

Keine — `test-results.md` trägt den Status `Keine Fehler` (Unit 2402/2402, Integration 176/176, E2E vollständig; ein feature-fremder Timing-Flake im Nachlauf grün; reale End-to-End-Abnahme bestanden).
