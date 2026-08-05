# Anforderung: Differenzierte Fehlermeldungen beim Zuruecksetzen eines Update-Locks

## Ausgangslage

Beim Zuruecksetzen eines Update-Locks ueber die Ribbonaktion zeigt FinanceManager aktuell die Meldung:

> Der aktuelle Prozess fuehrt noch eine Update-Installation aus.

Diese Meldung wird auch dann angezeigt, wenn der tatsaechliche Fehler nicht sicher eine laufende Update-Installation ist. Beobachtet wurde ein Lock, der bereits seit dem Vorabend angezeigt wurde. Anschliessend liess sich kein Update mehr starten.

## Problem

Der Endpoint `POST /api/setup/update/lock/reset` behandelt im Reset-Pfad jede `IOException` pauschal als Fehler `Err_Update_InstallRunning`.

Der Text zu diesem Fehler stammt aus `FinanceManager.Web/Resources/Pages.de.resx`. Dadurch behauptet die UI eine laufende Installation, obwohl eine andere Ursache vorliegen kann.

Die Reset-Logik in `UpdateOrchestratorAdapter.ResetLockAsync` verwendet `msTools.Updater` fuer:

- Lock-Datei
- Lock-Zeitpunkt
- Stale-Pruefung
- Loeschen des Locks

FinanceManager unterscheidet lokal jedoch nicht zwischen den relevanten Fehlerursachen.

## Ziel

FinanceManager soll beim Zuruecksetzen eines Update-Locks differenzierte, anwenderverstaendliche Fehlermeldungen anzeigen.

Zusätzlich sollen Logs oder API-Fehlerdetails ausreichend Diagnoseinformationen enthalten, damit erkennbar ist, ob die Ursache in FinanceManager oder in `msTools.Updater` liegt.

## Funktionale Anforderungen

### Fehlerfaelle

Beim Reset eines Update-Locks muessen mindestens folgende Fehlerfaelle getrennt behandelbar sein:

- `NoLock`: Es ist kein aktiver Lock vorhanden.
- `LockNotStale`: Der vorhandene Lock ist noch nicht stale und darf daher nicht zurueckgesetzt werden.
- `LockDeleteFailed`: Die Lock-Datei konnte nicht geloescht werden.
- `ResetFailed`: Der Reset ist aus einem sonstigen Grund fehlgeschlagen.

### API-Verhalten

`UpdateController.ResetLock` muss Reset-Fehler gezielt auf eigene API-Fehlercodes mappen.

Eine `IOException` im Reset-Pfad darf nicht mehr pauschal als `Err_Update_InstallRunning` ausgegeben werden.

Der bestehende Fehler `Err_Update_InstallRunning` darf nur noch verwendet werden, wenn die Aussage fachlich zutrifft, dass aktuell eine Update-Installation laeuft.

### Adapter-Verhalten

`UpdateOrchestratorAdapter.ResetLockAsync` muss die unterschiedlichen Ursachen des fehlgeschlagenen Resets fuer den Controller erkennbar machen.

Die Unterscheidung soll mindestens folgende Situationen abdecken:

- kein aktiver Lock vorhanden
- Lock ist noch nicht stale
- Lock-Datei konnte nicht geloescht werden
- sonstiger I/O- oder Reset-Fehler

### UI-Verhalten

Die UI muss beim fehlgeschlagenen Reset den konkreten Grund anzeigen.

Die Fehlermeldungen muessen fuer Anwender verstaendlich sein und duerfen keine irrefuehrende laufende Update-Installation behaupten, wenn diese Ursache nicht belegt ist.

### Ressourcen

Fuer die neuen Fehlerfaelle muessen deutsche und englische Ressourcen ergaenzt werden.

Die bestehenden Ressourcen duerfen nur weiterverwendet werden, wenn ihr Inhalt zur konkreten Fehlerursache passt.

### Statuskonsistenz

Nach einem erfolgreichen Reset muss der Update-Status neu geladen oder konsistent aktualisiert werden.

Die UI darf nach einem erfolgreichen Reset keine veralteten Lock-Statusdaten anzeigen.

### Diagnose

Bei fehlgeschlagenem Reset muessen Logs oder API-Fehlerdetails erkennen lassen:

- welcher Reset-Fehlerfall eingetreten ist
- ob FinanceManager den Fehler selbst erkannt hat
- ob die Ursache wahrscheinlich aus `msTools.Updater` stammt
- welche relevante technische Ursache vorliegt, soweit verfuegbar

## Nicht-Ziele

Die interne Implementierung von `msTools.Updater` soll nur angepasst werden, wenn sich waehrend der Umsetzung zeigt, dass die geforderte Diagnose oder Fehlerunterscheidung ohne Aenderung dort nicht moeglich ist.

Die Ribbonaktion selbst soll fachlich nicht veraendert werden, ausser es ist fuer die korrekte Fehleranzeige oder Statusaktualisierung erforderlich.

## Tests

Es sind Tests zu ergaenzen oder anzupassen fuer:

- Controller-Mapping der einzelnen Reset-Fehlerfaelle auf eigene API-Fehlercodes
- ViewModel- oder UI-nahe Logik zur Anzeige der konkreten Meldungen
- Adapter-Verhalten bei den unterschiedlichen Lock-Reset-Ursachen
- erfolgreichen Reset eines alten oder stalen Locks
- Vermeidung der bisherigen Pauschalisierung auf `Err_Update_InstallRunning`
- konsistente Statusaktualisierung nach erfolgreichem Reset

## Akzeptanzkriterien

- Ein alter oder staler Lock kann ueber die Ribbonaktion zurueckgesetzt werden.
- Wenn der Reset nicht moeglich ist, nennt die UI den konkreten Grund.
- Die Meldung "Der aktuelle Prozess fuehrt noch eine Update-Installation aus" wird nur noch angezeigt, wenn diese Aussage tatsaechlich zutrifft.
- Aus Logs oder API-Fehlerdetails ist erkennbar, ob die Ursache in FinanceManager oder in `msTools.Updater` liegt.
