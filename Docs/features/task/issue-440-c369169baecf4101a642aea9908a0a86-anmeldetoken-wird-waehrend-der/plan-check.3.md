# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die korrigierte Richtung ist angemessen: normaler Webhost, einmalige 30-Minuten-Referenz, erfolgreicher Beginn, Diagnose vor minimalem Auth-Fix und ein begrenzter passiver Pollingpfad. Es fehlen einzelne Nachweisbedingungen; eine neue Authentifizierungs- oder Testhostarchitektur ist dafür nicht erforderlich.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Echter Formularlogin ohne künstliches Cookie oder Login-API-Abkürzung | Produktionsgetreue Reproduktion, Schritte 1–2 | UI-Login über vorhandene Fixture und `LoginThroughUiAsync` | Abgedeckt |
| Erste geschützte Home-/Notifications-Anfrage nach Login erfolgreich, kein sofortiger Redirect | Erfolgreicher Start ausdrücklich vor Langzeitbeobachtung | Zeitgestempelte API-Statuscodes, sichtbare Route und initiale Home-/Notifications-Erfolge | Abgedeckt |
| Pflichtreferenz mit 30 Minuten und tatsächlicher Serverzeit; spätere verkürzte Läufe mindestens 10 Minuten | Designentscheidung und Umsetzungsschritte 2/6; keine Zeit-/Cookie-Manipulation | Pflicht-E2E einmal mit regulärer Laufzeit | Abgedeckt |
| Navigation und ausgelöste APIs bleiben während aktiver Nutzung vor und nach ursprünglichem Ablauf nutzbar | Zeitlinie und diagnoseabhängiger Minimalfix | Vorher-/Nachher-Nutzung geplant; die ausschließliche Anerkennung von Fehlern nach `exp` schließt reale frühere Langzeitfehler aus | Lücke |
| Quick-Edit-Eingaben in bestehenden und neuen Zeilen halten Sitzung, Fokus und ungespeicherte Werte | Beide Zeilentypen, Eingabe ohne Blur, anschließendes Speichern/Wiederöffnen | No-Blur-E2E vorhanden; zusätzliche Navigation/API-Aktivität könnte jedoch den fehlenden Eingabe-Refresh verdecken | Lücke |
| Ohne Bedienung keine Keepalive-Anfrage; erst geschützte Folgeaktion darf zum Login führen | Keine periodischen Keepalives; Idle-Polling ohne Redirect; Kontenfolgeaktion | Poll-401, stehenbleibende Home-Seite und Folgeaktions-Redirect geplant; explizite Null-Anfrage-Messung für Keepalive fehlt | Lücke |
| Passives Hintergrund-Polling redirectet bei Ablauf nicht und endet; reguläre API-Aufrufe behalten Authfehlerpfad | Eigener passiver `IApiClient`-Aufruf, unveränderter Exceptionvertrag, Ende des Pollloop | Unit-Test für Ereignisunterdrückung und E2E für Polling/geschützte Folgeaktion | Abgedeckt |
| Login-/Anwendungsinhalte nicht zum Abfangen von Initialisierungsrennen ausblenden | Keine entsprechende Änderung vorgesehen, aber Verbot nicht ausdrücklich im Plan festgehalten | Expliziter Sichtbarkeitsnachweis fehlt | Lücke |
| JWT-Principal/Stamp, Benutzerzustand und korrekter Token im API-Scope | Bestehende Validierung erhalten; konkrete Fehlerpfade anhand Zeitlinie prüfen | Diagnoseabhängige Regression vorgesehen; betroffene bestehende Auth-Testklassen sind nur pauschal benannt | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] **Aktive Sitzung:** Nach nachgewiesen erfolgreichem Start den ersten späteren echten Fehler erfassen und diagnostizieren, auch wenn er bereits beim Eintritt ins Refreshfenster vor dem ursprünglichen `exp` auftritt. Weiterhin erfolgreiche Nutzung über ursprüngliches `exp` einschließlich bestehendem Validierungs-/ClockSkew-Fenster als Ziel prüfen; ein früher Fehler darf diesen Lauf als Fehler beenden und dokumentiert werden.
- [ ] **Quick Edit:** Für bestehende und neue Zeile die No-Blur-Messphase allein durch tatsächliche Texteingabe aktiv halten. In dieser Phase keine zusätzliche Navigation oder künstlich ausgelöste API-/Keepalive-Aktion als Refreshquelle verwenden. Eingabebedingte Keepalive-/Erneuerungsbeobachtung, unveränderten Fokus/Werte und anschließendes erfolgreiches Speichern/Wiederöffnen ausdrücklich bestätigen. Dafür genügt eine Präzisierung der bereits geplanten E2E-Fälle.
- [ ] **Inaktivität:** Nach Abschluss initialer Login-/Navigationsrequests Beginn und Ende eines echten Idle-Intervalls festlegen; bis zum Ablauf des tatsächlich zuletzt ausgestellten Tokens samt bestehendem Validierungsfenster exakt null neue `/api/auth/keepalive`-Requests nachweisen. Parallel Poll-401 ohne Routewechsel und Ende weiterer Pollrequests beobachten; danach Kontenaktion mit 401 und Login/Rücksprungziel prüfen.
- [ ] **Kein UI-Ausblenden:** Im vorhandenen Formularlogin-/Start-E2E sichtbares Loginformular vor dem Absenden und sichtbare erwartete Home-/Anwendungsinhalte nach Login explizit prüfen. Die Nachplanung soll außerdem festhalten, dass kein Auth-Initialisierungs-Workaround Inhalte ausblendet.
- [ ] **Bestehende Regression:** `ApiClientAuthTests` mit den vorhandenen Fällen für Unauthorized-Ereignis, gewöhnliches Forbidden ohne Ereignis und Authentifizierungs-Forbidden mit Ereignis konkret aufführen; sie betreffen die geplante Fehlerauswertung unmittelbar. Für einen tatsächlich notwendigen Auth-Fix die vorhandenen `JwtCookieAuthTokenProviderTests` sowie `ApiClientAuthTests` für JWT-Validierung, Stamp/Benutzerstatus und Refresh gezielt nennen. Neue pauschale Tests sind damit nicht gefordert.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Formularlogin und erste Home-/Notifications-API | Pflichtreferenz auf normalem 30-Minuten-Host mit Statuscode-/Routenbeobachtung | Abgedeckt |
| Zeitlich späterer Fehler bei aktiver Navigation und Betrieb über Originalablauf | `AuthenticationFlowPlaywrightTests`, Zeitlinie | Lücke: Fehler vor `exp` nicht als gültigen Diagnosebefund ausschließen |
| Bestehende Quick-Edit-Zeile ohne Blur | `StatementDraftQuickEditValueTakeoverE2ETests`, Werte/Fokus/Speichern | Lücke: Eingabe als eigenständige Aktivitätsquelle absichern |
| Neue Quick-Edit-Platzhalterzeile ohne Blur | Dieselbe Testklasse, beide Zeilentypen ausdrücklich geplant | Lücke: Eingabe als eigenständige Aktivitätsquelle absichern |
| Idle mit passivem Polling, anschließend geschützte Kontenaktion | `AuthenticationFlowPlaywrightTests`, Poll-401 ohne Redirect und Folgeaktions-Redirect | Lücke: Null neue Keepalive-Requests im Idle-Intervall ausdrücklich messen |
| Login-/Anwendungsinhalte sichtbar | Bestehender Formularlogin-/Startfluss verwendbar | Lücke: Sichtbarkeitsassertionen und Verbot des Ausblendens ergänzen |
| JWT-/Benutzerzustandsregression | Vorhandene Unit-/Integrationstests | Nicht erforderlich mit Begründung: technische Validierungsverträge werden zusätzlich auf Service-/API-Ebene geprüft; UI-Redirects sind oben abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Die Formulierungen „erst nach `exp`“/„nur zeitlich nach `exp`“ in Übersicht, Reproduktionsablauf, Umsetzungsschritt 2 und offenen Punkten korrigieren. Maßgeblich ist **erfolgreicher Normalstart, danach zeitlich späterer echter Fehler**. Das 15-Minuten-Refreshfenster der 30-Minuten-Konfiguration kann einen Fehler vor `exp` sichtbar machen; dessen Ausschluss würde die geforderte Diagnose verhindern.
- [ ] Die oben genannten Beobachtungsbedingungen in die bestehenden E2E-Schritte aufnehmen; insbesondere Null Keepalives bei Inaktivität, eigenständige Texteingabe als Aktivität und sichtbare Inhalte. Vorhandene Fixture, Browserbeobachtung und redigierte serverseitige API-Logs reichen als vorgesehene Mittel aus; ein zusätzlicher Host oder eine neue Beobachtungsarchitektur ist nicht verlangt.
- [ ] Die konkret betroffenen bestehenden Auth-Testklassen und Ereignisfälle in der Regressionstabelle benennen, statt sie nur unter „eventuell betroffene Token-Refresh-Unit-/Integrationstests“ zusammenzufassen.

## Hinweise

- Prüfbasis: `requirement.md` einschließlich Nutzerklarstellung, `inventory.md` und alle vier verlinkten Detaildokumente, `test-fidelity.md`, korrigierter `plan.md` sowie `issue.md` im Repository-Root.
- Die Zweiminutenfixture ist kein anerkannter Original-Rotnachweis. Der kurze erfolgreiche 30-Minuten-Konfigurationsvergleich belegt nur den normalen Beginn; der verpflichtende tatsächliche Langzeitlauf bleibt Umsetzungsschritt.
- Die konkreten API-Statuscodes müssen aus den tatsächlich serverseitig ausgeführten Aufrufen stammen, insbesondere erster `Notifications_ListAsync`-Aufruf und geschützter `GetAccountStatisticsAsync`-Aufruf. Eine reine Browser-URL-/Cookieprüfung ersetzt diese Messung nicht. Die geplante Nutzung vorhandener Serverlogs ist dafür ausreichend und soll konkret genutzt werden.
- Die Diagnoseabhängigkeit des kleinsten Auth-Fixes ist ausdrücklich durch die Nutzersteuerung gedeckt. Sie ist keine Planlücke und kein Grund, die frühere Snapshot-/Circuit-/Data-Protection-Architektur wieder aufzunehmen.
- Die „offenen Punkte“ sind technische Diagnosearbeit mit klarer Reihenfolge, keine ausstehende Nutzerentscheidung. Redaktionell kann der Abschnitt „Noch zu diagnostizieren“ heißen. Es ist keine Rückfrage erforderlich.
- Ausschließlich diesen Bericht erstellt; Plan und Code unverändert. Kein Testlauf war Teil dieser Dokumentenprüfung.
