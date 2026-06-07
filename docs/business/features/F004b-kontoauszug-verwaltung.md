# F004b – Kontoauszugs-Verwaltung (Übersicht & Detailseiten)

## Einleitung

Die Kontoauszugs-Verwaltung ist die zentrale Schnittstelle zur Verwaltung von importierten Kontoauszügen (Drafts/Entwürfe). Nach dem Upload eines Kontoauszugs (siehe F004) können Sie:
- Die importierten Einträge prüfen und bearbeiten
- Fehlende oder falsche Daten korrigieren
- Transaktionen manuell kontieren
- Den kompletten Draft verbuchen oder teilweise buchen
- Die Originaldatei abrufen

Diese Funktion ist zentral für die **Qualitätskontrolle** vor der endgültigen Verbuchung.

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion:
- Nach dem Import von Kontoauszügen
- Zur Überprüfung und Korrektur von Transaktionsdaten
- Zum Verbuchen der Transaktionen ins System
- Bei Rückfragen zu einzelnen Transaktionen (Zugriff auf Original-PDF/CSV)

## Schritt-für-Schritt-Anleitung

### Kontoauszugs-Übersicht anzeigen

1. Sie navigieren zu **Kontoauszüge** → **Importverwaltung**
2. Sie sehen eine Liste aller importierten Kontoauszüge (Drafts):
   - **Dateiname**: Name der hochgeladenen Datei
   - **Konto**: Das betroffene Bankkonto
   - **Status**: z.B. "Entwurf", "Teilweise verbucht", "Verbucht"
   - **Einträge**: Anzahl der Transaktionen im Draft
   - **Datum**: Importdatum
3. Sie können die Liste nach Konto, Datum oder Status filtern

### Kontoauszug-Details öffnen

1. Sie klicken auf einen Draft in der Liste
2. Sie sehen die Detailseite mit:
   - **Allgemeine Informationen**: Dateiname, Konto, Importdatum, Status
   - **Original-Datei**: Download-Button für die ursprüngliche PDF/CSV
   - **Einträge-Tabelle**: Liste aller Transaktionen mit den Feldern:
     - Buchungsdatum
     - Wertstellungsdatum
     - Betrag
     - Beschreibung/Empfänger
     - Art (Bank/Kontakt/Sparplan)
     - Status (offen/kontiert/verbucht)
     - Aktionen (Bearbeiten, Löschen)

### Kontoauszug-Eintrag bearbeiten

#### Im Listenmodus (Schnelle Bearbeitung)

1. Sie aktivieren den **Schnellbearbeitungsmodus** (Toggle im Ribbon oder Schaltfläche)
2. Sie können Felder in der Tabelle direkt bearbeiten:
   - Beschreibung korrigieren
   - Empfänger/Kontakt ändern
   - Kategorie zuweisen
3. Sie klicken **Speichern** um die Änderungen zu übernehmen

#### Im Detail-Modus

1. Sie klicken auf einen Eintrag oder auf die **Bearbeiten**-Schaltfläche
2. Ein Detail-Panel/Dialog öffnet sich mit allen Feldern:
   - Buchungsdatum (änderbar)
   - Wertstellungsdatum (änderbar)
   - Betrag (schreibgeschützt für Bank-Posten)
   - Beschreibung (änderbar)
   - Empfänger/Kontakt (änderbar/auswählbar)
   - Kategorie (falls zutreffend)
3. Sie machen die notwendigen Änderungen
4. Sie klicken **Speichern**

### Original-Datei anzeigen

1. In der Detailseite klicken Sie auf **Original-Datei anzeigen** oder das **Download**-Symbol
2. Sie können:
   - Die Datei **ansehen** (PDF-Viewer oder CSV-Vorschau)
   - Die Datei **herunterladen** (z.B. als PDF speichern)

### Kontoauszug verbuchen

#### Einzelne Einträge verbuchen

1. Sie wählen einen oder mehrere Einträge aus
2. Sie klicken **Verbuchen**
3. Der Eintrag wird in die Postings-Tabelle übertragen
4. Der Eintrag ist danach schreibgeschützt (read-only)

#### Ganzen Kontoauszug verbuchen

1. Sie klicken **Alles verbuchen** im Ribbon oder der Detailseite
2. Ein Bestätigungsdialog fragt: "Wirklich alle Einträge verbuchen?"
3. Bei Bestätigung werden **alle Einträge** verbucht
4. Der Draft-Status ändert sich zu "Verbucht"
5. Die Original-Datei bleibt abrufbar

#### Technisches Sicherheitsverhalten

- Die Buchung ist transaktionssicher: Entweder werden alle nötigen Buchungen gemeinsam gespeichert oder gar keine.
- Wenn zwei Personen oder zwei Browser gleichzeitig denselben Draft buchen, blockiert das System den zweiten Versuch mit einer Konfliktmeldung.
- Ein bereits verbuchter Draft oder Eintrag wird nicht doppelt gebucht.
- Ein erneuter Versuch nach einer laufenden Buchung ist sicher möglich, sobald der erste Versuch abgeschlossen ist.

#### Massenbuchung mehrerer Kontoauszüge

1. Sie wählen mehrere Drafts in der Übersicht an
2. Sie klicken **Alle buchen** im Ribbon
3. Die Verbuchung läuft im Hintergrund ab
4. Sie sehen einen **Fortschritts-Indikator** (z.B. "3 von 10 verbucht")
5. Nach Abschluss zeigt eine Meldung: "✅ 10 Kontoauszüge verbucht"

### Doppeleinträge behandeln

1. Die Software erkennt automatisch verdächtige Duplikate
2. Bei Doppelten-Verdacht zeigt der Eintrag ein **⚠️ Duplikat-Symbol**
3. Sie können:
   - Den Eintrag **löschen** (wenn es ein echtes Duplikat ist)
   - Den Eintrag **behalten** (wenn es zwei separate Transaktionen sind)
   - Den Eintrag **manuell kontieren** (neue Kategorie/Zuordnung wählen)

### Fehlerbehandlung bei Verbuchung

Falls ein Eintrag nicht verbucht werden kann:
- Das System zeigt einen **Fehler** an
- Sie sehen die **Fehlermeldung** (z.B. "Kontakt nicht zugeordnet")
- Sie beheben das Problem (z.B. Kontakt auswählen)
- Sie verbuchen erneut

## Datenfelder pro Eintrag

Jeder Kontoauszug-Eintrag hat folgende Felder:

| Feld | Typ | Bearbeitbar | Beschreibung |
|------|-----|------------|-------------|
| **BookingDate** | Datum | ✅ | Buchungsdatum der Transaktion |
| **ValutaDate** | Datum | ✅ | Wertstellungsdatum (wirksam ab) |
| **Amount** | Betrag | ❌ | Betrag (Bank-Posten schreibgeschützt) |
| **Description** | Text | ✅ | Beschreibung/Betreff der Transaktion |
| **RecipientName** | Text | ✅ | Name des Empfängers/Absenders |
| **ContactId** | Kontakt | ✅ | Zugeordneter Kontakt (auswählbar) |
| **Kind** | Enum | ❌ | Art: Bank/Kontakt/Sparplan/Sicherheit |
| **Status** | Enum | ❌ | Offen/Kontiert/Verbucht |
| **Subject** | Text | ✅ | Zusätzlicher Betreff (falls vorhanden) |

## Beispiel: Klassisches Szenario

**Sie importieren einen PDF-Kontoauszug von der Sparkasse mit 50 Transaktionen:**

1. ✅ Die Datei wird hochgeladen
2. ✅ Der Draft wird erstellt (Status: "Entwurf")
3. 🔍 Sie öffnen den Draft und überprüfen die Einträge
4. ⚠️ Sie finden eine Transaktion ohne Kontakt: "Zahlung X123"
5. ✏️ Sie klicken auf die Transaktion und weisen den Kontakt "Amazon" zu
6. 🔄 Sie verbuchen diese Transaktion
7. ✅ Sie verbuchen alle übrigen Transaktionen auf einmal ("Alles buchen")
8. ✅ Der Draft ist jetzt "Verbucht" und alle 50 Transaktionen sind im System

Die Original-PDF können Sie später immer noch abrufen, falls Sie eine Transaktion überprüfen müssen.

## Was passiert im Hintergrund?

- **Beim Verbuchen**: Die Einträge werden zu **Posting**-Entitäten konvertiert
- **Unterschiedliche Arten**: Je nach Transaktion entstehen:
  - **Bank-Posten**: Von Bankkonten importiert
  - **Kontakt-Posten**: Zugeordnete Kontaktpartner
  - **Sparplan-Posten**: Automatische Sparpläne (falls konfiguriert)
  - **Wertpapier-Posten**: Bei Depot-Transaktionen
- **Originalität**: Die Upload-Originaldatei bleibt dauerhaft erhalten
- **Idempotenz**: Nochmaliges Verbuchen desselben Drafts ist nicht möglich (verhindert Duplikate)
- **Parallelitätsschutz**: Ein persistenter Guard verhindert die gleichzeitige doppelte Verarbeitung desselben Drafts
- **Fehlervertrag**: Konflikte liefern einen standardisierten 409-Fehler mit technischem Code und Wiederholungshinweis

## Häufige Fragen (FAQ)

**F: Kann ich einen Eintrag nach dem Verbuchen noch ändern?**  
A: Nein, verbuchte Einträge sind schreibgeschützt. Sie müssen den Posting über die Postings-Liste bearbeiten.

**F: Was ist der Unterschied zwischen "Teilweise verbucht" und "Verbucht"?**  
A: 
- "Teilweise verbucht": Einige Einträge sind verbucht, andere noch im Entwurf
- "Verbucht": Alle Einträge sind verbucht

**F: Kann ich einen Draft löschen?**  
A: Ja, Sie können einen Draft mit Status "Entwurf" löschen. Verbuchte Drafts können nicht gelöscht werden (Datensicherheit).

**F: Werden die Kontoauszüge automatisch verbucht?**  
A: Nein, Sie müssen explizit auf "Verbuchen" klicken. Dies gibt Ihnen die Kontrolle und Chance zur Überprüfung.

**F: Wo finde ich die verbuchten Transaktionen später?**  
A: Die verbuchten Einträge erscheinen in der **Postings-Liste** (Ausgabenverwaltung). Sie sind dort vollständig bearbeitbar.

**F: Was passiert mit Duplikaten?**  
A: Die Software markiert verdächtige Duplikate. Sie können sie:
- Löschen (wenn wirklich Duplikat)
- Behalten (wenn zwei separate Transaktionen)
- Manuell korrigieren (unterschiedliche Kategorien zuweisen)

**F: Was passiert, wenn ich zweimal auf „Verbuchen“ klicke?**
A: Der zweite Versuch wird als Konflikt abgewiesen, solange die erste Buchung noch läuft. Nach erfolgreichem Abschluss ist ein erneuter Versuch nicht nötig und erzeugt keine doppelten Buchungen.

**F: Kann ich eine Excel-Datei statt PDF importieren?**  
A: Ja, das System unterstützt mehrere Formate:
- CSV (Comma-Separated Values)
- PDF (für Sparkasse, ING, Barclays)
- XLSX (Microsoft Excel) – je nach Bank

**F: Kann ich die Original-Datei später noch abrufen?**  
A: Ja, jederzeit. Die Original-Datei wird dauerhaft gespeichert und ist abrufbar.

## Verwandte Funktionen

- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md) (Hochladen der Dateien)
- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md) (Verwaltung der verbuchten Postings)
- [F001 – Kontenübersicht](./F001-kontenuebersicht.md) (Übersicht der Konten)
- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md) (Für Depot-Transaktionen)
