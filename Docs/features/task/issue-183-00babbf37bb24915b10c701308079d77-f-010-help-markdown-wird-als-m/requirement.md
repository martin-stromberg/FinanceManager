# F-010: Help-Markdown wird als MarkupString gerendert

## Metadaten

- Aufgaben-ID: `00babbf3-7bb2-4915-b10c-701308079d77`
- Feature-ID: `F-010`
- Branch: `task/issue-183-00babbf37bb24915b10c701308079d77-f-010-help-markdown-wird-als-m`
- Status: Risiko
- Prioritaet: P2
- Schweregrad: Mittel
- Bereich: Frontend, XSS
- Erstellt: 2026-07-16

## Ziel

Help-Markdown soll sicher als formatierter Inhalt in der Benutzeroberflaeche dargestellt werden. Nicht vertrauenswuerdiger HTML- oder Script-Inhalt darf dabei nicht in den Browser gelangen oder ausgefuehrt werden.

## Ist-Zustand

- `FinanceManager.Web/Components/Pages/HelpPageView.razor:43-45` rendert `ConvertMarkdownToHtml(markdown)` als `MarkupString`.
- `FinanceManager.Web/Components/Pages/HelpPageView.razor:224-268` verwendet eine Regex-basierte Markdown-Konvertierung ohne erkennbare HTML-Sanitization.
- `FinanceManager.Web/wwwroot/help/js/help-search.js` verwendet mehrfach `innerHTML`.

## Anforderungen

1. Help-Markdown muss vor der Ausgabe mit einem Sanitizer oder einem Whitelist-Renderer verarbeitet werden.
2. Eingebettetes HTML muss standardmaessig escaped werden oder darf nur nach expliziter Whitelist-Pruefung ausgegeben werden.
3. Die Ausgabe als `MarkupString` darf keine ungeprueften HTML- oder Script-Inhalte aus Help-Dateien enthalten.
4. Inhalte, die aus dem Help-Suchindex oder anderen nicht vertrauenswuerdigen Quellen stammen, muessen ebenfalls sicher behandelt werden.
5. Die Help-Ausgabe soll durch eine geeignete Content Security Policy (CSP) zusaetzlich gegen die Ausfuehrung eingeschleuster Scripts geschuetzt werden.
6. Help-Dateien sollen als vertrauenswuerdige Build-Artefakte behandelt und gegen Manipulation ausserhalb des vorgesehenen Build-Prozesses geschuetzt werden.

## Akzeptanzkriterien

- Markdown mit HTML-Tags wird ohne ausfuehrbaren HTML- oder Script-Inhalt dargestellt.
- JavaScript-Payloads in Help-Markdown werden nicht ausgefuehrt.
- Manipulierte Eintraege im Help-Suchindex koennen keinen ausfuehrbaren HTML- oder Script-Inhalt in die UI einschleusen.
- Die gewuenschte sichere Markdown-Formatierung bleibt fuer erlaubte Inhalte erhalten.
- Alle Help-Ausgabepfade, einschliesslich der Suche, verwenden Escaping oder eine dokumentierte Whitelist-Sanitization.
- Eine CSP ist fuer die Help-Oberflaeche eingefuehrt oder die gewaehlte Schutzmassnahme ist nachvollziehbar dokumentiert.

## Sicherheitsanforderungen

- Standardbezug: OWASP Top 10 A03 Injection.
- Standardbezug: ASVS Output Encoding.
- Eintrittswahrscheinlichkeit: Niedrig bis Mittel.
- Auswirkung: Wenn Help-Inhalte durch nicht vertrauenswuerdige Autoren oder kompromittierte Dateien veraendert werden, kann HTML oder Script in die UI gelangen.
- Angriffsszenario: Manipulierter Help-Markdown oder Suchindex enthaelt HTML oder Script, das im Browser ausgefuehrt wird.

## Nachweise und Referenzen

- Nachweis: `FinanceManager.Web/Components/Pages/HelpPageView.razor:43-45`.
- Nachweis: `FinanceManager.Web/Components/Pages/HelpPageView.razor:224-268`.
- Nachweis: `FinanceManager.Web/wwwroot/help/js/help-search.js`.
- Matrixbezug: `endpoint-service-matrix.md`, Zeile `HelpController`.

## Empfehlung

Markdown mit einem Sanitizer oder Whitelist-Renderer ausgeben, HTML standardmaessig escapen, eine CSP einfuehren und Help-Dateien als vertrauenswuerdige Build-Artefakte behandeln.
