# F002 – Kontenverwaltung

## Einleitung

Die Kontenverwaltung ermöglicht es Ihnen, neue Bankkonten anzulegen, vorhandene Konten zu bearbeiten und nicht mehr benötigte Konten zu archivieren. Sie definieren hier die grundlegenden Informationen für Ihre Konten.

## Wer nutzt es?

**Finanzverwalter und Administratoren** nutzen diese Funktion, um Konten im System zu registrieren. Dies geschieht in der Regel bei der Ersteinrichtung oder wenn neue Bankkonten hinzugefügt werden.

## Schritt-für-Schritt-Anleitung

### Neues Konto erstellen

1. Sie navigieren zur **Konten**-Übersicht.
2. Sie klicken auf die Schaltfläche **Neues Konto**.
3. Sie öffnen die Eingabemaske für ein neues Konto.
4. Sie tragen folgende Informationen ein:
   - **Kontoname** (z.B. "Geschäftskonto")
   - **IBAN** oder **Kontonummer**
   - **Bankname** (optional)
   - **Kontotyp** (Girokonto, Sparkonto, etc.)
5. Sie klicken **Speichern**.

### Bestehendes Konto bearbeiten

1. Sie öffnen die Kontenübersicht.
2. Sie klicken auf ein Konto, um die Detailseite zu öffnen.
3. Sie klicken auf die Schaltfläche **Bearbeiten**.
4. Sie ändern die erforderlichen Informationen.
5. Sie klicken **Speichern**.

### Konto archivieren

1. Sie öffnen die Detailseite eines Kontos.
2. Sie klicken auf die Schaltfläche **Archivieren**.
3. Das Konto wird deaktiviert und nicht mehr in der Standardansicht angezeigt.

## Beispiel

Sie richten ein neues Geschäftskonto bei Ihrer Bank ein. Sie geben folgende Daten in der FinanceManager-Software ein:

- **Name**: "Geschäftskonto – Commerzbank"
- **IBAN**: DE89370400440532013000
- **Bank**: Commerzbank
- **Typ**: Girokonto

Danach können Sie sofort Transaktionen auf diesem Konto registrieren.

## Was passiert im Hintergrund?

Die Software speichert Ihre Kontoinformationen in der Datenbank. Diese Daten werden verwendet, um die Kontenübersicht zu generieren und Transaktionen dem richtigen Konto zuzuordnen.

## Häufige Fragen (FAQ)

**F: Kann ich die IBAN später ändern?**  
A: Ja, Sie können die IBAN bearbeiten, dies hat keine Auswirkung auf bereits erfasste Transaktionen.

**F: Was passiert mit den Transaktionen, wenn ich ein Konto archiviere?**  
A: Die Transaktionen bleiben erhalten. Archivierte Konten sind nur nicht mehr sichtbar, bis Sie sie reaktivieren.

**F: Kann ich ein Konto löschen?**  
A: Konten mit Transaktionen können nicht gelöscht, sondern nur archiviert werden.

**F: Kann ich ein archiviertes Konto reaktivieren?**  
A: Ja, Sie können ein archiviertes Konto jederzeit reaktivieren.

**F: Brauche ich die IBAN?**  
A: Die IBAN ist optional, aber empfohlen für die Nachverfolgung von Überweisungen.

## Verwandte Funktionen

- [F001 – Kontenübersicht](./F001-kontenuebersicht.md)
- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
- [F012 – Kontakte](./F012-kontakte.md)
