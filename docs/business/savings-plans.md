# Sparpläne (Business-Funktionalität)

Dieses Dokument beschreibt die fachliche Funktionalität von Sparplänen (Savings Plans) im System: Lebenszyklus, Regeln, Interaktionen mit Kontoauszügen, Buchung und Validierung.

Übersicht
- Ein Sparplan ist ein Ziel-/Zahlungsauftrag des Nutzers mit optionalem Zielbetrag und Fälligkeitsdatum oder als wiederkehrender Plan (z. B. monatlich).
- Sparpläne können Buchungen (Postings) zugeordnet werden, um Zielbeträge zu erreichen. Sie wirken auf Berichte und Aggregates.

Wesentliche Eigenschaften
- `Name`, `OwnerUserId`, `TargetAmount?`, `TargetDate?`, `Type` (OneTime / Recurring), `Interval` (bei Recurring), `IsActive`, `ContractNumber?`.
- `TargetAmount` und `TargetDate` können zusammen eine Fälligkeit definieren; Recurring-Pläne haben `Interval` (monatlich, quartalsweise, ...).

Geschäftsregeln
- Sparpläne dürfen nur Kontakten vom Typ `Self` zugewiesen werden (Eigentransfer-Regel).
- Ein Sparplan mit `TargetAmount`/`TargetDate` kann als „fällig“ gelten; das System schlägt Buchungen vor / markiert Informationsmeldungen.
- Auf einem Sparkonto (Account Type = Savings) ist die Zuordnung eines Sparplans nicht zulässig — Validierungscode `SAVINGSPLAN_INVALID_ACCOUNT`.
- Bei Zuweisung durch Klassifikation wird geprüft, ob das erkannte Konto das Anlegen/Archivieren von Sparplänen erlaubt (Account.SavingsPlanExpectation).
- Wenn ein Sparplan archiviert werden soll (Flag `ArchiveOnBooking`) wird beim Buchungszeitpunkt geprüft, ob der Planzielbetrag erreicht ist; ansonsten Fehler `SAVINGSPLAN_ARCHIVE_MISMATCH` oder Warnung `SAVINGSPLAN_GOAL_EXCEEDS`/`SAVINGSPLAN_GOAL_REACHED_INFO` ausgegeben.

Verhalten bei Import/Klassifikation
- Klassifizierungslogik versucht, bei Eigentransfers passende Sparpläne per Name oder ContractNumber zuzuordnen (siehe `TryAutoAssignSavingsPlan`).
- Wenn mehrere Treffer gefunden werden: Markierung `MarkNeedsCheck` und ggf. manuelle Nachbearbeitung erforderlich.
- Wenn das erkannte Konto keinen Sparplan erlaubt oder das Entry nicht `Self` ist, wird keine automatische Zuordnung vorgenommen.

Buchung/Posting
- Bei Buchung eines Eintrags mit zugeordnetem Sparplan wird zusätzlich ein `PostingKind.SavingsPlan` erzeugt (Gegenbuchung zum Bankposting) und dem Plan zugewiesen.
- Recurring-Pläne können beim erstmaligen Fälligkeitseintritt automatisch auf das nächste Intervall verschoben werden (AdvanceTargetDateIfDue) — Logik berücksichtigt Monatsende-Regeln.

Validierungscodes (häufig)
- `SAVINGSPLAN_NO_CONTACT` — kein Kontakt bei Zuweisungsversuch
- `SAVINGSPLAN_INVALID_CONTACT` — Kontakt ist nicht `Self`
- `SAVINGSPLAN_NO_ACCOUNT` — kein Konto erkannt
- `SAVINGSPLAN_ACCOUNT_NOT_ALLOWED` — Konto erlaubt keine Sparpläne
- `SAVINGSPLAN_ARCHIVE_MISMATCH` — Archivflag konnte nicht erfüllt werden
- `SAVINGSPLAN_GOAL_REACHED_INFO` / `SAVINGSPLAN_GOAL_EXCEEDS` — Informations-/Warnmeldungen beim Planziel

UI- und API‑Hinweise
- API: Endpunkte unter `SavingsPlansController` (GET/POST/PUT/DELETE). Bei Assignments wird Endpoint `StatementDrafts_SaveEntryAllAsync` / `SaveEntryAll` genutzt.
- UI: Bei Auswahl eines Self-Kontakts wird Sparplan‑Lookup eingeblendet; wenn Account erlaubt, sonst ausgeblendet/gesperrt.

Tests (Empfehlung)
- Unit-Tests für: Auto-Assign (single/multiple matches), Booking mit Archiv-Flag (erfolgreich/Fehler), Validierung bei falschem Konto, Recurring-Advance-Logik (Monatsende Edge-Cases).

Anmerkung
- Implementierungsdetails (Methoden/Validierungen) sind in `StatementDraftService.Classification` und `SaveEntryAllAsync` zu finden. Diese Dokumentation sollte mit konkreten API‑Schemas in `docs/api/models.md` verlinkt werden.
