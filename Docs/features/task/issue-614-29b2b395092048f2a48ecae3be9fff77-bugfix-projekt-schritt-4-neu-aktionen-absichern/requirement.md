# Anforderung — Schritt 4: „Neu"-Aktionen bei ungültigem Kontext absichern (#605, #609)

Projektschritt 4 des Bugfix-Projekts #614. Zwei verwandte Defekte: Neuanlage-Aktionen sind in Zuständen ausführbar, in denen sie sinnlos bzw. abstürzend sind.

## #605 — KI-Zugänge: „Neu" bei deaktivierter KI-Integration sperren

**Ist:** Bei `McpSettings.Enabled = false` zeigt `/list/mcp-accesses` den Hinweis `Mcp_Disabled_Hint`, aber die Ribbon-Aktion „Neu" bleibt aktiv und öffnet `McpAccessCreateWizard`; der Wizard läuft komplett durch und `POST api/mcp-accesses` legt einen toten Zugang an (`/mcp` liefert 503). Anwender merkt es erst am Ende.

**Soll:** Bei deaktivierter Integration kein Wizard-Start aus der Übersicht — Aktion deaktiviert oder frühzeitiger Hinweis statt Wizard. Listenhinweis bleibt. Bei aktiviertem Feature unverändert (inkl. Mobile-Shortcut).

**Offene Punkte / Entscheidungen:**
- Variante lt. Issue frei: **Ribbon „New" deaktiviert + Tooltip-Hinweis + Guard in `RequestCreateWizard()` + Hinweis im Wizard** (Defense in depth, deckt `IsMcpDisabled`-Latenz vor erstem Load und den SetupMcpTab-Pfad ab).
- Umschalten bei geöffneter Seite: tolerierbar (Status wird beim nächsten Load erneut gelesen); Wizard prüft Status beim Öffnen selbst.
- Serverseitige Sperrung von `POST api/mcp-accesses`: bewusst außerhalb des Scopes (Verwaltung im deaktivierten Zustand ist fachlich gewollt — Issue nennt Folge-Issue-Option).

## #609 — Budgetzweckregelliste: „Neu" wirft Exception bei ungespeichertem Budgetzweck

**Ist:** Neuer, ungespeicherter Budgetzweck (`BudgetPurposeId == Guid.Empty`, `BudgetCategoryId == null`) → Klick auf „Neu" in der eingebetteten Regelliste wirft `InvalidOperationException` (`BudgetCategoryId!.Value` auf null) statt Navigation.

**Soll:** Kein Absturz; Aktion im Zustand nicht verfügbar (deaktiviert) oder verständliche Rückmeldung. Bei gespeichertem Zweck/Kategorie unverändert (`purpose,{id}` / `category,{id}` → `card/budget/rules/new?prefill=…&back=…`).

**Offene Punkte / Entscheidungen:**
- Variante lt. Issue frei: **`UiRibbonAction.Disabled` + Tooltip „zuerst speichern"** (sichtbar erklärend statt versteckt; Ausblenden der kompletten Sektion wie bei Budgetkategorie wäre invasiver und inkonsistent — die Regelliste bleibt für den Workflow sichtbar).
- Payload-Berechnung zusätzlich null-sicher (kein `!.Value` ohne Prüfung; Guard-Return, da `Disabled` den Callback ohnehin blockt).

## Akzeptanzkriterien

- [ ] `#605`: Bei deaktivierter KI-Integration kein Wizard-Start aus `/list/mcp-accesses` (Aktion deaktiviert + Hinweis im Wizard bei direktem Öffnen); `Mcp_Disabled_Hint` in Liste unverändert; bei aktiviertem Feature unverändert inkl. Mobile-Shortcut.
- [ ] `#605`: `RequestCreateWizard()` weist den Request bei `IsMcpDisabled` ab (deckt SetupMcpTab-Pfad mit ab).
- [ ] `#609`: Klick auf „Neu" wirft bei ungespeichertem Budgetzweck keine Exception; Aktion ist disabled.
- [ ] `#609`: Bei gespeichertem Zweck und bei Kategorie unverändertes Payload-Verhalten.
- [ ] Beide durch VM-Tests abgesichert; UI-Nachweis via E2E-Screenshot.
