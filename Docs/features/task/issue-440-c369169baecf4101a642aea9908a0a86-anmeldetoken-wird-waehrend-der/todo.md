# Aufgabenliste – Anforderungsbearbeitung

Branch: task/issue-440-c369169baecf4101a642aea9908a0a86-anmeldetoken-wird-waehrend-der

| Status | Schritt | Beschreibung |
|---|---|---|
| [x] | 1 | Branch-Name ermitteln |
| [x] | 2 | Verzeichnisstruktur vorbereiten |
| [x] | E | Einstiegspunkt ermitteln |
| [x] | 3 | Anforderung übersetzen |
| [x] | 4 | Bestandsaufnahme |
| [x] | 5 | Umsetzungsplanung |
| [x] | 5a | Offene Punkte prüfen |
| [x] | 5b | Plan prüfen |
| [x] | 5c | Planungscommit |
| [x] | 6 | Implementierung |
| [x] | 7 | Plan-Review |
| [x] | 8 | Usability-Review |
| [x] | 9 | Code-Review |
| [x] | 10 | Tests ausführen |
| [x] | I | Iteration oder Abschluss entscheiden |
| [x] | 11 | Folgeaufgaben dokumentieren (falls erforderlich) |
| [x] | 12 | Dokumentation erstellen |
| [x] | 12b | README aktualisieren |
| [x] | 12c | Release Notes aktualisieren |
| [ ] | D | Feature-Verzeichnis löschen |
| [ ] | C | Commit durchführen |







## Unterbrechung am 2026-10-23

Schritt 6 gemäß Implementierungsworkflow abgebrochen: Planwiderspruch beim Hintergrund-Polling. Nutzerentscheidung zur Planergänzung ausstehend. Siehe implementation-blocker.md, reproduction.md und continue.md. Keine Produktivänderung, keine nachfolgenden Reviews. Testläufe beendet.


## Fortsetzung nach Nutzerentscheidung

Hintergrund-Polling wird in den Plan aufgenommen. Nutzerkorrektur: Produktiver Login, Notifications und Navigation funktionieren vor Tokenablauf. Sofortige 401 im neuen Testhost sind kein anerkannter Original-Rotnachweis. Einstieg: Testtreue gezielt prüfen, dann Plan korrigieren und erneut prüfen; vor Produktivänderungen den Ablauf bei aktiver Nutzung über ursprünglichen Tokenablauf real reproduzieren.











## Iteration 1 abgeschlossen

Plan vollständig umgesetzt, Usability ohne Befunde. Code-Review: zwei P2-Befunde. Tests: ein fehlgeschlagener bestehender E2E-Fall; 111 übrige E2E grün, Pflichtfälle sowie Unit/Integration grün. Offene Punkte gesamt: 3. Fortschritt gegenüber Start: ja. Nächster Schritt: Iteration 2, gezielte Korrekturen mit Testnachweis.


## Iteration 2

Gezielte Korrekturen abgeschlossen. Plan-Review bleibt vollständig umgesetzt. Neue Usability-/Code-Reviews und abschließender Regressionstest folgen. Siehe implement-iteration2.md.


## Iteration 2 abgeschlossen

Plan, Usability und Code-Review grün. Unit1472/1472 und Integration139/139 grün. Aktuelle übrigeE2E115:114grün,1Fehler Budgetzeile Gehalt Ist0statt3642.50. FrühereReviewbefundeundRegistrierungsTimeoutbehoben. OffenePunktegesamt:1 (vorher3), Fortschritterkannt. NächsterSchritt:Iteration3 gezielteDatenfehlerdiagnose.


## Iteration 3

VerbleibenderBudgetfehlerdurchverzögerteKlickzustellungrotnachgewiesen. Korrektur ausschließlich im E2E-Helper: Zielzeitraum vor Ladeende abwarten. Gezielt2/2grün,keineProduktivänderung. AbschließendeReviewsundRegressionfolgen.



## Iterationsabschluss

Iteration3: Plan vollständig umgesetzt, Usability/Code ohne Befunde, Tests ohne Fehler. OffeneReview-/Testpunkte0 (vorher1). Schleife erfolgreich beendet. Schritt11 nicht erforderlich; vorhandene Fortsetzungsaufgaben werden nach Dokumentation abgeschlossen.





## Abschlussprüfung

Dokumentation, README und ReleaseNotes vollständig. Strikter Solution-Build nach gezielter CancellationToken-Korrektur ohne Fehler. PassivePolling2/2, HelpAssetIntegrität9/9, Hilfekatalog10/10 grün. git diff --check ohne Befunde. Finale Nachweise werden vor dem Lifecycle-Aufräumen in der Git-Historie gesichert.

