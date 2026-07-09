# Rückmeldung des Kunden

- [x] Die Übersetzung für Card_Caption_Account_IsCollectionAccount fehlt.
	  Ergänze sie für alle Sprachen und erstelle einen Claude-Hook, der sicherstellt, dass solche Textbausteine stets übersetzt sind.
- [x] Die Ursprungsanforderung wurde nicht berücksichtigt: "Im UI gibt es einen Abschnitt zur Verwaltung der verknüpften IBANs (Hinzufügen, Entfernen, evtl. importierte IBANs anzeigen)."

- [x] Bei dem Hinzufügen einer IBAN sollen Leerzeichen automatisch entfernt werden.
- [x] Wird eine IBAN hinzugefügt, die bereits in der Liste enthalten ist, gibt es die Fehlermeldung "Response status code does not indicate success: 400 (Bad Request).". Hier muss ein sinnvoller Text gezeigt werden.
- [x] Es sollen E2E-Tests für die Anlage eines Sammelkontos erstellt werden. (Anlegen, Bearbeiten, Löschen)
- [x] Es sollen E2E-Tests erstellt werden für das Importieren und Buchen mehrerer Kontoauszügen mit verschiedenen IBAN von Sammelkonten. Sowohl bereits bekannte IBAN, als auch das nachträgliche Auswählen des Kontos im Auszug sollen getestet werden. Wird ein Kontoauszug gebucht so soll die bisher nicht gelistete IBAN hinterher bei dem Konto gelistet sein.
