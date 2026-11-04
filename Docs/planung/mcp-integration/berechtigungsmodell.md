# Berechtigungsmodell – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 3 der Anforderung; spezifiziert das Scope-, Aktions-
und Vererbungsmodell (F2), auf dem Toolkatalog (Schritt 2), Sicherheits- und
Approval-Konzept (Schritt 3) aufsetzen. Fachliche Begriffe (Rechtearten,
Freigabestufen, Datentypen, Filter) sind in
[fachkonzept.md](fachkonzept.md) definiert und hier normativ weitergeführt.

---

## 1. Modellüberblick

Das Berechtigungsmodell beantwortet für jeden eingehenden MCP-Aufruf eine
einzige Frage:

> *Darf MCP-Zugang Z (im Namen von Benutzer B) die Rechteart R auf Datensatz D
> des Datentyps T ausüben?*

Die Antwort ergibt sich aus vier aufeinander aufbauenden Prüfstufen, die in
**dieser Reihenfolge** anzuwenden sind und jeweils nur einschränken können:

| Stufe | Prüfung | Quelle |
|---|---|---|
| 0 | **Benutzerrecht:** Hat Besitzer B überhaupt Zugriff auf Datensatz D (Besitz oder `AccountShare`-Freigabe)? | bestehendes Besitzmodell (`OwnerUserId`, `AccountShare`) |
| 1 | **Scope-Recht:** Enthält das effektive Rechteprofil des Zugangs einen Scope (Datentyp T × Rechteart R)? | Rechteprofil aus Freigabestufe bzw. individueller Konfiguration |
| 2 | **Filter:** Erfasst ein auf dem Scope gesetzter Filter den Datensatz D? | Filterkonzept F4 |
| 3 | **Kontext-Deckel:** Liegt die Aktion unterhalb der Deckel (AccountShare-Rolle, Genehmigungsmodus, globale Notfallschalter)? | Share-Rolle, `AiActionRequest`-Pflicht, Feature-Flags |

Erst wenn **alle** Stufen passieren, wird der Aufruf ausgeführt. Jede Stufe
kann nur „Nein" sagen oder unverändert weiterreichen – keine Stufe kann ein
früheres „Nein" aufheben (**einschränkend gewinnt**, Abschnitt 5).

### Verbindliche Grundsätze

1. **MCP-Rechte dürfen Benutzerrechte nur einschränken, niemals erweitern**
   (belegte Vorgabe). Ein MCP-Zugang ist ein *Unterraum* der Möglichkeiten
   seines Besitzers.
2. **Keine impliziten Rechte.** Jedes Recht ist ein explizit vergebenes Scope-
   Tupel; es gibt kein „erben" zwischen Datentypen und kein „alles außer".
3. **Rechte wirken auf Datensätze, nicht auf Endpunkte.** Die Prüfung erfolgt
   datensatzscharf (inkl. indirekter Besitzauflösung, Abschnitt 6), nicht nur
   tool-scharf.
4. **Kein Informationsleck:** Verweigerte oder ausgefilterte Datensätze
   verhalten sich wie nicht vorhandene – konsistent zur API-Konvention
   (404 statt 403, `inventory.md` Abschnitt 1.3).
5. **Prozessrechte statt Feldrechte:** Rechte beziehen sich auf Geschäfts-
   aktionen (Vervollständigen, Buchen, Stornieren), nicht auf einzelne
   Spalten. Feldschärfe entsteht durch die Tool-Schnittstellen, nicht durch
   das Rechtemodell.

---

## 2. Scope-Modell (F2)

### 2.1 Scope-Definition

Ein **Scope** ist das Tupel:

```
Scope = (Datentyp T, Rechteart R, optionaler Filter F)
```

- **Datentyp T:** einer der 18 klassifizierten Datentypen
  (`fachkonzept.md`, Abschnitt 7.1), abgebildet auf konkrete Entitäten/Enums
  (`Account`, `Posting`, `StatementDraft`, `ContactCategory`,
  `AccountType`-Enum …).
- **Rechteart R:** eine der sieben Rechtearten (Lesen, Vorschlag,
  Vervollständigung, Änderung, Erstellung, Buchung, Löschung) mit der im
  Fachkonzept (Abschnitt 4) und in Abschnitt 7 dieses Dokuments
  festgelegten Semantik.
- **Filter F (optional):** Einschränkung auf Datensatzmengen über die F4-
  Dimensionen (`AccountType`, `ContactCategory`, `SecurityCategory`,
  `SavingsPlanCategory`, `AttachmentCategory`) als positive
  Wertliste im `ReportFavorite`-CSV-Muster.

Das **Rechteprofil** eines MCP-Zugangs ist die Menge seiner Scopes.
Effektiv erlaubt ist ein Aufruf, wenn **mindestens ein Scope** (T, R, F)
existiert, dessen Datentyp und Rechteart matchen und dessen Filter den
Datensatz erfasst – und keine Deckel-Regel (Stufe 0/3) verletzt wird.

### 2.2 Aktionsmodell: Abbildung auf Geschäftsprozesse

Das Rechtemodell kennt keine „HTTP-Verben"; jede Rechteart umfasst eine
definierte Menge fachlicher Aktionen auf dem Datentyp. Beispiele (verbindliche
Zuordnung für den Toolkatalog in Schritt 2):

| Rechteart | Fachliche Aktionen (Beispiele, codebelegt) |
|---|---|
| Lesen | `IStatementDraftService.Get*`, `IPostingsQueryService`-Abfragen, Listen/Details/Exporte aller Datentypen, `validate`-Auswertung lesen |
| Vorschlag | Klassifizierungs-/Zuordnungsvorschläge für `StatementDraftEntry` (Contact, SavingsPlan, Security), `AliasName.Pattern`- und `BudgetRule.PurposePattern`-Vorschläge, `ClassifyAsync`-Ergebnis als Vorschlag zurückgeben |
| Vervollständigung | `Set*`/`Save*`/`ApplyBatchEntryUpdatesAsync`, `SetAccountAsync`, `SetDescriptionAsync`, `Split`, Stammdaten-Ergänzungen (`AliasName` anlegen, `CategoryId` setzen) |
| Änderung | `edit-core` (Betrag/Datum/Text), Stammdaten-Updates (`Account.Rename`, `SetIban`, `SetBankContact`, `SetSecurityProcessingEnabled` …), Kategorie-Umbenennung |
| Erstellung | `Contact`/`SavingsPlan`/`Security`/`BudgetPurpose`/`BudgetRule`/`ReportFavorite`/`HomeKpi`/`Attachment` anlegen; `StatementDraft` via Upload/`CreateEmptyDraftAsync` |
| Buchung | `IStatementDraftService.Book*`/`Commit`/`book-all` (→ `Posting`, `StatementImport`+`StatementEntry`, `Account.CurrentBalance`, `PostingAggregate`-Rebuild, `IBudgetImpactEvaluationService`) |
| Löschung | Entwurf verwerfen, Stammdaten/Kategorien/KPIs/Favorites löschen, `SavingsPlan`/`Security` archivieren (`ArchivedUtc`), `Posting` **stornieren** (`PostingReversalService`) |

**Implikationskanten (fachliche Vorbedingungen, keine Rechtsvererbung):**
Vervollständigung/Änderung setzen fachlich Lesen desselben Datentyps voraus –
die Stufenprofile (Abschnitt 4.1) gewähren Lesen daher immer mit. Ein
individuelles Profil, das z. B. „Änderung ohne Lesen" vergibt, ist technisch
zulässig (Aufruf würde auf unlesbare Daten wirken), aber fachlich
unsinnig und von der Verwaltungs-UI zu verhindern.

### 2.3 Granularitätsgrenzen

- **Keine Scope-Ebene unterhalb von Datentyp × Rechteart × Filter.** Es gibt
  keine Feld-Scopes („darf Betrag nicht sehen") und keine Datensatz-Scopes auf
  Einzel-Ids außerhalb der F4-Dimensionen. (Begründung: Komplexität vs.
  Nutzen; die vorhandenen Gruppierungsstrukturen decken die Anforderung ab.)
- **Zeitliche Gültigkeit** ergibt sich aus Zugangs-Status und Ablaufdatum,
  nicht aus dem Scope.

---

## 3. Rechtevererbung: Stufe → Zugang → Profil

### 3.1 Standardrechte je Freigabestufe

Jede Freigabestufe (F5) definiert ein **Standardprofil**: die Menge der
Scopes, die ein neuer Zugang dieser Stufe erhält, bevor individuelle
Anpassungen greifen. Die Stufen sind total geordnet nach zunehmender
Macht (Analyse ⊂ Assistent ⊂ Sachbearbeiter ⊂ Buchhalter); „Individuell"
steht außerhalb der Kette als freie Zusammenstellung.

| Freigabestufe | Standardprofil (auf alle Datentypen, soweit die Rechteart dort anwendbar ist – vgl. F3-Matrix) |
|---|---|
| **Analyse** | `Lesen` auf allen Datentypen |
| **Assistent** | `Lesen` + `Vorschlag` auf allen Datentypen |
| **Sachbearbeiter** | `Lesen` + `Vorschlag` auf allen Datentypen; `Vervollständigung` + `Änderung` + `Erstellung` auf Kontoauszugsentwürfen und vervollständigenden Stammdaten (Kontakte/`AliasName`, Kategorie-Zuordnungen) – jeweils genehmigungspflichtig nach Zugangs-Einstellung |
| **Buchhalter** | `Lesen` + `Vorschlag` + `Vervollständigung` + `Änderung` + `Erstellung` auf allen Datentypen (soweit F3 „✓"); `Buchung` + `Löschung` (inkl. Storno) ausschließlich **genehmigungspflichtig** bzw. als vorab genehmigter Scope |
| **Individuell** | kein Standard – der Benutzer setzt jedes Scope-Tupel explizit; die UI startet mit einer wählbaren Stufe als Vorlage |

**Wichtig:** Auch ein Standardprofil vergibt nur Scopes, die in der F3-Matrix
als „✓" markiert sind. Ein Buchhalter-Profil enthält z. B. kein
`Erstellung`-Scope auf `Posting` (fachlich nicht existent) und kein
`Löschung`-Scope auf `StatementImport` (Beleg).

### 3.2 Individuelle Rechteanpassung je Zugang

- Bei Stufe **Individuell** (oder als Abweichung von einer Vorlage) kann der
  Benutzer Scopes **entziehen oder hinzufügen** – stets innerhalb der
  F3-Matrix („✓"-Zellen) und der Deckel.
- Anpassungen sind **zugangslokal**: Sie ändern nicht die Stufen-Definition
  und nicht andere Zugänge desselben Benutzers.
- **Darstellbarkeit:** Ein individuelles Profil bleibt eine Liste von
  Scope-Tupeln (Datentyp × Rechteart × Filter) – speicherbar z. B. als
  Zeilenmenge je Zugang (Datenmodell: Schritt 4) und in der UI als
  Matrix-Editor (UI-Konzept: Schritt 4).

### 3.3 Auflösung zur Laufzeit

Bei jedem Aufruf wird das effektive Profil neu bewertet (kein eingefrorener
Token-Claim): Änderungen an Stufe, Profil, Filtern, Zugangs-Status oder
AccountShare greifen **ab dem nächsten Aufruf** – konsistent zum
`OnTokenValidated`-Prinzip der JWT-Pipeline (serverseitige Sofortprüfung).

---

## 4. Konfliktverhalten

Konflikte entstehen, wenn mehrere Regeln denselben (Aufruf, Datensatz)
betreffen. Verbindliche Vorrangregeln:

1. **Einschränkend gewinnt immer.** Wenn irgendeine Regel den Zugriff
   verweigert oder einschränkt, gibt es keine Regel, die das aufheben kann.
   Es existiert **kein „erweiternder" Scope-Typ** – Scopes sind nur positiv
   (erlaubend) oder Filter (ausschließend); ein erlaubender Scope kann nie
   einen Filter, eine Share-Rolle oder ein Benutzerrecht überstimmen.
2. **Mehrere erlaubende Scopes auf denselben Datentyp × Rechteart:**
   Vereinigung (**ODER**) der Filtermengen – der Datensatz ist erlaubt, wenn
   ihn mindestens ein passender Scope erfasst. Beispiel: zwei
   `Lesen`-Scopes auf Kontakte, einer gefiltert auf Kategorie A, einer
   ungefiltert → ungefiltert gewinnt faktisch (weiteste erlaubende Menge).
3. **Mehrere Filterdimensionen innerhalb eines Scopes:**
   **UND**-Verknüpfung – jede gesetzte Dimension schränkt weiter ein
   (F4, `fachkonzept.md` Abschnitt 8.3).
4. **Benutzerrecht ⊃ Share-Deckel ⊃ Scope/Filter:** Die Hierarchie der
   Prüfstufen (Abschnitt 1) ist zugleich die Vorrangordnung. Stufe 0
   (Benutzerrecht) kann durch nichts erweitert werden; Stufe 3 (Deckel)
   kann alles darunter kappen.
5. **Keine Deny-Scopes.** Verweigerung wird durch *Nicht-Vergabe* und
   Filter ausgedrückt, nicht durch explizite Verbote – das hält die
   Auswertung monoton einschränkend und auditierbar.

---

## 5. Grundsatz „nur einschränken" – Operationalisierung

### 5.1 Benutzerrechte als absolute Obergrenze (Stufe 0)

Das bestehende Zugriffsmuster (`Service(ownerUserId, …)` + EF-`Where` auf
`OwnerUserId`) bleibt die erste Prüfung: Ein MCP-Tool arbeitet mit der
`ownerUserId` des **Zugangsbesitzers** (Auflösung Token → UserId, analog
`ICurrentUserService`). Alles, was der Besitzer nicht sehen/ändern darf,
existiert für den MCP-Client nicht.

### 5.2 AccountShare als Rechte-Deckel (Stufe 3)

`AccountShare`/`AccountShareRole` (`Read=0`, `Write=1`, `Admin=2`,
`Domain/Enums.cs`) gewährt einem Benutzer Zugriff auf ein fremdes `Account`.
Für MCP-Aufrufe des Teilnehmers (`AccountShare.UserId`) gilt die Rolle als
**harte Obergrenze** für alle vom Konto abhängigen Datentypen:

| Share-Rolle | Maximale MCP-Rechte auf kontoabhängige Daten (z. B. `Posting` mit `AccountId`, `StatementImport`/`StatementEntry`, `AccountLinkedIban`, Entwürfe mit `DetectedAccountId`) |
|---|---|
| `Read` | `Lesen` – keine Vorschlags-/Schreib-/Buchungs-/Löschwirkung auf kontoabhängige Datensätze |
| `Write` | `Lesen`, `Vorschlag`, `Vervollständigung`, `Änderung`, `Erstellung` (im kontoabhängigen Kontext), `Buchung` (Verbuchung von Entwürfen auf das geteilte Konto) |
| `Admin` | wie `Write`, zusätzlich `Löschung` (inkl. Storno) im kontoabhängigen Kontext |

- Überschreitet ein Scope die Share-Rolle (z. B. Buchhalter-Profil mit
  `Buchung`, aber Share = `Read`), wird der Aufruf **datensatzweise**
  verweigert – nicht der ganze Scope kassiert: eigene Konten des Besitzers
  bleiben vom selben Scope erfasst.
- Endet oder verändert sich eine Freigabe (`RevokedUtc`, Rollenwechsel),
  greift die neue Obergrenze sofort (Auflösung zur Laufzeit, 3.3).
- `AccountShare` gilt nur auf Kontoebene; andere Aggregate kennen kein
  Fremd-Freigabemodell – dort ist Stufe 0 allein durch `OwnerUserId`
  definiert.

### 5.3 Weitere Deckel (Stufe 3)

- **Genehmigungsmodus / Approval:** Ein technisch erlaubtes Recht kann
  ausführungsseitig zurückgehalten werden (`AiActionRequest` → Pending),
  bis der Benutzer freigibt – das Recht wird dadurch zeitlich
  eingeschränkt, nie erweitert (Detail: `approval-konzept.md`).
- **Globale Notfallschalter:** Feature-Flag „MCP deaktiviert" bzw.
  „Schreibzugriffe deaktiviert" (`appsettings`-Konvention) kappen alle
  Scopes global bzw. auf Lesewirkung (Detail: `sicherheitskonzept.md`).
- **Admin-Rolle:** erweitert keine Datenrechte (kein Fremdzugriff via MCP).

---

## 6. Rechteableitung für Entitäten ohne eigenes OwnerUserId

Die Besitz- und damit Rechteauflösung erfolgt über die referenzierten
Aggregate. Verbindliche Ableitungstabelle:

| Entität | Besitzauflösung | Rechte-/Filterwirkung |
|---|---|---|
| `Posting` | Je `Kind` (`PostingKind`): `Bank` → `AccountId` → `Account.OwnerUserId`; `Contact` → `ContactId` → `Contact.OwnerUserId`; `SavingsPlan` → `SavingsPlanId` → `SavingsPlan.OwnerUserId`; `Security` → `SecurityId` → `Security.OwnerUserId`. | Scope/Filter des *Buchungen*-Datentyps prüfen; zusätzlich muss das referenzierte Aggregate für den Besitzer zugänglich sein (Stufe 0: Fremdobjekt → Datensatz unsichtbar). Kategorie-Filter wirken **durch** das Aggregate (z. B. SecurityCategory-Filter filtert Postings mit `SecurityId` in gefilterter Kategorie; Kontotyp-Filter filtert `Kind=Bank` über `Account.Type`). |
| `PostingAggregate` | über dieselben Referenz-Ids wie `Posting`. | rein lesend; erbt die Posting-Auflösung eins zu eins. |
| `StatementImport` | `AccountId` → `Account.OwnerUserId`. | Datentyp „Kontoauszüge"; Kontotyp-Filter über `Account.Type`; Share-Deckel über das Konto. |
| `StatementEntry` | `StatementImportId` → `StatementImport.AccountId` → `Account.OwnerUserId`. | wie `StatementImport`; `ContactId`-Verknüpfung ändert nichts am Besitz (reines Zuordnungsfeld). |
| `StatementDraftEntry` | `DraftId` → `StatementDraft.OwnerUserId`. | Datentyp „Kontoauszugsentwürfe"; Filter über `DetectedAccountId` → `Account.Type` sowie über verknüpfte `ContactId`/`SavingsPlanId`/`SecurityId`-Kategorien, sofern gesetzt. |
| `AccountLinkedIban` | `AccountId` → `Account.OwnerUserId`. | Datentyp „Konten"; identische Scope-/Filterwirkung wie `Account`. |
| `AliasName` | `ContactId` → `Contact.OwnerUserId`. | Datentyp „Kontakte"; identische Wirkung wie `Contact` (Vorschlags-/Vervollständigungsrecht umfasst Musterpflege). |
| `SecurityPrice` | `SecurityId` → `Security.OwnerUserId`. | Datentyp „Wertpapiere"; rein lesend für MCP; Kategoriefilter über `Security.CategoryId`. |
| `Attachment` | eigenes `OwnerUserId`, **aber** polymorphe Zielbindung `EntityKind` + `EntityId` (12 `AttachmentEntityKind`-Werte). | Besitz über `OwnerUserId`; **zusätzlich** gilt das Recht auf das referenzierte Ziel als inhaltliche Decke: ein Anhang an ein `StatementDraftEntry` eines Kontos, das der Besitzer nur per `Read`-Share sieht, darf über MCP nicht verändert werden. Filter über `Attachment.CategoryId` (Anhangkategorien). |

**Regel für alle Fälle:** Wenn die Besitzkette auf ein Objekt zeigt, das der
Besitzer nicht (mehr) besitzt oder für das nur eine Share-Rolle existiert,
vererbt die abhängige Entität höchstens die Rechte, die auf dem Anker-
Aggregat bestehen – und der MCP-Scope kann sie weiter einschränken, nie
erweitern.

---

## 7. Semantik der Rechtearten (Normierung F2/F3)

Zur Vermeidung von Doppeldeutigkeit ist die Rechteart-Semantik hier normativ
fixiert:

- **Lesen:** jede Form der Ausgabe des Datentyps an den MCP-Client –
  Datensätze, Aggregate, exportierte Darstellungen, berechnete Werte
  (Reports), Anhanginhalte. Alles Ausgegebene ist untrusted content für den
  Client und umgekehrt ist jeglicher an den Client gelieferte Inhalt
  prompt-injection-relevant (Sicherheitskonzept).
- **Vorschlag:** Erzeugung und Rückgabe fachlicher Vorschläge **ohne
  Persistenz**. Ein Vorschlag wird erst durch eine separate, berechtigte
  Aktion (Vervollständigung/Änderung durch Client oder Übernahme durch den
  Benutzer in der UI) wirksam.
- **Vervollständigung:** Schreiben **nur in Lücken** vorhandener Datensätze –
  Setzen bisher fehlender Zuordnungen/Felder (z. B. `ContactId`,
  `SavingsPlanId`, `SecurityId`, `SetAccountAsync`). Überschreiben
  vorhandener Werte ist **Änderung**, nicht Vervollständigung.
  (Fachliche Kante: Ein Client mit nur Vervollständigungsrecht kann nicht
  korrigieren, sondern nur ergänzen – Fehlzuordnungen erfordern
  Änderungsrecht oder UI-Korrektur.)
- **Änderung:** Überschreiben vorhandener Feldwerte und fachliche
  Umorganisation (Split, Umbenennung, Neuzuordnung).
- **Erstellung:** Anlage neuer Datensätze; bei `StatementDraft` ausschließlich
  über die vorhandenen Import-/Entwurfswege (Upload, `CreateEmptyDraftAsync`)
  – kein alternativer Dateneingang.
- **Buchung:** ausschließlich die Verbuchung von Kontoauszugs-Entwürfen
  (`Book`/`Commit`/`book-all` auf `IStatementDraftService`). Erzeugt
  `Posting`, `StatementImport`/`StatementEntry`, aktualisiert
  `Account.CurrentBalance`, stößt `PostingAggregate`-Rebuild und
  Budget-Neubewertung an. **Es gibt kein Buchungsrecht auf `Posting`
  selbst** und keinen alternativen Posting-Schreibpfad.
- **Löschung:** datentypspezifisch: physisches Löschen für Entwürfe,
  Stammdaten, Kategorien, Konfigurationen; **Archivieren** für `SavingsPlan`
  und `Security`; **Storno** (`PostingReversalService`) für `Posting` – nie
  physisches Löschen von Buchungen; **nicht vergeben** für `StatementImport`/
  `StatementEntry` (Belege) und `AccountType`.

---

## 8. Auswertungslogik (fachliche Spezifikation)

Für einen Aufruf `(Zugang Z, Datentyp T, Rechteart R, Datensatz D)`:

1. **Zugang aktiv?** `Status == Aktiv` und `ExpiresUtc` nicht überschritten,
   sonst „verweigert".
2. **Besitz (Stufe 0):** Besitzer B = Z.Besitzer. Auflösung von D nach
   Abschnitt 6 → Anker-Aggregat A. Existiert A und gehört es B (`OwnerUserId`
   == B) oder ist A ein `Account` mit aktivem `AccountShare` für B
   (`RevokedUtc == null`)? Sonst „nicht vorhanden" (404-Semantik).
3. **Deckel bestimmen (Stufe 3):** maximal zulässige Rechteart aus Share-
   Rolle (falls Fremdkonto), Genehmigungsmodus, globalen Schaltern.
   Verlangte R über dem Deckel → „verweigert" bzw. „genehmigungspflichtig".
4. **Scope vorhanden (Stufe 1)?** Existiert im effektiven Profil von Z ein
   Scope (T, R, F*)? Nein → „verweigert".
5. **Filter (Stufe 2):** Mindestens ein passender Scope mit Filter, der D
   erfasst (ODER über Scopes, UND über Dimensionen, unklassifizierte
   Datensätze nicht erfasst)? Sonst „verweigert".
6. **Ausführen** – ggf. nach `AiActionRequest`-Freigabe (Stufe 3,
   zeitliche Dimension). Ergebnis protokollieren (Nutzung/Audit).

Die Auswertung ist **monoton einschränkend**: Jede Stufe kann die Menge der
zulässigen Aufrufe nur verkleinern. Es existiert kein Mechanismus, der über
diesen Pfad hinaus Rechte verleiht (keine Tool-eigenen Rechte, keine
rollenbedingte Erweiterung).

---

## 9. Konsistenzanforderungen an Folgeartefakte

- **Toolkatalog (Schritt 2):** Jeder Tool deklariert seine benötigten Scopes
  in der Terminologie dieses Dokuments (Datentyp × Rechteart); Tools ohne
  Scope-Zuordnung sind nicht zulässig.
- **Sicherheitskonzept (Schritt 3):** implementiert die Prüfstufen 0–3 als
  Durchsetzungsschicht (Token→UserId-Auflösung, datensatzscharfe Prüfung).
- **Approval-Konzept (Schritt 3):** übernimmt den Genehmigungs-Deckel
  (Stufe 3) und die Zustände `AiActionRequest`.
- **Datenmodell (Schritt 4):** persistiert Zugang, Token-Hash,
  Freigabestufe, individuelle Scope-Menge und Filter-CSVs nach dem
  `ReportFavorite`-Muster; `AccountShare` und `OwnerUserId` bleiben
  unverändert Quelle der Stufe-0-Prüfung.
