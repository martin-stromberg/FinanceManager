# Umsetzungsplan – Schritt 1: Back-Navigation

## Scope-Klärung

- **Verarbeitungseinheit:** `Back`-UiAction der `CardPage` + `OpenCategory`-UiAction;
  Auslöser = Ribbon-Klick des Anwenders; Wirkung = Navigation zur korrekten Route.
- **Scope-Ausschlüsse:** `ListPage`-Payload-Auswertung bleibt unverändert (kein Bug dort);
  `BudgetCategoryListViewModel`-Payload bleibt unangetastet. Status: bestätigt (Projektplan,
  geprüft durch `project-plan-check.md`).

## Änderungen

### A) `FinanceManager.Web/Components/Pages/CardPage.razor` — `case "Back"`

Neuer Prüfschritt **nach** der `?back=`-Auswertung (Z. 146–150) und **vor** dem GUID-
Sonderfall (Z. 154):

```csharp
// App-relative Pfad-Payloads (closed prefix whitelist) navigate directly,
// keeping any query part (e.g. nested ?back= chains).
if (!string.IsNullOrWhiteSpace(payload)
    && (payload.StartsWith("card/", StringComparison.Ordinal)
        || payload.StartsWith("list/", StringComparison.Ordinal)
        || payload.StartsWith("reports/", StringComparison.Ordinal))
    && !payload.Contains(':'))
{
    NavigationManager.NavigateTo(payload);
    break;
}
```

- `:`-Ausschluss verhindert Schema-/Host-Payloads (`card:x`, `javascript:` beginnt ohnehin
  nicht mit den Präfixen); `//`-Start kann durch die Präfixe nicht auftreten.
- Query-Teil bleibt automatisch erhalten (`NavigateTo(payload)`).
- Bestehende Reihenfolge sonst unverändert → alle Bestandsformate bleiben funktionsfähig.
- `BudgetPurposeCardViewModel.cs:481` („Category"-Aktion sendet `Back` mit
  `card/budget/categories/{id}`): durch Whitelist korrekt. Bekannte Trade-off: bei gesetztem
  `?back=` auf der Purpose-Karte gewinnt `?back=` (dokumentierte Entscheidung, s.
  project-plan-check.md Hinweis).

### B) `CardPage.razor` — `case "OpenCategory"` (Z. 257–259)

`?back=` anhängen, Muster `OpenBankContact` (Z. 233–243):

```csharp
case "OpenCategory":
    if (!string.IsNullOrWhiteSpace(payload))
    {
        var back = BuildCardPath(Kind, SubKind, Id.ToString(), Back);
        NavigationManager.NavigateTo($"{payload}?back={Uri.EscapeDataString(back)}");
    }
    break;
```

- `Id` kann bei „new"-Karten `Guid.Empty` sein — `BuildCardPath` baut dann
  `card/{kind}/{sub?}/00000000-…`; fachlich vertretbar (Kategorie-Öffnen nur bei
  gespeicherter Karte möglich, da `Model.CategoryId` vorhanden sein muss). Falls `Id`
  leer/nicht parsebar: defensiv ohne `?back=` navigieren? — Entscheidung: `?back=` nur
  anhängen, wenn `Id != Guid.Empty`, sonst Payload unverändert (kein sinnloser Back-Pfad).
- Achtung: `payload` könnte theoretisch schon `?` enthalten — nur `?back=` anhängen, wenn
  `payload` kein `?` enthält, sonst `&back=`. (Defensiv, tritt aktuell nicht auf.)

### C) Tests — `FinanceManager.Tests/Components/CardPageTests.cs` (oder neue Testklasse
`CardPageBackNavigationTests.cs`)

bUnit: `CardPage` mit `Mock<IApiClient>` rendern, Ribbon-„Back"-Button klicken
(`cut.FindAll("button").First(b => b.Id == "Back")`), Ziel aus `FakeNavigationManager`
(`Services.GetService<FakeNavigationManager>().Uri`) assertieren. Fälle:

1. VM mit `Back`-Payload `card/setup` (MCP-Ansicht simulieren — direktes
   `RaiseUiActionRequested` über die Testkarte ist schwierig; Alternative: das
   Back-Handling liegt in `CardPage.OnUiActionRequested` — über bUnit wird das Event
   durch das echte VM ausgelöst. Pragmatischer Weg: Payload-Erkennung als private
   Hilfslogik ist schwer isoliert zu testen → stattdessen bUnit-Test mit einem
   List-/Card-VM, das den Payload sendet; falls zu schwer, Extraktion der
   Zielermittlung in eine `internal static`/testbare Methode erwägen —
   Entscheidung im Implementierungsschritt, vorzugsweise minimal-invasiv:
   kleine `internal static bool IsAppRelativePathPayload(string)` in CardPage
   + `InternalsVisibleTo`? Prüfen, ob `FinanceManager.Tests` Internals sehen kann —
   sonst Methode `public` mit XML-Doku oder Test über bUnit-Render mit VM-Double.)
2. Regressionsfälle: Payload `account/{guid}` → `list/postings/account/{guid}`;
   `card,{id}` → `card/{Kind}/{id}`; leerer Payload → `BuildListPath()`; `?back=` gewinnt.
3. `OpenCategory`: Nach Klick navigiert Ziel enthält `?back=` mit escapeter Karten-URL;
   mit gesetztem `?back=` der Ursprungskarte wird die Kette genestet.

   → Konkrete Umsetzung der Tests im Implementierungsschritt festlegen: bevorzugt
   bUnit über echte VMs (z. B. `McpAuditViewModel` via Factory?— Routing ok), sonst
   gezielte Extraktion einer testbaren statischen Hilfsmethode für die
   Pfad-Payload-Erkennung + Integrationstest des OpenCategory-Falls.

## Akzeptanzszenarien (Happy Paths)

1. Regelkarte ohne `?back=` öffnen → „Zurück" → URL endet auf
   `card/budget/purposes/{id}` (nicht `list/budget/...`).
2. MCP-Audit `/card/mcp-audit` → „Zurück" → URL endet auf `card/setup`.
3. MCP-Audit mit `?back=card/mcp-accesses/{id}` → „Zurück" → Ziel = `?back=`-Wert.
4. Sparplankarte → „Kategorie" → Kategoriekarte-URL enthält `?back={sparplankarte}`;
   dort „Zurück" → Sparplankarte.
5. Postings-Karte → „Zurück" (Payload `account/{guid}`) → `list/postings/account/{guid}`.
6. Regel löschen → `NavigateBackExtended`-Payload `card/budget/purposes/{id}` →
   Purpose-Karte.

## Offene Punkte

Keine.
