# Anforderung

## Metadaten

| Feld | Wert |
|------|------|
| Aufgaben-ID | 2447e4b1-b7d7-4c36-90ea-f796915a92d0 |
| Feature | F-003: Lange JWT-Lebensdauer und Refresh ohne DB-Revalidierung |
| Branch | task/issue-186-2447e4b1b7d74c3690eaf796915a92d0-f-003-lange-jwt-lebensdauer-un |
| Erstellt | 2026-07-16 |
| Status | Risiko |
| Schweregrad | Hoch |
| Prioritaet | P1 |
| Bereich | Session Management, Benutzerstatus, Rollenentzug |
| Standardbezug | OWASP Top 10 A07, ASVS Session Management |
| Eintrittswahrscheinlichkeit | Mittel |

## Ausgangslage

Die Anwendung stellt JWTs mit sehr langer Lebensdauer aus. In den Konfigurationsdateien sind 43.200 Minuten gesetzt, was 30 Tagen entspricht. Die Refresh-Logik erneuert Tokens auf Basis bestehender Claims und erstellt ein neues Token, ohne dabei den aktuellen Benutzerstatus, den SecurityStamp, eine TokenVersion oder die aktuellen Rollen aus der Datenbank erneut zu pruefen.

Zudem koennen Benutzer administrativ deaktiviert werden. Beim Login ist nach vorliegendem Nachweis keine explizite Pruefung des Aktiv-Status vor der Tokenausgabe erkennbar.

## Nachweise

- `FinanceManager.Web/appsettings.json:14-18` setzt eine JWT-Lebensdauer von 43.200 Minuten.
- `FinanceManager.Web/appsettings.Production.json:17-21` setzt eine JWT-Lebensdauer von 43.200 Minuten.
- `FinanceManager.Web/Infrastructure/Auth/JwtRefreshMiddleware.cs:79-103` erneuert Tokens aus bestehenden Claims und ruft nur `IJwtTokenService.CreateToken` auf.
- `FinanceManager.Infrastructure/Auth/UserAdminService.cs:235-246` kann Benutzer deaktivieren.
- `FinanceManager.Infrastructure/Auth/UserAuthService.cs:199-250` zeigt beim Login keine explizite `Active`-Pruefung vor Tokenausgabe.
- OWASP-Risk-Rating ist in `endpoint-service-matrix.md` referenziert.

## Risiko

Deaktivierte Benutzer oder Benutzer mit entzogener Admin-Rolle koennen mit bereits ausgestellten Tokens weiterarbeiten, bis diese ablaufen oder ersetzt werden. Bei einer Laufzeit von 30 Tagen ist das Missbrauchsfenster erheblich. Durch die Refresh-Logik kann das Risiko zusaetzlich verlaengert werden, weil ein Token kurz vor Ablauf aus veralteten Claims erneuert werden kann.

## Angriffsszenario

Ein Benutzer erhaelt ein gueltiges Authentifizierungs-Cookie mit JWT. Danach wird der Benutzer deaktiviert oder verliert administrative Rechte. Solange das alte Token gueltig ist, kann der Benutzer weiterhin mit den alten Claims arbeiten. Kurz vor Ablauf erneuert die Middleware das Token aus diesen Claims, ohne die Datenbank zu konsultieren, und verlaengert dadurch die unberechtigte Sitzung.

## Zielzustand

Die Authentifizierung und Token-Erneuerung muessen sicherstellen, dass deaktivierte Benutzer und Benutzer mit geaenderten Rollen oder widerrufenen Berechtigungen nicht weiter mit veralteten Tokens arbeiten koennen. Token-Refresh darf nicht allein auf bestehenden Claims basieren, sondern muss serverseitig gegen den aktuellen Sicherheitszustand validiert werden.

## Funktionale Anforderungen

1. Die Access-Token-Laufzeit muss deutlich gegenueber 30 Tagen reduziert werden.
2. Token-Refresh muss serverseitig an einen aktuellen Sicherheitszustand gekoppelt werden, z. B. SecurityStamp, TokenVersion oder eine Session-Tabelle.
3. Bei jeder Token-Erneuerung muss der aktuelle Benutzerstatus aus der Datenbank geprueft werden.
4. Bei jeder Token-Erneuerung muessen die aktuell gueltigen Rollen oder Berechtigungen beruecksichtigt werden.
5. Deaktivierte Benutzer duerfen kein neues Token erhalten.
6. Benutzer mit entzogenen Rollen duerfen keine erneuerten Tokens mit alten Rollen-Claims erhalten.
7. Bei Deaktivierung eines Benutzers muessen aktive Sessions bzw. erneuerbare Tokens invalidiert werden.
8. Bei Rollenwechsel oder Rollenentzug muessen betroffene aktive Sessions bzw. erneuerbare Tokens invalidiert oder durch aktuelle Claims ersetzt werden.
9. Beim Login muss vor der Tokenausgabe explizit geprueft werden, ob der Benutzer aktiv ist.

## Nicht-funktionale Anforderungen

- Die Loesung muss OWASP Top 10 A07 und ASVS Session-Management-Anforderungen beruecksichtigen.
- Die Loesung muss nachvollziehbar verhindern, dass alte Claims ueber Refresh weitergetragen werden.
- Die Loesung muss bestehende legitime Benutzerfluesse moeglichst wenig beeintraechtigen.
- Sicherheitsrelevante Ablehnungen bei Login oder Refresh sollen eindeutig und wartbar implementiert sein.

## Akzeptanzkriterien

1. Ein deaktivierter Benutzer kann sich nicht neu anmelden und erhaelt kein JWT.
2. Ein deaktivierter Benutzer kann ein vorhandenes JWT nicht per Refresh verlaengern.
3. Ein Benutzer, dem eine Admin-Rolle entzogen wurde, erhaelt nach Refresh kein Token mehr mit Admin-Claims.
4. Ein Benutzer, dem eine Rolle entzogen wurde, kann mit erneuerten Tokens keine Aktionen ausfuehren, die diese Rolle erfordern.
5. Eine Aenderung des SecurityStamp, der TokenVersion oder der Session-Invalidierung macht bestehende refresh-faehige Tokens unwirksam.
6. Die konfigurierte Access-Token-Laufzeit ist deutlich kuerzer als 30 Tage und in allen relevanten Umgebungen konsistent gesetzt.
7. Automatisierte Tests decken mindestens Login fuer deaktivierte Benutzer, Refresh fuer deaktivierte Benutzer und Refresh nach Rollenentzug ab.

## Offene Punkte

- Welche konkrete Ziel-Laufzeit fuer Access-Tokens fachlich gewuenscht ist, ist nicht angegeben.
- Ob SecurityStamp, TokenVersion oder eine Session-Tabelle bevorzugt werden soll, ist fachlich nicht vorgegeben.
- Ob bestehende Benutzer bei Rollenwechsel sofort ausgeloggt werden sollen oder erst beim naechsten Refresh neue Claims erhalten, muss im Rahmen der Umsetzung entschieden oder fachlich bestaetigt werden.
