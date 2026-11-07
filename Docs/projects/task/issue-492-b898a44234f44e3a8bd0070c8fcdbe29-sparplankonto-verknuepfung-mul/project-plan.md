# Projektplan – Sparplan↔Konto-Verknüpfung (Multi-Link), informelle Beitragsbuchungen und Istmeldung

**Issue:** #492
**Basisbranch:** `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul`
**Eingaben:** `requirement.md`, `inventory.md`

## Übersicht

**Projekttitel:** Sparplan↔Konto-Verknüpfung (Multi-Link), informelle Beitragsbuchungen und Istmeldung

**Gesamtziel:** Sparpläne sollen die reale Ist-Situation ihrer Vertragskonten abbilden. Dazu wird ein dreigliedriges Zielmodell umgesetzt:

1. **Multi-Link Sparplan ↔ Bankkonto (n:m):** Bei gesetztem Konto-Link folgen `accumulatedAmount` und die Rhythmus-/Defizitanalyse den verknüpften Kontosalden bzw. dem Konto-Inflow statt der plan-verknüpften Posting-Summe.
2. **Informelle Beitragsbuchungen:** Plan-verknüpfte Funding-Postings werden bei gesetztem Konto-Link in der Plananalyse als informell behandelt — sie dokumentieren die Funding-Quelle, zählen aber nicht in `accumulated`/`expectedToDate` und werden in der Postings-Ausgabe mit `informal: true` gekennzeichnet.
3. **Istmeldung:** Für Pläne ohne verknüpfte Konten kann der Ist-Saldo über eine Korrekturbuchung gesetzt werden, die `accumulated` korrigiert, ohne eine Kontobewegung zu erzeugen.

**Betroffene Bereiche:**

- **Domänenmodell:** `SavingsPlan` (neue n:m-Beziehung zu `Account` über explizite Join-Entity), `Posting` (Marker für die Istmeldung; Informalität als Ableitung), `Account` (bestehendes `CurrentBalance`/`IsCollectionAccount` als Ist-Quelle).
- **Plananalyse/Aggregate:** Umschaltung von `accumulatedAmount`, `expectedToDate` und `scheduleDeficit`/Kadenz auf Kontosalden bzw. Konto-Inflow bei gesetztem Link; einheitliche „current"-Quelle auch für Ziel-Checks, Fälligkeitshinweise und Auto-Archivierung.
- **Budgetauswertung:** Fachlich unverändert — wird als Regression explizit abgesichert.
- **API/Shared-DTOs/ApiClient:** Endpunkte/DTOs für Link-Verwaltung und Istmeldung; `informal`-Kennzeichnung in der Postings-Ausgabe.
- **MCP:** `informal` im Postings-Tool, Schreiboperationen für Link/Istmeldung, angepasste Descriptor-Texte.
- **Web-UI:** Link-Verwaltung und Istmeldung-Aktion in den Sparplan-Details; Kennzeichnung informeller Buchungen und Istmeldungen in Posting-Listen/-Details.
- **Persistenz/Backup:** Neue Join-Tabelle inkl. `dotnet ef`-Migration; abwärtskompatible Erweiterung von Export und Import.

## Grobe Vorgehensentscheidungen

1. **n:m-Verknüpfung über explizite Join-Entity.** Das Projekt verwendet keine EF-Shadow-m:n-Konvention; alle Beziehungen sind explizite Entities (Vorbild `AccountLinkedIban`). Die Join-Entity führt `OwnerUserId` redundant (konsistent mit allen anderen Entities; vereinfacht Owner-Prüfung, Backup-Einsammlung und Indizes) und einen Unique-Index auf `(SavingsPlanId, AccountId)` gegen Duplikate. Die Migration wird ausschließlich per `dotnet ef` erzeugt; der Ablageort folgt der bestehenden Konvention (domänenbezogene Migrationen zuletzt in `Data/Migrations/Identity`, Snapshot in `Migrations/` — beim Anlegen mit der Projekt-Konvention abstimmen).

2. **„informal" als Auswertungsableitung statt persistiertem Flag.** Ein plan-verknüpftes Posting gilt als informell, wenn der Plan mindestens einen Konto-Link hat und das Posting eine Funding-Buchung dokumentiert (Ableitungsheuristik über die `GroupId`: Die Buchungsgruppe enthält ein Bank-Posting, das die Speisung aus einem anderen Konto belegt). Begründung: Das Requirement definiert Informalität ausdrücklich als Auswertungseigenschaft („keine Umbuchung"); ein persistiertes Flag müsste bei jedem Setzen/Entfernen eines Links rückwirkend auf historische Postings umgeschrieben werden — genau die als Nicht-Ziel definierte Datenmigration. Die Ableitung ist automatisch konsistent in beide Richtungen (Ablauf 2 „Link entfernen" fällt gratis mit) und erfordert keine Schemaänderung am `Posting`. Plan-Postings ohne Funding-Gruppe (z. B. Darlehensauszahlungen, Istmeldungen) werden sauber als „nicht informell" klassifiziert.

3. **„Istmeldung" als Marker/SubType auf `Kind == PostingKind.SavingsPlan` — kein neuer `PostingKind`.** Die Bestandsaufnahme zeigt eine hohe Kaskade eines neuen Kind-Werts: Alle `Kind == PostingKind.SavingsPlan`-Filter (Analyse-History, Postings-Listen, Exporte, MCP-Scope-Filter, Aggregat-Switch, Routen-Mapping) wären für die Istmeldung „unsichtbar", bis jede Stelle angepasst ist — mit dem Risiko, dass die Korrekturbuchung nicht in `accumulated` zählt und damit das Requirement bricht. Ein Marker-Feld (z. B. SubType „Adjustment") auf dem bestehenden Plan-Posting läuft dagegen ohne Umbau durch alle bestehenden Plan-Queries; `AccountId == null` erzwingt strukturell „keine Kontobewegung"; die Unterscheidbarkeit im Audit/Liste erfolgt über den Marker in der Ausgabe. Erzeugung über einen neuen plan-scoped Schreibpfad: Betrag = gemeldeter Ist-Saldo − aktuelles `accumulated` (Differenzbuchung).

4. **Einheitliche „current"-Quelle in allen plan-internen Ist-Verbrauchern.** Neben `GetAsync`/`AnalyzeAsync` nutzen auch die Buchungsvalidierung (`SAVINGSPLAN_GOAL_*`, `SAVINGSPLAN_DUE`) und die Auto-Archivierung (`current == target`) die Posting-Summe als „current". Diese Konsumenten folgen derselben Quellenwahl wie `accumulatedAmount` (Kontosalden bei Links, Posting-Summe sonst) — andernfalls würden Hinweise und automatische Aktionen dem angezeigten Ist-Stand widersprechen.

5. **Budgetsicht bleibt fachlich unverändert — inklusive Istmeldung-Ausschluss.** Keine Budget-Query nutzt die neue Join-Tabelle; Bankpostings verknüpfter Konten werden nicht zu Plan-Actuals. Da die Budgetauswertung plan-verknüpfte Postings rein über `SavingsPlanId` summiert, würde eine Istmeldung ungefiltert als Ist-Wert einfließen. Fachlich ist eine Saldenkorrektur jedoch kein Kostenfluss; sie wird daher über den Marker explizit aus den Budget-Actuals ausgenommen, damit die „Kostensicht wie bisher" gewahrt bleibt. Diese Entscheidung wird über die dedizierte Regressionsabsicherung verifiziert.

6. **Konto-Inflow = vollständiger Zufluss der verknüpften Konten.** Für die Defizitanalyse werden alle positiven Zuflüsse der verknüpften Konten herangezogen (Einzüge, VL, Zinsen, sonstige Zuflüsse) — konsistent mit der Issue-Formulierung und der dokumentierten Annahme in `requirement.md`.

7. **Kein Reversal der Istmeldung.** Die Storno-Owner-Auflösung läuft heute ausschließlich über `AccountId`; konto-lose Plan-Postings sind damit nicht stornierbar. Das Requirement fordert kein Storno — eine fehlerhafte Istmeldung wird fachlich durch eine weitere Istmeldung korrigiert.

8. **Backup-Erweiterung abwärtskompatibel.** Die Verknüpfungen werden als optionale Backup-Sektion ergänzt (Vorbild der optionalen MCP-Sektionen), nicht als Pflichtabschnitt — alte Backups bleiben gültig, die Payload-Version bleibt unverändert. Das neue Posting-Marker-Feld wird im `PostingBackupDto` und im Import mitgeführt (ID-Remapping beachten; die `AccountLinkedIban`-Lücke „BackupDto ohne Export" wird nicht wiederholt).

9. **Sequentielle, einzeln mergebare Schritte.** Die Entwicklungsschritte entstehen nacheinander auf dem Projekt-Basisbranch und werden später einzeln gemergt; jeder Schritt ist fachlich abgeschlossen, lieferbar und testbar.

## Entwicklungsschritte

### Schritt 1: Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung

Ein Sparplan kann mit keinem, einem oder mehreren eigenen Bankkonten verknüpft werden; ein Bankkonto kann umgekehrt mehrere Sparpläne bedienen (Sammelkonto). Dafür wird eine neue explizite n:m-Join-Entity zwischen `SavingsPlan` und `Account` eingeführt — mit eindeutiger Paarung (keine Duplikat-Links), Owner-Bezug und einer ausschließlich per `dotnet ef` erzeugten Migration am etablierten Migrationsort. Ergänzend werden fachliche Service-Operationen zum Setzen, Auflisten und Entfernen von Verknüpfungen bereitgestellt: Verknüpfungen sind nur zwischen Plan und Konto desselben Eigentümers zulässig, doppelte Links werden abgewiesen, das Entfernen einzelner Links ist möglich. Die Benennung der Verknüpfung wird bewusst so gewählt, dass keine Verwechslung mit der bestehenden `SavingsPlanExpectation`-Kopplung entsteht. API und UI folgen in späteren Schritten; der Schritt ist über Service-Tests vollständig belegbar.

**Akzeptanzkriterien:**

- Die Join-Entity liegt in einer eigenen Tabelle (per `dotnet ef` erzeugte Migration); die Paarung (Plan, Konto) ist eindeutig.
- Verknüpfungen lassen sich setzen, auflisten (Konten eines Plans bzw. Pläne eines Kontos) und einzeln entfernen.
- Links zwischen Plan und Konto unterschiedlicher Eigentümer sowie Duplikate werden abgewiesen.
- Ein Plan kann mehrere Konten und ein Konto mehrere Pläne bedienen.

**Abhängigkeiten:** —

**Betroffene Bereiche:** Domänenmodell, Persistenz/Migration

### Schritt 2: Konto-basierte Ist-Betrachtung in der Plananalyse

Sobald ein Sparplan verknüpfte Konten hat, folgt die Plananalyse der Realität der Vertragskonten: `accumulatedAmount` entspricht der Summe der Salden aller verknüpften Konten (additiv, ohne Gewichtung; ein Sammelkonto trägt seinen vollen Saldo zu jedem verknüpften Plan bei — keine Aufteilung). Die Rhythmus-/Defizitanalyse (`scheduleDeficit`, Kadenz, `expectedToDate`) wird aus dem Konto-Inflow der verknüpften Konten berechnet (alle Zuflüsse inkl. Einzüge, VL und Zinsen), nicht mehr aus dem Giro-seitigen Abgang. Plan-verknüpfte Funding-Postings werden in der Analyse dabei als informell behandelt und zählen nicht — der Geldfluss ist auf dem Vertragskonto bereits über den Saldo erfasst, es gibt keine Doppelzählung. Disbursements (plan-verknüpfte Postings ohne Repräsentation auf dem Vertragskonto, z. B. Darlehensauszahlungen) bleiben fachlich Auszahlungen und werden für die Financing-Cycle-Erkennung weiterhin berücksichtigt; sie mindern `accumulated` nicht, da der Kontostand die Realität enthält. Alle plan-internen Ist-Verbraucher (Ziel-erreicht-Prüfungen, Fälligkeitshinweise, Auto-Archivierung) verwenden dieselbe Quellenwahl. Für Pläne ohne Links bleibt das bisherige Verhalten unverändert; nach Entfernen des letzten Links fallen `accumulatedAmount` und Defizitanalyse auf die Posting-basierte Auswertung zurück.

**Akzeptanzkriterien:**

- `accumulatedAmount` eines verknüpften Plans entspricht der Summe der Salden aller verknüpften Konten (Validierungsbeispiel: Bausparen zeigt den realen Kontosaldo statt der negativen Posting-Summe).
- Mehrere verknüpfte Konten werden additiv summiert; bei Sammelkonten erhält jeder verknüpfte Plan den vollen Kontosaldo (keine Aufteilung).
- `scheduleDeficit`/Kadenz nutzen den Konto-Inflow (inkl. VL und Zinsen), nicht den Giro-Abgang; Funding-Postings werden nicht doppelt gezählt.
- Ziel-Checks, Fälligkeitshinweise und Auto-Archivierung bewerten denselben „current"-Stand wie die Analyse.
- Ohne Links bzw. nach Entfernen aller Links verhält sich die Analyse exakt wie bisher (Posting-basiert).

**Abhängigkeiten:** 1

**Betroffene Bereiche:** Plananalyse/Aggregate, Buchungsvalidierung

### Schritt 3: REST-API und ApiClient für die Kontoverknüpfung

Die Verknüpfungsverwaltung wird über die REST-API zugänglich: Endpunkte und DTOs zum Setzen, Auflisten und Entfernen der Konto-Links am Sparplan (Subressourcen-Muster analog zur `linked-ibans`-Verwaltung am Konto). Alle Endpunkte sind owner-scoped; Fachfehler (fremdes Konto/Plan, Duplikat, unbekannte IDs) werden auf die üblichen Fehlercodes gemappt. Die neuen Endpunkte werden im ApiClient gespiegelt, sodass das Verhalten Ende-zu-Ende über Integrationstests belegt ist: Nach dem Setzen eines Links über die API wirkt sich die Verknüpfung sofort auf `accumulatedAmount` und die Analyse aus (Wirkung aus Schritt 2).

**Akzeptanzkriterien:**

- Links lassen sich per API am Sparplan setzen, auflisten und entfernen.
- Verknüpfungen mit Konten/Plänen fremder Eigentümer, Duplikate und unbekannte IDs werden mit korrekten Fehlercodes abgewiesen.
- Der ApiClient enthält Methoden für alle neuen Endpunkte; Integrationstests belegen Setzen/Entfernen und den Analyse-Effekt.

**Abhängigkeiten:** 1, 2

**Betroffene Bereiche:** API, Shared-DTOs/ApiClient

### Schritt 4: „informal"-Kennzeichnung in der Postings-Ausgabe (API und MCP)

Plan-verknüpfte Funding-Postings konto-verknüpfter Pläne werden in der Postings-Ausgabe als informell gekennzeichnet (`informal: true`) — ableitungsbasiert und ohne persistiertes Flag: informell ist, wer eine Funding-Buchung dokumentiert (Buchungsgruppe mit Bank-Posting) und zu einem Plan mit Konto-Link gehört. Die Kennzeichnung wird in den REST-DTOs (Postings-Listen und Einzelposting im Sparplan-Kontext), in der MCP-Ausgabe des Postings-Query-Tools und in den betroffenen Exportpfaden mitgeführt. Die Postings bleiben unverändert sichtbar und als Funding-Quellennachweis auswertbar; historische Beträge werden nicht umgebucht. Nach dem Entfernen aller Konto-Links erscheinen dieselben Postings wieder ohne Kennzeichnung. Plan-Postings ohne Funding-Gruppe (z. B. Auszahlungen ohne Bank-Gegenposting) bleiben unmarkiert.

**Akzeptanzkriterien:**

- Plan-verknüpfte Funding-Postings verknüpfter Pläne liefern `informal: true` in der REST-Response und in `fm_postings_query`.
- Nach Entfernen aller Links liefert dieselbe Ausgabe `informal: false` (Ableitung, kein Datenupdate).
- Nicht-Funding-Plan-Postings werden nicht als informell markiert; die Funding-Quelle bleibt über die Postings auswertbar.
- Die Budgetsicht wird durch die Kennzeichnung nicht verändert.

**Abhängigkeiten:** 2, 3

**Betroffene Bereiche:** API/Shared-DTOs, Abfrageschicht, MCP, Exporte

### Schritt 5: Istmeldung – Korrekturbuchung ohne Kontobewegung

Für Pläne ohne verknüpfte Konten kann der Ist-Saldo über eine „Istmeldung" gesetzt bzw. korrigiert werden: Der Anwender meldet den realen Ist-Saldo, das System erzeugt ein plan-verknüpftes Korrekturposting in Höhe der Differenz zum aktuellen `accumulated`. Die Istmeldung ist ein Posting mit `Kind == SavingsPlan` plus einem eindeutigen Marker (SubType bzw. Flag — kein neuer `PostingKind`, um die Kaskade über alle Plan-Filter zu vermeiden) und ohne `AccountId` — es entsteht strukturell keine Kontobuchung. Die Aktion wird über einen neuen Endpunkt am Sparplan angeboten und im ApiClient gespiegelt; sie wird für Pläne mit Konto-Link abgewiesen (dort ist der Kontostand die alleinige Ist-Quelle). Der Marker wird in der Postings-Ausgabe (inkl. MCP) als Korrekturbuchung ausgewiesen und von der informell-Ableitung korrekt als „nicht informell" behandelt. Istmeldungen fließen nicht in die Budget-Actuals (Saldenkorrektur ist kein Kostenfluss). Ein Storno ist nicht vorgesehen; Korrekturen erfolgen per erneuter Istmeldung. Das Marker-Feld erfordert eine per `dotnet ef` erzeugte Migration und wird im `PostingBackupDto` mitgeführt (Backup-Umsetzung in Schritt 9).

**Akzeptanzkriterien:**

- Nach einer Istmeldung entspricht `accumulated` dem gemeldeten Wert (Differenzbuchung); es entsteht keine Kontobewegung.
- Die Istmeldung ist in Posting-Liste und Audit/MCP-Ausgabe klar als Korrekturbuchung erkennbar und gilt nicht als informell.
- Die Aktion wird bei konto-verknüpften Plänen abgewiesen.
- Die Istmeldung verändert die Budget-Actuals nicht.
- Migration und ApiClient-Methoden sind vorhanden; der Endpunkt ist owner-scoped.

**Abhängigkeiten:** 1, 3

**Betroffene Bereiche:** Domänenmodell (Posting-Marker), Persistenz/Migration, Plananalyse, API/ApiClient, MCP-Ausgabe, Budget (Ausschluss)

### Schritt 6: MCP-Schreiboperationen für Verknüpfung und Istmeldung

MCP-Clients können die neuen Fachfunktionen ebenfalls ausführen: Das Manage-Tool für Sparpläne (oder ein dediziertes Tool) erhält Operationen zum Setzen und Entfernen von Konto-Links sowie zum Erfassen von Istmeldungen — mit denselben fachlichen Validierungen wie die REST-API (Owner-Scoping, Duplikat-Abweisung, Istmeldung nur ohne Links). Dafür werden die erforderlichen Scopes ergänzt (u. a. Lesezugriff auf Konten für die Link-Verwaltung) und die Audit-Staging-Konventionen eingehalten. Zusätzlich werden die Descriptor-Texte des Analyse-Tools an das neue Modell angepasst, da die bisherige Beschreibung der Posting-Semantik den Konto-Link-Modus verbindlich dokumentieren muss.

**Akzeptanzkriterien:**

- Konto-Links lassen sich über MCP setzen und entfernen; Istmeldungen lassen sich über MCP erfassen — jeweils mit denselben Validierungen wie über die API.
- Scopes, Record-Kontext und Audit-Staging folgen den MCP-Konventionen; kein neuer Datentyp ist erforderlich.
- Die Tool-Beschreibungen (insb. des Analyse-Tools) beschreiben `accumulatedAmount`- und Inflow-Semantik des Konto-Link-Modus korrekt.

**Abhängigkeiten:** 3, 5

**Betroffene Bereiche:** MCP

### Schritt 7: Web-UI – Kontoverknüpfung verwalten und Kennzeichnungen anzeigen

In den Sparplan-Details können eigene Bankkonten verknüpft und die Verknüpfungen wieder entfernt werden (Mehrfachauswahl eigener Konten; Vorbild: die `LinkedIbans`-Verwaltung an der Konto-Karte). Die verknüpften Konten sind am Plan sichtbar. In den Posting-Listen und der Posting-Detailansicht werden informelle Buchungen und Istmeldungen erkennbar gekennzeichnet (Vorbild: die bestehenden Flag-Spalten für Vorläufig/Storno). Plan-Listen, -Details und Analysen zeigen ab diesem Schritt automatisch die konto-basierten Werte, da sie dieselben Endpunkte nutzen.

**Akzeptanzkriterien:**

- Verknüpfungen lassen sich in den Sparplan-Details anzeigen, hinzufügen (nur eigene Konten wählbar) und entfernen.
- Informelle Postings und Istmeldungen sind in Liste und Detailansicht als solche gekennzeichnet.
- Anzeige von `accumulatedAmount`/Analyse folgt den verknüpften Kontosalden; nach Entfernen aller Links erscheinen die Posting-basierten Werte.

**Abhängigkeiten:** 3, 4, 5

**Betroffene Bereiche:** Web-UI

### Schritt 8: Web-UI – Aktion „Istmeldung"

In den Sparplan-Details steht die Buchungsaktion „Istmeldung" zur Verfügung — ausschließlich für Pläne ohne Konto-Link. Der Anwender gibt den gemeldeten Ist-Saldo ein; das System erzeugt die Differenzbuchung über den bestehenden Endpunkt und aktualisiert die Anzeige. Bei konto-verknüpften Plänen ist die Aktion deaktiviert bzw. wird mit einem verständlichen Hinweis abgewiesen (Kontostand ist die Ist-Quelle).

**Akzeptanzkriterien:**

- Die Aktion ist nur für Pläne ohne Konto-Link verfügbar; bei verknüpften Plänen erscheint eine Deaktivierung/Hinweis.
- Nach dem Erfassen entspricht der angezeigte `accumulatedAmount` dem gemeldeten Wert; die Korrekturbuchung ist in der Posting-Liste als Istmeldung erkennbar.

**Abhängigkeiten:** 5, 7

**Betroffene Bereiche:** Web-UI

### Schritt 9: Backup und Restore der neuen Daten

Kontoverknüpfungen und Istmeldung-Postings überstehen Backup und Restore vollständig: Die Join-Entity wird als optionale, abwärtskompatible Backup-Sektion exportiert und beim Import mit ID-Remapping auf die neu angelegten Pläne und Konten wiederhergestellt; das Istmeldung-Marker-Feld wird im Posting-Backup-DTO und im Import mitgeführt. Alte Backups ohne die neue Sektion bleiben gültig; die Payload-Version wird nicht erhöht. Die Wiederholung der `AccountLinkedIban`-Lücke (BackupDto ohne Export/Import) wird vermieden.

**Akzeptanzkriterien:**

- Export→Import erhält alle Konto-Links mit korrekt gemappten Plan-/Konto-IDs sowie Istmeldung-Postings inklusive Marker.
- Backups ohne die neue Sektion lassen sich weiterhin importieren; die Backup-Payload-Version bleibt unverändert.

**Abhängigkeiten:** 1, 5

**Betroffene Bereiche:** Persistenz/Backup

### Schritt 10: Regressionsabsicherung der Budgetauswertung

Die fachliche Unverändertheit der Budgetsicht wird explizit abgesichert: Tests belegen, dass Konto-Links und die `informal`-Auswertung weder Quelle noch Vorzeichen der Budget-Actuals verändern, dass Bankpostings verknüpfter Konten nicht als Sparplan-Budgetactuals einfließen und dass Istmeldungen die Actuals nicht verändern. Deckt ein Test eine ungewollte Änderung auf, wird die Ursache behoben — Ziel ist die bestätigte Unverändertheit, nicht eine Budget-Anpassung.

**Akzeptanzkriterien:**

- Budget-Istwerte konto-verknüpfter Pläne stammen unverändert aus den plan-verknüpften Postings (negative Beträge bleiben Kosten; Vorzeichenkonvention unverändert).
- Bankpostings verknüpfter Konten erscheinen nicht als Sparplan-Budgetactuals; `informal` wirkt nicht auf Budgets.
- Istmeldungen verändern die Budget-Actuals nicht; alle bisherigen Budget-Tests bleiben grün.

**Abhängigkeiten:** 2, 4, 5

**Betroffene Bereiche:** Budgetauswertung

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Persistenz und fachliche Verwaltung der Sparplan↔Konto-Verknüpfung | — | Domänenmodell, Persistenz/Migration |
| 2 | Konto-basierte Ist-Betrachtung in der Plananalyse | 1 | Plananalyse/Aggregate, Buchungsvalidierung |
| 3 | REST-API und ApiClient für die Kontoverknüpfung | 1, 2 | API, Shared-DTOs/ApiClient |
| 4 | „informal"-Kennzeichnung in der Postings-Ausgabe (API und MCP) | 2, 3 | API/Shared-DTOs, Abfrageschicht, MCP, Exporte |
| 5 | Istmeldung – Korrekturbuchung ohne Kontobewegung | 1, 3 | Domänenmodell, Persistenz/Migration, Plananalyse, API/ApiClient, MCP-Ausgabe, Budget |
| 6 | MCP-Schreiboperationen für Verknüpfung und Istmeldung | 3, 5 | MCP |
| 7 | Web-UI – Kontoverknüpfung verwalten und Kennzeichnungen anzeigen | 3, 4, 5 | Web-UI |
| 8 | Web-UI – Aktion „Istmeldung" | 5, 7 | Web-UI |
| 9 | Backup und Restore der neuen Daten | 1, 5 | Persistenz/Backup |
| 10 | Regressionsabsicherung der Budgetauswertung | 2, 4, 5 | Budgetauswertung |

## Offene Punkte

Keine. Die im Issue genannten offenen Punkte sind durch die dokumentierten Annahmen in `requirement.md` und die getroffenen Vorgehensentscheidungen (Ableitung statt Umbuchung, additive Saldensumme, kein Sammelkonto-Split, Istmeldung nur ohne Links, vollständiger Konto-Inflow, Istmeldung-Ausschluss aus Budget-Actuals, kein Istmeldung-Storno) abgedeckt; keine Nutzerentscheidung ist zwingend erforderlich.
