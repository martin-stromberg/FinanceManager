# Implementierung – Iteration 2

Alle drei offenen Punkte aus `review-code.md` und `test-results.md` sind bearbeitet. Keine Commits, Reviews oder neuen Dokumentationspakete erstellt. Alle gestarteten Testprozesse sind beendet.

## 1. Aktueller Browsercookie synchronisiert einen älteren Circuit

Reproduzierender Integrationstest zunächst rot: erfolgreicher Keepalive mit frischem gültigem Cookie lieferte keinen `X-Auth-Token`. Der Test beginnt mit einem tatsächlich abgelaufenen Circuit-Cache und einem aktuell gültigen, noch nicht rotationsbedürftigen Token desselben Benutzers. [Rotnachweis](verification/iteration2/multitab-red.trx).

Minimaler Produktivfix ausschließlich in `JwtRefreshMiddleware.cs`: erfolgreicher `GET /api/auth/keepalive` meldet den tatsächlich validierten aktuellen JWT auch außerhalb des Erneuerungsfensters über die bestehenden Header zurück. Es findet dabei keine unnötige Rotation statt; der neue Test verlangt denselben Token und kein `Set-Cookie`. Antworten mit einem Synchronisationstoken sind `Cache-Control: no-store`. Die bestehenden Signatur-/Issuer-/Audience-/Ablauf- und Security-Stamp-Prüfungen bleiben erhalten; ein zusätzlicher Fall prüft ausdrücklich, dass ein widerrufener frischer Cookie mit 401 keinen Tokenheader erhält.

Der Regressionstest übernimmt den aktuellen Header in den älteren scoped Provider und führt danach über dessen echten `AuthenticatedHttpClientHandler` einen geschützten API-Aufruf erfolgreich aus, ohne `AuthenticationRequired`. Das ist ein gezielter Integrationsnachweis des Mehrtab-Vertrags, kein neuer künstlich verkürzter Browser-Ablauftest und keine neue Architektur.

## 2. Ausführbarer CI-Standard

`SessionExpiryWebAppFixture` verwendet ohne Override jetzt zehn Minuten. `E2E_SESSION_LIFETIME_MINUTES=30` bleibt für die explizite produktionsgetreue Referenz auswählbar. Unter zehn Minuten bleiben verboten. Der reguläre vierfach serielle Lauf benötigt damit rund 40 statt über 120 Minuten und passt zusammen mit dem übrigen Testbestand in das vorhandene 60-Minuten-Joblimit. Der Produktivstandard der Anwendung wurde nicht geändert.

Konfigurationstest zuerst rot (30 statt 10); danach alle drei Fälle grün: Default 10, explizit 30, zwei Minuten abgelehnt. [Rot](verification/iteration2/config-red.trx), [Grün](verification/iteration2/config-green.trx).

## 3. Budget-Demodaten-Regression

Der unveränderte Budgetfall bestand gezielt allein auf frischem normalen Host vollständig: 1/1, Testdauer 44,7s. [Report](verification/iteration2/budget-alone.trx). Im fehlgeschlagenen vorherigen Gesamtlauf hatten andere Playwright-Klassen bereits Benutzer im gemeinsam verwendeten Host angelegt. `Register.razor` zeigt `#create-demo-data` produktiv korrekt ausschließlich bei Erstbenutzerregistrierung an. Die vorhandene Testvoraussetzung wurde somit durch die geteilte Testdatenbank verletzt. Dies wird nicht als nachgewiesener Baselinefehler ausgegeben; die Baseline erreichte diese UI-Aktionen nicht.

Nur dieser Erstbenutzerfall besitzt jetzt eine eigene, nicht parallel laufende `BudgetDemoDataCollection` mit frischer normaler `PlaywrightWebAppFixture`. Vor dem UI-Fluss wird die leere Benutzertabelle ausdrücklich geprüft. Keine Änderung an produktivem Registrierungs-/Authentifizierungs-/Budgetcode und keine entfernte oder gelockerte UI-/Datenassertion.

Gezielter gemeinsamer Nachweis: zuerst normaler Registrieren-/Login-/Logout-Test auf dem gemeinsamen Host, danach vollständiger Budget-Demodatenfall auf seinem isolierten Host. 2/2 bestanden, 61,8s. Der Budgetfall umfasst weiterhin Formularregistrierung mit Checkbox, echten Formularlogin, Hintergrundaufgabe, Budget-/Istwerte und alle Buchungsdialogprüfungen. [Report](verification/iteration2/budget-isolated-green.trx), [Reihenfolge/Log](verification/iteration2/budget-isolated-green.log).

## Nachweise am Stand dieser Iteration

Vollständiger Solution-Build vor Testläufen: 0 Fehler. Testausführung über den vorhandenen xUnit-v3-Assembly-Runner; echte Edge-Browserläufe erhöht wegen Zertifikatzugriff. Filter und Ergebnisse sind in den verlinkten Logs/TRX erhalten.

| Lauf | Ergebnis | Nachweis |
|---|---|---|
| Auth-Integration einschließlich aller 15 bisherigen Auth-Fälle und zwei neuen Synchronisations-/Stampfällen | 17/17, 3,5s | [TRX](verification/iteration2/auth-green.trx) |
| Regressionskonfiguration | 3/3, 0,1s | [TRX](verification/iteration2/config-green.trx) |
| Isolierter Budgetfall plus vorheriger normaler Registrierungsfall | 2/2, 61,8s | [TRX](verification/iteration2/budget-isolated-green.trx) |
| Echter Formularlogin, erste Home-API, interaktive Navigation und beide kurzen Quick-Edit-Speichern-/Wiederöffnen-Fälle | 3/3, 15,6s | [TRX](verification/iteration2/keepalive-ui-green.trx) |

Der letzte Browserlauf prüft den jetzt auch ohne Rotation vorhandenen Header im tatsächlichen JS-/Circuit-/API-Fluss. Beide Quick-Edit-Smokes bestätigen API-Erfolg, echte gespeicherte DB-Werte und Wiederöffnen über `input.value`.

Die bereits grünen sieben Pflicht-Ablaufnachweise, Unit 1.472/1.472 und gesamter Integrationslauf 137/137 stammen aus Iteration 1. Sie wurden nicht nachträglich als neu ausgeführte Resultate dieser Iteration ausgegeben. Die Änderung ergänzt die Rückmeldung unverändert gültiger Tokens; Rotation, Ablaufgrenzen, Drosselung, Fokus und Input-Verarbeitung sind unverändert. Deshalb wurden die erfolgreichen Zehn-Minuten-Läufe nicht ohne neue begründete Sorge erneut ausgeführt. Der zusätzliche Mehrtab-Pfad ist durch den gezielten Integrationsfall und die echten kurzen Browserflüsse geprüft. Ein neuer vollständiger Integrationslauf hätte wegen der zwei zusätzlichen Tests nun 139 Fälle; ein solcher Gesamtlauf wurde hier nicht behauptet.

## Rest

Keine offenen Implementierungspunkte aus den drei übergebenen Befunden. Erneute unabhängige Codeprüfung und Lifecycle-Testbewertung liegen beim Koordinator. Die vorherigen roten/fehlgeschlagenen Reports bleiben unverändert; keine Abdeckung wurde übersprungen oder als Baselinefehler umgedeutet.
