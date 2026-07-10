# Umsetzungsplan - Bekannte Kontakte

Hinweis: Der Plan wurde lokal erstellt, weil der vorgesehene Subagent wegen Usage-Limit nicht verfuegbar war.

## Entscheidungen

- Die bekannte-Kontakte-Liste wird als JSON unter `FinanceManager.Web/Data/KnownContacts.json` ausgeliefert.
- Die Funktion ist standardmaessig aktiviert, damit das Feature ohne weitere Konfiguration wirkt.
- Ein Eintrag kann optional einen `type` enthalten; fehlt er, wird `Organization` verwendet.
- Beim automatischen Erstellen werden alle Aliasse aus der Definition uebernommen, die nicht dem Kontaktnamen entsprechen.
- Matching prueft Empfaenger, Betreff und Buchungsbeschreibung.
- Bei mehreren Treffern wird kein Kontakt automatisch angelegt.

## Schritte

1. Domain/User-Einstellung ergaenzen
   - `KnownContactAutoCreateEnabled` in `User`
   - Setter `SetKnownContactAutoCreateEnabled`
   - EF-Mapping und Migration fuer persistente Spalte mit Default `true`

2. Shared/API/UI-Einstellung erweitern
   - `ImportSplitSettingsDto` und `ImportSplitSettingsUpdateRequest` um Boolean erweitern
   - `UserSettingsController` GET/PUT erweitert
   - `SetupStatementsViewModel` Clone/Dirty/Reset/Save erweitert
   - `SetupStatementTab.razor` Checkbox mit lokalisierter Beschriftung

3. Katalog-Service einfuehren
   - neues Interface `IKnownContactCatalog`
   - JSON-Modelle und `KnownContactCatalog`
   - Laden aus `Data/KnownContacts.json`
   - Normalisierung und eindeutiges Matching ueber Name/Aliasse gegen mehrere Suchtexte
   - defensive Behandlung fehlender/ungueltiger Datei

4. Klassifikation erweitern
   - `StatementDraftService` bekommt `IKnownContactCatalog`
   - nach erfolgloser bestehender Kontaktzuordnung und nur bei aktiver User-Einstellung Katalog suchen
   - bei eindeutigem Treffer Benutzerkontakt und Aliasse anlegen
   - neuen Kontakt in lokale Kontakt-/Alias-Listen aufnehmen und Eintrag zuordnen

5. Tests
   - Katalog-Matching testen
   - StatementDraft-Klassifikation fuer Auto-Anlage, deaktivierte Einstellung und Vorrang bestehender Kontakte testen

6. Dokumentation
   - Hilfe fuer Kontoauszugsimport/Kontakte aktualisieren
   - README kurz um bekannte Kontakte ergaenzen

## Offene Punkte

Keine.
