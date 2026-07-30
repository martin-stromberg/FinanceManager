# Anforderungsanalyse: Updater in separates Repository auslagern

**Aufgaben-ID:** 12667718-88da-4403-a458-acf0f586db31  
**Branch:** task/issue-235-1266771888da4403a458acf0f586db31-updater-in-separates-repositor  
**Analysedatum:** 2026-07-30

---

## Fachliche Zusammenfassung

Die Anwendung soll die bisher im aktuellen Repository enthaltene Klassenbibliothek `FinanceManager.AutoUpdater` nicht mehr als lokales Projekt verwenden. Stattdessen soll ein bereits ausgelagerter Updater aus dem separaten Repository `https://github.com/martin-stromberg/msTools.Updater.git` eingebunden werden. Die aktuell in den Releases dieses Repositorys veröffentlichte Version der Bibliothek soll vor einer späteren NuGet-Veröffentlichung als Testlauf direkt heruntergeladen, als Ressource im aktuellen Projekt abgelegt und in der Anwendung verwendet werden.

Ziel ist, die lokale Updater-Klassenbibliothek und deren Testprojekt zu entfernen, alle Anwendungsprojekte auf die abgelegte externe Bibliothek umzustellen und notwendige Codeanpassungen, insbesondere durch geänderte Namespaces, vorzunehmen. Das automatisierte Update der Anwendung muss danach weiterhin mit der neuen Bibliothek funktionieren.

---

## Betroffene Klassen und Komponenten

### Zu entfernende Projekte

| Projekt | Beschreibung | Zielzustand |
|--------|-------------|-------------|
| `FinanceManager.AutoUpdater` | Bisherige lokale Klassenbibliothek für das automatische Update | Entfernt |
| Zugehöriges Testprojekt zu `FinanceManager.AutoUpdater` | Tests der bisherigen lokalen Updater-Bibliothek | Entfernt |

### Neue oder zu ersetzende Ressourcen

| Ressource | Beschreibung | Zielzustand |
|----------|-------------|-------------|
| Aktuelle Release-Version von `msTools.Updater` | Vorgefertigte Klassenbibliothek aus dem separaten Repository | Im aktuellen Projekt als Ressource abgelegt |
| Projekt- oder Build-Referenzen auf die Updater-Bibliothek | Referenzen der Anwendung auf den Auto-Updater | Auf die abgelegte externe Bibliothek umgestellt |

### Anwendungsintegration

| Komponente | Beschreibung | Zielzustand |
|-----------|-------------|-------------|
| Anwendungsprojekte mit Updater-Nutzung | Projekte, die bisher `FinanceManager.AutoUpdater` referenzieren | Referenzieren die abgelegte `msTools.Updater`-Bibliothek |
| Updater-Namespaces | Namespaces der verwendeten Updater-Klassen | An neuen Namespace der ausgelagerten Bibliothek angepasst |
| Automatischer Update-Ablauf | Bestehende Funktion zum automatisierten Aktualisieren der Anwendung | Funktioniert unverändert aus Anwendersicht mit der neuen Bibliothek |

---

## Implementierungsansatz

### Phase 1: Release-Artefakt beschaffen

**Ziel:** Die aktuell verfügbare Release-Version der externen Updater-Bibliothek lokal verfügbar machen.

1. Neueste Release-Version aus `https://github.com/martin-stromberg/msTools.Updater.git` ermitteln.
2. Passendes Bibliotheksartefakt aus dem Release herunterladen.
3. Heruntergeladenes Artefakt unverändert und nachvollziehbar im aktuellen Projekt als Ressource ablegen.
4. Ablageort so wählen, dass Build, Tests und spätere Wartung die Herkunft der Bibliothek nachvollziehen können.

### Phase 2: Lokale Updater-Projekte entfernen

**Ziel:** Die bisherige lokale Implementierung vollständig aus der Solution entfernen.

1. Projekt `FinanceManager.AutoUpdater` aus der Solution und dem Dateisystem entfernen.
2. Zugehöriges Testprojekt aus der Solution und dem Dateisystem entfernen.
3. Projektverweise, Build-Konfigurationen und Testeinträge bereinigen.
4. Sicherstellen, dass keine verbliebenen Referenzen auf entfernte Projektdateien existieren.

### Phase 3: Anwendungen auf externe Bibliothek umstellen

**Ziel:** Alle bisherigen Nutzer des lokalen Updaters verwenden die abgelegte externe Bibliothek.

1. Projekt- oder Assembly-Referenzen auf das abgelegte Release-Artefakt einrichten.
2. Alle bisherigen Referenzen auf `FinanceManager.AutoUpdater` ersetzen.
3. `using`-Direktiven und vollqualifizierte Typnamen auf den neuen Namespace der externen Bibliothek anpassen.
4. API-Unterschiede zwischen lokaler und ausgelagerter Bibliothek im Programmcode berücksichtigen.
5. Build der gesamten Solution wiederherstellen.

### Phase 4: Update-Funktion verifizieren

**Ziel:** Das automatisierte Update funktioniert mit der externen Bibliothek weiterhin.

1. Automatisierten Update-Ablauf mit der neuen Bibliothek ausführen oder über geeignete Tests verifizieren.
2. Prüfen, dass Versionsprüfung, Download, Installationsvorbereitung und Start des Update-Ablaufs weiterhin funktionieren.
3. Fehler durch geänderte Namespaces, Typnamen oder Signaturen beheben.
4. Entfernte Tests durch vorhandene Anwendungs- oder Integrationstests abdecken, soweit für den Testlauf sinnvoll.

---

## Konfiguration

### Externe Quelle

| Einstellung | Wert |
|------------|------|
| Repository | `https://github.com/martin-stromberg/msTools.Updater.git` |
| Quelle der Bibliothek | Neuester GitHub-Release |
| Einbindungsart | Lokales Release-Artefakt als Projektressource |
| Spätere Zielverwendung | NuGet-Paket nach erfolgreichem Testlauf |

### Projektstruktur

- Die heruntergeladene Bibliothek wird im aktuellen Repository abgelegt.
- Die bisherige lokale Updater-Klassenbibliothek wird nicht parallel weitergeführt.
- Die Anwendung referenziert genau die abgelegte externe Bibliothek.
- Entfernte Updater-Testprojekte bleiben entfernt, sofern sie ausschließlich die alte lokale Bibliothek testen.

---

## Architekturmuster

### Externe Bibliothek statt lokaler Projektimplementierung

- Updater-Funktionalität wird aus einem separaten Repository bezogen.
- Die aktuelle Anwendung nutzt für den Testlauf ein lokal abgelegtes Release-Artefakt.
- Eine spätere Umstellung auf ein NuGet-Paket soll dadurch vorbereitet werden.

### Namespace-Migration

- Der bisherige Namespace der lokalen Bibliothek gilt nicht mehr als verbindlich.
- Der Programmcode der Anwendung muss auf den Namespace der ausgelagerten Bibliothek angepasst werden.
- Anpassungen sollen auf Integrationscode beschränkt bleiben, sofern die externe API funktional kompatibel ist.

### Build- und Testkonsistenz

- Entfernte Projekte dürfen nicht mehr in Solution, Projektverweisen, Build-Skripten oder Testläufen referenziert werden.
- Die Anwendung muss nach der Umstellung vollständig bauen.
- Der automatisierte Update-Ablauf ist das zentrale fachliche Erfolgskriterium.

---

## Offene Fragen und Annahmen

### Annahmen

1. Im Repository `msTools.Updater` existiert mindestens ein GitHub-Release mit einem verwendbaren Bibliotheksartefakt.
2. Die Release-Bibliothek ist kompatibel mit der Zielplattform und dem .NET-Target der aktuellen Anwendung.
3. Die externe Bibliothek enthält funktional die benötigten Updater-Klassen der bisherigen lokalen Bibliothek.
4. Die Einbindung als lokales Artefakt ist nur ein Testlauf vor einer späteren NuGet-Veröffentlichung.
5. Entfernte Tests müssen nicht unverändert migriert werden, sofern sie ausschließlich die entfernte lokale Bibliothek testen.

### Zu klärende Punkte

1. Welcher konkrete Ablageort im Repository ist für externe Release-Artefakte vorgesehen?
2. Welches Release-Asset aus `msTools.Updater` ist maßgeblich, falls ein Release mehrere Artefakte enthält?
3. Soll die heruntergeladene Bibliothek versioniert im Repository eingecheckt werden oder nur als Build-Ressource referenziert werden?
4. Gibt es bestehende Update-End-to-End-Tests, die für die Verifikation erweitert oder wiederverwendet werden sollen?
5. Müssen Lizenz- oder Herkunftshinweise für das eingebundene Release-Artefakt dokumentiert werden?

---

## Abhängigkeiten und Integrationspunkte

### Externe Abhängigkeiten

- GitHub-Repository `martin-stromberg/msTools.Updater`
- Neuester Release der Bibliothek aus diesem Repository
- Release-Artefakt der Klassenbibliothek

### Interne Integrationspunkte

- Solution-Datei und Projektverweise
- Anwendungsprojekte mit Auto-Updater-Nutzung
- Build- und Testkonfiguration
- Update-Konfiguration der Anwendung
- Codebereiche mit bisherigen `FinanceManager.AutoUpdater`-Namespaces

### Migrationspunkte

- Entfernen lokaler Projektreferenzen auf `FinanceManager.AutoUpdater`
- Ersetzen durch Referenz auf das abgelegte externe Bibliotheksartefakt
- Anpassung der Namespace-Imports
- Anpassung von API-Aufrufen bei abweichenden Typ- oder Methodensignaturen

---

## Erfolgskriterien

- [ ] Neueste verfügbare Release-Version der Bibliothek aus `msTools.Updater` ist heruntergeladen.
- [ ] Die heruntergeladene Bibliothek ist als Ressource im aktuellen Projekt abgelegt.
- [ ] `FinanceManager.AutoUpdater` ist aus Solution und Projektstruktur entfernt.
- [ ] Das zugehörige Testprojekt ist aus Solution und Projektstruktur entfernt.
- [ ] Alle Anwendungsprojekte nutzen statt der lokalen Bibliothek die abgelegte externe Bibliothek.
- [ ] Namespaces und betroffene Programmcode-Stellen sind an die externe Bibliothek angepasst.
- [ ] Die gesamte Solution baut ohne Referenzen auf entfernte Projekte.
- [ ] Automatisierte Tests laufen ohne Fehler, soweit sie nicht die entfernte lokale Bibliothek betreffen.
- [ ] Das automatisierte Update funktioniert mit der neuen Klassenbibliothek.
