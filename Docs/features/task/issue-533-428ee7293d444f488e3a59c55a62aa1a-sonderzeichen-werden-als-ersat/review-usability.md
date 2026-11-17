# Usability-Review — Issue #533

Status: **Keine Befunde**

Die UI-Änderung besteht ausschließlich aus der Wiederherstellung einzelner Zeichen in bereits bestehendem Markup — keine neue Interaktion, keine geänderte Struktur, keine internen Kennungen:

- Budget-Kachel: `€` hinter Beträgen und Labels — erwartete Darstellung einer Währung; verifiziert per Screenshot (`verification/home.png`: „2234,17€", „0€").
- Berichtsübersicht: `·` zwischen Postenart und Intervall — dezent, konsistent mit Meta-Trennern anderswo in der App; verifiziert per Screenshot (`verification/reports.png`: „Konten · Jahr").
- Budgetbericht: `–` zwischen Von-/Bis-Datum — übliche Datumsbereich-Notation.
- Alle übrigen Korrekturen liegen in Kommentaren, CSS-Headern, Testdaten und Dokumentation ohne UI-Wirkung.

Keine Barriere für Endanwender; die Änderung stellt die ursprünglich intendierte Lesbarkeit wieder her.
