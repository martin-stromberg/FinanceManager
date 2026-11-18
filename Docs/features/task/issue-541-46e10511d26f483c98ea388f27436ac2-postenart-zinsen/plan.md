# Umsetzungsplan: Postenart Zinsen (Issue #541)

## Übersicht

Bank-Postings (`Posting` mit `PostingKind.Bank`) und Kontoauszug-Entwurfszeilen (`StatementDraftEntry`) erhalten eine persistierte Postenart (Eingang/Ausgang/Zins/Steuer). Sie wird bei der Klassifizierung automatisch ermittelt — zuerst über am `Account` hinterlegte Kontierungsregeln (Textfeld enthält Text), dann per Vorzeichen-Fallback — ist in der Kontoauszugzeile vor dem Buchen manuell korrigierbar (Überschreibschutz per `IsPostingTypeManual`-Flag) und wird beim Buchen auf den Bankposten übertragen. Zusätzlich gibt es am Bankkonto eine Regelverwaltung sowie eine Ribbon-Aktion, die alle gebuchten Bankposten des Kontos in einem neuen Hintergrundtask (`IBackgroundTaskExecutor`) neu bewertet. Demodaten erhalten Regeln für „Zinsen" und „Kapitalertragssteuer".

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Postenart eines einzelnen Bankpostens: `StatementDraftEntry` (vor dem Buchen) bzw. `Posting` mit `PostingKind.Bank` (nach dem Buchen). Die Hintergrund-Neubewertung verarbeitet je ausgelöstem Lauf alle `PostingKind.Bank`-Postings genau eines `Account`. Die Regelverwaltung betrifft die Collection `AccountPostingRule` eines Kontos. |
| Auslösende Ereignisse | (a) Klassifizierung einer Draft-Zeile in `ClassifyInternalAsync` (Import, `ClassifyAsync`, `SetAccountAsync`); (b) manuelle Auswahl der Postenart in der Kontoauszugzeile (Entry-Karte via `SaveEntryAllAsync`); (c) Buchen in `BookCoreAsync`/`BookSplitDraftGroupAsync` (Übertrag auf den `Posting`); (d) Ribbon-Aktion „Postenarten neu bewerten" am Bankkonto → `IBackgroundTaskManager.Enqueue(BackgroundTaskType.ReevaluatePostingTypes, …)`; (e) Anlegen/Bearbeiten/Entfernen von `AccountPostingRule` am Bankkonto. |
| Erwartete Wirkung | `StatementDraftEntry.PostingType` zeigt die ermittelte/gesetzte Postenart in der Kontoauszugzeile; `Posting.PostingType` des Bankpostens trägt sie nach dem Buchen; die Postenliste (`AccountPostingsListViewModel`) zeigt eine Postenart-Spalte; nach der Neubewertung tragen alle Bankposten des Kontos die aktuell gültige Art; Fortschritt/Ergebnis sichtbar über `BackgroundTaskStatusPanel`. |
| Umgang mit fehlenden Objekten | Konto ohne Regeln → Regelschritt entfällt, Vorzeichen-Fallback greift (`Amount < 0` → Ausgang, sonst Eingang; 0 = Eingang). `draft.DetectedAccountId == null` oder Konto gelöscht → nur Vorzeichen-Fallback. Mehrere zutreffende Regeln → die Regel mit dem kleinsten `OrderIndex` gewinnt (Tiebreak `Id`). Altbestand ohne Feld → SQL-Backfill in der generierten Migration (negativ → Ausgang, sonst Eingang); alte Backups ohne die neuen Felder/Sektion → DTO-Defaults, `AssignBackupDto` leitet fehlende `PostingType`-Werte aus dem Betragsvorzeichen ab; fehlende `AccountPostingRules`-Sektion = leere Liste. `StatementDraftEntry.PostingType == Unspecified` beim Buchen → Vorzeichen-Fallback auf den Buchungsbetrag. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Keine Auswertung der Postenart in Berichten, Budgetplanung, KPIs oder `PostingAggregate` | Anforderung verlangt nur Erfassung, Speicherung, Anzeige und Neubewertung | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 2 | Keine Einzelbearbeitung der Postenart an bereits gebuchten Postings (Posten-Ansicht bleibt read-only) | Änderung gebuchter Posten erfolgt ausschließlich über die Neubewertungsfunktion am Konto | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 3 | Keine Kontierungsregeln für Kontakte, Sparpläne oder Wertpapiere; Gegenpostings anderer `PostingKind`s bleiben ohne Postenart-Behandlung | Anforderung adressiert nur Bankkonten/Bankposten | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 4 | Nur die Textfelder `Subject` (Verwendungszweck) und `BookingDescription` (Buchungsbeschreibung) als Regel-Felder; `RecipientName` wird nicht angeboten; keine komplexen Bedingungen (Regex, Betragsgrenzen, Datumsbereiche, Kombinationen) | Geklärte Einschränkung; eine Regel ist genau „ein Textfeld enthält einen Text" | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 5 | Keine Änderung von Betrag, Kontaktzuordnung, `IsCostNeutral`, Sparplan-/Wertpapierzuordnung oder Buchungslogik — die Regel bestimmt ausschließlich die Postenart | Anforderungsausschluss | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 6 | Keine automatische Aufteilung/Verrechnung von Steuerbeträgen (keine separaten Steuerposten wie bei `SecurityPostingSubType.Tax`) | Anforderungsausschluss | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 7 | Offene (ungebuchte) Draft-Entries werden bei Regeländerung am Konto nicht automatisch neu bewertet; die neue Regel greift erst bei der nächsten regulären Klassifizierung | Geklärte Annahme; vermeidet unerwartete Änderungen an manuell gepflegten Entwürfen | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 8 | Neubewertung umfasst ohne Sonderbehandlung alle `PostingKind.Bank`-Postings des Kontos — auch stornierte (`ReversedByPostingId`), Reversal- und vorläufige (`IsPreliminary`) Postings | Geklärte Annahme; die Postenart ist ein beschreibendes Attribut und hat keine Auswirkung auf Storno-/Buchungslogik | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |
| 9 | Keine Inline-Bearbeitung der Postenart in der Schnellbearbeitungstabelle (`QuickEditTable`); die manuelle Korrektur erfolgt über die Entry-Karte | `QuickEditTable` besitzt kein Enum-/Select-Eingabeelement (nur fix verdrahtete Text-/Datums-/Betragsfelder); die Entry-Karte ist die vorgesehene Korrekturstelle | bestätigt (aus Anforderungstext abgeleitet, 2026-11-18) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `BankPostingType`-Enum (Ablage `FinanceManager.Shared/Dtos/Postings/`) | Nicht-nullable Enum mit Mitgliedern `Unspecified = 0`, `Inflow = 1`, `Outflow = 2`, `Interest = 3`, `Tax = 4` | Geklärt: nicht-nullable Spalte. `Unspecified` ist der neutrale Default für Nicht-Bank-Postings (deren Feld fachlich irrelevant ist) und noch nicht klassifizierte Draft-Entries; die Auswertung liefert ausschließlich `Inflow`/`Outflow`/`Interest`/`Tax`. Default `0` ist damit nie ein falsch positiver „Eingang". |
| Zentrale Bewertungslogik | Neue statische Klasse `PostingTypeEvaluator` in `FinanceManager.Domain/Postings/` mit `Evaluate(IReadOnlyCollection<AccountPostingRule> rules, string? subject, string? bookingDescription, decimal amount) → BankPostingType`; `NormalizeUmlauts` wird aus `StatementDraftService.Classification.cs` dorthin als einzige Implementierung verlagert, die bisherigen privaten Aufrufe delegieren | Gemeinsame Nutzung durch `StatementDraftService` (Infrastructure) und `PostingTypeReevaluationTaskExecutor` (Web) ohne DI-Verkabelung; „dieselbe Normalisierung" wie die bestehende Text-Klassifizierung (Umlaute → ASCII, `ToLowerInvariant`, Whitespace-Entfernung wie `TryAutoAssignSavingsPlan`) bleibt an genau einer Stelle. |
| Überschreibschutz | Bool-Flag `IsPostingTypeManual` am `StatementDraftEntry`; automatische Ermittlung setzt `PostingType` nur bei `!IsPostingTypeManual`; manuelle Pfade (SaveEntryAll) setzen das Flag | Geklärt; explizites Flag ist robuster als Wertvergleiche und macht den Zustand in Backup/DTO nachvollziehbar. |
| Regel-CRUD | Erweiterung von `IAccountService`/`AccountService` um `GetPostingRulesAsync`/`UpsertPostingRuleAsync`/`RemovePostingRuleAsync` und `AccountsController` um `{id}/posting-rules`-Endpunkte | Direktes Muster `AccountLinkedIban` (`AddLinkedIbanAsync`-Trio, `{id}/linked-ibans`-Endpunkte); Besitzerprüfung über `Account.OwnerUserId` (Regel-Entität trägt wie `AccountLinkedIban` kein eigenes `OwnerUserId` — Besitzerpfad gemäß AGENT.md). |
| Reihenfolge der Regeln | Explizites `OrderIndex`-Feld (int) an `AccountPostingRule`; Auswertung sortiert nach `OrderIndex`, `Id`; Index nur auf `AccountId`, kein Unique-Index auf `OrderIndex` | Geklärt: niedrigste Reihenfolge gewinnt. Ohne Unique-Constraint auf `OrderIndex` bleiben Umsortierungen (Tausch zweier Regeln) konfliktfrei; die Auswertung verträgt Duplikate per `Id`-Tiebreak. |
| Neubewertungs-Auslöser | Dedizierter Endpunkt `POST /api/accounts/{id}/posting-types/reclassify` im `AccountsController`, der `IBackgroundTaskManager.Enqueue(BackgroundTaskType.ReevaluatePostingTypes, userId, jsonPayload{accountId}, allowDuplicate: false)` aufruft | Der generische `POST /api/background-tasks/{type}` trägt kein Payload — die `AccountId` muss aber übergeben werden (Payload-Parsing-Muster `BookingTaskExecutor.ParseOptions`). `allowDuplicate: false` liefert Idempotenz. |
| Regelverwaltungs-UI | Neue Komponente `PostingRulesPanel.razor` (Muster `LinkedIbansPanel`) in `CardPage.razor` für `BankAccountCardViewModel` mit `Account?.Id != Guid.Empty` — sichtbar für **alle** Bankkonten | `LinkedIbansPanel` ist an `EffectiveIsCollectionAccount` gekoppelt; Regeln gelten für jedes Bankkonto, daher eigene Komponente mit eigener Sichtbarkeitsbedingung. Feld-Auswahl erfolgt per Klartext-Dropdown (`PostingRuleField`), nicht per Freitext-Kennung. |
| Postenart-Eingabe in der Kontoauszugzeile | `CardField` mit `lookupType: "Enum:BankPostingType"` im `StatementDraftEntryCardViewModel`, speichert über `SaveEntryAllAsync` (neuer Request-Parameter) | Bestehendes Enum-Lookup-Muster (`Enum:SecurityTransactionType`, `Enum:AccountType`); `SaveEntryAllAsync` ist der atomare Mehrfeld-Speicherpfad der Karte — kein neuer Endpunkt nötig. |
| Ausschluss von `Unspecified` aus Enum-Auswahlfeldern | Generischer `Exclude`-Filter in `BaseViewModel`: `QueryLookupAsync` (BaseViewModel.cs:410, Enum-Zweig :412–418) reicht `field.LookupFilter` an die private Methode `QueryEnumLookup` durch — deren Signatur `(string? q, Type enumType)` wird um `string? lookupFilter` erweitert (sie hat keinen `CardField`-Parameter). Dort wird `Exclude=<MemberName>[;<MemberName>]` ausgewertet und die genannten rohen Enum-Member **vor** der Lokalisierung aus der Liste gefiltert (Muster der vorhandenen `LookupFilter`-Schlüssel `Type=`, `OnlyActive=`, `BankContactId=`). Das neue `CardField` erhält `lookupFilter: "Exclude=Unspecified"`; die Postenart-`<select>` im `PostingRulesPanel` befüllt ihre Optionen explizit ohne `Unspecified` | `QueryEnumLookup` (BaseViewModel.cs:561) listet alle Enum-Member ohne Filter — ohne Mechanismus würde `Unspecified` in Entry-Karte und Regel-Postenart wählbar. Ein `QueryLookupAsync`-Override im ViewModel müsste auf lokalisierte Anzeigenamen filtern (fragil); der `Exclude`-Filter auf Membernamen ist robust und für beide Stellen (CardField + Panel) nutzbar. Serverseitige `Unspecified`-Ablehnung bleibt zusätzlich bestehen. |
| Enum-Options-Prefetch in `GenericCardPage.razor` | Der `_enumOptions`-Cache wird pro Kombination `LookupType` + `LookupFilter` aufgebaut (Hilfsmethode `EnumOptionsKey(CardField)` → `"{LookupType}\|{LookupFilter}"`), und `OnParametersSetAsync` übergibt den `LookupFilter` des realen Feldes im synthetischen `CardField` des Prefetch-Aufrufs; der Cache-Zugriff im Renderpfad (:200) nutzt denselben zusammengesetzten Schlüssel | Das Enum-`<select>` der Entry-Karte rendert **ausschließlich** aus dem `_enumOptions`-Prefetch-Cache, der heute pro `LookupType` mit einem `CardField` **ohne** `LookupFilter` befüllt wird (`GenericCardPage.razor:388–399`) — ein `Exclude`-Filter am Feld würde `QueryEnumLookup` sonst nie erreichen. Filterung erst beim Rendern der `<option>`s wurde verworfen: Der Cache enthält bereits lokalisierte Anzeigenamen, ein Abgleich auf rohe Membernamen müsste Lokalisierungs- und Parsing-Logik in der Razor-Datei duplizieren (fragil). |
| Re-Evaluierung nach Kernfeldänderung | `UpdateEntryCoreAsync` und `ApplyBatchEntryUpdatesAsync` bewerten `PostingType` für Entries mit `!IsPostingTypeManual` neu (Regeln + Vorzeichen), weil sich Betrag/Subject/Beschreibung geändert haben können | Annahme: ein automatisch ermittelter Wert soll automatisch korrekt bleiben; manuell gesetzte Werte bleiben geschützt. |
| Gegenpostings | Nur der `PostingKind.Bank`-Posten erhält `PostingType`; `PostingKind.Contact`/`Security`/`SavingsPlan`-Gegenpostings behalten `Unspecified` | Geklärte Festlegung. |
| Ribbon-Primäraktion (`MobileShortcut`) | Die neue Aktion `ReevaluatePostingTypes` erhält **kein** `MobileShortcut` — in der Gruppe `Ribbon_Group_Bookings` bleibt `CreatePreliminaryDraft` die primäre Aktion mit `MobileShortcut = true` (BankAccountCardViewModel.cs:287–299) | `MobileShortcut` markiert pro Gruppe die primäre Aktion; die selten genutzte Neubewertung verdrängt den alltäglicheren Erfassungs-Workflow nicht aus dem mobilen Schnellzugriff. |
| `VisibleBackgroundTaskTypes` am `BankAccountCardViewModel` | Beabsichtigte Einschränkung auf `{ ReevaluatePostingTypes }` | Das `BackgroundTaskStatusPanel` auf der Konto-Karte dient ausschließlich der Sichtbarmachung der Neubewertung; andere Task-Typen (Klassifizieren/Buchen auf der Draft-Liste, `RebuildAggregates`/`BackupRestore` im Setup) haben eigene Seiten mit eigenen Panel-Filtern (`StatementDraftsListViewModel`, `SetupCardViewModel`). Default `null` (= alle anzeigen) würde fremde Tasks auf der Konto-Seite einblenden. |
| Neu angelegte Draft-Entries (`Creates`/`AddEntryAsync`) | Werden **sofort** evaluiert — `ApplyBatchEntryUpdatesAsync` ruft pro `Creates`-Eintrag nach `draft.AddEntry` bereits `ClassifyInternalAsync` auf (BatchUpdate.cs:474–492), `AddEntryAsync` ebenso (StatementDraftService.cs:1409–1412); die Evaluierung greift dort automatisch mit | Konsistente Erwartung: Es gibt keinen Pfad, auf dem ein neuer Entry dauerhaft `Unspecified` bleibt — der Evaluierungs-Punkt ist ausschließlich die Klassifizierung; Batch-/Einzel-Anlage nutzen ihn bereits. |

## Programmabläufe

### Kontierungsregeln am Bankkonto verwalten

1. Anwender öffnet die Konto-Detailseite (`/card/accounts/{id}`); `CardPage.razor` rendert `PostingRulesPanel` für `BankAccountCardViewModel` mit geladenem Konto.
2. `BankAccountCardViewModel.LoadAsync` lädt Regeln über `IApiClient.GetPostingRulesAsync(id)` → `GET /api/accounts/{id}/posting-rules` → `AccountService.GetPostingRulesAsync` (Besitzerprüfung `Account.OwnerUserId`, `null` bei fremdem/fehlendem Konto).
3. Anlegen: Anwender wählt Feld (`PostingRuleField`-Dropdown: Verwendungszweck/Buchungsbeschreibung), gibt Suchtext, Postenart (`BankPostingType`-Dropdown ohne `Unspecified`) und Reihenfolge ein → `UpsertPostingRuleAsync` → `POST {id}/posting-rules` (`AccountPostingRuleUpsertRequest` mit `Id = null`) → Service erzeugt `AccountPostingRule`.
4. Bearbeiten: gleiche Route mit `Id != null` aktualisiert `Field`, `Pattern`, `PostingType`, `OrderIndex` der bestehenden Regel.
5. Entfernen: `DELETE {id}/posting-rules/{ruleId}` → `RemovePostingRuleAsync` (Besitzerprüfung über Konto).
6. Offene Draft-Entries werden hierbei **nicht** neu bewertet (Scope-Ausschluss 7); die Regel greift bei der nächsten Klassifizierung.

Beteiligte Klassen/Komponenten: `PostingRulesPanel`, `BankAccountCardViewModel`, `ApiClient`, `AccountsController`, `IAccountService`/`AccountService`, `AccountPostingRule`, `AppDbContext`.

### Automatische Postenart-Ermittlung bei der Klassifizierung

1. `ClassifyInternalAsync` lädt nach Ermittlung von `draft.DetectedAccountId` (bestehender `bankAccount`-Ladevorgang, Classification.cs:185) zusätzlich `AccountPostingRule`s des Kontos (`Where(r => r.AccountId == draft.DetectedAccountId)`, `AsNoTracking`, sortiert `OrderIndex`); ohne Konto bleibt die Liste leer.
2. Pro Entry — unmittelbar nach dem `ResetOpen()`/`AlreadyBooked`-Block (Classification.cs:196–210), also für alle nicht-`AlreadyBooked` Entries inkl. `IsAnnounced`: wenn `!entry.IsPostingTypeManual`, dann `entry.SetPostingType(PostingTypeEvaluator.Evaluate(rules, entry.Subject, entry.BookingDescription, entry.Amount), manual: false)`.
3. `Evaluate`: Regeln in `OrderIndex`-Reihenfolge; pro Regel wird das zugeordnete Textfeld (`Subject` oder `BookingDescription`) normalisiert (`NormalizeUmlauts` → `ToLowerInvariant` → Whitespace-Strip); `Pattern` wird identisch normalisiert; Teilstring-Enthält-Match; erster Treffer liefert dessen `PostingType`. Kein Treffer → `amount < 0 ? Outflow : Inflow` (0 = Inflow).
4. Persistierung erfolgt mit dem bestehenden `SaveChangesAsync` der Klassifizierung; `Map(StatementDraftEntry)` liefert `PostingType`/`IsPostingTypeManual` im `StatementDraftEntryDto`.

Beteiligte Klassen/Komponenten: `StatementDraftService` (`ClassifyInternalAsync`), `PostingTypeEvaluator`, `AccountPostingRule`, `StatementDraftEntry`.

### Manuelle Korrektur der Postenart (Entry-Karte)

1. `StatementDraftEntryCardViewModel.BuildFields` enthält ein `CardField` „Postenart" (`lookupType: "Enum:BankPostingType"`, `lookupFilter: "Exclude=Unspecified"`, lokalisierte Anzeige über `EnumType_BankPostingType_*`), editierbar solange `!isLocked` (nicht `IsAnnounced`, nicht `AlreadyBooked`) — unabhängig vom `_isEditMode`-Kernfeld-Flag, analog dem Kontakt-Feld. Das `<select>` der Entry-Karte bezieht seine Optionen aus dem `_enumOptions`-Prefetch-Cache von `GenericCardPage`; dieser wird pro `LookupType`+`LookupFilter` aufgebaut und ruft `QueryLookupAsync` mit dem `LookupFilter` tragenden Feld auf — `QueryEnumLookup` filtert `Unspecified` damit aus der Auswahlliste (siehe Designentscheidungen).
2. Speichern läuft über `StatementDrafts_SaveEntryAllAsync` → `POST {draftId}/entries/{entryId}/save-all` mit `StatementDraftSaveEntryAllRequest.PostingType`.
3. `SaveEntryAllAsync` setzt bei übergebenem Wert `entry.SetPostingType(value, manual: true)` (`Unspecified` wird vom Client nicht angeboten; serverseitig wird ein `Unspecified`-/undefinierter Wert als ungültig abgewiesen) und gibt das `StatementDraftEntryDto` inkl. `PostingType`/`IsPostingTypeManual` zurück — die Rückgabe-Projektion wird mit erweitert (StatementDraftService.cs:335), damit die Entry-Karte nach dem Speichern den gesetzten Wert anzeigt.
4. Spätere Klassifizierungsläufe (`ClassifyAsync`, `SetAccountAsync`, Re-Import) überschreiben den manuell gesetzten Wert nicht (Guard `!IsPostingTypeManual`).
5. Zusätzlich zeigt `StatementDraftEntriesListViewModel` eine anzeigende Postenart-Spalte in der Eintragsliste (lokalisiert, `Unspecified` → leere Zelle).

Beteiligte Klassen/Komponenten: `StatementDraftEntryCardViewModel`, `StatementDraftSaveEntryAllRequest`, `StatementDraftsController`, `IStatementDraftService`/`StatementDraftService`, `StatementDraftEntriesListViewModel`, `StatementDraftEntryItem`, `GenericCardPage` (Enum-Options-Prefetch), `BaseViewModel` (`QueryLookupAsync`/`QueryEnumLookup`).

### Buchung mit Postenart-Übertrag

1. `BookCoreAsync` → `CreateBankAndContactPostingAsync` (StatementDraftService.cs:2435): der erzeugte `PostingKind.Bank`-Posting erhält `SetPostingType(e.PostingType == Unspecified ? (amount < 0 ? Outflow : Inflow) : e.PostingType)`; der `PostingKind.Contact`-Gegenposten bleibt unverändert (`Unspecified`).
2. `BookSplitDraftGroupAsync` (bzw. dessen Posting-Erzeugung für Split-Kind-Entries) überträgt analog.
3. `PostingsQueryService` projiziert `p.PostingType` an allen vier `PostingServiceDto`-Erzeugungsstellen (PostingsQueryService.cs:170, :278, :381, :461); die Konto-Postenliste zeigt die Spalte.
4. Auch der Einzelposten-Endpunkt `GET /api/postings/{id}` projiziert `p.PostingType` in sein `PostingServiceDto` (PostingsController.cs:127) — sonst läge an der Detail-API stets `Unspecified`.

Beteiligte Klassen/Komponenten: `StatementDraftService` (`BookCoreAsync`, `BookSplitDraftGroupAsync`), `Posting.SetPostingType`, `PostingsQueryService`, `PostingsController`, `BasePostingsListViewModel`.

### Hintergrund-Neubewertung gebuchter Bankposten

1. Ribbon-Aktion `ReevaluatePostingTypes` im `BankAccountCardViewModel` (Gruppe `Ribbon_Group_Bookings`, nach `CreatePreliminaryDraft`) → `IApiClient.Accounts_EnqueuePostingTypeReevaluationAsync` → `POST /api/accounts/{id}/posting-types/reclassify`.
2. `AccountsController` injiziert dafür neu `IBackgroundTaskManager` (Konstruktor-Erweiterung — bisher nicht injiziert); der Endpunkt prüft Besitzer (Konto + `_current.UserId`, sonst `Err_NotFound`), dann `Enqueue(BackgroundTaskType.ReevaluatePostingTypes, userId, "{\"accountId\":\"…\"}", allowDuplicate: false)` → `BackgroundTaskInfo` zurück.
3. `BackgroundTaskRunner` dispatcht auf `PostingTypeReevaluationTaskExecutor` (`Type => ReevaluatePostingTypes`, registriert in `ProgramExtensions.cs`).
4. Executor: `ParseOptions`-Muster liest `accountId` aus dem JSON-Payload; `IServiceScopeFactory`-Scope → `AppDbContext`; Konto + Owner-Prüfung (Konto des `context.UserId`, sonst Abbruch mit Fehlermeldung); Regeln laden (sortiert `OrderIndex`).
5. Iteration über `_db.Postings.Where(p => p.Kind == PostingKind.Bank && p.AccountId == accountId)` seitenweise (z. B. 500er-Batches): pro Posten `posting.SetPostingType(PostingTypeEvaluator.Evaluate(rules, p.Subject, p.Description, p.Amount))`, batchweises `SaveChangesAsync`, `context.ReportProgress(processed, total, …)`; Cancellation über `ct.ThrowIfCancellationRequested()`.
6. Fortschritt/Status sichtbar über `BackgroundTaskStatusPanel` — `BankAccountCardViewModel.VisibleBackgroundTaskTypes` wird um `ReevaluatePostingTypes` ergänzt.
7. Keine Sonderbehandlung für stornierte/Reversal-/vorläufige Postings (Scope-Ausschluss 8).

Beteiligte Klassen/Komponenten: `BankAccountCardViewModel`, `ApiClient`, `AccountsController`, `IBackgroundTaskManager`, `BackgroundTaskRunner`, `PostingTypeReevaluationTaskExecutor`, `AppDbContext`, `BackgroundTaskStatusPanel`.

### Backup / Restore

1. Export: `BackupService.BuildBackupDataAsync` lädt `AccountPostingRule`s über `importAccountIds.Contains(r.AccountId)` (Muster `AccountLinkedIbans`, BackupService.cs:766–769) → `ToBackupDto()` → Sektion `AccountPostingRules`; **nicht** in `RequiredDataProperties`.
2. `Posting.PostingType` sowie `StatementDraftEntry.PostingType`/`IsPostingTypeManual` reisen in den bestehenden `PostingBackupDto`/`StatementDraftEntryBackupDto` mit optionalen Default-Endparametern.
3. Import: `SetupImportService` liest `AccountPostingRules` mit `TryGetProperty`-Guard (fehlende Sektion = leere Liste); `AccountId` wird auf die neue Id gemappt (`accountMap`), Einträge ohne Mapping oder doppelte `(AccountId, Field, Pattern)` werden übersprungen (Muster :471–500); `entity.AssignBackupDto(dto with { AccountId = mappedAccountId })`.
4. `AssignBackupDto` von `Posting`/`StatementDraftEntry`: `PostingType == null` (alte Backups) → vorzeichenbasiert ableiten (`Amount < 0` → `Outflow`, sonst `Inflow`); `IsPostingTypeManual` fehlend → `false`.

Beteiligte Klassen/Komponenten: `BackupService`, `SetupImportService`, `AccountPostingRule(AccountPostingRuleBackupDto)`, `Posting(PostingBackupDto)`, `StatementDraftEntry(StatementDraftEntryBackupDto)`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `BankPostingType` (`FinanceManager.Shared/Dtos/Postings/BankPostingType.cs`) | Enum | Postenart: `Unspecified = 0`, `Inflow`, `Outflow`, `Interest`, `Tax` |
| `PostingRuleField` (`FinanceManager.Shared/Dtos/Accounts/PostingRuleField.cs`) | Enum | Regel-Textfeld: `Subject = 0`, `BookingDescription = 1` |
| `AccountPostingRule` (`FinanceManager.Domain/Accounts/AccountPostingRule.cs`) | Datenmodellklasse | Kontierungsregel am Konto: `AccountId`, `Field`, `Pattern`, `PostingType`, `OrderIndex`, `Account`-Navigation, `Update(…)`, `AccountPostingRuleBackupDto` + `ToBackupDto`/`AssignBackupDto` (Muster `AccountLinkedIban`) |
| `PostingTypeEvaluator` (`FinanceManager.Domain/Postings/PostingTypeEvaluator.cs`) | statische Klasse | Zentrale Ermittlung der Postenart (Regeln sortiert → Teilstring-Match → Vorzeichen-Fallback) sowie alleinige `NormalizeUmlauts`-Implementierung |
| `AccountPostingRuleDto` (`FinanceManager.Shared/Dtos/Accounts/`) | Record-DTO | Regel-Transportobjekt für API/UI (`Id`, `Field`, `Pattern`, `PostingType`, `OrderIndex`) |
| `AccountPostingRuleUpsertRequest` (`FinanceManager.Shared/Dtos/Accounts/`) | Record-Request | Upsert-Payload (`Id?`, `Field`, `Pattern` `[Required, MaxLength]`, `PostingType`, `OrderIndex`) |
| `PostingTypeReevaluationTaskExecutor` (`FinanceManager.Web/Services/`) | `IBackgroundTaskExecutor` | Batch-Neubewertung aller `PostingKind.Bank`-Postings eines Kontos; `Type => BackgroundTaskType.ReevaluatePostingTypes` |
| `PostingRulesPanel` (`FinanceManager.Web/Components/Shared/PostingRulesPanel.razor`) | Razor-Komponente | Regelverwaltung in der Konto-Karte (Muster `LinkedIbansPanel`, Parameter `Vm`, `Localizer`); Postenart-`<select>` wird explizit nur mit `Inflow`/`Outflow`/`Interest`/`Tax` befüllt (`Unspecified` nicht angeboten); clientseitige Vorprüfung leeres `Pattern` (Pflichtfeld-Hinweis im Panel) + Anzeige serverseitiger Validierungsfehler über `SetError`-Muster |

## Änderungen an bestehenden Klassen

### `Posting` (Datenmodellklasse)

- **Neue Eigenschaften:** `PostingType` (`BankPostingType`, nicht-nullable, Default `Unspecified`) — Postenart des Bankpostens.
- **Neue Methoden:** `SetPostingType(BankPostingType type)` — Fluent-Setter (Muster `SetIsPreliminary`), `Touch()`.
- **Geänderte Methoden:** `PostingBackupDto` um optionalen Endparameter `BankPostingType? PostingType = null`; `ToBackupDto()` übergibt `PostingType`; `AssignBackupDto()` setzt `PostingType = dto.PostingType ?? (Amount < 0 ? Outflow : Inflow)` (Alt-Backup-Kompatibilität).

### `StatementDraftEntry` (Datenmodellklasse)

- **Neue Eigenschaften:** `PostingType` (`BankPostingType`, Default `Unspecified`); `IsPostingTypeManual` (`bool`, Default `false`).
- **Neue Methoden:** `SetPostingType(BankPostingType type, bool manual)` — setzt Postenart und bei `manual: true` das Flag; `Touch()`.
- **Geänderte Methoden:** `StatementDraftEntryBackupDto` um optionale Endparameter `BankPostingType? PostingType = null, bool IsPostingTypeManual = false` (bisheriger Record ohne Defaults — Defaults anhängen); `ToBackupDto()`/`AssignBackupDto()` entsprechend; `AssignBackupDto` leitet `PostingType == null` aus dem Betragsvorzeichen ab.

### `AppDbContext` (EF-Core-Kontext)

- **Neue Eigenschaften:** `DbSet<AccountPostingRule> AccountPostingRules`.
- **Geänderte Methoden:** `OnModelCreating` — Konfiguration `AccountPostingRules` (Muster `AccountLinkedIban`: `ToTable`, `Pattern` MaxLength 200 Required, `Field`/`PostingType` als int, Index `AccountId`, `HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(Cascade)`); `Posting.PostingType`/`StatementDraftEntry.PostingType` (int, Default 0), `StatementDraftEntry.IsPostingTypeManual` (Default `false`).

### `StatementDraftService` (partial, Service)

- **Geänderte Methoden:**
  - `ClassifyInternalAsync` — lädt Regeln des `DetectedAccountId`-Kontos; Entry-Schleife bewertet Postenart bei `!IsPostingTypeManual`.
  - `NormalizeUmlauts` (private static) — delegiert an `PostingTypeEvaluator.NormalizeUmlauts` (Verlagerung der einzigen Implementierung); die 6 bestehenden Aufrufstellen bleiben funktional identisch.
  - `SaveEntryAllAsync` — neuer Parameter `BankPostingType? postingType` (vor `ct`); bei Wert `entry.SetPostingType(value, manual: true)`; `Unspecified` als ungültig ablehnen.
  - `UpdateEntryCoreAsync` — nach `entry.UpdateCore` Re-Evaluierung bei `!IsPostingTypeManual` (Regeln des `DetectedAccountId`-Kontos + Vorzeichen).
  - `ApplyBatchEntryUpdatesAsync` — nach Anwendung der Feld-Updates Re-Evaluierung geänderter Entries bei `!IsPostingTypeManual`.
  - `BookCoreAsync`/`CreateBankAndContactPostingAsync` — `SetPostingType` auf den Bankposten (Fallback Vorzeichen bei `Unspecified`); `BookSplitDraftGroupAsync` analog.
  - **Alle sieben `StatementDraftEntryDto`-Konstruktionsstellen** übergeben `e.PostingType`/`e.IsPostingTypeManual` (die neuen DTO-Parameter sind optionale Endparameter — ohne Übergabe kompiliert der Code, die Werte gehen aber still verloren):
    1. `Map(StatementDraftEntry)` — `StatementDraftService.Mapping.cs:30`
    2. Inline-Projektion in `Map(draft, splitRefLookup, account)` — `StatementDraftService.Mapping.cs:77`
    3. Rückgabe von `SaveEntryAllAsync` — `StatementDraftService.cs:335` (direkt im manuellen Korrekturpfad — die Entry-Karte zeigt sonst nach dem Speichern `Unspecified`/`IsPostingTypeManual = false`)
    4. `GetOpenDraftsAsync`-Projektion — `StatementDraftService.cs:1213`
    5. Inline-Rückgabe in `UpdateEntryCoreAsync` — `StatementDraftService.cs:1740`
    6. `ResetDuplicateEntryAsync` (erste Rückgabe) — `StatementDraftService.cs:3027`
    7. `ResetDuplicateEntryAsync` (zweite Rückgabe) — `StatementDraftService.cs:3056`

### `IStatementDraftService` (Interface)

- **Geänderte Methoden:** `SaveEntryAllAsync` — zusätzlicher Parameter `BankPostingType? postingType` (Auswirkung: Aufrufer `StatementDraftsController`, `FmStatementDraftCompleteTool`, Moq-Setups in `McpStatementWriteToolsTests`).

### `IAccountService` / `AccountService` (Service)

- **Neue Methoden:** `GetPostingRulesAsync(accountId, ownerUserId, ct) → IReadOnlyList<AccountPostingRuleDto>?` (`null` bei fremdem/fehlendem Konto, sortiert `OrderIndex`); `UpsertPostingRuleAsync(accountId, ownerUserId, AccountPostingRuleUpsertRequest, ct)` (Besitzerprüfung → `ArgumentException`, `Id == null` → Insert mit nächstem `OrderIndex` falls nicht gesetzt, `Id != null` → Update nach Regel-Zugehörigkeit); `RemovePostingRuleAsync(accountId, ownerUserId, ruleId, ct) → bool`.
- **`StubAccountService` (Testhelfer):** um die drei neuen Member erweitern (NotSupported-Muster beibehalten).

### `AccountsController` (Controller)

- **Neue Abhängigkeit:** `IBackgroundTaskManager` wird neu per Konstruktor injiziert (bisher nicht injiziert — benötigt für den Reclassify-Endpunkt; Konstruktorparameter + Feld + XML-Doc ergänzen).
- **Neue Endpunkte:** `GET {id}/posting-rules`, `POST {id}/posting-rules` (Upsert, `ValidationProblem` bei `!ModelState.IsValid`, `Err_NotFound`/`Err_InvalidArgument`-Schema), `DELETE {id}/posting-rules/{ruleId}`, `POST {id}/posting-types/reclassify` (Besitzerprüfung → `Err_NotFound`; Enqueue mit JSON-Payload `{"accountId":"…"}`, `allowDuplicate: false`, `202`/`200` mit `BackgroundTaskInfo`).

### `StatementDraftsController` (Controller)

- **Geänderte Methoden:** `SaveEntryAllAsync` — übergibt `body.PostingType` an den Service.

### `FmStatementDraftCompleteTool` (MCP-Tool)

- **Geänderte Methoden:** `SaveEntryAllAsync`-Aufruf um `null` für `postingType` ergänzen (MCP-Werkzeug setzt die Postenart vorerst nicht — entspricht Scope-Ausschluss „kein neuer Anwendungsfall").

### `BackgroundTaskType` (Enum, Shared)

- Neues Mitglied `ReevaluatePostingTypes`.

### `IApiClient` / `ApiClient` (partial)

- **Neue Methoden (`ApiClient.Accounts.cs`):** `GetPostingRulesAsync`, `UpsertPostingRuleAsync` (POST `AccountPostingRuleUpsertRequest`), `RemovePostingRuleAsync`, `Accounts_EnqueuePostingTypeReevaluationAsync` (POST `{id}/posting-types/reclassify` → `BackgroundTaskInfo`) — Muster `GetLinkedIbansAsync`/`AddLinkedIbanAsync`/`RemoveLinkedIbanAsync`.

### `PostingsQueryService` (Service)

- **Geänderte Methoden:** alle vier `PostingServiceDto`-Projektionen (:170, :278, :381, :461) übergeben `p.PostingType`.

### `PostingsController` (Controller)

- **Geänderte Methoden:** Einzelposten-Endpunkt `GET /api/postings/{id}` — die `PostingServiceDto`-Konstruktion (PostingsController.cs:127) übergibt zusätzlich `p.PostingType` (sonst liefert die Detail-API stets `Unspecified`).
- **Bewusst unverändert:** `BudgetReportsController.cs:325` und `BudgetReportViewModel.cs:775` erzeugen ebenfalls `PostingServiceDto`, übergeben die Postenart aber nicht — begründet durch Scope-Ausschluss 1 (keine Postenart-Auswertung in Berichten/Budget); dort bleibt `Unspecified`.

### `StatementDraftEntryDto` / `PostingServiceDto` / `StatementDraftSaveEntryAllRequest` (Shared-Records)

- `StatementDraftEntryDto`: optionale Endparameter `BankPostingType PostingType = BankPostingType.Unspecified, bool IsPostingTypeManual = false`.
- `PostingServiceDto`: optionaler Endparameter `BankPostingType PostingType = BankPostingType.Unspecified`.
- `StatementDraftSaveEntryAllRequest`: optionaler Endparameter `[JsonIgnore(WhenWritingNull)] BankPostingType? PostingType = null`.

### `BaseViewModel` (ViewModel-Basisklasse)

- **Geänderte Methoden:** `QueryLookupAsync` (BaseViewModel.cs:410) reicht im Enum-Zweig (:412–418) `field.LookupFilter` an `QueryEnumLookup` durch; die private Methode (BaseViewModel.cs:561) wird um den Parameter `string? lookupFilter` erweitert (bisherige Signatur `(string? q, Type enumType)` — kein `CardField`-Parameter) und wertet `Exclude=<MemberName>[;<MemberName>]` aus: die genannten rohen Enum-Member werden vor der Lokalisierung aus der Ergebnisliste gefiltert — generischer Mechanismus (Muster `Type=`/`OnlyActive=`/`BankContactId=`), kein hartkodierter `BankPostingType`-Sonderfall. Alle `QueryLookupAsync`-Overrides (`SecurityCardViewModel`, `SavingsPlanCardViewModel`, `ContactCardViewModel`, `BudgetPurposeCardViewModel`) delegieren den Enum-Zweig an `base` — keine Folgeänderung nötig.

### `StatementDraftEntryCardViewModel` (ViewModel)

- **Geänderte Methoden:** `BuildFields` — neues `CardField` „Postenart" (`lookupType: "Enum:BankPostingType"`, `lookupFilter: "Exclude=Unspecified"`, `editable: !isLocked`, lokalisierte Anzeige); Save-Pfad liest den Wert und übergibt ihn im `StatementDraftSaveEntryAllRequest`.

### `StatementDraftEntriesListViewModel` / `StatementDraftEntryItem` (ViewModel/Item)

- `StatementDraftEntryItem`: neue Eigenschaft `PostingType` (+ `IsPostingTypeManual` für evtl. Kennzeichnung).
- `BuildRecords`: neue anzeigende Spalte `postingtype` (lokalisierte Enum-Bezeichnung, `Unspecified` → leer); `ToItem`/`CreateEditSnapshot` übernehmen den Wert. `EditableFields` bleibt unverändert (kein Inline-Edit, Scope-Ausschluss 9).

### `BankAccountCardViewModel` (ViewModel)

- **Neue Eigenschaften:** `PostingRules` (`IReadOnlyList<AccountPostingRuleDto>`), Eingabefelder für neue Regel (`NewPostingRuleField`, `NewPostingRulePattern`, `NewPostingRuleType`, `NewPostingRuleOrderIndex`), `Busy`-Fehleranzeige analog `SetError`.
- **Neue Methoden:** `LoadPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync(ruleId)`.
- **Geänderte Methoden:** `LoadAsync` lädt Regeln; `GetRibbonRegisterDefinition` — neue `UiRibbonAction("ReevaluatePostingTypes", …)` in der `Ribbon_Group_Bookings`-Gruppe (nach `CreatePreliminaryDraft`, deaktiviert bei `Account == null || Id == Guid.Empty`, **ohne** `MobileShortcut` — `CreatePreliminaryDraft` bleibt die Primäraktion der Gruppe, siehe Designentscheidung); `VisibleBackgroundTaskTypes` überschreiben: `{ ReevaluatePostingTypes }` (beabsichtigte Einschränkung, siehe Designentscheidung).

### `BasePostingsListViewModel` (ViewModel)

- **Geänderte Methoden:** Spaltenliste um `postingtype` (anzeigend, lokalisierte `EnumType_BankPostingType_*`-Bezeichnung; `Unspecified` → leer).

### `CardPage.razor` (Seite)

- `PostingRulesPanel` für `BankAccountCardViewModel` bei `Account?.Id != Guid.Empty` rendern (analog `LinkedIbansPanel`-Bedingung, aber ohne `EffectiveIsCollectionAccount`).

### `GenericCardPage.razor` (generische Kartenansicht)

- **Geänderte Methoden:** `OnParametersSetAsync` (:385–403) — der Enum-Options-Prefetch wird pro Kombination aus `LookupType` **und** `LookupFilter` aufgebaut: statt distinkter `LookupType`-Strings werden die tatsächlichen Felder gruppiert, und das synthetische `CardField` des Prefetch-Aufrufs erhält den `LookupFilter` (`new CardField("", CardFieldKind.Text, lookupType: lt, lookupFilter: filter)`), damit `QueryEnumLookup` den `Exclude`-Filter auswerten kann. Cache-Key ist der zusammengesetzte Schlüssel `"{LookupType}|{LookupFilter}"` (Hilfsmethode `EnumOptionsKey(CardField)`).
- **Geänderte Methoden:** `RenderEditableField` (:200) — Cache-Zugriff `_enumOptions[EnumOptionsKey(f)]` statt `_enumOptions[f.LookupType]`; das Rendern der `<option>`-Elemente bleibt unverändert. Felder ohne `LookupFilter` erhalten weiterhin die ungefilterte Optionsliste.

### `BackupService` (Service)

- **Geänderte Methoden:** `BuildBackupDataAsync` — optionale Sektion `AccountPostingRules` (Filter `importAccountIds.Contains(r.AccountId)`); kein Eintrag in `RequiredDataProperties`.

### `SetupImportService` (Service)

- **Geänderte Methoden:** Import — `TryGetProperty("AccountPostingRules")`-Guard, `accountMap`-Remapping, Duplikat-Toleranz `(AccountId, Field, Pattern)`, `AssignBackupDto`.

### `DemoDataService` (Service)

- Nach dem Anlegen der Konten: `_accountService.UpsertPostingRuleAsync` für Regeln „Zins" (`PostingRuleField.Subject`, `BankPostingType.Interest`) an `giroAccount`/`bausparAccount` (Demo-Zinsbuchungen „Zinsgutschrift"/„Jahresendzinsen") sowie „Kapitalertragssteuer" (`Subject`, `BankPostingType.Tax`) an `giroAccount`.

### `EnumCoverageTests` (Testklasse)

- `BackgroundTaskType_Values` um `ReevaluatePostingTypes`; neue Facts `BankPostingType_Values`, `PostingRuleField_Values` (AGENT.md-Pflicht).

### Ressourcen

- **`FinanceManager.Web/Resources/Pages.resx`/.de/.en:** `EnumType_BankPostingType_{Unspecified,Inflow,Outflow,Interest,Tax}`, `EnumType_PostingRuleField_{Subject,BookingDescription}`, `Card_Caption_StatementDrafts_PostingType`, `List_Th_PostingType`, `PostingRules_*` (Section_Title, Empty, Add/Remove/Save-Buttons, Feldlabels inkl. Validierungshinweis), `Ribbon_ReevaluatePostingTypes`.
- **`FinanceManager.Web/Resources/Components/BackgroundTaskStatusPanel.de.resx` + `.en.resx`:** `Bgt_Type_ReevaluatePostingTypes` — Pflicht: Das Panel löst Task-Typ-Labels über `Bgt_Type_{type}` auf (`BackgroundTaskStatusPanel.razor:160`); fehlt der Schlüssel, erscheint der rohe Enum-Name im Statuspanel.
- **`FinanceManager.Web/Resources/Services/PostingTypeReevaluationTaskExecutor.de.resx` + `.en.resx` (neu):** Fortschritts-/Status-/Fehlerschlüssel des Executors — Konvention `Resources/Services/<ExecutorName>.<culture>.resx` (Muster `BookingTaskExecutor`, `ClassificationTaskExecutor`).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddPostingTypeAndAccountPostingRules` | neue Tabelle `AccountPostingRules` (`Id`, `AccountId` FK→`Accounts` cascade, `Field` int, `Pattern` nvarchar(200), `PostingType` int, `OrderIndex` int + Index `AccountId`); `Postings.PostingType` int NOT NULL DEFAULT 0; `StatementDraftEntries.PostingType` int NOT NULL DEFAULT 0; `StatementDraftEntries.IsPostingTypeManual` bit NOT NULL DEFAULT 0 | Per `dotnet ef migrations add` erzeugt (AGENT.md). In der generierten Migration zusätzlich `migrationBuilder.Sql`-Backfill: `Postings` mit `Kind = 0` (Bank) bekommen `PostingType = 2` (`Outflow`) bei `Amount < 0`, sonst `1` (`Inflow`); `StatementDraftEntries` analog aus dem Betragsvorzeichen. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AccountPostingRuleUpsertRequest.Pattern` | `[Required]`, `[MaxLength(200)]`, getrimmt, nicht leer | `ValidationProblem` (400) / `Err_InvalidArgument` |
| `AccountPostingRuleUpsertRequest.PostingType` | muss definierter `BankPostingType`-Wert ≠ `Unspecified` sein | `ValidationProblem`/`Err_InvalidArgument` (400) |
| `AccountPostingRuleUpsertRequest.Field` | muss definierter `PostingRuleField`-Wert sein | `ValidationProblem` (400) |
| `AccountPostingRuleUpsertRequest.OrderIndex` | `>= 0`; nicht gesetzt → Service vergibt `max+1` | `Err_InvalidArgument` (400) |
| `StatementDraftSaveEntryAllRequest.PostingType` | definierter Wert ≠ `Unspecified` (wenn übergeben) | `DomainValidationException` → 409 `Err_InvalidState`-Schema bzw. Feldfehler |
| Besitzerprüfung Regel-Endpunkte | `Account.OwnerUserId == _current.UserId` (Konto fremd/fehlend → `Err_NotFound` 404) | 404 |
| `POST {id}/posting-types/reclassify` | Konto existiert und gehört dem Benutzer | 404 `Err_NotFound` |
| Re-Evaluierung nach Kernfeldänderung | nur Entries mit `!IsPostingTypeManual` und geänderten Kernfeldern | — (kein Fehlerfall, Guard) |

## Konfigurationsänderungen

Keine. Regeln sind pro Bankkonto-Datensatz konfigurierbar; keine globale Anwendungseinstellung.

## Seiteneffekte und Risiken

- **`IStatementDraftService.SaveEntryAllAsync`-Signatur:** Aufrufer `StatementDraftsController`, `FmStatementDraftCompleteTool` sowie Moq-`Setup`/`Verify`-Ausdrücke in `McpStatementWriteToolsTests` müssen angepasst werden (Kompilierfehler ohne Anpassung).
- **`StubAccountService`:** Interface-Erweiterung von `IAccountService` erfordert neue Member — ohne Ergänzung bricht der Build.
- **`EnumCoverageTests.BackgroundTaskType_Values`:** schlägt sofort fehl, sobald `ReevaluatePostingTypes` ohne Test-Ergänzung hinzukommt.
- **Verlagerung von `NormalizeUmlauts`:** `StatementDraftService.Classification.cs` nutzt die Methode an 6 Stellen (Kontakt-/Sparplan-/Security-Matching) — Delegation muss identisches Verhalten sichern (Namen/Signatur beibehalten oder Call-Sites umstellen).
- **Duplikaterkennung/Status-Logik:** die Postenart-Bewertung läuft in der bestehenden Entry-Schleife vor `TryAutoAssignContact` — keine Änderung an Kontakt-/Sparplan-/Security-Heuristiken.
- **`BankPostingType` in `PostingServiceDto`/`StatementDraftEntryDto`:** optionale Endparameter halten alle bestehenden Aufrufstellen kompilierfähig; alte API-Clients ignorieren das neue Feld.
- **Backup-Schema:** neue Sektion `AccountPostingRules` optional (kein `RequiredDataProperties`-Eintrag); `meta.Version` bleibt `3`; alte Backups ohne Feld/Sektion bleiben ladbar (DTO-Defaults + `TryGetProperty`-Guard).
- **Migration auf SQLite:** Spalten-Defaults + manueller SQL-Backfill (nur `Postings.Kind = 0` und alle `StatementDraftEntries`) — SQLite-Syntax verwenden; Backfill ändert Daten bestehender Datenbanken beim `Migrate`.
- **`QuickEditTable`:** bleibt unverändert (kein neues Eingabeelement); `EditableFields` unverändert.
- **`GenericCardPage`-Enum-Prefetch:** betrifft alle Karten-Seiten mit `Enum:`-Feldern (`Enum:AccountType`, `Enum:SecurityTransactionType` u. a.). Felder ohne `LookupFilter` erhalten denselben zusammengesetzten Cache-Key (`{LookupType}|` mit leerem Filter) und unverändert die volle Optionsliste — Verhalten bleibt für Bestandsfelder identisch; bei mehreren Feldern desselben Enum-Typs mit unterschiedlichen Filtern entstehen mehrere Prefetch-Abfragen (lokal, kein API-Call — `QueryEnumLookup` arbeitet ohne ApiClient).
- **`ReevaluatePostingTypes`-Task:** läuft pro Konto; `allowDuplicate: false` verhindert Doppel-Läufe; bei parallelem Buchen theoretisch Race — Posten werden zuletzt geschrieben (akzeptables, dokumentiertes Verhalten; der Task ist idempotent und jederzeit wiederholbar).
- **Demo-/Bestandsdaten:** bestehende Konten ohne Regeln verhalten sich unverändert (Vorzeichen-Fallback reproduziert bisheriges implizites Verhalten).
- **Weitere `PostingServiceDto`-Erzeugungsstellen ohne Postenart:** `BudgetReportsController.cs:325` und `BudgetReportViewModel.cs:775` bleiben bewusst unverändert (Scope-Ausschluss 1 — Berichte werten die Postenart nicht aus); die DTOs tragen dort `Unspecified`.

## Umsetzungsreihenfolge

1. **Enums `BankPostingType` und `PostingRuleField` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `FinanceManager.Shared/Dtos/Postings/BankPostingType.cs` (`Unspecified/Inflow/Outflow/Interest/Tax`) und `FinanceManager.Shared/Dtos/Accounts/PostingRuleField.cs` (`Subject/BookingDescription`); `EnumCoverageTests` sofort erweitern (zwei neue Facts).

2. **`AccountPostingRule`-Entität anlegen**
   - Voraussetzungen: Schritt 1 (Enums).
   - Beschreibung: `FinanceManager.Domain/Accounts/AccountPostingRule.cs` mit `AccountId`, `Field`, `Pattern`, `PostingType`, `OrderIndex`, `Account`-Navigation, `Update(…)`, `AccountPostingRuleBackupDto`, `ToBackupDto`/`AssignBackupDto` (Muster `AccountLinkedIban`).

3. **`PostingTypeEvaluator` anlegen + `NormalizeUmlauts` verlagern**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `FinanceManager.Domain/Postings/PostingTypeEvaluator.cs` mit `NormalizeUmlauts` (aus `StatementDraftService.Classification.cs` verlagert), `NormalizeForRuleMatch` (Umlaute + Kleinbuchstaben + Whitespace-Strip) und `Evaluate(rules, subject, bookingDescription, amount)`; private `NormalizeUmlauts` in `Classification.cs` delegiert.

4. **`Posting.PostingType` + `StatementDraftEntry.PostingType`/`IsPostingTypeManual` anlegen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Eigenschaften + `SetPostingType`-Methoden + Backup-DTO-Erweiterungen mit Defaults + vorzeichenbasierter Alt-Backup-Fallback in `AssignBackupDto`.

5. **`AppDbContext`-Konfiguration + EF-Migration mit SQL-Backfill**
   - Voraussetzungen: Schritte 2–4.
   - Beschreibung: `DbSet<AccountPostingRule>`, Entity-Konfigurationen, `dotnet ef migrations add AddPostingTypeAndAccountPostingRules`, danach `migrationBuilder.Sql`-Backfill (Bank-Postings + Draft-Entries aus Betragsvorzeichen).

6. **Shared-DTOs/-Requests erweitern + `BackgroundTaskType`-Mitglied**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `StatementDraftEntryDto`, `PostingServiceDto`, `StatementDraftSaveEntryAllRequest` (optionale Endparameter); `AccountPostingRuleDto`, `AccountPostingRuleUpsertRequest` neu; `BackgroundTaskType.ReevaluatePostingTypes` + `EnumCoverageTests.BackgroundTaskType_Values`-Ergänzung (muss zeitnah erfolgen, sonst roter Test).

7. **Regel-CRUD im Service (`IAccountService`/`AccountService`) + `StubAccountService`**
   - Voraussetzungen: Schritte 2, 5, 6 (`AccountPostingRuleDto`/Request).
   - Beschreibung: Trio `GetPostingRulesAsync`/`UpsertPostingRuleAsync`/`RemovePostingRuleAsync` mit Besitzerprüfung (Muster Linked-IBAN); Stub nachziehen.

8. **`StatementDraftService`-Anpassungen (Klassifizierung, Speichern, Buchen, Mapping)**
   - Voraussetzungen: Schritte 3–6.
   - Beschreibung: `ClassifyInternalAsync` Regel-Evaluierung; `SaveEntryAllAsync` + `IStatementDraftService`-Signatur (`postingType`); `UpdateEntryCoreAsync`/`ApplyBatchEntryUpdatesAsync` Re-Evaluierung; `BookCoreAsync`/`BookSplitDraftGroupAsync` Übertrag; **alle sieben** `StatementDraftEntryDto`-Konstruktionsstellen (Mapping.cs:30, Mapping.cs:77, StatementDraftService.cs:335, :1213, :1740, :3027, :3056); Controller + `FmStatementDraftCompleteTool` nachziehen.

9. **`AccountsController`-Endpunkte + `IBackgroundTaskManager`-Injection**
   - Voraussetzungen: Schritt 7 (und Schritt 6 für Payload/Tasktyp).
   - Beschreibung: `GET/POST/DELETE {id}/posting-rules`, `POST {id}/posting-types/reclassify` (Enqueue, Besitzerprüfung); `IBackgroundTaskManager` neu in den Controller-Konstruktor injizieren.

10. **`PostingTypeReevaluationTaskExecutor` + Registrierung**
    - Voraussetzungen: Schritte 3, 4, 5, 6 (Tasktyp).
    - Beschreibung: Executor in `FinanceManager.Web/Services/` (Scope-Factory, `ParseOptions`-Muster, Batch-Iteration mit `ReportProgress`, Cancellation); `ProgramExtensions.cs`-Registrierung.

11. **`ApiClient`-Erweiterungen**
    - Voraussetzungen: Schritte 6, 9 (Verträge).
    - Beschreibung: `GetPostingRulesAsync`, `UpsertPostingRuleAsync`, `RemovePostingRuleAsync`, `Accounts_EnqueuePostingTypeReevaluationAsync` in `ApiClient.Accounts.cs` + `IApiClient`.

12. **Backup/Restore**
    - Voraussetzungen: Schritte 2, 4.
    - Beschreibung: `BackupService.BuildBackupDataAsync` (Sektion `AccountPostingRules`), `SetupImportService` (`TryGetProperty`-Guard, `accountMap`-Remapping, Duplikat-Toleranz).

13. **UI — Kontoauszugzeile und Postenliste**
    - Voraussetzungen: Schritte 6, 8, 11.
    - Beschreibung: `BaseViewModel.QueryLookupAsync`/`QueryEnumLookup` um `Exclude`-Filter erweitern (`lookupFilter`-Parameter); `GenericCardPage.razor` — Enum-Prefetch-Cache pro `LookupType`+`LookupFilter` mit Filter-Übergabe ins synthetische `CardField` sowie Cache-Zugriff über denselben Schlüssel; `StatementDraftEntryCardViewModel` (Enum-Lookup-Feld mit `Exclude=Unspecified` + Save), `StatementDraftEntriesListViewModel`/`StatementDraftEntryItem` (Anzeigespalte), `PostingsQueryService`-Projektionen + `PostingsController`-Einzelposten-Projektion + `BasePostingsListViewModel`-Spalte.

14. **UI — Regelverwaltung und Ribbon-Aktion am Bankkonto**
    - Voraussetzungen: Schritte 9–11.
    - Beschreibung: `BankAccountCardViewModel` (Regel-State + Methoden + Ribbon-Aktion in `Ribbon_Group_Bookings` ohne `MobileShortcut` + `VisibleBackgroundTaskTypes`), `PostingRulesPanel.razor` (Postenart-Auswahl ohne `Unspecified`, Validierungsanzeige), `CardPage.razor`-Einbindung, alle resx-Schlüssel (de/en/neutral) inkl. `EnumType_*`-Bezeichnungen, `Bgt_Type_ReevaluatePostingTypes` in `BackgroundTaskStatusPanel.de/.en.resx` und neue Executor-Resx `Resources/Services/PostingTypeReevaluationTaskExecutor.de/.en.resx`.

15. **Demodaten**
    - Voraussetzungen: Schritt 7.
    - Beschreibung: `DemoDataService` legt Regeln „Zins" (Interest) und „Kapitalertragssteuer" (Tax) an den Demo-Konten an.

16. **Unit-/Persistenztests schreiben**
    - Voraussetzungen: jeweilige Implementierungsschritte.
    - Beschreibung: `PostingTypeEvaluatorTests`, Klassifizierungs-/Buchungs-/CRUD-/Executor-/Backup-Tests, `StubAccountService`- und `McpStatementWriteToolsTests`-Nachzug.

17. **Integrations-/API-Tests**
    - Voraussetzungen: Schritte 9–11.
    - Beschreibung: `WebApplicationFactory`-Tests für Regel-Endpunkte + Reclassify-Enqueue (Muster `ApiClientBackgroundTasksTests`).

18. **E2E-Tests (Playwright)**
    - Voraussetzungen: Schritte 13–14.
    - Beschreibung: Neue Testklasse mit UI-Szenarien (siehe Abschnitt E2E).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Evaluate_*` (Regel-Treffer Subject/BookingDescription, Case-/Umlaut-/Whitespace-Insensitivität, `OrderIndex`-Priorität bei Mehrfachtreffer, Vorzeichen-Fallback inkl. `Amount == 0` → `Inflow`, leere Regelliste) | `PostingTypeEvaluatorTests` (neu, `FinanceManager.Tests/Postings/` o. ä.) | Kernlogik der Postenart-Ermittlung |
| `Classify_*` Regel greift / manueller Wert bleibt / ohne Konto Vorzeichen | `StatementDraftClassificationTests` (Ergänzung) | `ClassifyInternalAsync`-Integration der Evaluierung |
| `Book_*` Postenart wird auf Bankposten übertragen, Contact-Gegenposten bleibt `Unspecified`; `Unspecified`-Fallback beim Buchen | `StatementDraftBookingTests` (Ergänzung) | Übertrag beim Buchen |
| `SaveEntryAll_*` manueller Set setzt `IsPostingTypeManual`; `Unspecified` abgewiesen; **Rückgabe-DTO enthält den gesetzten `PostingType`/`IsPostingTypeManual`** | `StatementDraftServiceTests` o. ä. (Ergänzung) | Überschreibschutz/Validierung + Rückgabe-Projektion (StatementDraftService.cs:335 — der Wert darf nicht `Unspecified` verloren gehen) |
| `EntryDto_Projection_*`: `GetOpenDraftsAsync` und `GetDraftAsync` liefern `PostingType`/`IsPostingTypeManual` eines klassifizierten/manuell gesetzten Entries im DTO | `StatementDraftServiceTests` (Ergänzung) | Absicherung der übrigen `StatementDraftEntryDto`-Konstruktionsstellen (Mapping.cs:30/:77, :1213, :1740, :3027/:3056) — optionale DTO-Endparameter gehen sonst still verloren |
| `Migration_Backfill_*`: SQLite-Test über `IMigrator`: Schema bis zur Vorgänger-Migration migrieren (`migrator.Migrate("<Vorgänger>")`), `Postings`/`StatementDraftEntries` per Roh-SQL einfügen (positive/negative Beträge, Bank + Nicht-Bank), dann `Migrate()` auf aktuell → assert `PostingType` = `Outflow` bei `Amount < 0` bzw. `Inflow` sonst (nur `Kind = Bank`; Nicht-Bank bleibt `Unspecified`) | `PostingTypeMigrationBackfillTests` (neu, `FinanceManager.Tests/Infrastructure/` — Muster `AccountServiceTests.CreateSqlite` + EF-Core-`IMigrator`) | Testnachweis für den `migrationBuilder.Sql`-Backfill-Pfad der Migration selbst |
| `UpdateEntryCore`/`ApplyBatch` Re-Evaluierung nur bei `!IsPostingTypeManual` | `StatementDraftServiceTests`/Batch-Tests (Ergänzung) | Automatische Neubewertung nach Kernfeldänderung |
| `Get/Upsert/RemovePostingRule_*` inkl. Besitzerprüfung, `OrderIndex`-Vergabe, Update-Pfad | `AccountServiceTests` (Ergänzung) | Regel-CRUD |
| `ExecuteAsync_*` (nur `PostingKind.Bank` des Kontos, stornierte/vorläufige eingeschlossen, Fremdkonto-Abbruch, Fortschritt) | `PostingTypeReevaluationTaskExecutorTests` (neu) | Hintergrundtask (Scope-Factory + InMemory/SQLite-DbContext, Muster `McpAuditRetentionExecutorTests`) |
| `AccountPostingRules`-Sektion exportiert/importiert; altes Backup ohne Sektion/ohne `PostingType` lädt (Defaults + Vorzeichen-Fallback) | `BackupRestoreCoverageTests`/`SetupImportServiceTests`/`PostingBackupDtoReversalTests`-Muster (Ergänzung) | AGENT.md-Backup-Vollständigkeit |
| `BankPostingType_Values`, `PostingRuleField_Values` | `EnumCoverageTests` (Ergänzung) | AGENT.md-Enum-Pflicht |
| `POST {id}/posting-rules` (+GET/DELETE), `POST {id}/posting-types/reclassify` (Enqueue-Nachweis, Fremdkonto 404) | neue/Ergänzung in `FinanceManager.Tests.Integration` (Muster `ApiClientBackgroundTasksTests`) | API-Verträge |
| `BankAccountCardViewModel`-Ribbon (Gruppe/Reihenfolge/Disabled), `StatementDraftEntryCardViewModel`-Feld, `PostingsQueryService`-Projektion | bestehende ViewModel-/Web-Tests (Ergänzung) | UI-Zustandslogik |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `EnumCoverageTests.BackgroundTaskType_Values` | Neues Enum-Mitglied `ReevaluatePostingTypes` muss ergänzt werden (schlägt sonst fehl) |
| `McpStatementWriteToolsTests` | Moq-`Setup`/`Verify` auf `SaveEntryAllAsync` brechen durch den neuen Parameter — `It.IsAny<BankPostingType?>()` ergänzen |
| `StubAccountService` (Testhelfer) | Neue `IAccountService`-Member müssen implementiert werden (Build-Bruch) |
| `FmStatementDraftCompleteTool`-Aufrufsite (Produktivcode, kein Test) | Signaturerweiterung `SaveEntryAllAsync` — `null` übergeben |
| `BackupRestoreCoverageTests`/`BackupServiceFullExportTests` | ggf. Sektionsliste/Abdeckung der optionalen `AccountPostingRules`-Sektion ergänzen |
| `StatementDraftSavingsPlanLinkedAccountTests` u. a. direkte `SaveEntryAllAsync`-Aufrufer | Signaturerweiterung — zusätzliches `null`-Argument |

### E2E-Tests (primärer Funktionsnachweis)

Neue Testklasse `FinanceManager.Tests.E2E/Tests/Accounts/AccountPostingRulesPlaywrightTests.cs` (bzw. Aufteilung `Tests/StatementDrafts/`), Muster `CollectionAccountPlaywrightTests`/`PreliminaryStatementDraftE2ETests`: `PlaywrightWebAppFixture` + `TestUserSeeder` + `AuthGateway` + `AccountsApiSeedHelper`/`BrowserApiHelper` + DB-Seeding über `AppDbContext` (`CreateDbContext`-Muster).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Regel anlegen (Feld/Pattern/Postenart/Reihenfolge per Panel-UI) → erscheint in Liste; Regel entfernen → verschwindet | `AccountPostingRulesPlaywrightTests.ManagePostingRules_ViaUi` | Regelverwaltung am Bankkonto funktioniert end-to-end | Benutzerfluss über `PostingRulesPanel` + API + Persistenz — nur im Browser nachweisbar |
| Pflicht | Im `PostingRulesPanel` mit leerem Pattern auf „Hinzufügen" klicken → sichtbarer Validierungshinweis im Panel, keine Regel angelegt; Postenart-Auswahl enthält `Unspecified` nicht | `AccountPostingRulesPlaywrightTests.ManagePostingRules_ValidationError_ViaUi` | Sichtbarer Fehlerfall der Regelverwaltung | Über die UI auslösbarer Fehlerfall — muss als E2E nachgewiesen sein, dass die Blockade dem Anwender verständlich erklärt wird |
| Pflicht | Konto + Regel „Zins" via API seeden; `StatementDraft`+Entry per `AppDbContext` seeden; `POST /classify`; Entry-Karte öffnen → Postenart-Feld zeigt „Zins"/„Interest" | `…Tests.ClassifiedEntry_CardShowsRuleBasedPostingType` | Regelbasierte automatische Ermittlung sichtbar in der Kontoauszugzeile | Sichtbare Wirkung der Klassifizierung in der UI |
| Pflicht | Postenart in der Entry-Karte per Dropdown ändern + speichern → Reload zeigt manuellen Wert; erneutes `POST /classify` via API → Wert bleibt (Überschreibschutz); das Dropdown enthält `Unspecified` nicht (`Exclude`-Filter über Prefetch-Cache) | `…Tests.ManualPostingTypeChange_PersistsAfterReclassify` | Manuelle Korrektur + Überschreibschutz | Nutzerinteraktion (Enum-Lookup) + Re-Klassifizierungsverhalten; deckt die `GenericCardPage`-Prefetch-Änderung ab |
| Pflicht | Entry buchen → `GET /api/postings/account/{id}` zeigt Postenart am Bankposten; Postenliste `/list/postings/account/{id}` zeigt Spalte | `…Tests.BookDraft_TransfersPostingTypeToPosting` | Übertrag beim Buchen + Anzeige in der Postenliste | Vollständiger Weg Draft→Posting→Anzeige |
| Pflicht | Gebuchte Posten vorhanden; Regel nachträglich via API angelegt; Ribbon-Aktion „Postenarten neu bewerten" klicken → `BackgroundTaskStatusPanel`/Polling bis Task `Completed` → Postenliste zeigt neu bewertete Art | `…Tests.ReevaluatePostingTypes_ViaRibbon` | Neubewertung als Hintergrundprozess aus UI auslösbar mit sichtbarem Ergebnis | Ribbon → Enqueue → Executor → sichtbares Ergebnis ist der geforderte Kernfluss |
| Optional | Ohne Regeln zeigt ein positiver Betrag „Eingang", negativer „Ausgang" in der Entry-Karte | `…Tests.EntryWithoutRules_ShowsSignBasedPostingType` | Vorzeichen-Fallback | Ergänzende Absicherung des Default-Pfads |

Betroffene bestehende E2E-Tests: Keine — die Änderungen erweitern bestehende Seiten additiv (neues Panel, neues Feld, neue Spalte, neuer Ribbon-Button); bestehende Selektoren und Abläufe bleiben unverändert.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Regel → Klassifizierung → Korrektur → Buchung → Anzeige | Nutzer legt Regel am Konto an | Konto existiert, keine Regeln, Draft mit Entry „Zinsgutschrift" +100 € noch nicht klassifiziert | Regel „Subject enthält 'zins' → Zins" anlegen; Draft klassifizieren (`POST /classify` oder Import); Entry-Karte öffnen; ggf. manuell auf „Steuer" ändern + speichern; buchen; Postenliste öffnen | Entry-Karte zeigt „Zins"; nach manueller Änderung „Steuer" + Flag; gebuchter Bankposten trägt die Postenart; Postenliste zeigt sie in der neuen Spalte | E2E `ClassifiedEntry_CardShowsRuleBasedPostingType` + `ManualPostingTypeChange_PersistsAfterReclassify` + `BookDraft_TransfersPostingTypeToPosting` |
| 2 | Vorzeichen-Fallback ohne Regeln | Klassifizierung | Konto ohne Regeln (fehlendes Objekt: Regel-Collection leer) | Draft mit negativem Betrag klassifizieren | Postenart „Ausgang" automatisch gesetzt | Unit `PostingTypeEvaluatorTests` + E2E `EntryWithoutRules_ShowsSignBasedPostingType` |
| 3 | Neubewertung (Wartesituation Hintergrundprozess) | Ribbon-Aktion am Konto | Gebuchte Bankposten mit Vorzeichen-Arten; Regel nachträglich angelegt | Ribbon klicken → Task `Queued`/`Running` im `BackgroundTaskStatusPanel` → Abschluss `Completed` → Postenliste neu laden | Alle Bankposten des Kontos tragen regel-/vorzeichenbasierte Art; Fortschritt sichtbar; Wiederaufnahme: Task ist idempotent, bei Unterbrechung erneut auslösbar (`allowDuplicate: false` verhindert Doppelung) | E2E `ReevaluatePostingTypes_ViaRibbon` + Unit `PostingTypeReevaluationTaskExecutorTests` |
| 4 | Überschreibschutz | Re-Klassifizierung nach manueller Korrektur | Entry mit manuell gesetzter Postenart (`IsPostingTypeManual = true`) | `ClassifyAsync`/`SetAccountAsync`/erneuter Import | Postenart bleibt der manuelle Wert | Unit + E2E `ManualPostingTypeChange_PersistsAfterReclassify` |
| 5 | Altbestand / alte Backups | Migration + Backup-Restore | DB/Backup ohne neue Spalten/Sektion | `Migrate` → Spalten mit Backfill (Vorzeichen); altes Backup (Version 3 ohne `AccountPostingRules`/`PostingType`) einspielen | Bankposten haben vorzeichenbasierte Art; Restore scheitert nicht, Regel-Liste leer | `PostingTypeMigrationBackfillTests` (SQL-Backfill-Pfad via `IMigrator`), `SetupImportServiceTests`, `PostingBackupDtoReversalTests`-Muster |
| 6 | Fremdkonto-Schutz | API-Aufruf mit fremder `accountId` | Konto gehört anderem Benutzer | `GET/POST/DELETE {id}/posting-rules`, `POST {id}/posting-types/reclassify` aufrufen | 404 `Err_NotFound`, keine Daten preisgegeben | Integrationstests |
| 7 | Regeländerung greift nicht rückwirkend auf offene Drafts | Regel speichern | Offener Draft mit bereits klassifiziertem Entry | Regel ändern/anhlegen | Entry-Postenart unverändert; nächste Klassifizierung wendet neue Regel an | Unit (Klassifizierung erneut aufrufen) |
| 8 | Validierungsfehler in der Regelverwaltung | Nutzer klickt „Hinzufügen" mit leerem Pattern | Konto-Karte geöffnet, `PostingRulesPanel` sichtbar | Add-Button betätigen | Verständlicher Hinweis im Panel, keine Regel angelegt; `Unspecified` ist in der Postenart-Auswahl nicht enthalten | E2E `ManagePostingRules_ValidationError_ViaUi` |

## Offene Punkte

Keine. Alle in `requirement.md` geführten offenen Fragen wurden durch die übergebenen Antworten geklärt und sind als Designentscheidungen/Programmablaufdetails eingearbeitet. Dokumentierte Annahmen: Re-Evaluierung nicht-manueller Postenarten nach Kernfeldänderungen (`UpdateEntryCoreAsync`/`ApplyBatchEntryUpdatesAsync`); Scope-Ausschluss 8 (Neubewertung ohne Sonderbehandlung stornierter/vorläufiger Postings); Scope-Ausschluss 9 (kein Inline-Edit in `QuickEditTable`).
