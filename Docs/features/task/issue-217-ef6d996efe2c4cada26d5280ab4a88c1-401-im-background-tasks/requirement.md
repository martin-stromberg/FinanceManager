# Anforderung

## Metadaten

- Aufgaben-ID: `ef6d996e-fe2c-4cad-a26d-5280ab4a88c1`
- Branch: `task/issue-217-ef6d996efe2c4cada26d5280ab4a88c1-401-im-background-tasks`
- Erstellt: `2026-07-26`
- Titel: `401 im Background-Tasks`

## Ausgangslage

Wenn die Webseite geoeffnet ist, entstehen fortlaufend sehr viele Warnungen im Protokoll. Die Warnungen betreffen HTTP-GET-Anfragen auf den Endpunkt `/api/background-tasks/active`, die mit dem Statuscode `401 Unauthorized` beantwortet werden.

Beispiel:

```text
2026-07-25 17:19:15.644 +02:00 [WARNING] FinanceManager.Web.Infrastructure.RequestLoggingMiddleware HTTP GET /api/background-tasks/active responded 401 in 0 ms (TraceId: 0HNN7B4LL6127:0000002F)
```

## Problem

Die Webseite loest im geoeffneten Zustand wiederholt Anfragen an `/api/background-tasks/active` aus, obwohl diese Anfragen nicht erfolgreich autorisiert werden. Dadurch wird das Protokoll mit zahlreichen Warnungen belastet.

## Ziel

Die wiederkehrenden `401 Unauthorized`-Warnungen fuer `GET /api/background-tasks/active` sollen beseitigt werden, wenn die Webseite geoeffnet ist.

## Funktionale Anforderungen

- Die Anwendung darf im normalen geoeffneten Zustand der Webseite keine fortlaufenden `401`-Antworten fuer `GET /api/background-tasks/active` erzeugen.
- Der Endpunkt `/api/background-tasks/active` darf nur dann regelmaessig abgefragt werden, wenn die Anfrage autorisiert ist oder fachlich sinnvoll verarbeitet werden kann.
- Nicht autorisierte oder nicht angemeldete Nutzer duerfen keine Polling-Schleife ausloesen, die wiederholt Warnungen im Request-Log erzeugt.
- Die Anzeige oder Verarbeitung aktiver Background-Tasks muss fuer autorisierte Nutzer weiterhin funktionieren.

## Nicht-funktionale Anforderungen

- Das Request-Log soll durch diesen Fall nicht mehr mit wiederkehrenden Warnungen geflutet werden.
- Die Loesung soll das bestehende Authentifizierungs- und Autorisierungskonzept respektieren.
- Die Loesung soll keine sicherheitsrelevanten Informationen fuer nicht autorisierte Nutzer offenlegen.

## Akzeptanzkriterien

- Bei geoeffneter Webseite treten keine unzaehligen Warnungen der Form `HTTP GET /api/background-tasks/active responded 401` mehr auf.
- Ist ein Nutzer nicht autorisiert oder nicht angemeldet, wird kein wiederholtes Polling gegen `/api/background-tasks/active` fortgesetzt.
- Ist ein Nutzer autorisiert, koennen aktive Background-Tasks weiterhin wie vorgesehen abgefragt werden.
- Bestehende Authentifizierungs- und Autorisierungsregeln werden nicht aufgeweicht.
- Die Aenderung ist durch geeignete Tests oder eine nachvollziehbare manuelle Pruefung abgesichert.

## Offene Punkte

- Keine.
