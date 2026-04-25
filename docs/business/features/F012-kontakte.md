# F012 – Kontakte

## Einleitung

Die Kontakte-Verwaltung ermöglicht es Ihnen, Geschäftspartner, Lieferanten, Kunden und andere Kontakte zu registrieren. Sie können Kontaktinformationen speichern, kategorisieren und später schnell darauf zuzugreifen. 

Kontakte werden oft mit Transaktionen verknüpft (z.B. "Zahlung an Lieferant XYZ"). Sie können auch:
- **Aliasse**: Alternative Namen/Schreibweisen speichern (z.B. "Google" vs "Alphabet Inc")
- **Verschmelzung**: Doppelte Kontakte zusammenfassen
- **Symbole**: Logos hochladen für visuelle Identifikation

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion täglich, um:
- Geschäftspartner zu erfassen
- Kontakte zu kategorisieren
- Kontakt-Details zu verwalten
- Doppelte Kontakte zu bereinigen

Dies unterstützt die Dokumentation von Geschäftsbeziehungen und macht Transaktionsverfolgung effizienter.

## Schritt-für-Schritt-Anleitung

### Neuen Kontakt erstellen

1. Sie navigieren zu **Kontakte**
2. Sie klicken **Neuer Kontakt** oder **Kontakt hinzufügen**
3. Sie füllen die erforderlichen Informationen aus:
   - **Name**: Name der Person oder Firma (erforderlich)
   - **Kontakttyp**: Person oder Firma (erforderlich)
4. *(Optional)* Sie geben zusätzliche Informationen ein:
   - **E-Mail**: E-Mail-Adresse
   - **Telefon**: Telefonnummer
   - **Adresse**: Postadresse
   - **Aliasse**: Alternative Namen (z.B. "Google" sowie "Alphabet Inc")
5. *(Optional)* Sie laden ein **Symbol** (Logo/Icon) hoch
6. Sie klicken **Speichern**

### Kontakt anzeigen und bearbeiten

1. Sie navigieren zu **Kontakte**
2. Sie können nach Name filtern (Suchfeld **q**)
3. Sie können nach **Kontakttyp** filtern (Person/Firma)
4. Sie klicken auf einen Kontakt um die Details zu sehen
5. Sie können:
   - **Informationen ändern**: Name, Kontaktdaten aktualisieren
   - **Aliasse hinzufügen**: Alternative Namen für bessere Suche
   - **Symbol hochladen**: Logo für visuelle Identifikation
   - **Mit anderen Kontakten verknüpfen**: Um eine neue Klasse, Firma oder ähnliches hinzuzufügen

### Doppelte Kontakte zusammenfassen

1. Sie öffnen zwei identische oder sehr ähnliche Kontakte
2. Sie klicken auf einen: **Mit anderem Kontakt verschmelzen**
3. Sie wählen den Ziel-Kontakt
4. Die Transaktionen werden automatisch auf den neuen Kontakt übertragen
5. Der alte Kontakt wird gelöscht (oder archiviert)

### Kontakt löschen

1. Sie öffnen den Kontakt
2. Sie klicken **Löschen**
3. Falls der Kontakt Transaktionen hat: Die Transaktion bleibt erhalten, aber der Kontakt-Link wird entfernt
4. Der Kontakt wird gelöscht

### Kontakte verwalten

1. Sie öffnen die **Kontaktliste**
2. Sie können:
   - **Nach Name suchen**: Geben Sie Teil des Namens ein
   - **Nach Typ filtern**: Nur "Personen" oder "Firmen" anzeigen
   - **Alle anzeigen**: Komplette Liste ohne Pagination laden

## Datenfelder

### Erforderlich:
- **Name**: Der Kontaktname (Person oder Firma)
- **ContactType**: Person oder Firma

### Optional:
- **Email**: E-Mail-Adresse
- **Phone**: Telefonnummer
- **Address**: Postadresse
- **Symbol**: Logo/Icon als Anhang (Attachment mit Rolle "Symbol")

### Spezial-Features:
- **Aliases**: Liste von alternativen Namen (z.B. Firmennamen in verschiedenen Sprachen/Schreibweisen)
- **Merging**: Zwei Kontakte können zusammengeführt werden
- **Transaction Count**: Anzahl der zugeordneten Transaktionen

## Beispiele

### Beispiel 1: Einfacher Kontakt
Sie arbeiten regelmäßig mit der Agentur "WebDesign Plus" zusammen:

- **Name**: "WebDesign Plus GmbH"
- **ContactType**: Firma
- **Email**: "info@webdesign-plus.de"
- **Phone**: "+49 30 12345678"
- **Address**: "Musterstraße 1, 10115 Berlin"

### Beispiel 2: Kontakt mit Aliassen
Sie haben einen großen Kunden mit mehreren Namen:

- **Name**: "Google LLC"
- **ContactType**: Firma
- **Aliases**: 
  - "Google"
  - "Alphabet Inc"
  - "Alphabet Group"
  - "Google Germany"

Das System kann dann Transaktionen mit "Google" oder "Alphabet" automatisch diesem Kontakt zuordnen.

### Beispiel 3: Kontakt mit Symbol
Sie möchten die Bank mit ihrem Logo identifizieren:

- **Name**: "Deutsche Bank"
- **ContactType**: Firma
- **Symbol**: Logo hochladen (PNG/JPG)

Das Bank-Logo wird dann überall wo dieser Kontakt verwendet wird angezeigt.

## Technische Details

### Kontakttypen
- **Person**: Einzelne Person (z.B. Freelancer, Einzelunternehmer)
- **Firma**: Unternehmen (z.B. GmbH, AG, Verein)

### Datenbankmodell
- Kontakte sind Benutzer-spezifisch (gehören zu einem Account)
- Jeder Kontakt kann mehrere Transaktionen haben
- Aliases sind ein Array/Liste von Strings
- Merging erstellt automatisch Redirects und transferiert Transaktionen

### API-Endpoints
- `GET /api/contacts` - Kontakte listen (mit Paging & Filtern)
- `GET /api/contacts/{id}` - Einzelnen Kontakt abrufen
- `POST /api/contacts` - Neuen Kontakt erstellen
- `PUT /api/contacts/{id}` - Kontakt aktualisieren
- `DELETE /api/contacts/{id}` - Kontakt löschen
- `POST /api/contacts/{id}/merge` - Mit anderem Kontakt verschmelzen
- `POST /api/contacts/{id}/aliases` - Aliasse verwalten

## Häufige Fragen (FAQ)

**F: Wie unterscheiden sich Kontakttypen?**  
A: "Person" ist für Einzelpersonen, "Firma" für Unternehmen. Dies wird für Berichterstattung und Filtering verwendet.

**F: Was sind Aliasse und warum brauche ich sie?**  
A: Aliasse sind alternative Namen. Beispiel: "Google" = "Alphabet Inc". Das System kann Transaktionen intelligenter zuordnen.

**F: Was passiert beim Verschmelzen von Kontakten?**  
A: Alle Transaktionen des alten Kontakts werden zum neuen übertragen. Der alte Kontakt wird gelöscht.

**F: Kann ich einen Kontakt mit Transaktionen löschen?**  
A: Ja, aber die Transaktionen bleiben bestehen - nur die Kontakt-Verknüpfung wird entfernt.

**F: Kann ich Kontakte exportieren?**  
A: Das hängt von den Systemeinstellungen ab. Über die API können Sie alle Kontakte abrufen.

**F: Wie funktioniert die Suche?**  
A: Die Suche prüft den Namen und alle Aliasse (Substring-Matching).

**F: Kann ich Symbole/Logos hochladen?**  
A: Ja, laden Sie eine Bilddatei als Anhang mit der Rolle "Symbol" hoch.

## Hinweise

- **Duplikate vermeiden**: Nutzen Sie die Suche um Doppel zu finden, bevor Sie neue Kontakte erstellen
- **Aliasse nutzen**: Für große Firmen mit mehreren Namen - das hilft beim automatischen Matching
- **Symbol hochladen**: Logos helfen bei der visuellen Identifikation in Listen und Transaktionen
- **Kontakt-Limits**: Es gibt keine fixen Limits für Kontaktzahl
- **Archivierung**: Gelöschte Kontakte sind nicht wiederherstellbar

## Verwandte Funktionen

- [F003 – Ausgabenverwaltung / Postings](./F003-ausgabenverwaltung.md)
- [F011 – Belege & Anhänge](./F011-belege-anhaenge.md)

