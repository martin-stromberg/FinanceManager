# F013 – Benachrichtigungen

## Einleitung

Das Benachrichtigungssystem informiert Sie über wichtige finanzielle Ereignisse in Echtzeit. Das System generiert Benachrichtigungen bei:
- Budget-Überschreitungen
- Sparplan-Meilensteinen
- Anderen konfigurierten Events

Sie können Benachrichtigungen in der Anwendung anzeigen und verwalten. 

**Hinweis**: Die Konfiguration von Benachrichtigungstypen erfolgt über **Benutzereinstellungen (F014)**, nicht hier.

## Wer nutzt es?

**Sachbearbeiter und Finanzverwalter** nutzen diese Funktion, um:
- Aktive Benachrichtigungen einzusehen
- Benachrichtigungen zu bearbeiten
- Benachrichtigungen zu verwerfen
- Notification-Feed zu überblicken

Dies hilft bei der schnellen Reaktion auf kritische Ereignisse.

## Schritt-für-Schritt-Anleitung

### Benachrichtigungen anzeigen

1. Sie navigieren zu **Benachrichtigungen** (üblicherweise ein Icon in der Header/Menü)
2. Das System zeigt alle **aktiven Benachrichtigungen** an (zeitlich gefiltert nach UTC-Zeit)
3. Sie sehen:
   - Benachrichtigungstext
   - Zeitpunkt der Benachrichtigung
   - *(Optional)* Link zur betroffenen Entität

### Benachrichtigung bearbeiten/anzeigen

1. Sie klicken auf eine Benachrichtigung
2. Sie sehen Details der Benachrichtigung
3. Sie können:
   - Die Benachrichtigung **lesen/verstehen**
   - Zur betroffenen Entität **navigieren** (z.B. zum Budget)

### Benachrichtigung verwerfen/abdismissen

1. Sie öffnen eine Benachrichtigung
2. Sie klicken **Verwerfen**, **Schließen** oder **X**
3. Die Benachrichtigung wird als "dismisst" markiert
4. Sie wird aus der aktiven Benachrichtigungsliste entfernt

### Benachrichtigungsfilter (Optional)

1. Die API unterstützt Filterung nach:
   - **Status**: Aktiv, verworfen, archiviert
   - **Typ**: Budget, SavingsPlan, etc.
   - **Datum**: Von/Bis

*(Dies hängt von der UI-Implementierung ab)*

## Datenfelder

### Alle Benachrichtigungen haben:
- **Id**: Eindeutige Identifikation
- **UserId**: Gehört dem aktuellen Benutzer
- **Title**: Benachrichtigungstitel
- **Message**: Benachrichtigungstext
- **Type**: Art der Benachrichtigung (Budget, SavingsPlan, etc.)
- **CreatedAt**: UTC-Zeitpunkt der Benachrichtigung
- **DismissedAt**: UTC-Zeitpunkt des Verwerfens (optional)
- **EntityLink**: Link zur betroffenen Entität (optional)

## Beispiele

### Beispiel 1: Budget-Benachrichtigung
```
Titel: "Budget überschritten"
Message: "Ihr Budget für 'Bürokosten' wurde überschritten (€ 4.500 von € 5.000)"
Type: BudgetExceeded
CreatedAt: 2026-04-11T11:30:00Z
EntityLink: /budgets/xyz
```

### Beispiel 2: Sparplan-Benachrichtigung
```
Titel: "Sparziel erreicht!"
Message: "Sie haben 75% Ihres 'Notfallfonds'-Sparplans erreicht (€ 3.750 von € 5.000)"
Type: SavingsPlanMilestone
CreatedAt: 2026-04-11T14:15:00Z
EntityLink: /savingsplans/abc
```

## Technische Details

### API-Endpoints
- `GET /api/notifications` - Alle aktiven Benachrichtigungen abrufen
- `DELETE /api/notifications/{id}` oder `POST /api/notifications/{id}/dismiss` - Benachrichtigung verwerfen

### Filtern
Benachrichtigungen werden automatisch gefiltert nach:
- **Benutzer**: Nur Benachrichtigungen des aktuellen Benutzers
- **Zeit**: Nur Benachrichtigungen die noch gültig sind (DateTime.UtcNow)
- **Status**: Nur aktive (nicht verworfene) Benachrichtigungen

### Notification Types (Beispiele)
- `BudgetExceeded` - Budget überschritten
- `SavingsPlanMilestone` - Sparplan-Ziel erreicht
- `StatementImported` - Kontoauszug importiert
- Weitere abhängig von Geschäftslogik

## Häufige Fragen (FAQ)

**F: Wo konfiguriere ich Benachrichtigungstypen?**  
A: In den **Benutzereinstellungen (F014)**, nicht hier. Dieses Feature zeigt nur Benachrichtigungen an.

**F: Werden Benachrichtigungen per E-Mail versendet?**  
A: Das hängt von den Benutzereinstellungen ab. Im NotificationsController werden nur In-App-Benachrichtigungen verwaltet.

**F: Kann ich alte Benachrichtigungen sehen?**  
A: Das NotificationsController zeigt nur "aktive" Benachrichtigungen. Ein Verlauf ist optional nicht in dieser API implementiert.

**F: Kann ich Benachrichtigungen zeitbasiert filtern?**  
A: Ja, das System filtert automatisch nach UTC-Zeit. Nur zeitlich gültige Benachrichtigungen werden angezeigt.

**F: Was passiert, wenn ich eine Benachrichtigung verwerfe?**  
A: Sie wird aus der aktiven Liste entfernt (DismissedAt wird gesetzt), aber nicht gelöscht.

**F: Werden Benachrichtigungen automatisch archiviert?**  
A: Das hängt von der Geschäftslogik ab. Aktuell können sie manuell verworfen werden.

## Hinweise

- **Echtzeit-System**: Benachrichtigungen werden asynchron generiert
- **UTC-Zeit**: Alle Zeitpunkte sind in UTC
- **Benutzer-isoliert**: Jeder Benutzer sieht nur seine eigenen Benachrichtigungen
- **Konfiguration woanders**: Benachrichtigungstypen und Einstellungen sind in Benutzereinstellungen (F014)
- **API-fokussiert**: NotificationsController ist primär für die API, nicht für UI-Darstellung

## Verwandte Funktionen

- [F017 – Backfill-Fehlerbenachrichtigung](../../../Docs/business/features/F017-backfill-fehlerbenachrichtigung.md)
- [F014 – Benutzereinstellungen](./F014-benutzereinstellungen.md) ← Benachrichtigungen KONFIGURIEREN
- [F008 – Budgetplanung](./F008-budgetplanung.md)
- [F010 – Ersparnispläne](./F010-ersparnisplaene.md)

