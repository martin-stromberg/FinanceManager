# Plan-Prüfung – Issue #593: „Zurücksetzen"-Aktion auf acht Karten-Detailansichten

**Status: `Plan vollständig`** (mit einer dokumentierten, bewusst getroffenen Scope-Abweichung — siehe Kritischer Punkt 1)

Geprüfte Eingaben: `requirement.md`, `inventory.md` inkl. `inventory/in-scope-viewmodels.md`, `inventory/reference-and-basetypes.md`, `inventory/tests-resources-excluded.md`, `plan.md`. Stichproben wurden zusätzlich direkt am Quellcode verifiziert (s. Abschnitt „Code-Verifikation").

---

## 1. Abdeckung der Anforderung

| Anforderung (requirement.md) | Plan | Bewertung |
|---|---|---|
| `UiRibbonAction("Reset", …)` in `Ribbon_Group_Manage` direkt hinter `Save`, je acht ViewModels | §„Änderungen an bestehenden Klassen" 1–8 mit konkreten Insert-Positionen | erfüllt |
| `Small`, Icon `icons/sprite.svg#undo` (relativ), Label `Ribbon_Reset`, `Disabled = !HasPendingChanges` | Scope-Klärung-Tabelle + gemeinsames Muster Z. 76–78 | erfüllt |
| `ClearPendingChanges()` + `CardRecord`-Neuaufbau aus ViewModel-Baseline, kein Server-Reload des Datensatzes | Programmablauf §„Reset-Fluss" + Baseline je VM | erfüllt |
| `try/catch`-tolerant, `null`-Guard auf Baseline, `RaiseStateChanged()` am Ende | Gemeinsames Muster + Scope-Klärung „Fehlende Objekte" | erfüllt |
| Neuanlage-Modus: initialer Zustand (leer/`InitPrefill`), kein API-Aufruf | Pro VM dokumentiert (Kontakt Z. 85, Sparplan Z. 90, Kategorien Z. 96/108, Wertpapier Z. 102, Budgetzweck Z. 113, Budgetregel Z. 118); MCP ohne Neuanlage-Modus korrekt erkannt | erfüllt |
| `SavingsPlanCardViewModel`: `Model` ist Eingabe-mutiert → Neuableitung aus `_loadedDto` bzw. initialer `SavingsPlanCreateRequest` | Designentscheidung 5 — deckt sich exakt mit `LoadAsync`-Muster (verifiziert, s. u.) | erfüllt |
| `SavingsPlanCategoryCardViewModel`/`SecurityCategoryCardViewModel`: Baseline des zuletzt persistierten Stands trotz Vorab-Mutation in `SaveAsync` | Designentscheidung 2: Snapshot/Rollback im Save-Pfad — erfüllt das Akzeptanzkriterium auf dem Fehlerpfad ohne zusätzlichen `_loaded*`-Zustand; als Alternative benannt | erfüllt |
| `McpAccessCardViewModel`: Reset nur Kartenfelder, Scope-Matrix/Grants/Filter unberührt (Sofort-Persistierung akzeptiert) | Designentscheidung 6 + §8 (Z. 120–124); `Disabled = Access is null \|\| !HasPendingChanges` als sinnvolle Ergänzung des Referenzmusters | erfüllt |
| Verbindliche Nebenpflicht: Folge-Issue in `martin-stromberg/FinanceManager` (Aufräumen KI-Zugangsansicht, Verweis auf #593) | Umsetzungsreihenfolge Schritt 7 mit konkretem `gh issue create`-Kommando und Inhaltsbeschreibung | erfüllt |
| Offene Fragen 1–4 konsistent entscheiden | Alle vier als Designentscheidungen 1/2+3/4/8 getroffen und begründet; Abschnitt „Offene Punkte" bilanziert korrekt | erfüllt |

## 2. Scope-Ausschlüsse

Alle acht Ausschlüsse aus `requirement.md` sind im Plan als Scope-Tabelle gespiegelt und mit Status „bestätigt" versehen:

- `UserCardViewModel`, `ContactGroupCardViewModel`, `BudgetCategoryCardViewModel`, `StatementDraftCardViewModel`, `StatementDraftEntryCardViewModel`: keine Änderung — korrekt übernommen.
- Keine resx-/Icon-/API-/Entity-/Migrations-/Backup-Änderungen: übernommen; `Ribbon_Reset` und `#undo` sind laut Inventory vorhanden (resx ~Z. 306, `sprite.svg` Z. 41).
- Keine Änderung an Ribbon-Infrastruktur/`BaseCardViewModel`: übernommen.
- Kein `ReloadAsync`/`InitializeAsync` im Reset-Pfad (kein Datensatz-GET): explizit als Ausschluss 7 festgehalten — wichtig, da `ReloadAsync` (z. B. `ContactCardViewModel` Z. 216–219) einen Server-Call auslösen würde.
- Zusätzlich sinnvoll ergänzt: `SetupCardViewModel.ResetAll` bleibt unverändert (Schutz der `SetupCardViewModelTests`-Assertion „genau eine Reset-Aktion").

## 3. Test- und E2E-Nachweis

- **Unit-Tests:** Pro betroffenem ViewModel definiert (Ribbon-Shape hinter `Save`, `Small`, `undo`-Icon, Label, kein `MobileShortcut`; Disabled-Umschaltung; Verwerfen; Neuanlage; kein Datensatz-Reload via `Times.Once`/`Times.Never`; Fehlschlag-Save-Baseline für die drei Rollback-VMs; MCP-`Times.Never` auf `Mcp_Set*Async`; `null`-Baseline-Guard). Zusätzlich optionaler parametrierter Konsistenztest über alle acht VMs. Testinfrastruktur (`CreateVm`, `FindRibbonAction`, Callback-Invokation, `DummyLocalizer`, `IStringLocalizer<>`-Registrierung für MCP) ist inventory-belegt vorhanden.
- **Bestehende Tests:** Bruchanalyse plausibel — kein Test enumeriert die Manage-Liste exakt; `RibbonConfigTests` (nur Linked-Items), `SecurityCategoryDetailViewModelTests` (`Save` per `First`), `SavingsPlanCardLinkTests` (Tab-Titel-Sequenz), `SetupCardViewModelTests` (nur Setup-Aggregat) bleiben unberührt.
- **E2E-Tests:** Vier Pflicht-Szenarien (Kontakt-Edit Happy Path inkl. Disabled-Umschaltung, Sparplan mit `Model`-Besonderheit, Kontakt-Neuanlage, MCP-Sonderfall mit Admin-Enablement) — decken die kritischen Akzeptanzszenarien am lebenden System ab; Screenshot-Artefakte via `PlaywrightBrowserSession`-Dispose; Selektor `button#Reset` ist korrekt (`Ribbon.razor` Z. 70: `id="@item.Id"`).
- **Reale End-to-End-Abnahme:** Als Pflicht-Schritt 8 mit Dokumentation in `test-results.md` und Eskalationspfad vorgesehen — erfüllt die Projektregel.
- **Akzeptanzszenarien-Tabelle:** Alle sieben Szenarien mit jeweils konkretem Nachweis (Unit/E2E/Screenshot/reale Abnahme) versehen.

## 4. Code-Verifikation (Stichproben)

| Prüfpunkt | Befund |
|---|---|
| `BankAccountCardViewModel` Reset-Referenz | Z. 291: `Small`, `undo`-Icon, `!HasPendingChanges`, `{ MobileShortcut = true }` — wie beschrieben |
| `ContactCardViewModel` Manage-Liste | `Save` endet Z. 289, `Delete` Z. 290 — Insert-Position korrekt; `Contact` wird nach erfolgreichem Save aktualisiert (Z. 277/287) → Baseline-Anforderung „zuletzt gespeicherter Stand" gegeben |
| `SavingsPlanCardViewModel` `Model`-Ableitung | `LoadAsync` Z. 144–145 (Neuanlage mit `InitPrefill`, `SavingsPlanType.OneTime`) und Z. 160 (Edit aus `dto`) — Plan-Muster deckt sich exakt |
| `SecurityCardViewModel` Save-Struktur | `ApplyPendingValuesToModel()` Z. 211 vor API-Aufruf; Fehlschlag-Pfade Z. 218–224/236–242/`catch` Z. 251–256 — Snapshot/Rollback-Ansatz passt; `Security` als Baseline wird in `LoadAsync`/`SaveAsync` gesetzt |
| Ribbon-Button-ID für E2E | `Ribbon.razor` Z. 70 `id="@item.Id"` → `button#Reset` gültig; Disabled-Re-Evaluation pro Render (Z. 163–170) bestätigt den Disabled-Umschaltzyklus |
| Card-Routen | `CardPage.razor` deckt `card/{Kind}/new`, `card/{Kind}/{Id}` und `card/{Kind}/{SubKind}/{Id}` ab — alle geplanten E2E-Routen valide |
| Testdateien | Alle benannten Testklassen existieren (`ContactCardViewModelTests`, `SecurityCardViewModelTests`, `McpAccessCardViewModelTests`, `SavingsPlanCardLinkTests`, `SavingsPlanCategoryDetailViewModelTests`, `SecurityCategoryDetailViewModelTests`, `AccountDetailViewModelTests`); E2E-Infrastruktur (`PlaywrightWebAppFixture`, `TestUserSeeder`, `AuthGateway`, `BrowserApiHelper`) vorhanden |

## 5. Kritische Punkte / Abweichungen

1. **Scope-Abweichung `BankAccountCardViewModel` (MobileShortcut-Entfernung):** `requirement.md` beschreibt die Referenzimplementierung explizit als „unverändert" (Abschnitt „Betroffene Klassen"). Der Plan ändert sie dennoch: `{ MobileShortcut = true }` an der `Reset`-Aktion (Z. 291) wird entfernt (Designentscheidung 1, Umsetzungsschritt in §9). **Einordnung:** Die Abweichung ist transparent dokumentiert und gut begründet — die AGENT.md-Regel („primäre Aktion erhält `MobileShortcut`", hier `Save`), die requirement-eigene Präferenz („kein `MobileShortcut` auf `Reset`") und die Rendering-Konsequenz (`Ribbon.razor` Z. 95–113: alle `MobileShortcut && !Disabled`-Items werden Header-Shortcuts) sprechen dafür; offene Frage 1 delegierte die „konsistente" Entscheidung zudem an die Planung. Dennoch geht der Plan über den Wortlaut „unverändert" hinaus. **Empfehlung:** Entscheidung beibehalten, aber bei der Umsetzung/dem PR explizit benennen (z. B. im PR-Body), damit die Scope-Erweiterung für den Reviewer nachvollziehbar ist. Kein Test- oder Funktionsrisiko (kein Test assertiert `MobileShortcut` auf dieser Aktion).
2. **Bekannte, unbehandelte Eigenheit `SecurityCardViewModel`:** `SaveAsync` ruft nach erfolgreichem Speichern kein `ClearPendingChanges()` auf (inventory §4, Code Z. 204–261 verifiziert) — `HasPendingChanges` bleibt ggf. `true`, `Reset` bleibt nach dem Speichern aktiviert und verwirft dann korrekt auf den gerade gespeicherten Stand. Fachlich unschädlich und nicht Teil dieses Issues; im Plan nicht explizit erwähnt, aber ohne Handlungsbedarf.
3. **Geringfügige Mehrdeutigkeit:** Testplatzierung für `SavingsPlanCardViewModel` ist offen formuliert („`SavingsPlanCardLinkTests` erweitern **oder** neue `SavingsPlanCardViewModelTests`"). Kein Mangel — Entscheidung bei der Umsetzung vertretbar.

Keine dieser Punkte stellt eine inhaltliche Lücke dar: Scope, Ausschlüsse, Baseline-Semantik, Fehler-/Neuanlage-Pfade, MCP-Sonderfall, Folge-Issue und der geforderte Test-/E2E-Nachweis sind vollständig und code-verifiziert abgedeckt.

## 6. Fazit

Der Plan deckt die Anforderung vollständig ab: alle acht In-Scope-ViewModels mit korrekten Insert-Positionen und Baselines, alle Scope-Ausschlüsse eingehalten, die drei Baseline-Sonderfälle (Sparplan-`Model`, Kategorie-Vorab-Mutation, Wertpapier-`Model`-Fallback) sauber als Designentscheidungen gelöst, der MCP-Sonderfall inkl. verbindlichem Folge-Issue geplant, und der Test-/E2E-Nachweis (Unit je VM + Konsistenztest + vier Playwright-Szenarien mit Screenshot-Artefakten + reale Abnahme mit `test-results.md`) ist vollständig spezifiziert. Die einzige Abweichung (MobileShortcut-Entfernung an der Referenz) ist bewusst, regelkonform und dokumentiert — sie ist als Hinweis, nicht als Lücke zu werten.

**Status: `Plan vollständig`**
