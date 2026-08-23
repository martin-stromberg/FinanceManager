# Anforderung bearbeiten

Dieses Kommando koordiniert die vollständige Bearbeitung einer Kundenanforderung. Es plant und implementiert selbst nichts — alle inhaltlichen Aufgaben werden an Unteragenten delegiert.

Das Kommando kann sowohl für eine neue als auch für eine bereits begonnene Anforderung ausgeführt werden. Im letzteren Fall wird anhand der vorhandenen Artefakte ermittelt, welche Schritte bereits abgeschlossen sind, und der Ablauf wird ab dem ersten noch ausstehenden Schritt fortgesetzt.

**Eingabe:** Die Kundenanforderung wird als Argument übergeben oder folgt direkt nach dem Kommandoaufruf. Bei Fortsetzung einer bereits begonnenen Anforderung kann die Eingabe entfallen, sofern `requirement.md` bereits existiert.

---

## Wichtiger Hinweis vorab

Falls die Ausführung von Unteragenten nicht möglich ist, so führe die in diesem Dokument beschriebenen Schritte nacheinander selbst aus.

## Schritt 1: Branch-Name ermitteln

Führe `git branch --show-current` aus, um den aktuellen Branch-Namen zu ermitteln. Dieser wird als Verzeichnisname verwendet.

Ist der Branch-Name `main`, `master`, `develop` oder `dev`, verweigere die Arbeit: Informiere den Anwender, dass Anforderungen nicht direkt auf einem Hauptbranch bearbeitet werden dürfen, und brich den Ablauf ab.

## Schritt 2: Verzeichnisstruktur vorbereiten

Erstelle das Verzeichnis `docs/features/{branchname}/`, falls es noch nicht existiert. Alle Artefakte dieser Anforderungsbearbeitung werden dort abgelegt.

Erstelle anschließend die Datei `docs/features/{branchname}/todo.md` mit folgender Aufgabenliste (überschreibe sie, falls sie bereits existiert):

```
# Aufgabenliste – Anforderungsbearbeitung

Branch: `{branchname}`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [ ] | 1 | Branch-Name ermitteln | – |
| [ ] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/{branchname}/` |
| [ ] | – | Einstiegspunkt ermitteln | – |
| [ ] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` |
| [ ] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` |
| [ ] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [ ] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [ ] | 5b | Planungscommit | – |
| [ ] | 6 | Implementierung (Unteragent) | Codeänderungen |
| [ ] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [ ] | 8 | Code-Review (Unteragent) | `review-code.md` |
| [ ] | 8b | Tests ausführen (Unteragent) | `test-results.md` |
| [ ] | – | Iteration oder Abschluss entscheiden | – |
| [ ] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [ ] | 9 | Dokumentation erstellen (Unteragent) | `docs/help/` |
| [ ] | 9b | README aktualisieren (Unteragent) | `README.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |
```

Markiere anschließend die Schritte 1 und 2 in `todo.md` als erledigt (`[x]`).

## Einstiegspunkt ermitteln

Prüfe, welche Artefakte unter `docs/features/{branchname}/` bereits vorhanden sind, und bestimme daraus den Schritt, bei dem der Ablauf fortgesetzt wird:

| Bedingung | Einstieg bei |
|-----------|-------------|
| `requirement.md` fehlt | Schritt 3 |
| `requirement.md` vorhanden, `inventory.md` fehlt | Schritt 4 |
| `inventory.md` vorhanden, `plan.md` fehlt | Schritt 5 |
| `plan.md` vorhanden mit ungeklärten Offenen Punkten (Abschnitt „Offene Punkte" nicht leer) | Schritt 5a — offene Punkte anzeigen, Antworten einholen und Planung erneut ausführen |
| `plan.md` vorhanden, `review.md` und `review-code.md` fehlen | Schritt 5b |
| `review.md` mit Status `Offene Aufgaben vorhanden` (ohne `continue.md`) | Schritt 6 |
| `review-code.md` mit Status `Befunde vorhanden` (ohne `continue.md`) | Schritt 6 |
| `continue.md` vorhanden (Schleife wurde zuvor abgebrochen) | Schritt 6 — bearbeite die noch offenen Punkte aus `continue.md`; Dokumentation existiert bereits |
| `review.md` mit `Vollständig umgesetzt` und `review-code.md` mit `Keine Befunde`, Dokumentation fehlt noch | Schritt 9 |
| Dokumentation vorhanden (`docs/help/` enthält Verzeichnis zum Feature) | Kein weiterer Schritt — informiere den Anwender, dass die Anforderung vollständig abgeschlossen ist |

Markiere den Einstiegspunkt-Schritt in `todo.md` als erledigt (`[x]`).

Überspringe alle Schritte vor dem ermittelten Einstiegspunkt.

## Schritt 3: Anforderung übersetzen (Unteragent)

Starte einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/translate-requirements` mit der folgenden Kundenanforderung aus:
>
> {anforderung}
>
> Speichere das Ergebnis als `docs/features/{branchname}/requirement.md`. Die Datei soll ausschließlich den strukturierten Output des Kommandos enthalten (keine zusätzlichen Kommentare).

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 3 in `todo.md` als erledigt (`[x]`).

## Schritt 4: Bestandsaufnahme (Unteragent)

Starte einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/inventory` aus. Die übersetzte Anforderung liegt unter `docs/features/{branchname}/requirement.md`.
>
> Speichere das Ergebnis als `docs/features/{branchname}/inventory.md`. Detaildokumente kommen in das Unterverzeichnis `docs/features/{branchname}/inventory/`. Verlinke alle Detaildokumente im Hauptdokument.

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 4 in `todo.md` als erledigt (`[x]`).

## Schritt 5: Umsetzungsplanung (Unteragent)

Starte einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/plan` aus. Die Eingaben liegen unter:
> - `docs/features/{branchname}/requirement.md`
> - `docs/features/{branchname}/inventory.md` (inkl. Detaildokumente in `docs/features/{branchname}/inventory/`)
>
> Speichere den fertigen Plan als `docs/features/{branchname}/plan.md`.

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 5 in `todo.md` als erledigt (`[x]`).

### Schritt 5a: Prüfschleife offene Punkte

Lies `docs/features/{branchname}/plan.md` und prüfe den Abschnitt „Offene Punkte".

- Ist der Abschnitt leer → Markiere Schritt 5a in `todo.md` als erledigt (`[x]`). Fahre mit Schritt 5b fort.
- Enthält er mindestens einen Eintrag:
  1. Zeige dem Anwender die vollständige Liste der offenen Punkte.
  2. Warte auf seine Reaktion.
     - Signalisiert der Anwender, dass er keine weiteren Antworten geben möchte (z. B. „reicht so", „weiter so", „egal") → Fahre mit Schritt 5b fort.
     - Gibt der Anwender Antworten auf die offenen Punkte → Starte einen weiteren Unteragenten **(Modell: haiku)**:

       > Führe das Kommando `/plan` erneut aus. Berücksichtige dabei die folgenden Antworten auf die offenen Punkte:
       >
       > {antworten des Anwenders}
       >
       > Die übrigen Eingaben liegen unter:
       > - `docs/features/{branchname}/requirement.md`
       > - `docs/features/{branchname}/inventory.md` (inkl. Detaildokumente in `docs/features/{branchname}/inventory/`)
       >
       > Speichere den aktualisierten Plan als `docs/features/{branchname}/plan.md`.

       Warte auf den Abschluss des Unteragenten und wiederhole Schritt 5a.

## Schritt 5b: Planungscommit

Stage alle Dateien unter `docs/features/{branchname}/` und erstelle einen Git-Commit:

```
git add docs/features/{branchname}/
git commit -m "plan: {kurze Beschreibung der geplanten Anforderung}"
```

Gibt `git status` an, dass keine Änderungen vorhanden sind (z. B. weil dieser Schritt bei einer Fortsetzung erneut aufgerufen wird), überspringe den Commit-Befehl.

Markiere anschließend Schritt 5b in `todo.md` als erledigt (`[x]`).

## Schritte 6–8: Implementierung und Reviews (Iterationsschleife)

Die Schleife läuft maximal **3 Iterationen**. Zusätzlich wird nach jeder Iteration geprüft, ob Fortschritt erzielt wurde. Führe intern Buch über:

- **Iterationszähler** (beginnt bei 1, wird nach jedem vollständigen Durchlauf von Schritt 6–8 um 1 erhöht)
- **Offene Punkte der letzten Iteration** (Gesamtzahl offener Einträge aus beiden Reviews, zu Beginn: ∞)

### Schritt 6: Implementierung (Unteragent)

Starte einen Unteragenten mit folgendem Auftrag:

> Führe das Kommando `/implement` aus. Die Eingaben liegen unter:
> - `docs/features/{branchname}/plan.md`
> - `docs/features/{branchname}/requirement.md`
> - `docs/features/{branchname}/inventory.md` (inkl. Detaildokumente in `docs/features/{branchname}/inventory/`)
>
> Falls `docs/features/{branchname}/continue.md` vorhanden ist, bearbeite ausschließlich die dort als offen (`- [ ]`) markierten Punkte.
>
> Andernfalls, falls aus einem vorherigen Durchlauf Reviews vorliegen, bearbeite ausschließlich die dort gemeldeten offenen Punkte:
> - Offene Planelemente aus `docs/features/{branchname}/review.md` (falls vorhanden)
> - Code-Befunde aus `docs/features/{branchname}/review-code.md` (falls vorhanden)

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 6 in `todo.md` als erledigt (`[x]`).

**Abbruchbedingung:** Meldet der Unteragent einen Abbruch (z. B. wegen eines Widerspruchs im Plan oder einer ungeklärten technischen Frage), brich den gesamten Ablauf sofort ab. Informiere den Anwender über den Abbruchgrund und warte auf seine Entscheidung. Fahre nicht mit Schritt 7 fort.

### Schritt 7: Plan-Review (Unteragent, bedingt)

Überspringe diesen Schritt, wenn `docs/features/{branchname}/review.md` aus einem früheren Durchlauf bereits den Status `Vollständig umgesetzt` trägt — der Plan gilt dann weiterhin als erfüllt.

Andernfalls: Existiert `docs/features/{branchname}/review.md` bereits, benenne sie vor dem Start des Unteragenten um. Bestimme dazu den nächsten freien Zähler (beginnend bei 1): Prüfe, ob `review.1.md` existiert, dann `review.2.md` usw., und benenne die vorhandene Datei in `review.{zähler}.md` um (erster freier Zähler).

Starte anschließend einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/review-plan` aus. Die Eingaben liegen unter:
> - `docs/features/{branchname}/plan.md`
> - `docs/features/{branchname}/inventory.md` (inkl. Detaildokumente in `docs/features/{branchname}/inventory/`)
>
> Speichere das Ergebnis als `docs/features/{branchname}/review.md` (überschreibe eine vorhandene Datei).

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 7 in `todo.md` als erledigt (`[x]`).

### Schritt 8: Code-Review (Unteragent)

Existiert `docs/features/{branchname}/review-code.md` bereits, benenne sie vor dem Start des Unteragenten um. Bestimme dazu den nächsten freien Zähler (beginnend bei 1): Prüfe, ob `review-code.1.md` existiert, dann `review-code.2.md` usw., und benenne die vorhandene Datei in `review-code.{zähler}.md` um (erster freier Zähler).

Starte anschließend einen Unteragenten mit folgendem Auftrag:

> Führe das Kommando `/review-code` aus.
>
> Prüfe insbesondere bei UI-Features: Für jede `RaiseUiActionRequested(...)`-Aktion im ViewModel muss in der zugehörigen Blazor-Seite/-Komponente ein passender `case`-Zweig existieren.
>
> Speichere das Ergebnis als `docs/features/{branchname}/review-code.md` (überschreibe eine vorhandene Datei).

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 8 in `todo.md` als erledigt (`[x]`).

### Schritt 8b: Tests ausführen (Unteragent)

Starte einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/run-tests` aus.
>
> Wenn die Anforderung einen UI-Fluss (z. B. Ribbon-Button, Navigation, Fokus) umfasst, prüfe ob mindestens ein E2E-Test für den exakten Benutzerfluss existiert und erfolgreich durchläuft. Das Testergebnis ist mit den übrigen Ergebnissen in `docs/features/{branchname}/test-results.md` (überschreiben) abzulegen.

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 8b in `todo.md` als erledigt (`[x]`).

### Iteration oder Abschluss

Lies alle drei Prüfergebnisse. Zähle die **Gesamtzahl offener Punkte** dieser Iteration: Anzahl offener Einträge in `review.md` + Anzahl der Befunde in `review-code.md` + Anzahl fehlgeschlagener Tests in `test-results.md`.

**Falls `continue.md` vorhanden ist:** Aktualisiere die Datei — markiere alle Einträge als erledigt (`- [x]`), die nicht mehr unter den aktuellen offenen Review-Einträgen oder fehlgeschlagenen Tests erscheinen. Sind danach alle Einträge auf `[x]` gesetzt, benenne die Datei um in `continue-done.md` und markiere Schritt 10 in `todo.md` als erledigt (`[x]`).

Entscheide anhand der folgenden Tabelle:

| Bedingung | Entscheidung |
|-----------|-------------|
| Alle drei Prüfungen grün (`Vollständig umgesetzt` + `Keine Befunde` + `Keine Fehler`) | Markiere den Iterationsschritt in `todo.md` als erledigt (`[x]`). Schleife erfolgreich beenden → Schritt 9 |
| Iterationszähler < 3 **und** offene Punkte < offene Punkte der letzten Iteration | Fortschritt erkannt → Zähler erhöhen, offene Punkte merken, zurück zu Schritt 6 |
| Iterationszähler = 3 **oder** offene Punkte ≥ offene Punkte der letzten Iteration | Kein weiterer Fortschritt zu erwarten → Markiere den Iterationsschritt in `todo.md` als erledigt (`[x]`). Schleife abbrechen, weiter mit Schritt 8a |

### Schritt 8a: Folgeaufgaben dokumentieren (bei Abbruch der Schleife)

Erstelle die Datei `docs/features/{branchname}/continue.md` (überschreibe vollständig, falls sie bereits existiert):

```
# Offene Aufgaben

Erstellt am: {heutiges Datum}
Abbruchgrund: {Maximale Iterationsanzahl erreicht | Kein Fortschritt zwischen den letzten zwei Iterationen}

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

{Vollständige Liste der offenen Einträge aus docs/features/{branchname}/review.md — Abschnitt "Offene Aufgaben", je Eintrag als Checkbox: - [ ] <Eintrag>}

## Code-Review-Befunde

{Vollständige Liste der Befunde aus docs/features/{branchname}/review-code.md — Abschnitt "Befunde", je Befund als Checkbox: - [ ] <Befund>}

## Fehlgeschlagene Tests

{Vollständige Liste der fehlgeschlagenen Tests aus docs/features/{branchname}/test-results.md — Abschnitt "Fehlgeschlagene Tests", je Test als Checkbox: - [ ] <Testname> — <Fehlermeldung>}
```

Markiere anschließend Schritt 8a in `todo.md` als erledigt (`[x]`).

Füge außerdem die folgende Zeile in `todo.md` **vor der letzten Tabellenzeile** (Commit) ein:

```
| [ ] | 10 | Nacharbeiten abschließen (offene Punkte aus `continue.md`) | `continue-done.md` |
```

Fahre mit Schritt 9 fort.

## Schritt 9: Dokumentation (Unteragent)

**Falls dieser Lauf als Fortsetzung einer `continue.md` gestartet wurde** (d. h. der Einstiegspunkt war Schritt 6 aufgrund einer vorhandenen `continue.md`) und die Dokumentation bereits existiert:

Prüfe, ob die durchgeführten Korrekturen grundlegende Aspekte der Programmlogik verändert haben (z. B. geänderte Schnittstellen, neue oder entfallene Kernfunktionen, geändertes Datenmodell). Ist das **nicht** der Fall, überspringe diesen Schritt und markiere Schritt 9 in `todo.md` direkt als erledigt (`[x]`).

Andernfalls — also beim ersten Durchlauf oder wenn sich grundlegende Programmlogik geändert hat — starte einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/update-docs` aus. Die Eingaben liegen unter:
> - `docs/features/{branchname}/requirement.md`
> - `docs/features/{branchname}/plan.md`

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 9 in `todo.md` als erledigt (`[x]`).

## Schritt 9b: README aktualisieren (Unteragent)

**Falls Schritt 9 übersprungen wurde**, überspringe auch diesen Schritt und markiere ihn direkt als erledigt (`[x]`).

Andernfalls starte einen Unteragenten **(Modell: haiku)** mit folgendem Auftrag:

> Führe das Kommando `/update-readme` aus. Die Eingaben liegen unter:
> - `docs/features/{branchname}/requirement.md`
> - `docs/features/{branchname}/plan.md`

Warte auf den Abschluss des Unteragenten, bevor du fortfährst. Markiere anschließend Schritt 9b in `todo.md` als erledigt (`[x]`).

## Feature-Verzeichnis löschen

Prüfe zunächst, ob alle Zeilen der Tabelle in `docs/features/{branchname}/todo.md` (außer „Feature-Verzeichnis löschen" und „Commit durchführen") als erledigt markiert sind (`[x]`). Ist das der Fall:

1. Markiere die Einträge „Feature-Verzeichnis löschen" und „Commit durchführen" in `todo.md` als erledigt (`[x]`).
2. Lösche das Verzeichnis `docs/features/{branchname}/` rekursiv und stage die Löschung:

```
git rm -r docs/features/{branchname}/
```

Die Planungsartefakte bleiben über den Planungscommit (Schritt 5b) in der Git-Historie erhalten und belasten zukünftige Agenten-Kontexte nicht mehr.

## Abschluss: Commit durchführen

Stage alle geänderten und neuen Dateien, die zur Bearbeitung dieser Anforderung gehören (Quellcode, Dokumentation). Das Feature-Verzeichnis ist bereits durch den vorherigen Schritt gestaged.

Erstelle einen Commit mit einer prägnanten Nachricht, die den Inhalt der Anforderung zusammenfasst. Verwende das Format: `feat: {kurze Beschreibung der umgesetzten Anforderung}`.

Informiere anschließend den Anwender, dass die Anforderung vollständig abgeschlossen und committed wurde.

## Automatische Fortsetzung (bei verbleibender continue.md)

Prüfe, ob `docs/features/{branchname}/continue.md` noch existiert.

- **Datei nicht vorhanden** → kein weiterer Schritt erforderlich.
- **Datei vorhanden, und der Einstiegspunkt dieses Laufs war Schritt 6 aufgrund einer vorhandenen `continue.md`** (d. h. dieser Lauf war selbst bereits eine Fortsetzung): Kein weiterer Schritt. Informiere den Anwender, dass offene Punkte verbleiben, die trotz erneutem Versuch nicht gelöst werden konnten und manuelle Intervention erfordern.
- **Datei vorhanden, und dieser Lauf war kein `continue.md`-Lauf**: Starte einen Unteragenten mit folgendem Auftrag:

  > Führe das Kommando `/lifecycle` aus. Es ist keine neue Anforderung vorhanden — setze die Bearbeitung des bestehenden Feature-Branches fort. Die Datei `docs/features/{branchname}/continue.md` enthält die noch offenen Punkte.

  Warte auf den Abschluss des Unteragenten.

---

## Hinweise zur Ausführung

- Dieses Kommando selbst gibt keine inhaltlichen Antworten — es orchestriert nur.
- Ersetze `{branchname}` und `{anforderung}` jeweils mit den tatsächlichen Werten vor dem Delegieren.
- Falls kein Branch aktiv ist (z. B. detached HEAD), brich ab und informiere den Benutzer.
- Der Abbruch durch den Implementierungsagenten ist kein Fehler des Ablaufs — er zeigt an, dass eine menschliche Entscheidung benötigt wird.
