# Planprüfung – Schritt 2

- Persistenz: Beide Einstellungen sind nicht-nullbare Enum-Spalten mit Standard `0`; vorhandene Favoriten behalten Historie über der Tabelle.
- Kompatibilität: DTO- und Backup-Parameter haben Defaults; alte API-Payloads und Backups ohne die Felder bleiben lesbar.
- Kreisdiagramm: Es werden die Betragswerte der aktuell sichtbaren Top-Level-Entitäten verwendet. Bei mehr als zehn Entitäten bleiben die neun größten sichtbar, die übrigen werden als „Sonstiges“ summiert; damit bleiben maximal zehn Farben.
- Negativwerte: Balken behalten das Vorzeichen farblich; Kreissegmente verwenden Beträge, weil Kreisflächen keine negativen Anteile abbilden können.
- Position: Die Diagramm-/Tabellenregion wird über CSS geordnet; ohne Diagramm bleibt die Tabelle unverändert.
