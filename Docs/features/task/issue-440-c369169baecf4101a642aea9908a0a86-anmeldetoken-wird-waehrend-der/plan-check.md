# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Die Nachträge schließen die zuvor dokumentierten Lücken. Die Prüfung betrifft die Vollständigkeit des Plans; die vorgesehenen Langzeit-, E2E- und Regressionsnachweise sind noch auszuführen.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Echter Formularlogin und erste erfolgreiche geschützte Home-/Notifications-Anfrage | Normaler Webhost, bestehende UI-Loginhilfe und Statuscode-/Routenbeobachtung | `AuthenticationFlowPlaywrightTests`, initiale API-Erfolge ausdrücklich prüfen | Abgedeckt |
| Pflichtreferenz mit regulären 30 Minuten ohne Cookie-/Zeitmanipulation | Umsetzungsschritte 1–2 und 6; spätere verkürzte Läufe mindestens 10 Minuten | Einmaliger echter 30-Minuten-Referenzlauf verpflichtend | Abgedeckt |
| Aktive Navigation und APIs vor/nach ursprünglichem Ablauf | Stabiler Beginn, erste spätere Fehlerzeitlinie auch im Refreshfenster vor `exp`, danach diagnoseabhängiger Minimalfix | Aktive Nutzung über ursprüngliches `exp` samt ClockSkew, reproduzierenden Fall nach Fix wiederholen | Abgedeckt |
| Quick Edit in bestehenden und neuen Zeilen erhält Sitzung, Fokus und Werte | Texteingabe ohne Blur als einzige Aktivitätsquelle, keine zusätzlich ausgelöste Navigation/API-/Keepalive-Aktion | Beide Zeilentypen über `exp` samt ClockSkew; Eingabe-Refresh beobachten, Fokus/Werte und Speichern/Wiederöffnen prüfen | Abgedeckt |
| Ohne Bedienung keine Keepalive-Anfrage | Idle erst nach Abschluss initialer Requests; maßgeblich zuletzt ausgestellter Token | Exakt null neue `/api/auth/keepalive`-Requests bis Ablauf samt ClockSkew | Abgedeckt |
| Passiver Poll-401 redirectet nicht; geschützte Folgeaktion redirectet | Eigener passiver Aufrufpfad mit unverändertem Exceptionvertrag, Pollloop endet; regulärer Authfehlerpfad bleibt | Unit-Nachweis ohne `AuthenticationRequired`, E2E für stehenbleibende Home-Seite und nachfolgende Kontenaktion mit Login/Rücksprungziel | Abgedeckt |
| Inhalte nicht wegen Initialisierungsrennen ausblenden | Ausdrückliches Verbot einer ausblendenden Initialisierungsbarriere | Vorhandene `InitializationVisibilityProbe` für Loginfelder und Home-/Anwendungsinhalte | Abgedeckt |
| JWT-/Stamp-/Benutzerzustandsprüfung und verlässlicher API-Token bleiben erhalten | Nur nachgewiesenen Fehlerpfad minimal korrigieren; bestehende Validierung erhalten | Konkrete `ApiClientAuthTests` für 401/403-Ereignisse; bei Auth-Fix vorhandene Provider-, JWT-, Stamp-/Benutzerzustands- und Refresh-Regressionen | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Formularlogin, erste Startseiten-API und regulärer Langzeitbeginn | `AuthenticationFlowPlaywrightTests`, normaler 30-Minuten-Host, API-Status-/Zeitbeobachtung | Abgedeckt |
| Aktive Navigation über die ursprüngliche Laufzeit | Zeitlicher Diagnosebefund einschließlich möglichem Fehler vor `exp`, erfolgreicher Verlauf über `exp` samt ClockSkew | Abgedeckt |
| Bestehende Quick-Edit-Zeile ohne Blur | `StatementDraftQuickEditValueTakeoverE2ETests`, alleinige Texteingabe, Fokus/Werte/Speichern | Abgedeckt |
| Neue Quick-Edit-Platzhalterzeile ohne Blur | Dieselbe Testklasse und dieselben ausdrücklichen Nachweisbedingungen | Abgedeckt |
| Idle, passives Polling und anschließende geschützte Kontenaktion | Null Keepalives, Poll-401 ohne Redirect und Ende des Pollloop, danach 401/Login/Rücksprungziel | Abgedeckt |
| Sichtbare Login-/Anwendungsinhalte während Initialisierung | Formularlogin-/Start-E2E mit vorhandener `InitializationVisibilityProbe` | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

## Hinweise

- Erneut geprüft wurde der korrigierte `plan.md` gegen die bereits vollständig gelesene Anforderung einschließlich Nutzerklarstellung, Bestandsaufnahme mit Detaildokumenten, `test-fidelity.md` und Root-`issue.md`. Die Lücken aus `plan-check.3.md` sind geschlossen.
- Der Zweiminuten-Sofortfehler bleibt ein Testartefakt. Der verpflichtende Referenzlauf muss zunächst normalen erfolgreichen Betrieb bestätigen und danach den ersten echten zeitlich späteren Fehler erfassen; dessen Anerkennung ist jetzt nicht mehr auf die Zeit nach `exp` beschränkt.
- Konkreter Auth-Fix und betroffene Klassen bleiben bewusst diagnoseabhängig. Das ist autorisierte technische Arbeit und keine offene Nutzerentscheidung; eine neue Authentifizierungs- oder Testhostarchitektur wird nicht verlangt.
- Ausschließlich diesen Bericht erstellt; keine Plan- oder Codeänderungen und keine Testausführung im Rahmen der Gegenprüfung.
