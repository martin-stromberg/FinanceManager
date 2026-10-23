# Fortsetzung

## Autorisierung und geklärte Nutzerentscheidungen

- Hintergrund-Polling ist autorisiert: `BackgroundTaskStatusPanel` soll bei abgelaufener Sitzung passiv enden, ohne selbst einen Login-Redirect auszulösen. Eine geschützte Nutzeraktion darf weiterhin den bestehenden 401-/Redirectpfad verwenden.
- Produktivlogin, Notifications und Navigation funktionieren zunächst; der Logout tritt laut Nutzer erst nach Tokenablauf während längerer Nutzung auf.
- Der frühere Rotnachweis mit zwei Minuten Laufzeit ist ein Testartefakt, kein Originalnachweis. Die umfangreiche Circuit-Snapshot-/Scope-Architektur daraus ist nicht vorgegeben und muss anhand neuer Evidenz verworfen oder begründet werden.
- Keine Produktiv- oder Testcodeänderung ist in diesem Planungsschritt autorisiert/erforderlich.

## Nächste Schritte

1. Bestehende Playwright-Fixture und Testhilfen auf produktionsgetreuen Start zurückführen/verwenden; echten Browserlogin über das Formular mit normalem Webhost ausführen.
2. Verpflichtende 30-Minuten-Referenz: Home/Notifications und Navigation unmittelbar nach Login erfolgreich bestätigen, danach mit echten Navigationen und Quick-Edit-Eingaben über das ursprüngliche `exp` hinaus beobachten. Kein Messfenster unter 10 Minuten bei späteren verkürzten Läufen.
3. Erst nach einem zeitlich belegten Fehler die konkrete Ursache anhand Browser-/API-Zeitlinie diagnostizieren; keinen Fix vorwegnehmen. Wenn kein Fehler auftritt, Bedingungen und Laufzeit prüfen und Reproduktionslücke dokumentieren.
4. Den kleinstmöglichen evidenzbasierten Fix umsetzen. Dabei passives Polling im `BackgroundTaskStatusPanel` bei Authentifizierungs-401 beenden, ohne globale Redirectsemantik für geschützte Nutzeraktionen zu ändern.
5. E2E-Regression: aktiver Ablauf über `exp`, Quick-Edit-Werte/Fokus ohne Blur, Inaktivität ohne Polling-Redirect und anschließende geschützte Aktion mit Login-Redirect. Vorhandene Testartefakte anpassen, sofern ausreichend; keine unnötige Testarchitektur ausbauen.
6. Danach Lifecycle-Reviews, gezielte Regressionstests, Dokumentation und Commit nach dem Lifecycle-Workflow fortsetzen.

## Arbeitsstand

Plan und langlebige Taskliste wurden anhand der Nutzerkorrektur neu gefasst. Die Belege zum Zwei-Minuten-Sofort-401 bleiben als historischer Testartefakt-Befund erhalten. Es wurden keine Produktiv- oder Testquellen geändert.
