# UI-Konzept – MCP-Integration für FinanceManager (Issue #470)

**Status:** Konzept (Planungsartefakt, keine Implementierung)
**Einordnung:** Artefakt 5 der Anforderung; spezifiziert die
Benutzeroberfläche (F11) für die MCP-Integration. Fachliche Begriffe
(Freigabestufen, Rechtearten, Filter, Genehmigungsmodus, Zugänge) werden
aus [fachkonzept.md](fachkonzept.md) und
[berechtigungsmodell.md](berechtigungsmodell.md) übernommen; die
Anforderungen an das Genehmigungscenter aus
[approval-konzept.md](approval-konzept.md) Abschnitt 7, an die
Audit-Ansicht aus [audit-konzept.md](audit-konzept.md) Abschnitt 6 und
an die Sicherheitshinweise aus
[sicherheitskonzept.md](sicherheitskonzept.md) (Einmalanzeige des
Tokens, TLS-/HTTP-Hinweis, IP-Bindung).

---

## 1. Zielbild und UX-Leitlinien

Die MCP-Verwaltungsoberfläche richtet sich an den **datenbesitzenden
Benutzer** (kein Admin-Werkzeug für Fremddaten – „Admin ist kein
Datentürsteher", Fachkonzept 5.4). Sie muss ein technisch komplexes
Rechtemodell (18 Datentypen × 7 Rechtearten × Filter) so führen, dass
der Standardfall in einer Minute erledigt ist und der Expertenfall
vollständig kontrollierbar bleibt.

**Verbindliche Leitlinien:**

1. **Standardfall zuerst.** Der Anlage-Dialog führt über eine
   Freigabestufen-Auswahl mit Klartext-Erklärung („Was darf dieses
   KI-System?"). Der Benutzer muss für einen funktionierenden Zugang
   **nie** das Scope-Modell verstehen – Stufe wählen, benennen,
   fertig.
2. **Expertenmodus als bewusste Eskalation, nicht als zweiter Weg.**
   Individuelle Rechte und Filter erscheinen erst nach explizitem
   Umschalten („Anpassen" bzw. Stufe „Individuell"). Die Matrix zeigt
   stets das *effektive* Profil – auch bei Standardstufen lesbar, aber
   nur im Expertenmodus editierbar.
3. **Verständlichkeit sicherheitsrelevanter Begriffe.** Jede Stufe,
   jede Rechteart und jeder Filter erhält einen lokalisierten
   Erklärtext mit konkretem Beispiel (z. B. „Buchung = legt
   Kontoauszugs-Entwürfe endgültig als Buchungen an – nicht
   rückgängig über die KI"). Fachbegriffe der Konzeptebene
   („Scope", „Deckel") werden in der UI **nicht** verwendet; die UI
   spricht von „Rechten", „Einschränkungen" und „Genehmigung".
4. **Kritische Aktionen über das Bestätigungsmuster.** Widerruf,
   Löschen eines Zugangs, Stufen-Downgrade/-Upgrade mit Rechteverlust,
   Freigabe finaler Wirkungen und Mehrfach-Entscheidungen laufen über
   `IConfirmationService.ConfirmAsync` (Severity-Stufen; respektiert
   die Benutzereinstellung `ShowConfirmations`). Token-Rotation und
   Genehmigungen von Buchung/Storno/Löschung sind immer
   „high severity".
5. **Nichts Verstecktes.** Der Benutzer sieht je Zugang immer:
   effektive Rechte, letzte Nutzung, offene Genehmigungsanfragen und
   die Frage „welches KI-System hat wann was getan" (Audit-Ansicht).
   Sicherheitsrelevante Zustände (abgelaufen, deaktiviert, HTTP-
   Betrieb ohne TLS) werden als sichtbare Hinweise geführt, nicht als
   Tooltip-Fußnoten.
6. **Kein Modellbruch in der UI.** Die UI bildet exakt die Modelle der
   Vorgängerdokumente ab: fünf Freigabestufen, sieben Rechtearten,
   fünf Filterdimensionen, sechs `AiActionRequest`-Zustände
   (inkl. `Expired`), Genehmigungsmodus „je Aktion" vs. „dauerhaft
   vorab genehmigt". Es gibt keine UI-eigene Rechtesemantik.

---

## 2. Einordnung in die UI-Architektur des Repos

Die Umsetzung folgt ausschließlich den bestehenden Konventionen –
keine neue Technik für MCP:

|| Konvention (Ist, codebelegt) | Anwendung für MCP-UI |
|---|---|---|
| Blazor Server, Razor Components `InteractiveServer` | Alle neuen Seiten/Overlays sind Razor Components in `FinanceManager.Web/Components/` |
| MVVM: `ViewModelBase` (`IAsyncDisposable`, `IRibbonProvider`), `BaseListViewModel<TItem>`, `BaseCardViewModel<T>` | Neue ViewModels unter `FinanceManager.Web/ViewModels/Mcp/` (siehe Abschnitt 4); Erzeugung per `ActivatorUtilities`/`CreateSubViewModel`, nicht per DI-Registrierung |
| Routing: `/list/{Kind}` über `ListViewModelFactory`, `/card/{Kind}/{Id}` über `[CardRoute]`/`CardViewModelResolver` | `case "mcp-accesses"` in der Factory; `[CardRoute("mcp-accesses")]` bzw. `[CardRoute("mcp-approvals")]` auf den ViewModels |
| UI ↔ API nur über `IApiClient` | Neue Methoden in `FinanceManager.Shared/IApiClient` + Partial `ApiClient.Mcp*.cs`; ViewModels rufen **niemals** Services direkt auf |
| Neue API-Endpunkte als `[ApiController]` mit `[Authorize(JwtBearer)]`, `ICurrentUserService`, `ApiErrorDto`/`Err_*`, `skip`/`take`-Paging | `McpAccessesController`, `AiActionRequestsController`, `McpAuditController` (Skizze Abschnitt 6) – dieselbe BFF-REST-API, kein zweiter Kanal |
| Lokalisierung de/en via `IStringLocalizer` + resx (`Resources/Pages.*`, `Resources/Components.*`) | Neue resx-Schlüssel `Mcp_*` in den bestehenden Ressourcen-Dateien der Seiten/Komponenten |
| Navigation in `MainLayout.razor` (NavLink), Ribbon über `IRibbonProvider`/`UiRibbonRegister` | Platzierung siehe Abschnitt 3 |
| Overlays über `UiOverlaySpec` + `UiActionRequestedEx`/`EmbeddedPanelHost` | Token-Einmalanzeige, Anlage-Assistent, Scope-Matrix-Editor als Overlays |
| Bestätigungen über `IConfirmationService` + `ConfirmationDialogHost` | Widerruf, Löschen, Genehmigung/Ablehnung finaler Wirkungen |
| Benachrichtigungen `Notification` (EventDriven, `NotificationTarget`) | Genehmigungsanfragen (Dashboard/HomePage), Ausführungsergebnisse, Ablauf-/Sicherheitshinweise; Anzeige über vorhandene `HomeNotifications`-Infrastruktur |
| Admin-Sichten über `Roles="Admin"` bzw. `_current.IsAdmin`-Prüfung | Admin-Sicherheitsübersicht (Abschnitt 5) |

**Wichtig für die Zuordnung:** Die MCP-**Verwaltungs**-UI spricht –
wie jede andere UI – mit der bestehenden REST-API (`/api/...`,
JWT). Der MCP-Endpunkt (`/mcp`, PAT-Authentifizierung,
Architekturkonzept) ist ein **getrennter** Kanal für externe Clients
und wird von der Blazor-UI nie direkt benutzt. Verwaltungsaktionen des
Benutzers in der UI werden trotzdem als Zugangsereignisse
protokolliert (Audit-Konzept 3.3: Vollständigkeit der Rechtehistorie).

---

## 3. Platzierung in Navigation und Ribbon

### 3.1 Navigation (`MainLayout.razor`)

- **Neuer Nav-Eintrag „KI-Zugänge (MCP)"** nach „Setup" und vor den
  Admin-Einträgen: führt auf `/list/mcp-accesses`. Für alle
  authentifizierten Benutzer sichtbar (jeder Benutzer verwaltet seine
  eigenen Zugänge).
- **Genehmigungscenter** ist **kein** separater Nav-Eintrag, sondern
  wird über zwei vorhandene Mechanismen erreicht:
  1. EventDriven-`Notification` (HomePage/Dashboard) verlinkt bei
     neuer Anfrage auf `/card/mcp-approvals` – der bestehende
     `HomeNotifications`-Mechanismus bleibt der Eingangskanal.
  2. Ein **Zähler-Badge** am Nav-Eintrag „KI-Zugänge" zeigt die Zahl
     offener Pending-Requests (Aktualisierung beim Seitenwechsel über
     einen kompakten Count-Endpunkt; kein Polling-Zwang, kein
     SignalR-Ausbau über die bestehende Circuit-Mechanik hinaus).
- **Admin-Erweiterung:** bestehender Admin-Bereich (`/list/users`
  etc.) erhält keine MCP-Benutzerverwaltung – wohl aber eine
  **Sicherheitsübersicht** (Abschnitt 5.2), erreichbar als Tab im
  Setup-/Admin-Kontext, der nur Sicherheitsereignisse zeigt.

### 3.2 Ribbon-Belegung

- **Zugangsliste** (`McpAccessListViewModel`): Aktionen „Neuer
  Zugang" (öffnet Anlage-Assistent), „Aktualisieren".
- **Zugangskarte** (`McpAccessCardViewModel`): Register „Aktionen"
  mit „Token rotieren", „Aktivieren/Deaktivieren", „Zugang
  löschen" (Ribbon-Analogie zu anderen Karten, Small/Large-
  Konvention, `MobileShortcut` für Primäraktionen).
- **Genehmigungscenter** (`McpApprovalCenterViewModel`): Register
  „Entscheidungen" mit „Freigeben", „Ablehnen" (aktiviert bei
  Auswahl), „Alle markierten freigeben" (Batch), „Aktualisieren";
  Register „Ansicht" mit Filter-Umschaltern (offen/entschieden,
  Zeitraum).
- **Audit-Ansicht** (Tab der Zugangskarte + eigenständige Karte):
  „Export (JSON)", „Export (CSV)", „Integrität prüfen".

---

## 4. UI-Komponenten im Detail (F11)

Alle Komponenten folgen dem Muster: Liste (`BaseListViewModel<T>`,
`IListItemNavigation` → Karte) bzw. Karte (`BaseCardViewModel<T>`,
Sections/Overlays). Die ViewModels laden ausschließlich über
`IApiClient`; `InitializeAsync`/`LoadAsync`-Semantik wie bei den
bestehenden List-/Card-ViewModels.

### 4.1 Verwaltung der MCP-Zugänge

**Liste `/list/mcp-accesses` – `McpAccessListViewModel : BaseListViewModel<McpAccessListItem>`**

`McpAccessListItem` implementiert `IListItemNavigation` →
`/card/mcp-accesses/{id}`.

Spalten (`ListColumn`-Konvention):

| Spalte | Inhalt |
|---|---|
| Name | Bezeichnung des Zugangs (unique je Benutzer) |
| Freigabestufe | lokalisierte Stufenbezeichnung mit Icon/Farbgebung nach Wirkung (Analyse → grün, Buchhalter → warnend) |
| Status | Aktiv / Deaktiviert / Abgelaufen (aus `ExpiresUtc` abgeleitet) / Widerrufen – abgelaufene und deaktivierte als `muted`-Zeile |
| Letzte Nutzung | `LastUsedUtc` relativ („vor 3 Tagen"), „nie" bei Neu |
| Offene Anfragen | Anzahl Pending-`AiActionRequest`s des Zugangs |
| Ablaufdatum | `ExpiresUtc` mit Warnfärbung bei baldiger Fälligkeit |

Standardfilter: Suche über Name (vorhandenes
`AllowSearchFiltering`), Zeitraum-Filter auf letzte Nutzung
(`AllowRangeFiltering`). Kein Fremdzugriff – Liste ist per
`ownerUserId` besitzerbeschränkt (Service-Layer, Stufe 0).

**Karte `/card/mcp-accesses/{id}` – `McpAccessCardViewModel : BaseCardViewModel<McpAccessDetail>`**

Die Karte gliedert sich in Sections (Accordion-Muster des
`SetupCardViewModel`/`SetupSections.razor`, DynamicComponent):

| Section | Inhalt | Editierbarkeit |
|---|---|---|
| **Stammdaten** | Name, optionale Beschreibung (für den Benutzer – welches KI-System/welcher Zweck), Freigabestufe, Ablaufdatum, optionale IP-Bindung (Adressbereich-Liste; Hinweistext aus Sicherheitskonzept 8.2) | immer |
| **Token** | Status (aktiv/rotiert/widerrufen), `LastUsedUtc`, Aktionen: rotieren, widerrufen – **kein** Klartext (Einmalanzeige nur im Dialog, 4.2) | Aktionen |
| **Rechte** | Effektives Rechteprofil: bei Standardstufen lesende Matrix-Anzeige; bei „Individuell" der Matrix-Editor (4.3); Genehmigungsmodus-Auswahl (4.3.3) | Expertenmodus |
| **Einschränkungen (Filter)** | je Datentyp die gesetzten Filterdimensionen mit Auflösung der Kategorie-Ids zu lesbaren Namen (4.4) | Expertenmodus |
| **Nutzung & Verlauf** | kompakte Nutzungsübersicht (`LastUsedUtc`, Aufrufzahl, Fehlschlagquote, zuletzt verwendete Tools – aus dem Audit aggregiert) + Link in die Audit-Ansicht (4.5) mit voreingestelltem Zugangsfilter | lesend |
| **Offene Anfragen** | Liste der Pending-/Approved-`AiActionRequest`s des Zugangs (kompakt), Link ins Genehmigungscenter | lesend |

**Statuswechsel** (Aktivieren/Deaktivieren) erfolgt direkt über eine
Ribbon-/Section-Aktion mit `IConfirmationService` bei Deaktivierung
(Hinweis: laufende Client-Sessions werden sofort abgelehnt –
Sofortwirkung nach `OnTokenValidated`-Prinzip). Stufenwechsel mit
Rechteverlust (z. B. Buchhalter → Analyse) erfordert Bestätigung und
zeigt eine Vorher/Nachher-Kurzliste der entfallenen Rechte.

### 4.2 Tokenverwaltung (Einmalanzeige!)

Der Token-Lebenszyklus (Erzeugen, einmalige Anzeige, Rotation mit
Sofort-Invalidierung, Widerruf) ist fachlich in Fachkonzept 6.3 und
Sicherheitskonzept 3 festgelegt; die UI setzt ihn wie folgt um:

- **Anlage-Assistent** (`UiOverlaySpec`, modal): dreistufig –
  1. Name + Freigabestufe (mit Stufen-Erklärtexten, Auswahlkarten),
  2. optional: Ablaufdatum, IP-Bindung, Experten-Rechte/Filter
     (ausgeblendet hinter „Erweiterte Einstellungen"),
  3. **Einmalanzeige-Seite:** Klartext-Token `fm_mcp_…` in
     Copy-Box mit Kopieren-Button, großflächigem Hinweis „wird nur
     einmal angezeigt – jetzt sicher speichern" und einer
     **Pflicht-Bestätigung** („Ich habe den Token gespeichert") bevor
     der Dialog schließt. Der Server liefert den Klartext einmalig in
     der Anlage-Antwort; die UI hält ihn nur in der Dialog-Instanz –
     kein Caching, kein LocalStorage, kein erneutes Abrufen (es gibt
     keinen solchen Endpunkt).
- **Rotation:** Ribbon-Aktion „Token rotieren" →
  `IConfirmationService` (high severity, Hinweis „das alte Token wird
  sofort ungültig") → derselbe Einmalanzeige-Dialog mit dem neuen
  Token.
- **Widerruf/Löschen:** `IConfirmationService` mit Klartext-Folge
  („alle Aufrufe dieses Zugangs werden sofort abgelehnt; offene
  Genehmigungsanfragen werden zurückgezogen – Approval-Konzept 6.3").
- **HTTP-/TLS-Hinweis (Pflicht aus Sicherheitskonzept 8.2.5):** Läuft
  die Anwendung ohne TLS (serverseitig über die
  Endpunkt-/Konfigurationslage erkennbar und vom API-DTO
  mitgeliefert), zeigt der Anlage-Dialog und die Token-Section einen
  Warnhinweis „Token wird unverschlüsselt übertragen – TLS-Setup
  empfohlen" inklusive Empfehlung kurzer Ablaufdaten. Hinweis ist
  informativ, nicht blockierend (unterstützter LAN-Betrieb).
- **Verbindlichkeitsregel für die Umsetzung:** Token-Klartext darf in
  keinem persistenten Client-State landen – der Einmalanzeige-Dialog
  verwirft ihn beim Schließen; `RequestLoggingMiddleware`-Redaction
  greift serverseitig; die UI baut keine URL, die den Token enthält
  (Sicherheitskonzept 3.1: niemals Token in URLs).

### 4.3 Rechteverwaltung (Freigabestufen + individuelle Scopes)

#### 4.3.1 Einfache Führung: Freigabestufe wählen

Fünf Auswahlkarten (de/en lokalisiert) mit je ein bis zwei Sätzen
Klartext und einem Beispiel:

|| Stufe | UI-Klartext (sinngemäß) | Warnhinweis |
|---|---|---|---|
| Analyse | „Darf alle Daten ansehen und auswerten. Kann nichts ändern." | – |
| Assistent | „Wie Analyse, zusätzlich dürfen Vorschläge erzeugt werden (z. B. Kategorisierungsideen). Ändert nichts." | – |
| Sachbearbeiter | „Darf Kontoauszugs-Entwürfe vorbereiten und ergänzen – Änderungen laufen über Ihre Genehmigung. Bucht und löscht nicht." | Hinweis auf Genehmigungsfluss |
| Buchhalter | „Volles Prozessrecht inkl. Verbuchung und Storno – immer über Ihre Genehmigung (oder dauerhaft vorab genehmigt)." | deutlicher Warnhinweis (höchste Wirkung) |
| Individuell | „Rechte je Datentyp selbst zusammenstellen (Expertenmodus)." | Verweis auf Matrix-Editor |

Die Auswahlkarte zeigt **aufklappbar** die effektive Rechtematrix der
Stufe (lesend) – Verständlichkeit ohne Bearbeitungsdruck.

#### 4.3.2 Expertenmodus: Scope-Matrix-Editor

Bei Stufe „Individuell" (oder „Von Vorlage anpassen" – startet mit
dem Standardprofil einer gewählten Stufe, Berechtigungsmodell 3.2):

- **Matrix:** Zeilen = die 18 Datentypen (lokalisierte Fachnamen,
  gruppiert nach Fachbereich: Konten & Auszüge, Stammdaten,
  Budget & Auswertung, Anhänge), Spalten = die 7 Rechtearten
  (Lesen, Vorschlag, Vervollständigung, Änderung, Erstellung,
  Buchung, Löschung). Zellen = Checkboxen – **nur dort aktivierbar,
  wo die F3-Matrix „✓" bzw. „◑" markiert**; „—"-Zellen sind
  deaktiviert und mit Kurzbegründung belegt (z. B. „Buchungen können
  nicht erstellt werden – nur über Entwurfs-Verbuchung",
  „Kontoauszüge sind Belege und nicht löschbar").
- **Implikationshilfe:** Wird eine höherwertige Rechteart ohne Lesen
  gesetzt, warnt die UI („Änderung ohne Lesen ist fachlich unsinnig" –
  Berechtigungsmodell 2.2) und schlägt das Setzen des Leserechts vor;
  die API lehnt unsinnige Kombinationen zusätzlich ab.
- **Effektiv-Anzeige:** Die Matrix zeigt bei Standardstufen das aus
  der Stufe abgeleitete Profil; Wechsel der Stufe verwirft
  individuelle Anpassungen (Bestätigung).
- **Kontext-Hinweis Deckel:** Ein erklärender Hinweistext macht
  sichtbar, dass `AccountShare`-Freigaben und die Genehmigungs-
  einstellung die wirksamen Rechte weiter einschränken können
  („Dieses Recht gilt nur bis zur Obergrenze Ihrer eigenen
  Benutzerrechte – ein geteiltes Konto mit Leserecht bleibt auch
  für die KI schreibgeschützt."). Die komplexen Einzelfälle bleiben
  Server-Sache; die UI vermittelt nur das Prinzip.

#### 4.3.3 Genehmigungsmodus

In der Rechte-Section eine Auswahl (Radio):

- **„Jede Aktion einzeln genehmigen"** (Standard bei Sachbearbeiter;
  bei Buchhalter für Buchung/Storno/Löschung ohnehin erzwungen) –
  Text: „Das KI-System legt Anfragen an; Sie entscheiden im
  Genehmigungscenter."
- **„Dauerhaft vorab genehmigen"** (pre-approved Scope): direkte
  Ausführung für die erlaubten Aktionen – mit deutlichem Hinweis, dass
  jede Ausführung trotzdem protokolliert wird und die Genehmigungs-
  instanz dokumentiert bleibt (Approval-Konzept 3.4). Umschalten auf
  „vorab genehmigt" für Buchung/Löschung erfordert
  `IConfirmationService`-Bestätigung (high severity).

### 4.4 Filterverwaltung

- **Platz:** Section „Einschränkungen (Filter)" der Zugangskarte;
  sichtbar bei allen Stufen (Filter sind auf jedem Scope zulässig,
  Toolkatalog 5.2).
- **Darstellung:** je Datentyp, für den Filterdimensionen existieren,
  eine Zeile mit der Dimension und einer Mehrfachauswahl der
  konkreten Werte – `AccountType` (Giro/Spar), `ContactCategory`,
  `SecurityCategory`, `SavingsPlanCategory`, `AttachmentCategory`.
  Die Kategorie-Listen kommen per `IApiClient` aus den bestehenden
  Kategorie-Endpunkten (lesender Zugriff des Benutzers – keine
  neue Server-Semantik).
- **Semantik-Hinweise in der UI:**
  - „Kein Filter = Recht gilt für alle Datensätze" (Standardregel).
  - „Datensätze ohne Kategorie werden von einem gesetzten Filter
    **nicht** erfasst" (verbindliche Konvention,
    Berechtigungsmodell/Fachkonzept 8.3) – als Kurzinfo an jeder
    Kategorieauswahl.
  - „Mehrere Filter schränken sich gegenseitig ein" (UND über
    Dimensionen).
- **Indirekte Wirkung erklärt:** Bei Filter auf Kontotyp/Kategorien
  ein Hinweistext, dass die Einschränkung auch abhängige Daten
  betrifft (z. B. „Buchungen dieser Konten"), mit Beispiel-Tooltip.

### 4.5 Audit-Ansicht

**Benutzer-Sicht `/card/mcp-audit` (bzw. Tab „Verlauf" der
Zugangskarte mit Deep-Link `/card/mcp-audit?accessId={id}`) –
`McpAuditViewModel`**

Erfüllt die fachlichen Anforderungen aus Audit-Konzept 6.1:

- **Timeline-Liste** (`BaseListViewModel`-Muster mit Server-Paging
  `skip`/`take`): Ereigniszeitpunkt, Kategorie-Badge (Toolaufruf,
  Änderung, Zugangsereignis, Genehmigung, Sicherheit), kurze
  lokalisierte Ereigniszeile („Tool `fm_postings_query` aufgerufen –
  42 Buchungen gelesen"), Ergebnisstatus.
- **Filterleiste:** Zeitraum (Range-Filter), Zugang (Dropdown der
  eigenen Zugänge inkl. gelöschter per Name-Snapshot), Tool,
  Ereigniskategorie, Ergebnisstatus mit Schnellfilter „nur
  Verweigertes", Request-Zustand.
- **Drilling:** Expandieren eines Eintrags → Detailansicht mit
  Parametern (abgestufte Detailtiefe des Audit-Konzepts),
  Feld-Deltas (Vorher/Nachher als Tabelle), verlinkter
  `AiActionRequest` (öffnet Genehmigungscenter-Detail bzw. die
  Historien-Ansicht) und Ergebnis-Referenzen (Posting-Ids →
  Postings-Listenroute, `StatementImportId` → Import-Ansicht – die
  vorhandenen Navigations-URLs via `IListItemNavigation`-Logik).
- **Verständlichkeit:** Standardansicht vollständig in
  Klartextzeilen (de/en, resx); Rohdaten (Parameter-Snapshot,
  Hashes) hinter „Details anzeigen" für Experten – keine
  JSON-Pflichtlektüre.
- **Aktionen (Ribbon):** „Export (JSON)"/„Export (CSV)" des eigenen
  Protokolls; „Integrität prüfen" – startet die
  Hash-Ketten-Verifikation (Audit-Konzept 7.2) und zeigt Ergebnis
  („Kette intakt bis Eintrag n" bzw. Bruchstelle).
- **Leer- und Fehlerzustände:** freundliche Leeransicht („Noch keine
  Aktivität über diesen Zugang").

### 4.6 Genehmigungscenter

**`/card/mcp-approvals` – `McpApprovalCenterViewModel`** (Card mit
zwei Ribbon-Registern, siehe 3.2). Erfüllt Approval-Konzept 7:

- **Eingangskorb:** Liste offener Pending-Requests des Benutzers
  über alle seine Zugänge, sortiert nach `RequestedUtc`; je Zeile:
  Zugangsname, Tool (lokalisierte Prozessbezeichnung, nicht der
  `fm_*`-Name – der technische Name erscheint im Detail),
  kompakte Wirkungsangabe (z. B. „Verbuchung: 14 Buchungen, Konto
  'Giro', Summe −812,40 €" – aus der Wirkungsvorschau generiert),
  Restlaufzeit bis `ExpiresUtc` (Farbwarnung bei < 24 h).
- **Detailansicht (expandiert oder Overlay):** **Wirkungsvorschau**
  aus dem `ParameterSnapshot`, je Aktionstyp:
  - Verbuchung (`fm_statement_draft_book`): Buchungsplan – Entries
    mit Beträgen/Zuordnungen, gesperrte Entries mit Grund,
    `forceWarnings`-Hinweise;
  - Storno (`fm_posting_reverse`): betroffene Buchungsgruppe
    (Vorlage `GetRelatedPostingsAsync`-Semantik);
  - Löschen/Archivieren/Merge: betroffene Datensätze +
    Konsequenzhinweis („Merge ist nicht rückgängig zu machen",
    „Archivierung statt Löschen");
  - schreibvorbereitende Batches (Sachbearbeiter-Modus): Liste der
    Feld-Änderungen des Batch.
  Der `ReasonText` des Clients wird angezeigt, aber mit sichtbarer
  Kennzeichnung als „Nachricht des KI-Systems" (untrusted content –
  Approval-Konzept 7.2, Sicherheitskonzept 5.3).
- **Entscheidungsaktionen:** „Freigeben" / „Ablehnen" (mit optionalem
  Begründungsfeld – wird dem Client als Entscheidungsfeedback
  zurückgespielt); Freigabe finaler Wirkungen über
  `IConfirmationService`.
- **Mehrfachauswahl:** Checkboxen für gleichartige Requests;
  „Alle markierten freigeben" führt jeden Request einzeln über
  (jeder bleibt ein eigener Zustandsübergang – Approval-Konzept 7.4),
  mit Zusammenfassungs-Dialog („3 Anfragen freigeben?").
- **Historie:** umschaltbare Ansicht entschiedener Requests
  (Approved/Executed/Failed/Rejected/Expired) mit Ergebnis,
  `FailureReason`-Anzeige (lokalisiert, `Err_*`-Code → Text) und
  `ExecutionResult`-Referenzen; Einstiegspunkt in die Audit-Ansicht
  (per `CorrelationId`-Filter übergeben).
- **Benachrichtigungs-Einbindung:** neue Anfragen kommen als
  EventDriven-`Notification` (Target Dashboard/HomePage – kein
  Modal-Spam bei Agenten-Workloads, Approval-Konzept 7.6) und
  verlinken direkt ins Center; Ausführungsergebnisse ebenfalls
  (kurzer Erfolgs-/Fehlschlags-Text).

---

## 5. Benutzer- vs. Admin-Sichten

### 5.1 Benutzer-Sicht (Regelfall)

Alles oben Beschriebene ist besitzerbeschränkt: der Benutzer sieht
ausschließlich seine eigenen Zugänge, Requests und Audit-Einträge –
serverseitig durch `ownerUserId`-Bindung erzwungen (keine
UI-eigene Filterung als Sicherheitsgrenze).

### 5.2 Admin-Sicht

Der Admin erhält **keine** Quersicht auf fremde Zugänge/Finanzdaten
(Fachkonzept 5.4; Audit-Konzept 6.2), aber eine
**MCP-Sicherheitsübersicht**:

- **Instanzweite Sicherheitsereignisse:** Anomaliealarme,
  Schalter-Aktivierungen (`Mcp:Enabled`, `Mcp:WriteAccess:Enabled`
  inkl. aktuellem Zustand als Anzeige), IP-Sperrungen mit
  MCP-Bezug, Fehlversuchsserien, Rate-Limit-Aggregate – ohne
  fachliche Nutzlasten (Audit-Konzept 6.2: „nicht die fachlichen
  Finanzinhalte fremder Benutzer").
- **Einbindung:** Tab im bestehenden Admin-/Setup-Kontext
  (`McpSecurityOverviewViewModel`); `SystemAlert`-Benachrichtigungen
  der Anomalieerkennung verlinken dorthin.
- **Globale Schalter:** bleiben `appsettings`-Konfiguration
  (Betreiber-Ebene, Sicherheitskonzept 6); die Admin-Sicht zeigt
  ihren Zustand an und erklärt die Wirkung – eine UI-Umschaltung ist
  kein Konzeptbestandteil (Begründung: Notfallschalter sollen
  betriebsseitig greifen, nicht an der UI-Session hängen).

---

## 6. Fachliche Skizze der API-Erweiterungen

Neue Endpunkte folgen 1:1 den bestehenden API-Konventionen
(`[ApiController]`, `[Route("api/<bereich>")]`,
`[Authorize(JwtBearer)]`, `ICurrentUserService`-`ownerUserId`,
`ApiErrorDto`/`Err_*`, `skip`/`take` ≤ 200, 404-Semantik). DTOs in
`FinanceManager.Shared/Dtos/Mcp/`; `IApiClient`-Methoden im
Namensmuster `Mcp_*`/`Admin_*` in einer neuen Partial-Datei
`ApiClient.Mcp.cs`.

### 6.1 `McpAccessesController` (`/api/mcp-accesses`)

| Endpunkt | Zweck |
|---|---|
| `GET /api/mcp-accesses` | Liste eigener Zugänge (Suche, Paging) |
| `GET /api/mcp-accesses/{id}` | Detail: Zugang + effektives Scope-Profil + Filter + Nutzungskennzahlen |
| `POST /api/mcp-accesses` | Anlage (Name, Stufe, optionale Scopes/Filter, `ExpiresUtc`, IP-Bindung, Genehmigungsmodus) → Antwort enthält **einmalig** den Klartext-Token (`McpAccessCreatedDto` mit `PlainTextToken`) |
| `PUT /api/mcp-accesses/{id}` | Stammdaten, Stufe, Genehmigungsmodus, Ablaufdatum, IP-Bindung ändern (Änderung = Zugangsereignis im Audit) |
| `PUT /api/mcp-accesses/{id}/scopes` | Individuelles Scope-Profil setzen (nur Stufe „Individuell"; Server validiert gegen F3-Matrix + Deckel → `Err_Mcp_InvalidScope`) |
| `PUT /api/mcp-accesses/{id}/filters` | Filter je Scope/Dimension setzen (CSV-Wertelisten) |
| `POST /api/mcp-accesses/{id}/activate` / `…/deactivate` | Statuswechsel, sofort wirksam |
| `POST /api/mcp-accesses/{id}/rotate-token` | Rotation → einmaliger Klartext in der Antwort |
| `DELETE /api/mcp-accesses/{id}` | Widerruf + Konfiguration entfernen (Protokoll bleibt – Soft-Referenz) |
| `GET /api/mcp-accesses/levels` | Stufen-Definitionen inkl. Standardprofile für die UI-Darstellung (liest die serverseitige Stufenmatrix – keine UI-Duplikation der Rechtelogik) |
| `GET /api/mcp-accesses/options` | UI-Metadaten: Datentypen, Rechtearten, Filterdimensionen + konkrete Kategorielisten des Benutzers, Server-Betriebshinweise (TLS-Status für den Warnhinweis) |

### 6.2 `AiActionRequestsController` (`/api/ai-action-requests`)

| Endpunkt | Zweck |
|---|---|
| `GET /api/ai-action-requests?status=pending` | Eingangskorb (Paging, Filter Zugang/Tool/Zustand); `GET …/count` für das Nav-Badge |
| `GET /api/ai-action-requests/{id}` | Detail inkl. Wirkungsvorschau (aus `ParameterSnapshot` serverseitig aufbereitet – die Vorschau ist Serverlogik, nicht UI-Interpretation) |
| `POST /api/ai-action-requests/{id}/approve` | Freigabe → Zustandsübergang Approved → Queue |
| `POST /api/ai-action-requests/{id}/reject` | Ablehnung mit optionaler `DecisionNote` |
| `POST /api/ai-action-requests/batch` | Mehrfach-Entscheidung (Liste von Ids + Entscheidung; je Request eigener Übergang, Teilerfolg meldbar) |

### 6.3 `McpAuditController` (`/api/mcp-audit`)

| Endpunkt | Zweck |
|---|---|
| `GET /api/mcp-audit` | Audit-Einträge des Benutzers (Filter: Zeitraum, `mcpAccessId`, `toolName`, Kategorie, Status, `correlationId`, Paging) |
| `GET /api/mcp-audit/{id}` | Eintrag + Feld-Deltas |
| `GET /api/mcp-audit/export?format=json\|csv` | Export des eigenen Protokolls (selbst ein Audit-Ereignis) |
| `POST /api/mcp-audit/verify` | Integritätsprüfung der Hash-Kette (Ergebnis: intakt/Bruchstelle) |
| `GET /api/mcp-audit/security-overview` | **Admin-only** (`Roles="Admin"`): instanzweite Sicherheitsereignisse ohne Fachnutzlast |

### 6.4 `IApiClient`-Erweiterung (Shared)

Neue Partial `ApiClient.Mcp.cs` mit Methoden im Bestandsmuster
(`Mcp_ListAccessesAsync`, `Mcp_CreateAccessAsync`,
`Mcp_RotateTokenAsync`, `Mcp_SetScopesAsync`,
`Mcp_ListActionRequestsAsync`, `Mcp_ApproveRequestAsync`,
`Mcp_RejectRequestAsync`, `Mcp_ListAuditEntriesAsync`,
`Mcp_VerifyAuditChainAsync`, `Admin_McpSecurityOverviewAsync` …).
Fehler weiterhin als `ApiErrorDto` mit `Err_Mcp_*`-Codes; die UI
zeigt lokalisierte Texte (resx `Controller.*`/`Pages.*`-Muster).

---

## 7. Verständlichkeit und Barrierearmut der Konzepte

|| Konzept der Fachmodelle | Vermittlung in der UI |
|---|---|---|
| Freigabestufen | Auswahlkarten mit Klartext + Beispiel + aufklappbarer Effektiv-Matrix |
| Scope-Tupel (Datentyp × Rechteart × Filter) | Matrix-Editor (4.3.2) + Filter-Section (4.4); Begriff „Scope" erscheint nicht |
| Prüfstufen 0–3 / Deckel | Hinweistexte (4.3.2) + Fehlertexte des Servers mit Handlungsempfehlung statt Roh-`Err_*` |
| Genehmigungsmodus / `AiActionRequest` | „Genehmigung"-Begriff konsistent; Zustände als lokalisierte Badges („wartet", „freigegeben", „ausgeführt", „fehlgeschlagen", „abgelehnt", „abgelaufen") |
| Untrusted content / Prompt-Injection | Kennzeichnung des `ReasonText` als Client-Nachricht; Hinweis in der Anlage, dass Ausgaben des KI-Systems nicht als Anweisungen zu lesen sind (kurzer erklärender Satz) |
| Hash-Verkettung/Integrität | „Integrität prüfen" als einfache Aktion mit Klartext-Ergebnis |
| Token-Einwegigkeit | Einmalanzeige-Dialog mit Pflicht-Bestätigung |

---

## 8. Konsistenzanforderungen an Folgeartefakte und Umsetzung

- **Alle Konzeptbegriffe** stammen aus den Vorgängerdokumenten; die UI
  führt keine eigenen Rechte-/Zustandsmodelle ein.
- **Server ist die Wahrheit:** Scope-Validierung, Filterauswertung,
  Wirkungsvorschau und Deckel-Prüfung laufen serverseitig; die UI
  visualisiert und sammelt Eingaben (Prüfstufen-Monotonie des
  Berechtigungsmodells bleibt unangetastet).
- **Roadmap (Schritt 5):** UI-Bausteine sind phasenfähig – Phase 1
  braucht Zugangsverwaltung + Token + (lesende) Audit-Sicht nicht
  zwingend, aber Genehmigungscenter wird erst mit Phase 4 relevant;
  Filter-/Expertenmodus kann Phase 2+ folgen. Die Zuordnung erfolgt im
  Roadmap-Dokument.
- **Datenmodell (dieser Schritt):** liefert die Felder, die diese UI
  anzeigt (`McpAccess`, Scopes/Filter, `AiActionRequest`,
  Audit-Einträge) – Konsistenz der Feldnamen dort normativ.
- **Architekturkonzept (dieser Schritt):** liefert die API-Seite der
  skizzierten Endpunkte und die Trennung `/api` (Verwaltung) vs.
  `/mcp` (Client-Zugriff).
