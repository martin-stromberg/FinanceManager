# Code-Review

Status: Keine Befunde

## Befunde

Keine.

## Hinweise

- Die Berechtigungspruefung wurde in der Service-Schicht ergaenzt, sodass alternative serverseitige Aufrufwege nicht allein von Controller-Attributen abhaengen.
- Der neue Integrationstest prueft explizit, dass Nicht-Admin-Create/Update-Versuche keine Benutzer anlegen oder vorhandene Benutzer umbenennen.
