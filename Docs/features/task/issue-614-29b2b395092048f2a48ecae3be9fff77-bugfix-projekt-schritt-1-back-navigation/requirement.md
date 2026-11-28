# Anforderung – Schritt 1: Back-Navigation zentral reparieren + Rücksprungziele verknüpfter Karten

Projekt: Bugfix-Projekt (Issue #614) · Schritt 1 von 5
Behebt Issues: #600, #602, #606, #607, #608

## Ziel

Der „Zurück"-Ribbon-Button mehrerer Kartenansichten erzeugt ungültige Routen oder führt nicht
zur Herkunftsansicht zurück. Ursache: `CardPage.razor` interpretiert `Back`-Payloads nur dann
als Direktziel, wenn sie mit `/` oder `http` beginnen; app-relative Pfade ohne führenden Slash
(`card/setup`, `card/budget/purposes/{id}`) fallen in den Legacy-Subtyp-Branch und erzeugen
`list/{Kind}/{payload}` — ungültige Routen, die dauerhaft „Laden …" zeigen. Zusätzlich öffnet
die Ribbon-Aktion „Kategorie" der Sparplankarte die Kategoriekarte ohne `?back=`-Rücksprungziel.

## Fachliche Anforderungen

1. **Zentrale Payload-Behandlung (#602, #606, #607, #608):** `CardPage` `case "Back"` muss
   app-relative Pfad-Payloads mit geschlossener Präfix-Whitelist (`card/`, `list/`, `reports/`,
   Ordinal, ohne `:`-Schema, ohne `//`-Start) als `NavigateTo`-Ziel akzeptieren — inklusive im
   Payload enthaltenem Query-Teil. Position der Prüfung: **nach** der `?back=`-Auswertung
   (`?back=` gewinnt vor dem Payload), **vor** GUID-/Komma-/Legacy-Auswertung.
   Betroffene Aufrufer: `BudgetRuleCardViewModel` (Back + NavigateBackExtended),
   `BudgetPurposeCardViewModel` („Category"), `McpAccessCardViewModel`,
   `McpSecurityOverviewViewModel`, `McpAuditViewModel`, `McpApprovalCenterViewModel`.
2. **Rücksprungziel Kategoriekarte (#600):** Ribbon-Aktion „Kategorie" in
   `SavingsPlanCardViewModel` öffnet `card/savings-plans/categories/{id}` — `?back=` mit der
   aktuellen Karten-URL anhängen, inkl. Weitergabe eines vorhandenen `back`-Kontexts
   (Navigationsketten, Muster `OpenBankContact`/`BuildCardPath`). Direktaufruf ohne `back`
   behält Fallback auf Kategorieübersicht.
3. **Kein Eingriff** an `BudgetCategoryListViewModel` (Back-Payload geht an `ListPage`, die
   Payloads ignoriert — funktioniert zufällig korrekt; dokumentiert).

## Akzeptanzkriterien

- Zurück auf der Budget-Regelkarte navigiert zur aufrufenden Purpose- bzw. Kategorie-Karte —
  auch nach dem Löschen einer Regel (`NavigateBackExtended`).
- Zurück auf allen vier MCP-Ansichten (`/card/mcp-audit`, `/card/mcp-security`,
  `/card/mcp-approvals`, `/card/mcp-accesses/{id}`) navigiert zu `card/setup` — bzw. zum
  `?back=`-Ziel, falls gesetzt. Keine Route `list/<kind>/card/setup` mehr.
- Sparplankarte → „Kategorie" → „Zurück" landet wieder auf der Sparplankarte; bei mit `?back=`
  geöffneter Sparplankarte wird die Kette weitergegeben.
- „Zurück" der Purpose-Karte per „Category"-Aktion navigiert zur Budgetkategorie-Karte.
- Keine Regression: `card,{id}`-Payloads, einteilige Subtyp-Payloads (explizit: `account/{guid}`
  → `list/postings/account/{guid}`), `?back=`-Auswertung, leerer Payload (`BuildListPath`),
  GUID-Sonderfall (Statement-Draft-Entries), Direkt-URL-Payloads (`/…`, `http…`).
- Tests auf bUnit-/VM-Ebene für neue und bestehende Payload-Formate.

## Nicht-Ziele

- Kein genereller Umbau des `?back=`-Frameworks; kein `back`-Parameter für `ListPage`.
- Kein Hinweis bei ungespeicherten Änderungen beim Wegnavigieren (Bestandsverhalten).
- Keine Änderung am Verhalten von `PostingsCardViewModel`-Subtyp-Payloads.

## Kontext

- `CardPage.razor` `OnUiActionRequested` Z. 133–198 (Back-Block), `BuildCardPath` Z. 100–106,
  `case "OpenCategory"` Z. 257–259, `case "OpenBankContact"` Z. 233–243.
- `IsSameOriginUri`/`IsSafeNavigationCandidate` in `NavigationManagerExtensions.cs:186–247`.
- Projekt-Bestandsaufnahme §5/§6: `Docs/projects/task/issue-614-…-bugfix-projekt/inventory.md`.
