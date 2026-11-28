# Code-Review — Schritt 4 „Neu"-Aktionen absichern (#605, #609)

**Reviewer:** unabhängiger Review-Subagent (Read-only), Befund gegen Commit-Stand + Arbeitsstand.
**Gesamturteil:** Solide Defense-in-depth-Umsetzung, keine kritischen Befunde.

## Befunde und Auflösung

| # | Schwere | Befund | Auflösung |
|---|---------|--------|-----------|
| M1 | mittel | Wizard fail-open bei Status-Fehler: `catch → _disabled=false` zeigte halb-leeren Wizard (Levels/Options würden ebenfalls fehlschlagen) | `catch` setzt jetzt `_error` (Api.LastError ?? ex.Message) — bestehende Error-Box zeigt den Fehler; bUnit-Test `StatusCallFails_ShowsErrorMessage` ergänzt |
| M2 | mittel | `IsMcpDisabled=false` vor erstem Load → „New" kurz enabled | Bewusst fail-open (Wizard prüft Status selbst als zweite Verteidigungslinie); XML-Dokument am Property dokumentiert das Design |
| L1 | niedrig | `!`-Operator in `LoadPageAsync` + null/Empty-Logik an vier Stellen dupliziert | `HasContext`-Helper konsolidiert alle Prüfungen; `BudgetCategoryId!.Value` → `GetValueOrDefault()` |
| L2 | niedrig | `noContext` vs. Payload-Logik doppelt ausgewertet | Beide nutzen jetzt `HasContext` — strukturell äquivalent |
| L3 | niedrig | E2E-`title`-Assert quasi-vacuous (`GetItemTitle` fällt auf Label zurück → immer non-empty) | Asserts prüfen jetzt konkreten Tooltip-Text (de/en-Alternation via Regex) |
| L4 | niedrig | Fehler-Pfade ungetestet (Status wirft; List-Call wirft `Err_Mcp_Disabled`) | Zwei Unit-Tests ergänzt: best-effort-Pfad + Err_Mcp_Disabled-Fallback mit Hint-Record |
| L5 | niedrig | `Mcp_Disabled_Hint` als Button-Tooltip sehr lang, admin-zentriert | Bewusst unverändert — derselbe Text steht bereits als Listen-Hint; kein Konventionsverstoß |

## Verifizierte Punkte (kein Befund)

- Disabled blockt Callback zusätzlich in `Ribbon.razor` (`OnItemClick` Guard) — Force-Klick im E2E doppelt abgesichert.
- `InitializeForPurposeAsync(Guid.Empty)` → `noContext` beim ersten Render — kein Race-Fenster bei NewRule.
- TOCTOU im Wizard (Deaktivierung zwischen Status-Check und Create) → `CreateAsync` zeigt `Err_Mcp_Disabled` lokalisiert.
- `RequestCreateWizard`-Guard deckt den `SetupMcpViewModel.RequestNewAccess()`-Pfad mit ab.
- AGENT.md-Konventionen: resx de+en+neutral, keine hartcodierten Strings, sprite.svg-Icons, Ribbon-Gruppierung konform.
- Mobile-Pfad: identisches `disabled`-Attribut; Mobile-Shortcuts filtern `!item.Disabled`.

## Ergebnis

Alle mittleren und relevanten niedrigen Befunde umgesetzt; L5 bewusst zurückgestellt. Tests: 6 VM-Tests + 4 bUnit-Tests + 4 E2E-Tests — alle grün.
