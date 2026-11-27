# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Verlauf

- **Runde 1** (`review-code.1.md`): 4 Befunde (defekte `ExpandSectionAsync`-Assertion; Overlay-Hosts im `.card-page-content`-Wrapper; Open-Redirect in `ListPage`/`BudgetReport`; unleserlicher `.icon-btn.danger:hover`) — alle in Iteration 2 behoben.
- **Runde 2** (`review-code.2.md`, dort als „Runde 3" bezeichnet): Fix-Verifikation 4/4 bestanden; Restbefunde — `CardPage.razor` `?back=`-Senke (Altbestand) sowie zu harte Aussagen fehlender Bypass-Testfälle.
- **Runde 3** (`review-code.2.md`): CardPage-Fix strukturell verifiziert, aber der zentrale Befund „`IsSameOriginUri` ist per WHATWG-/`.NET`-Parser-Differenz umgehbar" (`https:evil.test`, `java%09script:`, `ht%09tps://`) neu gemeldet.

## Schlussverifikation (diese Runde)

Der Runde-3-Befund wurde in Iteration 3b behoben und hier abschließend geprüft:

- **`NavigationManagerExtensions.IsSameOriginUri`** (`NavigationManagerExtensions.cs` Z. 186–211) führt vor der `ToAbsoluteUri`-Auflösung `IsSafeNavigationCandidate` (Z. 227–250) auf den **Rohstring und die percent-decodierte Form** aus:
  - Reject bei Zeichen < 0x20 oder Whitespace (tötet TAB-/Newline-Obfuskation, die der WHATWG-Parser still entfernt).
  - Reject bei Scheme-Präfix (`^[a-zA-Z][a-zA-Z0-9+.\-]*:`), dem nicht `//` folgt (tötet `javascript:`, `data:`, `https:host`, `https:/host`, `file:`). Die Verschärfung „`//` statt `/`" wurde gewählt, weil der Browser `https:/evil.test` zu `https://evil.test` kanonisiert — empirisch gegen die reale Assembly verifiziert.
  - Anschließend unverändert: absolute URIs nur bei identischem Scheme+Authority der `BaseUri`.
- **Alle `?back=`-Senken** auf `CardPage.razor`, `ListPage.razor`, `BudgetReport.razor` laufen durch den gehärteten Helper; `GenericCardPage.razor` besitzt keine Senke (Runde-3-Verifikation unverändert gültig).
- **`NavigationManagerExtensionsTests`**: 33 Cases — Accept-Seite enthält ausschließlich legitime interne Ziele (relative Pfade, verschachteltes `?back=`/`%2F`-Encoding, absolute Same-Origin-URL); Reject-Seite deckt alle dokumentierten Bypasses ab (`https:evil.test`, `https:/evil.test`, `java\tscript:`, `java%09script:`, `ht%09tps://`, `https%3Aevil.test`, `data:`, Inline-`\t`/`\n`, Whitespace). Ausgeführt: **33/33 grün**.
- **Legitime Flows ungebrochen:** Alle `?back=`-Erzeuger (`CardPage` Prev/Next/OpenBankContact/NewRule, `ListPage` BudgetReport-Link, `BaseViewModel.SetBackNavigation`) emittieren `EscapeDataString`-kodierte relative Pfade oder absolute `Nav.Uri`-Werte — beide passieren die neue Regel (verifiziert an den Erzeuger-Stellen).

## Verbleibende Hinweise (keine Befunde)

- `BudgetRuleCardViewModel` emittiert `Back`-Payload ohne führenden `/` → landet im ungültigen `list/{Kind}/...`-Fallback — **preexisting**, unverändert, bereits als Issue #599 erfasst (Budgetzweckregeldetail Zurück-Route).
- `scripts/screenshots/out-review/` + `fm-*-shots.js` sind untracked Arbeitsartefakte (Nachweise) — werden beim Commit nicht gestaged.
