# F019 – Buchungsstornierung

## Einleitung

Mit der Buchungsstornierung können Sie eine bereits erfasste Buchung rückgängig machen. Dabei wird keine Buchung gelöscht – stattdessen legt das System automatisch eine **Gegenbuchung** mit dem umgekehrten Betrag an. So bleibt Ihre Buchungshistorie vollständig und nachvollziehbar. Die ursprüngliche Buchung bleibt erhalten und ist als „storniert" gekennzeichnet. Diese Funktion ist ideal, wenn Sie eine Buchung versehentlich doppelt erfasst oder die falschen Daten eingegeben haben.

## Wer nutzt es?

Diese Funktion nutzen **Finanzverwalter und Sachbearbeiter**, die eigene Buchungen korrigieren müssen. Sie haben Zugriff auf ihre eigenen Buchungen und können diese bei Bedarf stornieren.

## Wann verwende ich die Stornierung?

Typische Anwendungsfälle:

- Sie haben eine Buchung **doppelt erfasst** und möchten die zweite rückgängig machen.
- Sie haben einen **falschen Betrag** oder das **falsche Konto** verwendet.
- Eine Transaktion wurde **irrtümlich importiert** und soll aus der Auswertung herausgenommen werden.
- Ein **Wertpapierkauf oder -verkauf** wurde falsch verbucht und muss korrigiert werden.

## Schritt-für-Schritt-Anleitung

1. Sie navigieren zur **Detailseite der Buchung**, die Sie stornieren möchten.
2. Sie sehen im Aktionsmenü oben auf der Seite die Schaltfläche **Stornieren**.
3. Sie klicken auf **Stornieren**.
4. Das System prüft automatisch, ob die Buchung storniert werden darf.
5. Bei Erfolg erscheint eine **Erfolgsmeldung** mit einem Link zur neu erstellten Gegenbuchung.
6. Die ursprüngliche Buchung ist nun als **storniert** markiert und in der Buchungsliste mit einem Hinweis in der Spalte **Storno** versehen.

> **Hinweis:** Es erscheint kein Bestätigungsdialog. Die Stornierung wird sofort ausgeführt, sobald Sie auf **Stornieren** klicken.

## Beispiel

Sie haben im Januar eine Ausgangsrechnung über 500,00 EUR an einen Kontakt gebucht. Kurz darauf stellen Sie fest, dass die Buchung doppelt erfasst wurde. Sie öffnen die Detailseite der doppelten Buchung und klicken auf **Stornieren**. Das System erstellt sofort eine Gegenbuchung über −500,00 EUR mit demselben Buchungsdatum. Ihr Kontostand ist damit wieder korrekt. Die ursprüngliche Buchung bleibt sichtbar und ist als storniert gekennzeichnet – Sie sehen jederzeit, was passiert ist.

## Was passiert im Hintergrund?

Das System erstellt eine neue Buchung mit dem gleichen Datum und den gleichen Referenzen wie das Original – jedoch mit dem **umgekehrten Vorzeichen** des Betrags. Originalposten und Gegenbuchung sind miteinander verknüpft, sodass die Stornierung im Buchungsjournal vollständig nachvollziehbar bleibt. Wenn zur ursprünglichen Buchung weitere verknüpfte Buchungen gehören (z. B. ein Kontakt- oder Wertpapierposten), werden diese automatisch ebenfalls storniert – alle Gegenbuchungen werden als Gruppe zusammengefasst. Nach Abschluss zeigt das System eine Erfolgsmeldung mit einem direkten Link zur Gegenbuchung.

## Einschränkungen und Regeln

| Situation | Was passiert? |
|-----------|--------------|
| Buchung bereits storniert | Die Schaltfläche **Stornieren** ist deaktiviert. Eine erneute Stornierung ist nicht möglich. |
| Gegenbuchung selbst stornieren | Gegenbuchungen können **nicht** storniert werden. Die Schaltfläche ist deaktiviert. |
| Buchung gehört einem anderen Benutzer | Stornierung ist nicht erlaubt. Die Schaltfläche ist nicht verfügbar. |
| Buchung gehört zu einer Gruppe | **Alle Buchungen der Gruppe** werden gemeinsam storniert (Alles-oder-nichts). |
| Stornierung rückgängig machen | Stornierungen sind **endgültig**. Sie können nicht selbst wieder storniert werden. Korrekturen sind nur durch eine neue Buchung möglich. |

> **Wichtig:** Stornierungen können nicht rückgängig gemacht werden. Prüfen Sie daher sorgfältig, welche Buchung Sie stornieren möchten, bevor Sie auf **Stornieren** klicken.

## Häufige Fragen (FAQ)

**F: Wird die ursprüngliche Buchung gelöscht?**  
A: Nein. Die ursprüngliche Buchung bleibt erhalten und ist als storniert markiert. Sie sehen in der Buchungsliste sowohl die Originalbuchung als auch die Gegenbuchung.

**F: Was ist eine Gegenbuchung?**  
A: Eine Gegenbuchung ist eine neue Buchung mit dem umgekehrten Betrag (z. B. −500,00 EUR statt +500,00 EUR). Sie gleicht die ursprüngliche Buchung rechnerisch aus und hält die Buchungshistorie vollständig.

**F: Kann ich eine versehentliche Stornierung rückgängig machen?**  
A: Nein. Stornierungen sind endgültig. Falls Sie eine Buchung irrtümlich storniert haben, erfassen Sie den richtigen Betrag einfach als neue Buchung.

**F: Warum ist die Schaltfläche „Stornieren" ausgegraut?**  
A: Die Schaltfläche ist in drei Fällen deaktiviert: Die Buchung wurde bereits storniert, die Buchung ist selbst eine Gegenbuchung, oder Sie haben keine Berechtigung für diese Buchung.

**F: Was passiert, wenn eine Buchung mehrere verknüpfte Buchungen hat?**  
A: Alle verknüpften Buchungen werden gemeinsam storniert. Das System erledigt das automatisch. Sie müssen nicht jede Buchung einzeln stornieren.

**F: Wie erkenne ich, ob eine Buchung storniert wurde?**  
A: In der Buchungsliste gibt es die Spalte **Storno**. Stornierte Buchungen sind dort entsprechend gekennzeichnet.

**F: Kann ich nur einzelne Buchungen einer Gruppe stornieren?**  
A: Nein. Wenn Buchungen zu einer Gruppe gehören, werden stets alle Buchungen der Gruppe gemeinsam storniert. Eine teilweise Stornierung ist nicht möglich.

## Verwandte Funktionen

- [F001 – Kontenübersicht](./F001-kontenuebersicht.md)
- [F003 – Ausgabenverwaltung](./F003-ausgabenverwaltung.md)
- [F004 – Kontoauszug-Import](./F004-kontoauszug-import.md)
- [F006 – Wertpapier-Verwaltung](./F006-wertpapier-verwaltung.md)
