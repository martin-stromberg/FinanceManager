# Offene Aufgaben

Erstellt am: 2026-07-19
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Manifest-Integritaet vollstaendig ueber HTTP nachweisen: fehlendes Manifest sowie manipulierte Markdown-, JSON-, CSS- und JS-Dateien ueber echte Requests pruefen; zusaetzlich nach einem Build Vollstaendigkeit und Konsistenz des erzeugten Manifests mit allen tatsaechlich ausgelieferten Dateien verifizieren.
- [ ] API- und Renderer-Abdeckung vervollstaendigen: die drei API-Pfade durchgaengig mit echtem Integritaetsvalidator und echten HTTP-Responses gegen manipulierte Inhalte testen; verschachtelte Payloads ueber die API, mehrere Pflichtfeld-/Grenzfaelle und die Kombination aus Codeblock-, Tabellen- und Linkfaellen nachweisen.
- [ ] Browser-/beidsprachige Nachweise ergaenzen: E2E-Test fuer `/help` und `/help/view/...` unter CSP mit Suche, internen Links, externen Links, manipulierten Indexwerten und CSP-Verletzungen; deutsche und englische Help-Ansicht dokumentiert pruefen.
- [ ] Abhaengigkeiten bewerten: `NU1608` fuer `HtmlSanitizer`/`AngleSharp`, `NU1902` fuer AngleSharp und `NU1903` fuer SQLitePCLRaw bewerten; die AngleSharp-/HtmlSanitizer-Kombination als unmittelbar relevante Sanitization-Abhaengigkeit aktualisieren oder Advisory-Auswirkung dokumentiert akzeptieren.

## Code-Review-Befunde

- [ ] Mittel - Sanitization-Abhaengigkeit bleibt verwundbar bzw. uneinheitlich aufgeloest: Das Web-Projekt bindet `HtmlSanitizer` als zentrale Sicherheitsgrenze ein; `dotnet list FinanceManager.Web/FinanceManager.Web.csproj package --vulnerable --include-transitive` meldet weiterhin `AngleSharp 0.17.1` mit moderater Sicherheitsanfaelligkeit `GHSA-pgww-w46g-26qg`, und die Unit-Tests laufen wegen `NU1608` nicht nachweislich mit derselben kompatiblen Dependency-Basis.

## Fehlgeschlagene Tests

Keine.
