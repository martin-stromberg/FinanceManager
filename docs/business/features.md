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
| F007 | [Wertpapierpreise](./features/F007-wertpapierpreise.md) | Automatische Kursaktualisierung mit behobenem AlphaVantage-Fehlerfall, sicheren Hinweisen und robustem Retry-Verhalten | ✅ Dokumentiert |
| F017 | [Backfill-Fehlerbenachrichtigung](../../Docs/business/features/F017-backfill-fehlerbenachrichtigung.md) | Benachrichtigt gezielt bei dauerhaften Kursabruf-Fehlern im Nachlade-Lauf und läuft bei anderen Wertpapieren weiter | ✅ Dokumentiert |
| F008 | Budgetplanung | Festlegung und Überwachung von Budgets | 🔄 In Bearbeitung |
| F009 | Budgetberichte | Auswertung und Analyse von Budget-Abweichungen | 🔄 In Bearbeitung |
| F010 | Ersparnispläne | Planung und Verfolgung von Sparziele | 🔄 In Bearbeitung |
| F011 | Belege & Anhänge | Verwaltung von Rechnungen, Quittungen und Dokumenten | ✅ Dokumentiert |
| F012 | Kontakte | Verwaltung von Geschäftspartnern und Kontakten | ✅ Dokumentiert |
| F013 | Benachrichtigungen | Automatische Erinnerungen und Benachrichtigungen | 🔄 In Bearbeitung |
| F014 | Benutzereinstellungen | Personalisierung der Anwendung | ✅ Dokumentiert |
| F015 | Datensicherung | Backup und Wiederherstellung von Daten | 🔄 In Bearbeitung |
| F016 | Berichte & Dashboards | Finanzielle Übersichtsberichte und Auswertungen | ✅ Dokumentiert |

---

## Status der Dokumentation

**Web Layer (Agent 1):** ✅ 9 Features dokumentiert
- F001 – Kontenübersicht (Benutzer-Perspektive)
- F002 – Kontenverwaltung
- F003 – Ausgabenverwaltung (Benutzer-Perspektive)
- F004 – Kontoauszug-Import (Benutzer-Perspektive)
- F006 – Wertpapier-Verwaltung (Benutzer-Perspektive)
- F011 – Belege & Anhänge
- F012 – Kontakte (Benutzer-Perspektive)
- F014 – Benutzereinstellungen
- F016 – Berichte & Dashboards

**Application Layer (Agent 2):** ✅ 8 Features dokumentiert
- F005 – Automatische Kategorisierung
- F007 – [Wertpapierpreise](./features/F007-wertpapierpreise.md) (Business-Perspektive)
- F008 – Budgetplanung (Business-Perspektive)
- F009 – Budgetberichte
- F010 – Ersparnispläne
- F013 – Benachrichtigungen (Business-Perspektive)
- F015 – Datensicherung (Business-Perspektive)
- F017 – [Backfill-Fehlerbenachrichtigung](../../Docs/business/features/F017-backfill-fehlerbenachrichtigung.md) (Business-Perspektive)

**Infrastructure Layer (Agent 3):** ✅ 4 Features dokumentiert (technische Vertiefung)
- F004 – Kontoauszug-Import (technische Details: Parser, Formate)
- F007 – [Wertpapierpreise (Infrastructure-Perspektive)](./features/F007-wertpapierpreise-infrastructure.md)
- F013 – Benachrichtigungen (technische Details: E-Mail, Holiday-API)
- F015 – Datensicherung (technische Details: Backup-Format, Restore-Prozess)

**Domain Layer (Agent 4):** ✅ 5 Features dokumentiert (fachliche Datenmodelle)
- F001 – Kontenübersicht (Account-Entität, Geschäftsregeln)
- F003 – Ausgabenverwaltung (Posting-Entität, Aggregates)
- F006 – Wertpapier-Verwaltung (Security-Entität, Portfolio-Berechnung)
- F008 – Budgetplanung (BudgetRule, BudgetCategory, BudgetOverride)
- F012 – Kontakte (Contact-Entität, Self-Contact, Kontakt-Gruppen)

**Gesamtfortschritt:** ✅ 100% – Alle 17 Features vollständig dokumentiert

**Dokumentations-Art:**
- **Benutzer-Perspektive** (Web Layer): Schritt-für-Schritt für Endbenutzer
- **Business-Perspektive** (Application Layer): Geschäftsprozesse und Use-Cases
- **Technische Perspektive** (Infrastructure Layer): Externe APIs, Datenformate, Integration
- **Domain-Perspektive** (Domain Layer): Datenmodelle, Geschäftsregeln, Aggregates

**Letzte Aktualisierung**: 09.05.2026

---

## Hinweise zur Verwendung

- Jedes Feature hat eine eigene Detailseite mit Schritt-für-Schritt-Anleitungen.
- Die Dokumentation richtet sich an Fachanwender ohne IT-Hintergrund.
- Fragen oder Ergänzungswünsche bitte über das Projekt-Wiki erfassen.
