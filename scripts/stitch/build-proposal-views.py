# Generates scripts/stitch/proposal-views.json from inventory-routes.json.
# One entry per inventoried view with a constraining description for Stitch.
import json

routes = json.load(open('scripts/screenshots/inventory-routes.json', encoding='utf8'))

TITLES = {
    'login': 'Anmeldung', 'register': 'Registrierung', 'error': 'Fehlerseite',
    'legal': 'Impressum / Rechtliches', 'help-hub': 'Hilfe-Uebersicht',
    'help-view': 'Hilfe-Artikel', 'home': 'Start / Dashboard',
    'change-password': 'Passwort aendern', 'reports': 'Berichte-Uebersicht',
    'reports-dashboard': 'Berichts-Dashboard', 'reports-budget': 'Budgetbericht',
    'portfolio-analysis-report': 'Portfolio-Analysebericht',
    'setup-sections': 'Einrichtung (Setup-Sektionen)',
    'overlay-home-mass-import': 'Massenimport-Dialog',
    'overlay-budget-settings': 'Budgeteinstellungen-Dialog',
    'overlay-dashboard-filters': 'Dashboard-Filter-Dialog',
    'overlay-mcp-access-wizard': 'KI-Zugang-Assistent',
    'overlay-confirm-dialog': 'Bestaetigungsdialog'}

ENTITY = {
    'list-accounts': ('Konten', 'Kontoliste: Name, Typ, IBAN, aktueller Saldo'),
    'list-contacts': ('Kontakte', 'Kontaktliste: Name, Art (Person/Unternehmen), Gruppe/Kategorie'),
    'list-contacts-categories': ('Kontaktkategorien', 'Kategorieliste fuer Kontakte: Name, zugeordnete Kontakte'),
    'list-postings-account': ('Buchungen eines Kontos', 'Buchungsliste: Datum, Beschreibung, Betrag, Empfaenger, Postenart; Summenzeile'),
    'list-postings-contact': ('Buchungen eines Kontakts', 'Buchungsliste eines Kontakts: Datum, Beschreibung, Betrag, Konto'),
    'list-postings-savings-plan': ('Buchungen eines Sparplans', 'Buchungsliste eines Sparplans: Datum, Beschreibung, Betrag'),
    'list-postings-security': ('Buchungen eines Wertpapiers', 'Wertpapier-Buchungen: Datum, Art (Kauf/Verkauf/Dividende), Stueckzahl, Betrag'),
    'list-savings-plans': ('Sparplaene', 'Sparplanliste: Name, Intervall, Betrag, Zielkonto, aktiv'),
    'list-savings-plans-categories': ('Sparplankategorien', 'Kategorieliste fuer Sparplaene'),
    'list-securities': ('Wertpapiere', 'Wertpapierliste: Name, ISIN/WKN, Symbol, aktueller Kurs'),
    'list-securities-categories': ('Wertpapierkategorien', 'Kategorieliste fuer Wertpapiere'),
    'list-securities-prices': ('Kurshistorie', 'Kursliste eines Wertpapiers: Datum, Schlusskurs'),
    'list-statement-drafts': ('Kontoauszugs-Entwuerfe', 'Importierte Kontoauszuege als Entwuerfe: Zeitraum, Konto, Anzahl Eintraege, Status'),
    'list-users': ('Benutzerverwaltung', 'Benutzerliste (Admin): Name, E-Mail, Rolle, aktiv/gesperrt'),
    'list-mcp-accesses': ('KI-Zugaenge', 'Liste der KI/MCP-Zugaenge: Name, Stufe, Freigabemodus, Gueltigkeit'),
    'list-budget': ('Budgetplanung', 'Budgetliste: Zeitraum, Zweck, Kategorien, geplant/ist'),
    'list-budget-purposes': ('Budgetzwecke', 'Budgetzwecke: Name, Beschreibung, zugeordnete Kategorien'),
    'list-budget-categories': ('Budgetkategorien', 'Budgetkategorien: Name, Postenart, Zweck-Zuordnung'),
    'card-account': ('Konto-Detailkarte', 'Kontodetails: Name, IBAN, Typ, Saldo, Symbol, Bankkontakt, Sparplanangabe, Flags (Wertpapierabwicklung, Sammelkonto); darunter Ergebnisdiagramm (Balken pro Monat)'),
    'card-budget-category': ('Budgetkategorie-Detailkarte', 'Kategoriedetails: Name, Postenart, Zweck'),
    'card-budget-purpose': ('Budgetzweck-Detailkarte', 'Zweckdetails: Name, Beschreibung, Gueltigkeit, zugeordnete Kategorien, Budgetregeln'),
    'card-budget-rule': ('Budgetregel-Detailkarte', 'Budgetregel: Betrag, Intervall, Gueltigkeit ab/bis'),
    'card-contact': ('Kontakt-Detailkarte', 'Kontaktdetails: Name, Art, Gruppe, Beschreibung, Symbol, Zahlungsdaten; alternative Namen (Alias-Liste); darunter Bewegungsdiagramm'),
    'card-contact-category': ('Kontaktkategorie-Detailkarte', 'Kategoriedetails: Name, zugeordnete Kontakte'),
    'card-mcp-access': ('KI-Zugang-Detailkarte', 'KI-Zugang: Name, Stufe, Freigabemodus, Ablaufdatum, API-Schluessel (maskiert/anzeigbar), Widerrufen'),
    'card-mcp-access-new': ('Neuen KI-Zugang anlegen', 'Formular: Name, Stufe, Freigabemodus, Gueltigkeit'),
    'card-mcp-approvals': ('KI-Freigaben', 'Offene Freigabeanfragen des KI-Zugangs: Liste mit Anfrage, Zeitpunkt, Genehmigen/Ablehnen'),
    'card-mcp-audit': ('KI-Auditprotokoll', 'Audit-Liste: Zeitpunkt, Aktion, Ergebnis, eingeloggter Zugang'),
    'card-mcp-security': ('KI-Sicherheit', 'Sicherheitsuebersicht der KI-Zugaenge: Status-Kacheln, Hinweise'),
    'card-posting': ('Buchungs-Detailkarte', 'Buchungsdetails: Datum, Betrag, Empfaenger/Zahlungspflichtiger, Beschreibung, Konto, Postenart, Rueckbuchung'),
    'card-savings-plan': ('Sparplan-Detailkarte', 'Sparplandetails: Name, Intervall, Betrag, Zielkonto, Kategorie, aktiv; darunter Bewegungsdiagramm'),
    'card-savings-plan-category': ('Sparplankategorie-Detailkarte', 'Kategoriedetails: Name'),
    'card-security': ('Wertpapier-Detailkarte', 'Wertpapierdetails: Name, ISIN/WKN, Symbol, Kategorie, Waehrung; Kurshistorie-Diagramm'),
    'card-security-category': ('Wertpapierkategorie-Detailkarte', 'Kategoriedetails: Name'),
    'card-setup': ('Einrichtung', 'Setup-Seite: Akkordeon-Sektionen (Systeminformationen, Benachrichtigungen, Sicherungen, Wartungsmodus, Anhaenge, Sprache u. a.), jeweils mit eigenen Formularinhalten'),
    'card-user': ('Benutzer-Detailkarte', 'Benutzerdetails (Admin): Name, E-Mail, Rolle, aktiv; zusaetzlich Liste der Konten des Benutzers'),
    'card-statement-draft': ('Kontoauszug-Entwurf', 'Import-Entwurf: Metadaten (Zeitraum, Konto, Status) und Tabelle der Auszugseintraege'),
    'card-statement-draft-entry': ('Auszugseintrag-Detail', 'Einzeleintrag des Auszugs: Datum, Betrag, Buchungstext, Postenart-Zuordnung, Klassifikation'),
    'security-performance': ('Wertpapier-Performance', 'Performance-Ansicht eines Wertpapiers mit Sub-Tabs (Uebersicht, Zeitreihe, Cashflows, Kennzahlen, Benchmark)'),
    'security-performance-overview': ('Performance > Uebersicht', 'Sub-Tab Uebersicht: Kennzahlen-Kacheln und Diagramm'),
    'security-performance-timeseries': ('Performance > Zeitreihe', 'Sub-Tab Zeitreihe: Liniendiagramm der Entwicklung'),
    'security-performance-cashflows': ('Performance > Cashflows', 'Sub-Tab Cashflows: Tabelle/Diagramm der Ein- und Auszahlungen'),
    'security-performance-metrics': ('Performance > Kennzahlen', 'Sub-Tab Kennzahlen: Tabelle mit Metriken (IRR, TWR u. a.)'),
    'security-performance-benchmark': ('Performance > Benchmark', 'Sub-Tab Benchmark: Vergleichsdiagramm mit Referenzindex')}

DESC = {
    'login': 'Oeffentliche Anmeldeseite: zentrierte Karte mit Logo, E-Mail- und Passwortfeld, Anmelden-Button, Link zur Registrierung. KEINE Seitennavigation und KEIN Ribbon auf dieser Seite.',
    'register': 'Oeffentliche Registrierungsseite: zentrierte Karte mit Feldern (Name, E-Mail, Passwort, Passwort bestaetigen), Registrieren-Button, Link zur Anmeldung. KEINE Seitennavigation und KEIN Ribbon.',
    'error': 'Fehlerseite: zentrierte Fehlermeldung (Fehler aufgetreten), Link zur Startseite. KEINE Seitennavigation und KEIN Ribbon.',
    'legal': 'Impressumsseite: schmaler Textbereich mit rechtlichen Angaben, Zurueck-Link. Schlichte oeffentliche Seite ohne Ribbon.',
    'help-hub': 'Hilfe-Uebersicht (oeffentlich): Suchfeld oben, Kartenliste der Hilfethemen (Konten und Buchungen, Kontakte, Sparplaene, Wertpapiere, Import, Budget, Berichte, Einrichtung).',
    'help-view': 'Hilfe-Artikel (oeffentlich): Artikeltitel, Brotkruemel, Fliesstext mit Zwischenueberschriften und Screenshot-Platzhaltern; links ein Inhaltsverzeichnis der Hilfethemen.',
    'home': 'Startseite/Dashboard: Ribbon mit einer Gruppe (Massenimport starten). Content: KPI-Kacheln-Reihe (Gesamtsaldo, Konten, offene Entwuerfe, Budgetstatus), darunter Aktivitaets-/Buchungsverlauf-Listenbereich.',
    'change-password': 'Passwort-aendern-Seite im Standard-Layout: schmales Formular (aktuelles Passwort, neues Passwort, bestaetigen), Speichern-Button.',
    'reports': 'Berichte-Uebersicht: Kartenliste der verfuegbaren Berichte (Dashboard, Budgetbericht, Portfolio-Analyse) mit Kurzbeschreibung; Klick oeffnet den Bericht.',
    'reports-dashboard': 'Berichts-Dashboard: Ribbon mit Filter-Button (oeffnet Filterdialog). Content: mehrere Chart-Karten (Ausgaben/Einnahmen ueber Zeit, Top-Kategorien Donut, Kontoentwicklung Linie) im Raster.',
    'reports-budget': 'Budgetbericht: Ribbon mit Einstellungen-Button. Content: Budgetuebersicht mit Fortschrittsbalken pro Kategorie/Zweck, Soll-/Ist-Werte, Gesamtsummen.',
    'portfolio-analysis-report': 'Portfolio-Analysebericht: Kennzahlen-Zeile (Gesamtwert, Rendite, Volatilitaet), Allokations-Donut nach Kategorien, Performance-Liniendiagramm, Tabelle der Positionen.',
    'setup-sections': 'Setup-Seite mit aufgeklappter Akkordeon-Sektion: Sektionstitel als aufklappbare Header, darin Formularfelder (Zeitraeume, Schwellwerte, Schalter) und Sektions-Aktionsbuttons.',
    'overlay-home-mass-import': 'Modaler Dialog ueber der Startseite: Massenimport von Kontoauszuegen — Dateiauswahl/Dropzone, Hinweis auf Dateiformate, Abbrechen/Importieren-Buttons; Seite dahinter abgedunkelt.',
    'overlay-budget-settings': 'Modaler Dialog ueber dem Budgetbericht: Budgeteinstellungen — Eingabefelder fuer Betragsgrenzen/Zeitraum, Abbrechen/Uebernehmen-Buttons.',
    'overlay-dashboard-filters': 'Modaler Dialog ueber dem Berichts-Dashboard: Filtereinstellungen — Kontrollkaestchen pro Berichtskarte (welche Diagramme angezeigt werden), Datumsbereich; Abbrechen/Uebernehmen.',
    'overlay-mcp-access-wizard': 'Modaler Mehrschritt-Assistent ueber der KI-Zugangsliste: Schritt-Anzeige, Formularfelder (Name, Stufe, Freigabemodus, Gueltigkeit), Zurueck/Weiter/Abbrechen-Buttons.',
    'overlay-confirm-dialog': 'Kleiner modaler Bestaetigungsdialog ueber einer Detailkarte: Titel "Loeschen bestaetigen", Hinweistext, Abbrechen- und Bestaetigen-Button (primaer).'}

views = []
for v in routes['views']:
    vid, group = v['id'], v['group']
    title = TITLES.get(vid)
    if vid in DESC:
        desc = DESC[vid]
    elif group == 'list':
        title = title or ENTITY[vid][0]
        desc = ('Listenansicht (' + ENTITY[vid][1] + '). Aufbau: Filter-/Suchleiste oben, darunter Datentabelle mit genau diesen Spalten; Zeilenklick fuehrt zur Detailseite. '
                'Auf kleinen Screens wird die Tabelle als Kartenliste dargestellt. Ribbon: "Neu"-Aktion (Gruppe Verwaltung); bei Buchungslisten zusaetzlich eine Filtergruppe.')
    elif group == 'card':
        title = title or ENTITY[vid][0]
        desc = (ENTITY[vid][1] + '. Aufbau: Ribbon mit Gruppen in dieser Reihenfolge — Navigation (Zurueck), Verwalten (Speichern, Zuruecksetzen, Loeschen), evtl. weitere Aktionsgruppen, Verknuepfte Informationen. '
                'Content: Formularfelder in Label/Wert-Zeilen.')
    elif vid in ENTITY:
        title = title or ENTITY[vid][0]
        desc = ENTITY[vid][1] + '.'
    else:
        desc = title or vid
    views.append({'id': vid, 'title': title or vid, 'description': desc})

with open('scripts/stitch/proposal-views.json', 'w', encoding='utf8') as f:
    json.dump({'views': views}, f, ensure_ascii=False, indent=2)
print('wrote', len(views), 'views')
