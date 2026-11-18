# Anforderung – Schritt 2: Diagramm konfigurieren

## Quelle
Issue #540, Teil „Berichte erweitern / Diagramme“.

## Umfang
- Favoriten speichern Diagrammtyp und Position relativ zur Tabelle.
- Diagrammtypen: historische Summe (Bestand), Aufteilung als Balken mit Entitäten und Beträgen, Kreisdiagramm mit Entitäten und Beträgen.
- Das Kreisdiagramm gruppiert die kleinsten Werte bei Bedarf zu „Sonstiges“, sodass höchstens zehn Segmente/Farben entstehen.
- Position über oder unter der Tabelle.
- API, ViewModel, UI, Backup/Restore und Migration bleiben abwärtskompatibel.

## Abnahmekriterien
- Neue Favoriten speichern und laden beide Einstellungen.
- Bestehende Favoriten und alte Backups verwenden Historie über der Tabelle als Standard.
- Das Kreisdiagramm zeigt höchstens zehn Segmente.
- Alle neuen Enum-Werte sind durch `EnumCoverageTests` abgesichert.
