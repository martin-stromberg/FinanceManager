# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die fachlichen Ziele sind benannt. Die konkrete Scope-Übergabe und wesentliche Voraussetzungen der E2E-Nachweise sind jedoch nicht ausführbar festgelegt. Insbesondere ersetzt ein erwarteter sofortiger Loginfehler noch keine belastbare Erst-Reproduktion des gemeldeten Ablaufs bei aktiver Nutzung.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Echter Browserlogin über das sichtbare Loginformular; kein API-Login oder künstliches Cookie als Nachweis | Programmablauf Login und Schritt 2 verwenden `LoginThroughUiAsync`. | Expliziter Formularlogin in den neuen E2E-Szenarien. | Abgedeckt |
| Zuerst das reale Problem reproduzieren, vor Produktivänderungen | Schritt 2 verlangt einen roten ersten Home-Aufruf; Schritt 3 setzt dessen Fehlschlag voraus. | Kein Vorgehen, falls dieser Aufruf auf dem Ausgangsstand funktioniert; der ursprüngliche Ablauf bei längerer aktiver Nutzung wird erst nach Produktivänderungen getestet. | Lücke |
| Erste geschützte Home-API-Anfrage nach Login erfolgreich, kein Redirect | Schritte 2–4 nennen `Notifications_ListAsync`. | Beobachtung des serverseitigen Circuit-Aufrufs wird verlangt, aber kein dazu geeigneter Testmechanismus geplant. | Lücke |
| Navigation und ausgelöste API-Aufrufe funktionieren vor und nach ursprünglichem Tokenablauf | Programmablauf und Schritt 5 beschreiben das Ziel. | Konkrete UI-Navigationen, API-Beobachtung und belastbare Ablaufgrenze fehlen (T1–T3). | Lücke |
| Neue Quick-Edit-Zeilen und Bearbeitung vorhandener Zeilen erhalten Sitzung, Fokus und ungespeicherte Werte | Schritt 6 nennt Eingabe/Blur und Werterhalt. | Neue Zeilen, Eingabe ohne Blur und Daten-/Zeitsetup sind nicht konkret geplant (T4). | Lücke |
| Keine Keepalives bei Inaktivität; erst spätere geschützte Aktion führt nach Ablauf zum Login | Schritt 7 und Programmablauf nennen beide Erwartungen sowie ausbleibenden Redirect bei fehlgeschlagenem Ping. | Messbeginn, tatsächliche Ablaufgrenze und konkrete geschützte UI-Aktion fehlen (T2/T5). | Lücke |
| Keine Login-/Anwendungsinhalte zur Abwehr eines Initialisierungsrennens ausblenden | Als Nicht-Anforderung und Risiko berücksichtigt. | E2E-Tabellenzeile ohne konkrete Beobachtung während Initialisierung und ohne Ausführungsschritt (T6/P3). | Lücke |
| Refresh verwendet validierten eingegangenen JWT-Principal einschließlich `security_stamp` | Programmablauf, Validierungsregeln und Schritt 3 legen dies fest. | Schritt 4 und neue Auth-Tests nennen Erfolg, fehlenden/ungültigen Stamp sowie Ablehnung. | Abgedeckt |
| Token ist im tatsächlichen API-Aufruf-Scope verfügbar, unabhängig von anderem Scoped-Cache oder zufälligem `HttpContext` | Schritt 3 wiederholt die notwendige Eigenschaft; Übergabemechanismus und Lebensdauern bleiben offen. | Scope-Test ist vorgesehen, besitzt aber noch keinen überprüfbaren Übergabevertrag (P1). | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] **T1 – Erste API-Anfrage und Navigation:** Einen konkreten Beobachtungspunkt für die **serverseitigen** API-Aufrufe aus dem Blazor-Circuit einschließlich Zuordnung zum Testnutzer, Reihenfolge und HTTP-Status planen; Aufzeichnung vor dem Formular-Submit aktivieren. Reine Playwright-Browser-Request-Beobachtung erfasst diese Server-zu-Server-Aufrufe nicht. Navigation über konkrete UI-Aktionen und repräsentative Ziele (beispielsweise Home und Kontenliste samt `GetAccountStatisticsAsync`) festlegen; ein separater Browser-API-Aufruf ersetzt diesen Nachweis nicht.
- [ ] **T2 – Zeit und Fixtures:** Testlaufzeit, nötige Fixture-Konfiguration und Wartehilfen festlegen. Den ursprünglich durch echten Login ausgestellten Token und dessen `exp` nur auslesen, keine Ersatz-Cookies setzen. Positive Ablaufnachweise müssen über `exp` **plus Validierungstoleranz** hinausgehen und einen tatsächlich erneuerten Token mit späterem Ablauf sowie erfolgreiche Circuit-API-Aufrufe zeigen. Für Inaktivität ab dem letzten tatsächlich ausgestellten Token rechnen. Die bestehende Laufzeit beträgt 30 Minuten, `ClockSkew` zehn Sekunden; normale/erzwungene JS-Pings sind auf 60/5 Sekunden gedrosselt. Eine verkürzte E2E-Konfiguration benötigt passende Aktivitätsintervalle, Timeouts und Isolation gegenüber der gemeinsamen Fixture. Ein bloßes Vorspulen der Browseruhr verändert die serverseitige JWT-Prüfung nicht.
- [ ] **T3 – Echter initialer Rotnachweis:** Vor Produktivänderungen den berichteten Ausfall mit UI-Login und echter Nutzung reproduzieren und als Assertion-Fehler dokumentieren. Festlegen, wie bei bereits erfolgreicher erster Home-Anfrage mit dem Ablauf-/Aktivitätsszenario weiter reproduziert wird. Setup-/Beobachtungsfehler sind kein fachlicher Rotnachweis.
- [ ] **T4 – Quick Edit:** Testdaten und Setup für einen editierbaren Kontoauszug, bestehende Zeile und neu eingegebene Zeile benennen; API-Anmeldung in vorhandenen Setup-Hilfen für diese Szenarien ausschließen. Während der relevanten Erneuerung echte Tastatureingaben bei unverändert fokussiertem Feld **ohne Blur/Navigation/manuellen Ping** ausführen. Für beide Zeilentypen Feldidentität, ungespeicherte Werte über mehrere Felder und weitere Eingabe jenseits der ursprünglichen Ablaufgrenze prüfen. Ein reiner Blur-Test belegt die geforderte laufende Eingabe nicht.
- [ ] **T5 – Inaktivität:** Nach Abschluss der letzten echten Aktion und bereits gestarteter Requests einen klaren Beobachtungsbeginn festlegen; bis zum tatsächlichen Ablauf null Keepalive-Requests und keine automatische Login-Navigation prüfen. Danach eine konkrete UI-Aktion mit geschütztem API-Aufruf, erwarteten Auth-Fehler und sichtbaren Redirect samt Rücksprungroute benennen. „API-Aufruf von der Home-Seite“ legt noch keinen ausführbaren Benutzerfluss fest. Den fehlgeschlagenen Keepalive separat bis vor der Folgeaktion auf ausbleibenden Redirect prüfen.
- [ ] **T6 – Sichtbarkeit:** Sichtbare Loginfelder und die regulären Inhalte der Zielseite während ihres jeweiligen Initialisierungsablaufs konkret benennen und prüfen; bloße Sichtbarkeit nach Abschluss des Logins würde vorübergehendes Ausblenden nicht erkennen.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Reales Loginformular | `AuthenticationFlowPlaywrightTests`, `LoginThroughUiAsync` | Abgedeckt |
| Erste Home-API direkt nach Login | Neuer Home-API-Test; serverseitige Beobachtung fehlt (T1). | Lücke |
| Initiale Reproduktion des Aktivitäts-/Ablauffehlers | Roter Home-Aufruf vorausgesetzt; Originalablauf erst nach Fix (T3). | Lücke |
| Aktive Navigation und tatsächliche API-Nutzung über Ablauf | Neuer Navigationstest; konkrete Aktionen und Zeitfixture fehlen (T1/T2). | Lücke |
| Neue und vorhandene Quick-Edit-Zeilen während laufender Eingabe | Neuer Quick-Edit-Test; Zeilen-/Eingabevarianten und Setup fehlen (T2/T4). | Lücke |
| Inaktivität, erfolgloser Ping ohne Redirect, spätere geschützte Aktion | Neuer Inaktivitätstest plus bestehender Fehlerfall; Messgrenzen und Aktion fehlen (T2/T5). | Lücke |
| Regulär sichtbare Inhalte während Initialisierung | Nur Szenariotitel, keine konkreten Beobachtungen (T6). | Lücke |

## Fehlende oder unvollständige Planbestandteile

- [ ] **P1 – Scope-Übergabe entscheiden:** Quelle und Übernahme des Tokens beim Aufbau des Circuits, Übergabe an den tatsächlich verwendeten API-Handler, betroffene DI-Registrierungen/Lebensdauern sowie Rückführung einer Erneuerung in Circuit und Browser konkret festlegen. Dabei die Trennung verschiedener Nutzer und konkurrierende Erneuerungen berücksichtigen. „Im tatsächlichen Scope bereitstellen“ ist das Ziel, noch kein umsetzbarer Mechanismus; ein Refresh im serverseitigen API-Aufruf aktualisiert das Browser-Cookie nicht automatisch. Daraus einen konkreten Scope-Integrationstest ableiten.
- [ ] **P2 – Abhängigkeiten und Implementierungsschritte:** Testhilfen/Fixture-Anpassungen aus T1/T2/T4 vor dem ersten Reproduktionslauf einordnen und den Rotnachweis gemäß T3 vor Schritt 3 sicherstellen. Für Navigation, Quick-Edit-Eingabe und Inaktivität nach dem jeweiligen Test auch die gegebenenfalls nötigen Änderungen an `MainLayout`, `financeManager.js` und `QuickEditTable` als ausführbare Umsetzungsschritte aufnehmen; Schritte 5–7 nennen bisher nur Prüfen/Absichern, obwohl die Klassenabschnitte mögliche Änderungen vorsehen.
- [ ] **P3 – Sichtbarkeitsprüfung einordnen:** Den in der E2E-Tabelle genannten Sichtbarkeitstest samt T6 in die Umsetzungsreihenfolge aufnehmen. Die abschließende Aussage „Offene Punkte: Keine“ erst nach Festlegung dieser Test- und Übergabeentscheidungen treffen.

## Hinweise

Prüfbasis: `issue.md`, `requirement.md`, `inventory.md` einschließlich aller vier Detaildokumente und `plan.md`. Zur Einordnung der Testvoraussetzungen wurden die bestehende Fixture, Testhilfen und Zeitkonstanten gezielt nachgelesen (`JwtTokenValidationParametersFactory`: zehn Sekunden `ClockSkew`; `financeManager.js`: 60/5 Sekunden Drosselung). Dies ist eine Vollständigkeitsprüfung, keine Codequalitätsprüfung oder Umsetzung.

Die Vorbereitung von Zertifikat, Restore und Browserstart sowie die Berücksichtigung der dokumentierten Baseline-Fehler sind im Plan enthalten. Die vorhandenen Auth-, Refresh-, Redirect- und Quick-Edit-Testklassen sind als Regressionen benannt. Deren Ausführung ersetzt die oben fehlenden konkreten E2E-Voraussetzungen nicht. Es wurden keine Tests ausgeführt und weder Plan noch Produktivcode verändert.
