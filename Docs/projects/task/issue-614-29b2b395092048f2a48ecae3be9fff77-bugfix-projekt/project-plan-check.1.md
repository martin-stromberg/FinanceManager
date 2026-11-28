# Projektplan-Prüfung (Durchlauf 1, archiviert)

## Ergebnis

**Status:** Projektplan lückenhaft

Der Plan deckt alle zwölf Teil-Issues grundsätzlich ab und respektiert die Nicht-Ziele. Zwei Punkte sind jedoch materiell unterspezifiziert bzw. bergen ein bestätigtes Regressionsrisiko:

1. **Schritt 1:** Die Payload-Erkennungsregel ist ambig formuliert. Der Code bestätigt, dass `PostingsCardViewModel.cs:124–134` `Back`-Payloads wie `account/{guid}` sendet — einteilig, aber **mit** `/`. Wird die Regel als „enthält `/` ohne `:`" umgesetzt (die Formulierung „bzw. enthalten `/`" lässt das zu), navigiert Zurück nach `/account/{guid}` statt `list/postings/account/{guid}` — die Zurück-Navigation der Buchungskarte wäre kaputt. `IsSameOriginUri` (`NavigationManagerExtensions.cs:186`) akzeptiert relative Pfade, die Validierung würde das nicht abfangen. Die Regel muss auf eine Präfix-Whitelist (`card/`, `list/`, `reports/`) eingegrenzt und der Postings-Fall in die Regressionskriterien aufgenommen werden.
2. **Schritt 2:** `AddIntervalWithMonthEndRule` (`SavingsPlan.cs:205`) ist `private static` mit Signatur `(DateTime, SavingsPlanInterval)` — das Intervall kommt aus dem Enum und kann **nur vorwärts** addieren. Für `TargetDate − n·Interval` (Vergangenheits-Fälligkeiten, explizit gefordert „in beide Zeitrichtungen") reicht das Sichtbarmachen allein nicht; der Plan benennt weder das Verfahren (Richtungsparameter/Subtrakt-Variante vs. Anker-Iteration) noch eine konkrete Abbruchbedingung („sinnvolle Begrenzung" ist keine Lifecycle-taugliche Spezifikation). Zudem ist die Monatsend-Rückrechnung wegen Tag-Clamping verlustbehaftet mehrdeutig.

## Abgleich Anforderung ↔ Entwicklungsschritte

| Issue | Schritt | Abgedeckt | Bemerkung |
|---|---|---|---|
| #599 | 2 | teilweise | Lücke: Recurring-Rückrechnung nicht implementierbar beschrieben |
| #600 | 1 | ja | CardPage-seitiges `?back=`-Anhängen korrekt |
| #601 | 2 | ja | |
| #602 | 1 | teilweise | Erkennungsregel ambig; Präzedenz vs. `?back=` nicht festgelegt |
| #603 | 3 | ja | Anker existiert immer; resx-Keys vorhanden |
| #604 | 2 | ja | |
| #605 | 4 | ja | |
| #606/#607/#608 | 1 | ja | abhängig von #602-Klärung |
| #609 | 4 | ja | |
| #610 | 5 | ja | |

## Abhängigkeitsprüfung

Alle fünf Schritte deklarieren „Keine" Abhängigkeiten — zyklenfrei und konsistent. Keine Befunde.

## Fehlende oder unvollständige Punkte

1. **Schritt 1 — Payload-Regel festnageln (blockierendes Regressionsrisiko):** geschlossene Präfix-Whitelist (`card/`, `list/`, `reports/`) statt „enthält `/`"; `account/{guid}`-Format in Regressionskriterien.
2. **Schritt 1 — Präzedenz neuer Check vs. `?back=`:** Position des App-relativ-Checks festlegen (Payload-Direktziel vs. `?back=`).
3. **Schritt 2 — Rückwärts-Fälligkeiten konkretisieren:** Verfahren (Richtungsparameter/Subtrakt-Variante) und Abbruchregel benennen.
4. **Schritt 1 — Detail:** `InitBack`-Payloads können selbst `?back=…` enthalten; Query-Teil muss mitgenommen werden.
5. **Projektkriterium PR:** Delivery-Schritt fehlt in der Tabelle.
6. **Schritt 5 — Umfang:** „E2E-Screenshot-Nachweis" als Schritt-Muss überdimensioniert; gebündelt auf Projektebene.

## Hinweise

- `BudgetCategoryListViewModel.cs:94`: Planeinschätzung korrekt — `ListPage.razor` ignoriert Payloads.
- Schritt 3 lifecycle-tauglich (Anker immer gesetzt).
- resx `Ribbon_NextMonth`/`Ribbon_NextYear`/`Mcp_Disabled_Hint` vorhanden; #609-Tooltip braucht neue Keys in allen drei resx.
