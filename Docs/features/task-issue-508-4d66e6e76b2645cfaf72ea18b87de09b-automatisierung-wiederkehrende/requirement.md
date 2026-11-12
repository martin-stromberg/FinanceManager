# Anforderung – Automatisierung wiederkehrender Architektur-, Sicherheits- und Compliance-Reviews (Issue #508)

## Ziel

Drei zeitgesteuerte GitHub Actions erzeugen automatisch Issues und erwähnen darin `@softwarewerkstatt`, um die automatische Analyse und Bearbeitung anzustoßen. Ein Issue wird nur angelegt, wenn kein offenes Issue desselben Typs aus dem vorherigen Zyklus existiert (Duplikatvermeidung).

## Prüfungen

1. **Monatliches Architektur- und Sicherheitsreview** – jeden Monat am 1. Tag; Issue-Body mit Checkliste (Sicherheitsrisiken, CVEs in NuGet, ASP.NET Core/Blazor/.NET-Empfehlungen, AuthN/AuthZ-Änderungen, Design Patterns, veraltete APIs, Wartbarkeit/Testbarkeit, Logging/Telemetrie, Community/Microsoft-Doku/Sicherheitsmeldungen); Ergebnis: priorisierte Handlungsempfehlungen.
2. **Wöchentliche NuGet-Prüfung** – wöchentlich; Issue-Body mit Checkliste (Updates, Security-Updates, bekannte Schwachstellen, veraltete/ungepflegte Pakete, Nachfolgepakete, Breaking Changes); Ergebnis: Update-Empfehlungen mit Risikobewertung.
3. **Jährliches Compliance- und Copyright-Review** – jährlich am 1. Januar; Issue-Body mit Checkliste (Copyright im Quellcode, Webseiten, About-Dialoge, Impressum, Datenschutz, Drittanbieter-Lizenzen, Open-Source-Verpflichtungen, Unternehmens-/Kontaktinfos, App-Store-Metadaten); Ergebnis: Änderungsvorschläge und Risikoliste.

## Akzeptanzkriterien

- Separate GitHub Action pro Prüfung, Auslösung per Cron-Trigger
- Aussagekräftige Issue-Titel; `@softwarewerkstatt` wird im Issue erwähnt
- Duplikatprüfung auf offene Issues desselben Typs vor dem Anlegen
- Vollständige Dokumentation innerhalb des Repositories
