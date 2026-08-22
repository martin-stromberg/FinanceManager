# Uebersetzte Anforderung

## Titel

Weiterleitung zum Login bei abgelaufener Anmeldesession

## Aufgaben-ID

`c8b912a7-377a-4453-8dde-940d10309c73`

## Problem

Wenn die Anmeldesession eines Anwenders im Hintergrund ablaeuft, erkennt die Oberflaeche diesen Zustand derzeit nicht. Die zuletzt angezeigte Ansicht bleibt sichtbar, obwohl die Anmeldung nicht mehr gueltig ist. Beim anschliessenden Navigieren zu einer anderen Seite werden keine Inhalte angezeigt, ohne dass der Anwender einen Hinweis erhaelt oder zur Login-Seite weitergeleitet wird.

## Ausgangssituation

1. Der Anwender meldet sich an.
2. Der Anwender navigiert durch die Anwendung.
3. Die Anwendung bleibt lange genug inaktiv, bis die Anmeldesession ablaeuft.
4. Der Anwender navigiert zu einer anderen Seite innerhalb der Anwendung.
5. Die Seite zeigt keine Inhalte, ohne auf die abgelaufene Anmeldung hinzuweisen.

## Ziel

Die Anwendung erkennt, wenn ein Datenabruf aufgrund einer abgelaufenen Benutzeranmeldung fehlschlaegt, und leitet den Anwender automatisch zur Login-Seite weiter. Nach erfolgreicher erneuter Anmeldung wird der Anwender zu der urspruenglich aufgerufenen Seite zurueckgeleitet.

## Funktionale Anforderungen

- Ein fehlgeschlagener Datenabruf aufgrund einer abgelaufenen oder ungueltigen Anmeldesession muss als Authentifizierungsfehler erkannt werden.
- Bei einem erkannten Authentifizierungsfehler muss automatisch zur Login-Seite navigiert werden.
- Die urspruenglich aufgerufene Zielseite muss vor der Weiterleitung erhalten bleiben.
- Nach erfolgreicher erneuter Anmeldung muss die Anwendung zur gespeicherten urspruenglichen Zielseite zurueckkehren.
- Die Weiterleitung darf nicht als allgemeiner Fehler behandelt werden, wenn der Datenabruf aus einem anderen Grund fehlschlaegt.
- Der Anwender darf nach Ablauf der Session nicht dauerhaft eine leere oder veraltete Ansicht ohne Statushinweis sehen.

## Akzeptanzkriterien

- **Session abgelaufen:** Wenn ein geschuetzter Datenabruf wegen einer abgelaufenen Anmeldung mit einem Authentifizierungsfehler fehlschlaegt, wird der Anwender automatisch zur Login-Seite weitergeleitet.
- **Zielseite merken:** Die URL oder die fachlich gleichwertige Zielroute der urspruenglich angeforderten Seite bleibt fuer die Rueckkehr nach der Anmeldung erhalten.
- **Rueckkehr nach Login:** Nach erfolgreicher Anmeldung wird die urspruenglich angeforderte Seite geoeffnet und ihre Inhalte werden geladen.
- **Andere Fehler:** Bei einem nicht authentifizierungsbezogenen Fehler erfolgt keine Weiterleitung zur Login-Seite aufgrund dieser Anforderung.
- **Kein leerer Zustand:** Nach Erkennung des abgelaufenen Logins bleibt keine geschuetzte Ansicht ohne Fehlermeldung oder Navigationsreaktion als Endzustand sichtbar.
- **Direkte Login-Navigation:** Wird die Login-Seite ohne vorherigen Authentifizierungsfehler aufgerufen, erfolgt nach erfolgreicher Anmeldung keine ungewollte Weiterleitung auf eine nicht gespeicherte Zielseite.

## Technischer Kontext

Die Aenderung betrifft die clientseitige Behandlung von Session- und Authentifizierungsfehlern bei Datenabrufen sowie die Navigation zwischen geschuetzten Seiten, der Login-Seite und der urspruenglichen Zielroute.

## Nicht im Umfang

- Aenderungen an der serverseitigen Session-Laufzeit oder an der Authentifizierungslogik selbst.
- Automatische Verlaengerung einer ablaufenden Session ohne erneute Anmeldung.
