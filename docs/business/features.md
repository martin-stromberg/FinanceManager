# FinanceManager – Features & Funktionen

Dies ist die zentrale Übersicht aller Features der FinanceManager-Anwendung. Jedes Feature ist dokumentiert für nicht-technische Nutzer, Sachbearbeiter und Stakeholder.

**Legende:**
- **Kennung**: Eindeutige Feature-ID (z.B. F001)
- **Name**: Verständlicher Feature-Name
- **Kurzbeschreibung**: Was leistet das Feature?
- **Pfad**: Link zur Detaildokumentation

---

## Features nach Funktionsbereich

| Kennung | Name | Kurzbeschreibung | Status |
|---------|------|------------------|--------|
| F001 | Kontenübersicht | Ansicht aller Konten und deren Saldo | ✅ Dokumentiert |
| F002 | Kontenverwaltung | Anlage, Bearbeitung und Archivierung von Konten | ✅ Dokumentiert |
| F003 | Ausgabenverwaltung | Erfassung und Verwaltung von Ausgaben und Einnahmen | ✅ Dokumentiert |
| F004 | Kontoauszug-Import | Import von Kontoauszügen aus verschiedenen Bankformaten | ✅ Dokumentiert |
| F005 | Automatische Kategorisierung | Automatische Zuordnung von Ausgaben zu Kategorien | 🔄 In Bearbeitung |
| F006 | Wertpapier-Verwaltung | Verwaltung von Aktien und Wertpapieren | ✅ Dokumentiert |
| [F007](./features/F007-wertpapierpreise-ing-csv-import.md) | Wertpapierpreise (ING-CSV-Import) | Import von ING-Kursdateien auf der Wertpapier-Kursseite mit klarer Ergebnisübersicht | ✅ Dokumentiert |
| F008 | Budgetplanung | Festlegung und Überwachung von Budgets | 🔄 In Bearbeitung |
| F009 | Budgetberichte | Auswertung und Analyse von Budget-Abweichungen | 🔄 In Bearbeitung |
| F010 | Ersparnispläne | Planung und Verfolgung von Sparziele | 🔄 In Bearbeitung |
| F011 | Belege & Anhänge | Verwaltung von Rechnungen, Quittungen und Dokumenten | ✅ Dokumentiert |
| F012 | Kontakte | Verwaltung von Geschäftspartnern und Kontakten | ✅ Dokumentiert |
| F013 | Benachrichtigungen | Automatische Erinnerungen und Benachrichtigungen | 🔄 In Bearbeitung |
| F014 | Benutzereinstellungen | Personalisierung der Anwendung | ✅ Dokumentiert |
| F015 | Datensicherung | Backup und Wiederherstellung von Daten | 🔄 In Bearbeitung |
| F016 | Berichte & Dashboards | Finanzielle Übersichtsberichte und Auswertungen | ✅ Dokumentiert |
| F017 | Wertpapier-Renditeanalyse | Performance-Analyse mit TWR, IRR, CAGR, Sharpe Ratio, Max. Drawdown | 🔄 In Bearbeitung |
| F018 | Budgetwirkung bei Buchung | Sofort-Hinweise und Abschluss-Summary zur Budgetauswirkung beim Buchen von Draft-Entries | ✅ Dokumentiert |
| [F019](./features/F019-buchungsstornierung.md) | Buchungsstornierung | Stornierung einer Buchung durch automatische Gegenbuchung mit umgekehrtem Betrag | ✅ Dokumentiert |

---

## Status der Dokumentation

**Web Layer (Agent 1):** ✅ 10 Features dokumentiert
- F001 – Kontenübersicht (Benutzer-Perspektive)
- F002 – Kontenverwaltung
- F003 – Ausgabenverwaltung (Benutzer-Perspektive)
- F004 – Kontoauszug-Import (Benutzer-Perspektive)
- F006 – Wertpapier-Verwaltung (Benutzer-Perspektive)
- F011 – Belege & Anhänge
- F012 – Kontakte (Benutzer-Perspektive)
- F014 – Benutzereinstellungen
- F016 – Berichte & Dashboards
- F018 – Budgetwirkung bei Buchung
- F019 – Buchungsstornierung

**Application Layer (Agent 2):** ✅ 7 Features dokumentiert
- F005 – Automatische Kategorisierung
- F007 – Wertpapierpreise (Business-Perspektive)
- F008 – Budgetplanung (Business-Perspektive)
- F009 – Budgetberichte
- F010 – Ersparnispläne
- F013 – Benachrichtigungen (Business-Perspektive)
- F015 – Datensicherung (Business-Perspektive)

**Infrastructure Layer (Agent 3):** ✅ 4 Features dokumentiert (technische Vertiefung)
- F004 – Kontoauszug-Import (technische Details: Parser, Formate)
- F007 – Wertpapierpreise (technische Details: AlphaVantage API)
- F013 – Benachrichtigungen (technische Details: E-Mail, Holiday-API)
- F015 – Datensicherung (technische Details: Backup-Format, Restore-Prozess)

**Domain Layer (Agent 4):** ✅ 5 Features + 1 in Bearbeitung (fachliche Datenmodelle)
- F001 – Kontenübersicht (Account-Entität, Geschäftsregeln)
- F003 – Ausgabenverwaltung (Posting-Entität, Aggregates)
- F006 – Wertpapier-Verwaltung (Security-Entität, Portfolio-Berechnung)
- F008 – Budgetplanung (BudgetRule, BudgetCategory, BudgetOverride)
- F012 – Kontakte (Contact-Entität, Self-Contact, Kontakt-Gruppen)
- F017 – Wertpapier-Renditeanalyse (Kennzahlen, FIFO, Sicherheitsregeln) 🔄

**Gesamtfortschritt:** 🔄 In Bearbeitung – 18 von 19 Features vollständig dokumentiert (F017 in Bearbeitung)

**Dokumentations-Art:**
- **Benutzer-Perspektive** (Web Layer): Schritt-für-Schritt für Endbenutzer
- **Business-Perspektive** (Application Layer): Geschäftsprozesse und Use-Cases
- **Technische Perspektive** (Infrastructure Layer): Externe APIs, Datenformate, Integration
- **Domain-Perspektive** (Domain Layer): Datenmodelle, Geschäftsregeln, Aggregates

**Letzte Aktualisierung**: 02.07.2026

---

## Hinweise zur Verwendung

- Jedes Feature hat eine eigene Detailseite mit Schritt-für-Schritt-Anleitungen.
- Die Dokumentation richtet sich an Fachanwender ohne IT-Hintergrund.
- Fragen oder Ergänzungswünsche bitte über das Projekt-Wiki erfassen.
