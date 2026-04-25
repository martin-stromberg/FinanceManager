# F011 – Belege & Anhänge

## Einleitung

Die Belege & Anhänge-Verwaltung ermöglicht es Ihnen, Dokumente und externe URLs zu speichern und mit verschiedenen Entitäten zu verknüpfen:
- **Datei-Anhänge**: Hochgeladene Dokumente (PDF, Bilder, etc.)
- **URL-Anhänge**: Links zu externen Ressourcen
- **Kategorisierung**: Organisieren Sie Anhänge in benutzerdefinierten Kategorien
- **Rollen**: Spezielle Anhänge wie "Symbole" (z.B. für Konten- oder Wertpapier-Logos)

Anhänge können mit folgenden Entitäten verknüpft werden:
- Transaktionen (Bankposten)
- Konten
- Kontakte
- Wertpapiere
- Ersparnispläne

Dies ist wichtig für die Nachvollziehbarkeit und das Auditing.

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion, um:
- Belege und Rechnungen zu speichern
- Externe Ressourcen zu verlinken
- Dokumente zu kategorisieren und wiederzufinden
- Geschäftsunterlagen für Audits zu organisieren

## Schritt-für-Schritt-Anleitung

### Anhang hochladen (Datei)

1. Sie navigieren zu einer Entität (z.B. Transaktion, Konto, Kontakt)
2. Sie öffnen den **Anhänge**-Bereich oder klicken **Datei hinzufügen**
3. Sie wählen die Datei von Ihrem Computer (z.B. PDF, JPG, PNG)
4. *(Optional)* Sie ordnen die Datei einer **Kategorie** zu (z.B. "Rechnung", "Quittung", "Vertrag")
5. *(Optional)* Sie weisen eine **Rolle** zu (z.B. "Symbol" für Logos)
6. Sie klicken **Hochladen**

### Anhang verlinken (URL)

1. Sie navigieren zu einer Entität
2. Sie öffnen den **Anhänge**-Bereich
3. Sie klicken **URL hinzufügen** oder **Link einfügen**
4. Sie geben die externe URL ein (z.B. "https://beispiel.de/dokument")
5. *(Optional)* Sie ordnen die URL einer **Kategorie** zu
6. Sie klicken **Speichern**

### Anhang anzeigen/herunterladen

1. Sie öffnen die Entität mit Anhängen
2. Sie sehen die Anhänge in einer Liste
3. Sie klicken auf einen Anhang, um ihn anzusehen oder herunterzuladen
4. Für temporäre Zugriffe wird ein kurzlebiger Download-Token generiert

### Anhang kategorisieren

1. Sie öffnen den Anhang
2. Sie können die Kategorie ändern oder eine neue hinzufügen
3. Sie können zwischen **Standard-Kategorien** und **System-Kategorien** wählen

### Anhang löschen

1. Sie öffnen den Anhang
2. Sie klicken **Löschen**
3. Der Anhang wird entfernt

## Datenfelder

### Alle Anhänge haben:
- **Name**: Dateiname (für Dateien) oder Display-Name (für URLs)
- **Kategorie**: Optional - für Organisierung und Filterung
- **EntityKind**: Zu welcher Entität gehört der Anhang (Konto, Kontakt, etc.)
- **EntityId**: ID der verknüpften Entität
- **CreatedAt**: Zeitpunkt des Uploads/der Verknüpfung

### Datei-Anhänge zusätzlich:
- **File Content**: Der Upload-Stream (wird sicher gespeichert)
- **Content-Type**: MIME-Type (z.B. "application/pdf")
- **Size**: Dateigröße in Bytes

### URL-Anhänge zusätzlich:
- **URL**: Der externe Link
- **IsUrl**: Markiert als URL-Anhang (true)

### Optionale Rollen:
- **Symbol**: Spezielle Rolle für Logos/Icons (z.B. für Konten)
- Standard: Null (normale Dokumentation)

## Beispiel

### Datei-Anhang
Sie erhalten eine Rechnung von Ihrem Stromversorger für 450 EUR. Sie speichern diese:

1. Sie öffnen die entsprechende Transaktion
2. Sie klicken **Datei hinzufügen**
3. Sie wählen die PDF-Rechnung
4. Sie ordnen sie der Kategorie "Betriebskostenrechnung" zu
5. Sie speichern
6. Später können Sie die Rechnung jederzeit herunterladen

### URL-Anhang
Sie haben einen Link zu einem Online-Banking-Beleg:

1. Sie öffnen das Konto
2. Sie klicken **URL hinzufügen**
3. Sie geben ein: `https://meine-bank.de/dokument/123456`
4. Sie speichern
5. Der Link ist nun mit dem Konto verknüpft

### Symbol-Rolle
Sie möchten ein Logo für Ihr Geschäftskonto hochladen:

1. Sie öffnen das Konto
2. Sie klicken **Datei hinzufügen**
3. Sie wählen die Logo-Datei (PNG/JPG)
4. Sie weisen die Rolle **"Symbol"** zu
5. Das System erstellt automatisch die Kategorie "Symbole" falls nötig
6. Das Logo wird als Konto-Symbol verwendet

## Technische Details

### Dateigrößenlimits
- Maximum: Konfigurierbar (typisch 10-50 MB)
- Wird beim Upload überprüft
- Zu große Dateien werden abgelehnt

### Unterstützte Dateitypen
- PDF: `application/pdf`
- Bilder: `image/jpeg`, `image/png`, etc.
- Weitere: `application/vnd.ms-excel`, `application/msword`, etc.
- Wird durch Server-Konfiguration definiert

### Sicherheit
- Authentifizierung erforderlich (JWT Token)
- Alle Anhänge gehören zum angemeldeten Benutzer
- Download-Links sind kurzlebig (Data Protection Token)
- Dateigrößenlimits verhindern Speicher-Missbrauch

## Häufige Fragen (FAQ)

**F: Welche Dateitypen können hochgeladen werden?**  
A: Das wird vom Server konfiguriert. Standardmäßig: PDF, Bilder (JPG, PNG), Office-Dokumente.

**F: Wie groß darf eine Datei sein?**  
A: Abhängig von der Server-Konfiguration, typischerweise 10-50 MB pro Datei.

**F: Kann ich einen Anhang mit mehreren Entitäten verknüpfen?**  
A: Ein Anhang ist eindeutig einer Entität zugeordnet. Sie müssen denselben Anhang ggf. mehrfach hochladen.

**F: Wo werden die Anhänge gespeichert?**  
A: Anhänge werden auf dem Server (Dateisystem oder Cloud) sicher gespeichert. Die Metadaten sind in der Datenbank.

**F: Was ist die "Symbol"-Rolle?**  
A: Eine spezielle Rolle für Logo/Icon-Dateien, die vom System als Konten-, Kontakt- oder Wertpapier-Symbol verwendet werden.

**F: Kann ich Anhänge nach Upload umbenennen?**  
A: Ja, Sie können den Display-Namen ändern (nicht den Dateinamen selbst).

**F: Gibt es Kategorien für Anhänge?**  
A: Ja, Sie können benutzerdefinierte Kategorien erstellen oder vordefinierte System-Kategorien nutzen.

## Hinweise

- **Authentifizierung**: Alle Anhang-Operationen erfordern einen gültigen JWT-Token
- **Pagination**: Bei vielen Anhängen werden diese seitenweise geladen (Standard: 50 pro Seite)
- **Suchfunktion**: Sie können nach Dateiname oder URL-Substring suchen
- **Temporäre Download-Links**: Download-Links sind kurzlebig aus Sicherheitsgründen
- **Kategorien sind optional**: Anhänge funktionieren auch ohne Kategorisierung

## Verwandte Funktionen

- [F003 – Ausgabenverwaltung / Postings](./F003-ausgabenverwaltung.md)
- [F002 – Kontenverwaltung](./F002-kontenverwaltung.md)
- [F012 – Kontakte](./F012-kontakte.md)

