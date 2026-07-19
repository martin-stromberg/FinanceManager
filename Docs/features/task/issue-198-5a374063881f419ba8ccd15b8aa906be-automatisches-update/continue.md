# Offene Aufgaben

Erstellt am: 2026-07-19
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Admin-Lock-Reset nur fuer haengende Locks erlauben. `POST lock/reset` soll nur eine als haengend bewertete Lock-Datei loeschen und den Reset verweigern, wenn der aktuelle Prozess noch eine laufende Installation kennt. Die laufende-Installation-Pruefung ist umgesetzt, aber der Lock wird noch ohne Alters-, Owner- oder Stale-Bewertung geloescht.
- [ ] Testabdeckung gemaess Plan vervollstaendigen: `InstalledReleaseMetadataProvider` mit vorhandener `release-metadata.json`, explizite Plattform-/Assetauswahl fuer Windows und Linux, Lock-Datei-Verhalten fuer freien/aktiven/verwaisten Lock, Admin-Lock-Reset-Regeln fuer verwaiste vs. nicht verwaiste Locks, vollstaendige Statusuebergaenge fuer Check/Download/Ready/Installing/Failed, Integrationstests fuer `POST install/start` mit Conflict und BadRequest, ApiClient-Flows fuer Check/Schedule/Install/Reset sowie ViewModel-Flows fuer Laden/Speichern/Installationsfehler.

## Code-Review-Befunde

- [ ] `404 Err_Update_NotReady` beim Installationsstart wird clientseitig als `null` behandelt; `SetupUpdateViewModel.StartInstallAsync` setzt danach trotzdem `Installing = true`. `Updates_StartInstallAsync` soll 404 nicht als erfolgreichen Null-Rueckgabewert modellieren oder das ViewModel darf `Installing` nur bei einem nicht-null `UpdateStatusDto` mit `Status == Installing` setzen. Dazu einen ViewModel- oder ApiClient-Test ergaenzen.

## Fehlgeschlagene Tests

Keine.
