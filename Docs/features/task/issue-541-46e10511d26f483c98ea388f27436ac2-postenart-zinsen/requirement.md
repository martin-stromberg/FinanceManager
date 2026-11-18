# Übersetzte Anforderung

## Metadaten

| Feld | Wert |
|------|------|
| Issue | #541 (Follow-up-Kommentar zu PR #542) |
| Branch | `task/issue-541-46e10511d26f483c98ea388f27436ac2-postenart-zinsen` |
| Erstellt | 2026-11-18 |
| Kurztitel | Kontierungsregeln-Panel als Overlay mit Ribbon-Aufruf und Symbolbuttons |

## Ziel / Ausgangslage

Mit Issue #541 (PR #542, offen gegen staging) wurden Kontierungsregeln (PostingRules) für Bankkonten eingeführt. Die Verwaltung erfolgt derzeit über das Panel `FinanceManager.Web/Components/Shared/PostingRulesPanel.razor`, das dauerhaft unterhalb der Karte auf der Bankkonto-Seite (`CardPage.razor`, Zeilen 46–48) eingebettet ist und textbasierte Buttons (`btn btn-sm`) für die Aktionen Bearbeiten, Entfernen, Abbrechen sowie Hinzufügen/Speichern verwendet.

Der Kunde wünscht zwei UI-Nachbesserungen:

1. Die Buttons im Kontierungsregeln-Panel sollen durch schickere Symbolbuttons ersetzt werden.
2. Das Kontierungsregeln-Panel soll nicht mehr eingebettet, sondern als Overlay-Panel angezeigt werden, das über einen Aktionsbutton in der Ribbon-Gruppe „Buchungen" geöffnet wird.

Es handelt sich um eine reine Darstellungs-/Bedienungsänderung; die fachliche Funktion der Kontierungsregeln bleibt unverändert.

## Fachliche Anforderungen

### FA-1 Symbolbuttons im Kontierungsregeln-Panel

Die vorhandenen Textbuttons im Panel sind durch Symbolbuttons (Icon-Buttons mit SVG-Icon aus `/icons/sprite.svg`, entsprechend dem in der Anwendung üblichen Erscheinungsbild, z. B. `icon-btn`) zu ersetzen. Betroffen sind:

- „Bearbeiten" und „Entfernen" je Regelzeile
- „Abbrechen" (nur im Bearbeitungsmodus sichtbar) sowie „Hinzufügen"/„Speichern" in der Eingabezeile

Die Bedeutung jeder Schaltfläche muss weiterhin erkennbar sein; da die Buttons keinen Text mehr tragen, ist ein Tooltip (Titel-Attribut) mit dem lokalisierten Beschriftungstext vorzusehen. Funktion, Disabled-Verhalten und Sichtbarkeitsregeln (z. B. Abbrechen nur beim Bearbeiten) bleiben unverändert.

### FA-2 Overlay-Panel statt eingebetteter Anzeige

Das Kontierungsregeln-Panel wird auf der Bankkonto-Karte nicht mehr dauerhaft angezeigt. Stattdessen öffnet es sich bei Bedarf als Overlay-Panel (über dem Karteninhalt liegender Dialog) und kann vom Benutzer wieder geschlossen werden. Das eingebettete Rendering in `CardPage.razor` entfällt; sonstige eingebettete Panels (z. B. `LinkedIbansPanel`) bleiben unverändert.

### FA-3 Aktionsbutton in der Ribbon-Gruppe „Buchungen"

In der bestehenden Ribbon-Gruppe „Buchungen" (`Ribbon_Group_Bookings`) der Bankkonto-Karte – die bereits die Aktionen „Vorläufige Buchungen erfassen" und „Postenarten neu bewerten" enthält – ist ein zusätzlicher Aktionsbutton (z. B. „Kontierungsregeln") mit passendem Icon und lokalisiertem Label (de/en) zu ergänzen, der das Overlay-Panel öffnet. Die Positionierung innerhalb der Gruppe folgt der in `AGENT.md` festgelegten Gruppierung (größere Aktionen mit dem Datensatz).

### FA-4 Verfügbarkeit analog zum bisherigen Verhalten

Das Öffnen des Panels ist – wie bisher die Einblendung des eingebetteten Panels – nur für bereits gespeicherte Bankkonten sinnvoll (bisherige Bedingung: `Account?.Id != Guid.Empty`); für ein neues, ungespeichertes Konto ist der Ribbon-Button nicht verfügbar (deaktiviert bzw. ohne Funktion, siehe Offene Fragen).

### FA-5 Funktionserhalt

Alle bisherigen Funktionen des Panels bleiben im Overlay erhalten und verhalten sich fachlich identisch:

- Auflisten der Regeln (Feld, Muster, Postenart, Reihenfolge)
- Anlegen einer neuen Regel inkl. Vorprüfung auf leeres Muster und Fehleranzeige im Panel
- Bearbeiten einer bestehenden Regel (Übernahme der Werte in die Eingabezeile, Abbrechen möglich)
- Löschen einer Regel
- Enter-Taste in der Muster-Eingabe löst Speichern aus

### FA-6 Lokalisierung

Alle neuen bzw. geänderten UI-Texte (Ribbon-Button-Label, Overlay-Titel, Tooltips der Symbolbuttons) werden in `Resources/*.resx` für Deutsch und Englisch gepflegt; keine hartkodierten Strings.

## Akzeptanzkriterien

1. Im Kontierungsregeln-Panel werden alle Aktionen als Symbolbuttons mit Icon und lokalisiertem Tooltip dargestellt; keine Textbuttons mehr.
2. Das Kontierungsregeln-Panel ist auf der Bankkonto-Karte nicht mehr dauerhaft sichtbar.
3. Ein neuer Aktionsbutton in der Ribbon-Gruppe „Buchungen" öffnet das Kontierungsregeln-Panel als Overlay über dem Karteninhalt.
4. Das Overlay lässt sich vom Benutzer wieder schließen; der Karteninhalt bleibt darunter erhalten.
5. Regeln lassen sich im Overlay weiterhin auflisten, anlegen, bearbeiten und löschen; Validierungs- und Fehlerverhalten entsprechen dem bisherigen Panel.
6. Der Ribbon-Button ist bei einem neuen, noch nicht gespeicherten Bankkonto nicht funktional (deaktiviert/ausgeblendet).
7. Alle neuen UI-Texte sind in Deutsch und Englisch lokalisiert.
8. Andere Bereiche der Bankkonto-Karte (Karte, `LinkedIbansPanel`, übrige Ribbon-Gruppen und -Aktionen) sind unverändert.

## Offene Fragen

1. **Verfügbarkeit des Ribbon-Buttons:** Soll der Button bei einem neuen, ungespeicherten Konto deaktiviert (grau, klickbar ohne Effekt) oder ausgeblendet werden? (Bisheriges Verhalten: Panel nur bei gespeichertem Konto eingeblendet; deaktiviert entspräche dem Muster der übrigen Ribbon-Aktionen.)
2. **Icon-Auswahl:** Welche vorhandenen Icons aus `/icons/sprite.svg` sollen für den Ribbon-Button und die Panel-Aktionen (Bearbeiten, Entfernen, Abbrechen, Hinzufügen/Speichern) verwendet werden – oder ist ggf. ein neues Symbol im Sprite zu ergänzen?
3. **Overlay-Verhalten:** Soll das bestehende Overlay-Framework (`OverlayHost`/`UiOverlaySpec`, modaler Dialog mit Titelzeile und Schließen-Button, u. a. für Anhänge und Zusammenführen verwendet) genutzt werden, oder ist eine andere Overlay-Form (z. B. Seitenpanel) gewünscht? (Annahme: Wiederverwendung des bestehenden Mechanismus.)
4. **Sichtbarkeit der Regelanzahl:** Ist ein Hinweis auf dem Ribbon-Button oder der Karte gewünscht, dass bereits Kontierungsregeln vorhanden sind (z. B. Anzahl/Badge), da die Regeln nicht mehr permanent sichtbar sind? (Vermutlich Out of Scope, siehe unten.)

## Scope-Ausschluss-Kandidaten

- Änderungen an der Fachlogik oder den APIs der Kontierungsregeln (ViewModel-Methoden `LoadPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync`; Controller-Endpunkte) – die Anforderung ist rein visuell/bedienerisch.
- Umstellung anderer eingebetteter Panels (z. B. `LinkedIbansPanel`, `SavingsPlanLinkedAccountsPanel`) auf Overlays.
- Anzeige eines Zählers/Badges mit der Anzahl vorhandener Regeln auf dem Ribbon-Button oder der Karte (nur falls Offene Frage 4 anders beantwortet wird).
- Änderungen am generischen Overlay-Mechanismus (`OverlayHost`/`UiOverlaySpec`) selbst.
- Anpassungen an Übersichts-/Listenseiten; betroffen ist ausschließlich die Bankkonto-Karte.
