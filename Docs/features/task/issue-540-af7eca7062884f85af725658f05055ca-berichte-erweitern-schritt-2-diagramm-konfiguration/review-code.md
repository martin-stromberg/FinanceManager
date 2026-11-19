# Review – Schritt 2

## Geprüfte Punkte
- Favoriten-Service wendet `ChartType`/`ChartPosition` in Create, Update, Get und List vollständig an.
- API- und interne Requests besitzen kompatible Standardwerte.
- Backup-DTO enthält beide Felder mit Defaults und wird über `ToBackupDto`/`AssignBackupDto` gespiegelt.
- UI-Auswahl ist lokalisiert, deaktiviert mit dem Diagramm und wird beim Favoritenladen wiederhergestellt.
- Pie-Gruppierung begrenzt auf maximal zehn Segmente und aggregiert den Rest als „Sonstiges“.

## Befund
Kein offener Mangel. Die Kreisdarstellung nutzt Betragswerte, da negative Anteile nicht als Kreissektor darstellbar sind; die Balken- und Tabellendarstellung behalten die Vorzeichen.
