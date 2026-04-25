# F002 – Kontenverwaltung

## Einleitung

Die Kontenverwaltung ermöglicht es Ihnen, neue Bankkonten anzulegen, vorhandene Konten zu bearbeiten und nicht mehr benötigte Konten zu löschen. Sie definieren hier die grundlegenden Informationen für Ihre Konten und verwalten ihre Bankverbindungen.

## Wer nutzt es?

**Finanzverwalter und Administratoren** nutzen diese Funktion, um Konten im System zu registrieren. Dies geschieht in der Regel bei der Ersteinrichtung oder wenn neue Bankkonten hinzugefügt werden. Sie können auch Konten aktualisieren oder löschen.

## Schritt-für-Schritt-Anleitung

### Neues Konto erstellen

1. Sie navigieren zur **Konten**-Übersicht
2. Sie klicken auf die Schaltfläche **Neues Konto** oder **Konto hinzufügen**
3. Sie öffnen die Eingabemaske für ein neues Konto
4. Sie tragen folgende Informationen ein:
   - **Kontoname** (erforderlich) - z.B. "Geschäftskonto"
   - **Kontotyp** (erforderlich) - Girokonto, Sparkonto, etc.
   - **IBAN** (optional) - Ihre Internationale Kontonummer
   - **Bankname** (optional) - Name der Bank (wird automatisch aus Kontakt übernommen)
5. *(Optional)* Sie laden ein **Symbol/Logo** hoch (für visuelle Identifikation)
6. Sie klicken **Speichern**

Das System erstellt automatisch einen entsprechenden **Bankkontakt**, wenn dieser noch nicht existiert.

### Bestehendes Konto bearbeiten

1. Sie öffnen die **Kontenübersicht**
2. Sie klicken auf ein Konto, um die Detailseite zu öffnen
3. Sie klicken auf die Schaltfläche **Bearbeiten**
4. Sie ändern die erforderlichen Informationen:
   - Kontoname
   - IBAN
   - Kontotyp
   - Symbol/Logo
   - Sonstige Einstellungen
5. Sie klicken **Speichern**

### Symbol/Logo für Konto hochladen

1. Sie öffnen die Kontodetails
2. Sie klicken auf **Symbol hochladen** oder wählen ein existierendes Symbol
3. Sie wählen eine Bilddatei (PNG, JPG)
4. Das Logo wird als Konto-Symbol verwendet und in Listen angezeigt

### Konto löschen

1. Sie öffnen die Detailseite eines Kontos
2. Sie klicken auf die Schaltfläche **Löschen**
3. Das Konto wird sofort gelöscht
4. **Hinweis**: Alle zugeordneten Anhänge werden auch gelöscht. Transaktionen bleiben erhalten, sind aber nicht mehr an das Konto gebunden.
5. Falls dieses der letzte Konto des Bankkontakts ist, wird auch der Bankkontakt automatisch gelöscht

## Datenfelder

### Erforderlich:
- **Name**: Kontoname (eindeutig pro Benutzer)
- **Type**: Kontotyp (Girokonto, Sparkonto, Festgeldkonto, etc.)

### Optional:
- **IBAN**: Internationale Kontonummer (eindeutig pro Benutzer wenn angegeben)
- **BankContactId**: Verknüpfter Bankkontakt (wird automatisch erstellt/verwendet)
- **SymbolAttachmentId**: Logo/Icon als Anhang
- **SavingsPlanExpectation**: Erwartung für Sparpläne (Optional/Recommended)
- **SecurityProcessingEnabled**: Flag für Wertpapier-Verarbeitung

## Beispiel

Sie richten ein neues Geschäftskonto bei Ihrer Bank ein. Sie geben folgende Daten in der FinanceManager-Software ein:

- **Name**: "Geschäftskonto – Commerzbank"
- **Type**: Girokonto
- **IBAN**: DE89370400440532013000
- **Symbol**: Logo der Commerzbank hochladen

Danach können Sie sofort Transaktionen auf diesem Konto registrieren. Das System ordnet diese automatisch dem richtigen Konto zu.

## Technische Details

### Bank Kontakt
- Jedes Konto ist mit einem **Bankkontakt** verknüpft
- Der Bankkontakt wird automatisch erstellt beim Konto-Erstellen
- Falls mehrere Konten denselben Bankkontakt nutzen: Der Kontakt wird nur gelöscht wenn das letzte Konto gelöscht wird

### Symbol/Logo Auflösung (Fallback)
Das System nutzt eine Symbol-Auflösungshierarchie:
1. Zuerst: Konto-Symbol (falls vorhanden)
2. Fallback: Bankkontakt-Symbol (falls vorhanden)
3. Fallback: Symbol der Bankkontakt-Kategorie (falls vorhanden)

### Anhänge
- Anhänge zum Konto (z.B. Kontoverträge) können mit der Rolle "Symbol" hochgeladen werden
- Beim Löschen eines Kontos werden alle Anhänge ebenfalls gelöscht

## Häufige Fragen (FAQ)

**F: Kann ich die IBAN später ändern?**  
A: Ja, Sie können die IBAN bearbeiten. Dies hat keine Auswirkung auf bereits erfasste Transaktionen.

**F: Was passiert mit den Transaktionen, wenn ich ein Konto lösche?**  
A: Die Transaktionen bleiben erhalten! Sie werden aber nicht mehr angezeigt als "gehört zu diesem Konto". Sie können die Transaktionen noch via API abrufen, aber die Konto-Verknüpfung ist aufgelöst.

**F: Kann ich ein Konto archivieren statt zu löschen?**  
A: Nein, es gibt keine Archivierungs-Funktion. Sie können nur löschen. Es gibt aber eine Alternative: Ändern Sie den Konto-Namen z.B. in "[ARCHIVIERT] Alter Kontoname" um es als inaktiv zu markieren.

**F: Kann ich ein gelöschtes Konto wiederherstellen?**  
A: Nein, gelöschte Konten können nicht wiederhergestellt werden. Stellen Sie sicher, bevor Sie ein Konto löschen.

**F: Brauche ich die IBAN?**  
A: Die IBAN ist optional, aber empfohlen für die Nachverfolgung von Überweisungen. Sie kann jederzeit nachträglich hinzugefügt werden.

**F: Was ist "SavingsPlanExpectation"?**  
A: Ein Flag das angibt, ob das Konto typischerweise für Ersparnisse verwendet wird. Dies wird für Berichte und Kategorisierung genutzt.

**F: Was ist "SecurityProcessingEnabled"?**  
A: Ein Flag das angibt, ob das Konto für Wertpapier-Transaktionen (Käufe/Verkäufe) verwendet wird.

**F: Kann ich den Bankkontakt später ändern?**  
A: Ja, Sie können den Bankkontakt bearbeiten. Das System erlaubt auch die Verknüpfung mit anderen existierenden Bankkontakten.

## Hinweise

- **Eindeutigkeit**: Der Kontoname muss eindeutig pro Benutzer sein. Die IBAN muss ebenfalls eindeutig sein wenn angegeben.
- **Automatische Kontakt-Erstellung**: Beim Konto-Erstellen wird automatisch ein Bankkontakt erzeugt wenn keiner vorhanden ist
- **Löschung ist final**: Gelöschte Konten können nicht wiederhergestellt werden
- **Transaktionen persistent**: Transaktionen bleiben auch nach Konto-Löschung bestehen
- **Symbol-Fallback**: Das Konto nutzt Logos von sich selbst, dann dem Bankkontakt, dann der Bankkontakt-Kategorie
- **Kein Archivieren**: Es gibt keine native Archivierungs-Funktion - verwende stattdessen Namenskonventionen wie "[ARCHIVIERT]"

## Verwandte Funktionen

- [F001 – Kontenübersicht](./F001-kontenuebersicht.md)
- [F003 – Ausgabenverwaltung / Postings](./F003-ausgabenverwaltung.md)
- [F012 – Kontakte](./F012-kontakte.md)
- [F011 – Belege & Anhänge](./F011-belege-anhaenge.md)

