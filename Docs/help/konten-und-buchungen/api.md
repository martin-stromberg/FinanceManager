← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — API

## Übersicht

Die API für diesen Bereich wird primär über `AccountsController` und `PostingsController` bereitgestellt.

## Endpunkte / Methoden

### `GET /api/accounts`

**Beschreibung:** Liefert Konten des angemeldeten Benutzers.

### `POST /api/accounts`

**Beschreibung:** Legt ein Konto an. Sammelkonten können dabei direkt markiert werden.

### `PUT /api/accounts/{id}`

**Beschreibung:** Aktualisiert ein Konto inklusive Sammelkonto-Flag.

### `GET /api/accounts/{id}/linked-ibans`

**Beschreibung:** Liefert die verknüpften IBANs eines Sammelkontos.

### `POST /api/accounts/{id}/linked-ibans`

**Beschreibung:** Fügt eine verknüpfte IBAN hinzu.

### `DELETE /api/accounts/{id}/linked-ibans/{iban}`

**Beschreibung:** Entfernt eine verknüpfte IBAN.

### `GET /api/accounts/{id}/posting-rules`

**Beschreibung:** Liefert die Kontierungsregeln des Kontos als Liste von `AccountPostingRuleDto` (`Id`, `Field`, `Pattern`, `PostingType`, `OrderIndex`), sortiert nach `OrderIndex`. Regelfeld `Field` ist `PostingRuleField` (`Subject` = Verwendungszweck, `BookingDescription` = Buchungsbeschreibung), `PostingType` ist `BankPostingType` (`Inflow`, `Outflow`, `Interest`, `Tax`).

**Fehler:**

| Code | Ursache |
|------|---------|
| 404 | Konto existiert nicht oder gehört nicht zum Benutzer (`Err_NotFound`) |

### `POST /api/accounts/{id}/posting-rules`

**Beschreibung:** Legt eine Kontierungsregel an oder aktualisiert eine bestehende. Request `AccountPostingRuleUpsertRequest` (`Id?`, `Field`, `Pattern`, `PostingType`, `OrderIndex?`): `Id = null` erzeugt eine neue Regel, mit gesetzter `Id` wird die bestehende Regel aktualisiert. `Pattern` ist Pflicht (max. 200 Zeichen, wird getrimmt), `PostingType` muss ein definierter Wert ungleich `Unspecified` sein; ein leeres `OrderIndex` hängt neue Regeln ans Ende (`max + 1`).

**Rückgabe:** `204 No Content`.

**Fehler:**

| Code | Ursache |
|------|---------|
| 400 | Ungültiges Feld, leeres/zu langes Pattern oder `PostingType = Unspecified` (`Err_InvalidArgument`/`ValidationProblem`) |
| 404 | Konto existiert nicht oder gehört nicht zum Benutzer (`Err_NotFound`) |

### `DELETE /api/accounts/{id}/posting-rules/{ruleId}`

**Beschreibung:** Entfernt eine Kontierungsregel des Kontos.

**Fehler:**

| Code | Ursache |
|------|---------|
| 404 | Konto oder Regel existiert nicht bzw. gehört nicht zum Benutzer (`Err_NotFound`) |

### `POST /api/accounts/{id}/posting-types/reclassify`

**Beschreibung:** Reiht einen Hintergrundtask vom Typ `BackgroundTaskType.ReevaluatePostingTypes` ein, der die Postenart aller `PostingKind.Bank`-Postings des Kontos anhand der Kontierungsregeln (Fallback: Betragsvorzeichen) neu bewertet. Der Task trägt `{"accountId":"…"}` als Payload; `allowDuplicate: false` verhindert parallele Läufe für dasselbe Konto.

**Rückgabe:** `202 Accepted` mit `BackgroundTaskInfo`; Fortschritt über die üblichen Hintergrundtask-Statusendpunkte.

**Fehler:**

| Code | Ursache |
|------|---------|
| 404 | Konto existiert nicht oder gehört nicht zum Benutzer (`Err_NotFound`) |

### `GET /api/postings/account/{accountId}`

**Beschreibung:** Liefert Buchungen zu einem Konto. Das `PostingServiceDto` enthält mit `postingType` (`BankPostingType`) die Postenart des Bankpostens; bei Nicht-Bankpostings und Buchungen ohne bestimmte Postenart lautet sie `Unspecified`.

### `GET /api/postings/account/{accountId}/export`

**Beschreibung:** Exportiert Buchungen des Kontos.

### `POST /api/postings/{id}/reverse`

**Beschreibung:** Führt eine fachliche Stornierung durch.

### `GET /api/postings/{id}/validate-reversal`

**Beschreibung:** Prüft, ob eine Stornierung zulässig ist.

## Fehler

Typische Fehler sind ungültige IDs, fehlende Berechtigung und fachliche Validierungsfehler bei Stornierungen.
