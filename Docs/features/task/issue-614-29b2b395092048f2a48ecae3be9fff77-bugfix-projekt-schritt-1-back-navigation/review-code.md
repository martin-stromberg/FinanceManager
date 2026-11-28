# Code-Review – Schritt 1: Back-Navigation

**Status:** Keine Befunde

Alle ursprünglich gemeldeten (niedrigschwelligen) Befunde wurden in derselben Iteration behoben:

- [x] ~~`OpenCategory` hängte `back=` auch bei Payload mit vorhandenem `back`-Parameter an (doppelter Query-Key)~~ — behoben: Guard `payload.Contains("back=", StringComparison.Ordinal)` in `CardPage.razor` `case "OpenCategory"`.
- [x] ~~`:`-Ausschluss nur auf Roh-String, case-sensitiver Präfixvergleich~~ — behoben: Präfixe jetzt `OrdinalIgnoreCase`, zusätzlich `NavigationManager.IsSameOriginUri(payload)` als Zusatzprüfung.
- [x] ~~Testabdeckung ließ Regressionsfälle aus (`card,{id}`, leerer Payload, verschachtelte `?back=`-Kette)~~ — behoben: drei bUnit-Tests ergänzt (StatementDraftEntryCard-Komma-Payload, ContactCard leerer Payload, SavingsPlanCard verschachtelte Kette); plus zwei Tests für die Usability-Fixes.

## Reviewbericht

**Geprüfter Diff:** `CardPage.razor` (`case "Back"` Whitelist, `case "OpenCategory"` `?back=`-Anhang), `BudgetPurposeCardViewModel.cs` (Category-Aktion via `OpenCategory`), `BudgetRuleCardViewModel.cs` (Fallback `list/budget/purposes`), `CardPageBackNavigationTests.cs` (11 bUnit-Tests).

**Routing-Logik — korrekt:** Reihenfolge der Back-Fälle richtig; `card,{id}`-/`list,{sub}`-Komma-Payloads, `account/{guid}`-Subtyp-Payloads und GUID-Payloads bleiben im Legacy-Pfad; `?back=` gewinnt vor Payload (dokumentierter Trade-off); `InitBack`-Echos unproblematisch.

**Security — ausreichend:** Whitelist enger als `IsSameOriginUri`; `:`-Ausschluss + fehlender `//`-Start + `IsSameOriginUri`-Zusatzprüfung schließen Open-Redirects aus; verschachtelte `?back=`-Werte werden am Konsumptionspunkt erneut validiert.

**`?back=`-Anhang — korrekt:** `BuildCardPath` + `Uri.EscapeDataString` erzeugen korrekt genestete Ketten; Separator, `Guid.Empty`-Guard und `back=`-Dubletten-Guard sauber. Konsistent mit `OpenBankContact`/`OpenAccountDetails`-Muster.

**Konventionen:** Relative `NavigateTo`-Aufrufe PathBase-konform; keine neuen UI-Texte; keine neuen public Member ohne XML-Doc; keine Nebenwirkungen auf andere Ribbon-Aktionen. Dokumentierte, fachlich plausible Nebenwirkung: `Saved`/`Deleted` auf der Kategoriekarte kehrt zur Ursprungskarte zurück.
