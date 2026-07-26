### Fachliche Zusammenfassung

Die Kontoauszugseite soll den bestehenden Massenänderungsmodus für Kontoauszugseinträge erweitern. Nutzer können dort Einträge bisher nur bearbeiten; künftig sollen sie Einträge im Massenänderungsmodus auch zum Löschen vormerken können. Vorgemerkte Löschungen werden unmittelbar aus der Tabelle ausgeblendet, aber erst beim Speichern der gesamten Massenänderung tatsächlich persistiert. Wird der Massenänderungsmodus abgebrochen, dürfen die ausgeblendeten Einträge nicht gelöscht sein.

Zusätzlich soll die Tabelle im Massenänderungsmodus immer eine letzte leere Eingabezeile enthalten, über die ein neuer Kontoauszugseintrag erfasst werden kann. Auch dieser neue Eintrag bleibt zunächst nur Teil der lokalen Massenänderung und wird erst mit dem Speichern aller Änderungen angelegt.

---

### Betroffene Klassen und Komponenten

#### UI-Komponenten

- **`QuickEditTable`** (`FinanceManager.Web.Shared.QuickEdit.QuickEditTable`)
  - Erweiterung der Massenänderungstabelle um eine Löschaktion je editierbarer Zeile.
  - Ausblendung lokal zum Löschen vorgemerkter Zeilen aus der sichtbaren Tabelle.
  - Darstellung einer stets vorhandenen letzten Eingabezeile zum Anlegen eines neuen Eintrags.
  - Behandlung von Validierung, Eingabestatus und Tabellendarstellung für bestehende, gelöschte und neue Zeilen.

- **`GenericCardPage`** (`FinanceManager.Web.Components.Pages.GenericCardPage`)
  - Weiterhin Einbettung der QuickEdit-Tabelle, wenn die eingebettete Liste der Kontoauszugseinträge im Massenänderungsmodus aktiv ist.
  - Keine eigenständige fachliche Änderung erwartet, aber relevant für die Integration der erweiterten QuickEdit-Tabelle.

#### ViewModels

- **`StatementDraftCardViewModel`** (`FinanceManager.Web.ViewModels.StatementDrafts.StatementDraftCardViewModel`)
  - Erweiterung von `SaveQuickEditAsync`, sodass neben Änderungen auch vorgemerkte Löschungen und neu erfasste Einträge in einem Speichervorgang übertragen werden.
  - Sicherstellen, dass `CancelQuickEditAsync` alle lokalen Änderungen, Löschvormerkungen und neue unpersistierte Einträge verwirft.
  - Aktualisierung der Ribbon-/Save-Logik, damit Speichern aktiv wird, wenn Löschungen oder neue Zeilen vorhanden sind.

- **`StatementDraftEntriesListViewModel`** (`FinanceManager.Web.ViewModels.StatementDrafts.StatementDraftEntriesListViewModel`)
  - Erweiterung des lokalen Massenänderungszustands um:
    - zum Löschen vorgemerkte bestehende Einträge,
    - neue, noch nicht persistierte Einträge,
    - Validierungsstatus neuer Einträge.
  - Bereitstellung der Datenbasis für die QuickEdit-Tabelle ohne gelöschte Zeilen und mit letzter Eingabezeile.
  - Ermittlung der ausstehenden Änderungen für den Speichervorgang.

- **`StatementDraftEntryItem`** (`FinanceManager.Web.ViewModels.StatementDrafts.StatementDraftEntryItem`)
  - Ggf. Erweiterung um UI-Zustände wie `IsPendingDelete`, `IsNew` oder vergleichbare Felder, sofern der Zustand nicht ausschließlich im ListViewModel verwaltet wird.

#### API / DTOs

- **QuickEdit-Speicherrequest für Statement-Draft-Einträge**
  - Bestehender Request für Massenänderungen ist um Löschungen und Neuanlagen zu erweitern oder durch einen kombinierten Upsert-/Delete-Request zu ergänzen.
  - Erwartete Bestandteile:
    - Liste geänderter bestehender Einträge,
    - Liste zu löschender Entry-IDs,
    - Liste neu anzulegender Einträge.

- **`IApiClient`** / **StatementDrafts-API-Client**
  - Anpassung der Clientmethode für das Speichern der Massenänderung an die erweiterte Request-Struktur.

- **`StatementDraftEntriesController`** / **`StatementDraftsController`**
  - Erweiterung des Endpunkts für QuickEdit-Speicherung oder Ergänzung eines passenden Endpunkts, der Änderungen, Löschungen und Neuanlagen gemeinsam verarbeitet.
  - Bestehende Einzel-Lösch- und Einzel-Anlage-Funktionen bleiben für die Detailseite nutzbar.

#### Anwendungsschicht / Persistenz

- **Statement-Draft-Entry-Service / Infrastruktur**
  - Persistieren des kombinierten Speichervorgangs in einer fachlich konsistenten Reihenfolge.
  - Löschen vorgemerkter bestehender Einträge erst beim Speichern.
  - Anlegen neuer Einträge erst beim Speichern.
  - Validierung, dass nur Einträge des aktuellen Kontoauszugsentwurfs und des aktuellen Nutzers betroffen sind.

#### Ressourcen / Lokalisierung

- **Statement-Draft-Ressourcen** (`StatementDraftDetail.*.resx`, `StatementDraftEntryDetail.*.resx`, ggf. gemeinsame Ressourcen)
  - Texte und Tooltips für Löschen im Massenänderungsmodus.
  - Texte für die letzte Eingabezeile, Validierungsfehler und ggf. Undo-/Abbrechen-Verhalten.

#### Tests

- Tests für das ViewModel des Massenänderungsmodus:
  - bestehende Zeile zum Löschen vormerken,
  - gelöschte Zeile wird ausgeblendet,
  - Abbrechen stellt gelöschte und neue Zeilen wieder auf den Ausgangszustand zurück,
  - Speichern überträgt Änderungen, Löschungen und Neuanlagen.

- API-/Integrationstests:
  - kombinierter Speichervorgang löscht bestehende Einträge,
  - kombinierter Speichervorgang legt neue Einträge an,
  - Änderungen, Löschungen und Neuanlagen werden atomar und nutzerbezogen verarbeitet.

---

### Implementierungsansatz

1. **Lokalen QuickEdit-Zustand erweitern**: Das `StatementDraftEntriesListViewModel` hält neben bestehenden Pending-Änderungen auch eine Menge vorgemerkter Löschungen sowie eine Sammlung neuer, noch nicht gespeicherter Einträge.

2. **Löschen in der Tabelle anbieten**: `QuickEditTable` rendert pro löschbarer Bestandszeile eine Löschaktion. Beim Auslösen wird die Zeile nicht direkt per API gelöscht, sondern im ViewModel als gelöscht vorgemerkt und aus der sichtbaren Tabelle entfernt.

3. **Letzte Eingabezeile ergänzen**: Im aktiven Massenänderungsmodus erzeugt die Tabelle immer eine leere Eingabezeile am Tabellenende. Sobald dort fachlich relevante Daten eingegeben werden, wird daraus ein neuer lokaler Eintrag; anschließend steht wieder eine neue leere letzte Zeile bereit.

4. **Speicherrequest erweitern**: `SaveQuickEditAsync` sammelt geänderte Bestandszeilen, zu löschende Entry-IDs und neue Einträge in einem gemeinsamen Request. Bestehende Validierung bleibt aktiv; neue Einträge werden nur gesendet, wenn sie ausreichend ausgefüllt und valide sind.

5. **Serverseitig gemeinsam persistieren**: Der zugehörige API-Endpunkt verarbeitet alle Teile des Requests in einem Speichervorgang. Löschungen und Neuanlagen werden erst dort tatsächlich in der Datenbank ausgeführt.

6. **Abbrechen rückstandsfrei machen**: Beim Beenden ohne Speichern werden Pending-Änderungen, Löschvormerkungen und neue lokale Einträge verworfen; die Liste wird aus dem zuletzt geladenen Serverzustand neu aufgebaut.

---

### Konfiguration

Es ist keine neue anwendungsweite Konfiguration erforderlich. Das Verhalten ist Bestandteil des bestehenden Massenänderungsmodus für Kontoauszugseinträge.

---

### Offene Fragen

1. Soll eine zum Löschen vorgemerkte Zeile nach dem Ausblenden per Undo wiederherstellbar sein, oder reicht das Abbrechen des gesamten Massenänderungsmodus als Rücknahme?
2. Welche Pflichtfelder muss die letzte Eingabezeile erfüllen, damit daraus beim Speichern ein neuer Kontoauszugseintrag angelegt wird?
3. Soll der kombinierte Speichervorgang vollständig atomar sein, d. h. bei einem ungültigen neuen Eintrag werden auch Änderungen und Löschungen nicht persistiert?
4. Dürfen bereits gebuchte oder angekündigte Kontoauszugseinträge im Massenänderungsmodus gelöscht werden, oder gilt die Löschfunktion nur für regulär offene Einträge?
