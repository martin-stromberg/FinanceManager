# Erledigte Nacharbeiten

Erledigt am: 2026-07-16

## Offene Planelemente

- [x] Das Problem besteht weiterhin: Meldet sich ein Benutzer an, der nicht Administrator ist, und navigiert zur Benutzerverwaltung, kann er dort weiterhin weitere Benutzer anlegen und bestehende Benutzer aendern. Dass die Seite aufrufbar ist, ist zunaechst zweitrangig. Zuerst muessen die serverseitigen Datenaenderungen der Benutzerverwaltung abgesichert werden, insbesondere Create- und Update-Operationen.

## Ergebnis

Die Benutzerverwaltung ist nun zusaetzlich in der administrativen Service-Schicht abgesichert. Nicht authentifizierte oder nicht administrative Aufrufer erreichen keine User-Admin-Operationen mehr, bevor Datenbankzugriffe fuer Create/Update oder andere administrative Benutzeroperationen ausgefuehrt werden.
