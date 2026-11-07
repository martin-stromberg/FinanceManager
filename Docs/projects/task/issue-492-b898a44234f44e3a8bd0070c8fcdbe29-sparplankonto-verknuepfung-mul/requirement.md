# Anforderung – Sparplan↔Konto-Verknüpfung (Multi-Link), informelle Beitragsbuchungen und Istmeldung-Korrekturen

**Issue:** #492
**Branch:** `task/issue-492-b898a44234f44e3a8bd0070c8fcdbe29-sparplankonto-verknuepfung-mul`
**Aufgaben-ID:** b898a442-34f4-4e3a-8bd0-070c8fcdbe29

## Ziel

Sparpläne sollen die reale Ist-Situation ihrer Vertragskonten abbilden. Heute hat ein `SavingsPlan` keine Kontoverknüpfung; `accumulatedAmount` wird ausschließlich aus plan-verknüpften Postings gebildet und kann dadurch stark von der Realität abweichen (Beispiel Bausparen: `accumulatedAmount` −760,74 bei realem Kontosaldo +28.801,86). Ziel ist ein dreigliedriges Zielmodell:

1. **Multi-Link Sparplan ↔ Bankkonto:** n:m-Verknüpfung, bei der `accumulatedAmount` und die Rhythmus-/Defizitanalyse aus den verknüpften Kontosalden bzw. -inflows folgen.
2. **Informelle Beitragsbuchungen:** Plan-verknüpfte Funding-Postings (z. B. Giro-Abbuchungen) werden bei gesetztem Konto-Link in der Plananalyse als informell behandelt — sie dokumentieren die Funding-Quelle, zählen aber nicht in `accumulated`/`expectedToDate`.
3. **Istmeldung:** Eine neue Posting-Art korrigiert den Plan-Saldo für Pläne ohne Vertragskonto, ohne eine Kontobewegung zu erzeugen.

Budgetauswertungen bleiben fachlich unverändert (Quelle und Vorzeichen wie bisher); siehe Abschnitt „Explizite Nicht-Ziele".

## Fachliche Anforderungen

### FA-1 Multi-Link Sparplan ↔ Bankkonto

- Ein Sparplan kann mit **keinem, einem oder mehreren** Bankkonten verknüpft werden; ein Bankkonto kann **mehrere Sparpläne** bedienen (inkl. Sammelkonten).
- Die Verknüpfung ist **owner-scoped**: Es dürfen nur Konten und Pläne desselben Eigentümers verknüpft werden.
- Bei gesetztem Konto-Link folgt `accumulatedAmount` der **Summe der Salden der verknüpften Konten** (`currentBalance`), nicht mehr der plan-verknüpften Posting-Summe.
- Bei mehreren verknüpften Konten pro Plan werden die Salden **additiv summiert** (keine Gewichtung).
- Bedient ein Konto mehrere Pläne (Sammelkonto), erhält **jeder** verknüpfte Plan den vollen Kontosaldo als Beitrag zu seinem `accumulatedAmount`; es erfolgt **keine prozentuale oder betragsmäßige Aufteilung**.
- Die Rhythmus-/Defizitanalyse (`scheduleDeficit`/Kadenz) nutzt bei verknüpften Plänen den **Konto-Inflow** der verknüpften Konten (eigene Einzüge, VL, Zinsen, sonstige Zuflüsse), nicht den Giro-seitigen Abgang.
- Plan-verknüpfte Postings, die nicht auf dem Vertragskonto repräsentiert sind (z. B. Darlehensauszahlungen), bleiben fachlich `disbursement` und mindern `accumulated` nicht, da der Kontostand die Realität bereits enthält.

### FA-2 Informelle Beitragsbuchungen

- Plan-verknüpfte Postings, die die Speisung des Plans aus einem anderen Konto dokumentieren (Funding-Seite), werden bei gesetztem Konto-Link in der **Plananalyse als informell** behandelt.
- Informelle Postings zählen **nicht** in `accumulated` und `expectedToDate`, bleiben aber als Funding-Quellennachweis auswertbar.
- Informelle Postings sind in der Postings-Ausgabe (inkl. MCP) als solche gekennzeichnet (`informal: true`).
- Historische Beträge werden **nicht** umgebucht; die Informalität ist eine Auswertungseigenschaft, keine Umbuchung.
- `informal` wirkt ausschließlich auf die Plananalyse (`accumulated`/`expectedToDate`), nicht auf die Budgetsicht.

### FA-3 Istmeldung (Korrekturbuchung ohne Kontobewegung)

- Für Pläne **ohne** verknüpfte Konten kann der Ist-Saldo über eine „Istmeldung" gesetzt bzw. korrigiert werden (z. B. Plan „Klamotten": angezeigt −25,30, real 50 € → Istmeldung +75,30).
- Eine Istmeldung ist ein plan-verknüpftes Posting, das `accumulated` korrigiert, **ohne** eine Kontobuchung zu erzeugen.
- Istmeldungen sind im Audit bzw. in der Postings-Ausgabe als Korrekturbuchung erkennbar (eigene Posting-Art bzw. klar unterscheidbare Kennzeichnung).
- Istmeldungen sind **nur** für Pläne ohne verknüpfte Konten zulässig; bei konto-verknüpften Plänen ist der Kontostand die alleinige Ist-Quelle.

### FA-4 Budgetsicht (unverändert)

- Budgetauswertungen ändern weder Quelle noch Vorzeichen.
- Budget-Istwerte konto-verknüpfter Pläne stammen weiterhin aus den **plan-verknüpften Postings** (Kostensicht wie bisher, negative Beträge bleiben Kosten).
- Konto-Links machen Bankpostings **nicht** zu Sparplan-Budgetactuals.

## Betroffene Bereiche

- **Domänenmodell:** `SavingsPlan` (neue n:m-Beziehung zu `Account` über Join-Entity), `Posting` (informelle Auswertung, neue Posting-Art „Istmeldung"), `Account` (bestehendes `IsCollectionAccount` bleibt relevant).
- **Plananalyse/Aggregate:** Berechnung von `accumulatedAmount`, `expectedToDate` und `scheduleDeficit`/Kadenz-Analyse (Umschaltung auf Kontosalden bzw. Konto-Inflow bei gesetztem Link).
- **Budgetauswertung:** Kein Umbau; lediglich sicherzustellen, dass Konto-Links und `informal` die bisherige Budgetsicht nicht verändern.
- **API/MCP:** Postings-Ausgabe mit `informal`-Kennzeichnung; Endpunkte/DTOs zum Setzen und Entfernen der Plan↔Konto-Verknüpfung sowie zum Erfassen von Istmeldungen.
- **Web-UI:** Sparplan-Details (Verwaltung der Kontoverknüpfungen, Buchungsaktion „Istmeldung"), Posting-Anzeige (Kennzeichnung informeller Buchungen und Istmeldungen).
- **Persistenz/Backup:** Neue Join-Tabelle bzw. -Entity inkl. Migration; Erweiterung der Backup-DTOs um die Verknüpfungen und die neue Posting-Art.

## Abläufe

### Ablauf 1: Konto-Link setzen

1. Anwender öffnet die Sparplan-Details und wählt ein oder mehrere eigene Bankkonten zur Verknüpfung.
2. System speichert die n:m-Verknüpfung (owner-scoped, Duplikate werden abgewiesen).
3. Ab sofort wird `accumulatedAmount` aus der Summe der verknüpften Kontosalden berechnet; die Defizitanalyse nutzt den Konto-Inflow.
4. Bestehende plan-verknüpfte Postings bleiben unverändert bestehen; Funding-Postings werden in der Plananalyse als informell ausgewertet.

### Ablauf 2: Konto-Link entfernen

1. Anwender entfernt eine Verknüpfung in den Sparplan-Details.
2. Hat der Plan danach keine verknüpften Konten mehr, fällt `accumulatedAmount` auf die bisherige Posting-basierte Berechnung zurück; die Funding-Postings gelten wieder als regulär.

### Ablauf 3: Informelle Funding-Buchung

1. Ein Giro-seitiges plan-verknüpftes Posting speist einen konto-verknüpften Plan.
2. In der Plananalyse zählt das Posting nicht in `accumulated`/`expectedToDate`; in der Postings-Ausgabe ist es mit `informal: true` gekennzeichnet.
3. Derselbe Geldfluss ist auf dem Vertragskonto als Zufluss sichtbar und zählt dort über den Kontosaldo — keine Doppelzählung.

### Ablauf 4: Istmeldung erfassen

1. Anwender wählt in den Sparplan-Details eines Plans **ohne** Konto-Link die Aktion „Istmeldung" und gibt den gemeldeten Ist-Saldo ein.
2. System erzeugt ein plan-verknüpftes Korrekturposting in Höhe der Differenz zum aktuellen `accumulated`.
3. `accumulated` entspricht anschließend dem gemeldeten Wert; es entsteht keine Kontobuchung.
4. Die Istmeldung ist in Posting-Liste und Audit als Korrektur erkennbar.

## Konfiguration/Datenmodell

- **Neue Join-Entity** `SavingsPlan` ↔ `Account` (n:m), Felder mindestens: `SavingsPlanId`, `AccountId`, `OwnerUserId` (bzw. ableitbar über den Plan), Erstellungs-/Metadaten; eindeutige Paarung (keine Duplikat-Links).
- **`Posting`:** Mechanismus zur Kennzeichnung/Ableitung der Informalität für Funding-Postings (Auswertungseigenschaft; konkrete Umsetzung — Flag, Zuordnungsart oder Ableitung aus dem Konto-Link — ist Umsetzungsdetail, fachlich verbindlich ist nur das Ergebnis: `informal: true` in der Ausgabe, Ausschluss aus `accumulated`/`expectedToDate`).
- **Neue Posting-Art „Istmeldung"** (bzw. klar unterscheidbare Kennzeichnung auf einem plan-verknüpften Posting), die ohne `AccountId` auskommt und `accumulated` direkt korrigiert.
- **Migration:** Neue Tabelle für die Join-Entity; keine Datenmigration bestehender Posting-Beträge (keine Umbuchung historischer Postings).
- **Backup/Restore:** Join-Verknüpfungen und die neue Posting-Art sind backupfähig (Erweiterung der entsprechenden Backup-DTOs).
- Keine neue Laufzeit-Konfiguration (kein Feature-Flag, keine Einstellungsparameter) erforderlich.

## Akzeptanzkriterien

1. `accumulatedAmount` eines konto-verknüpften Plans entspricht der Summe der Salden aller verknüpften Konten (Validierungsbeispiel: Bausparen zeigt 28.801,86 statt −760,74).
2. Bei mehreren verknüpften Konten pro Plan werden die Salden additiv summiert; bei Sammelkonten erhält jeder verknüpfte Plan den vollen Kontosaldo (keine Aufteilung).
3. Verknüpfungen sind nur zwischen Konten und Plänen desselben Eigentümers möglich; doppelte Links werden abgewiesen.
4. Funding-Postings konto-verknüpfter Pläne sind in der Postings-Ausgabe (inkl. MCP) mit `informal: true` gekennzeichnet und fließen weder in `accumulated` noch in `expectedToDate` ein, bleiben aber für die Funding-Quellenanalyse verfügbar.
5. `scheduleDeficit`/Kadenz-Analyse nutzt bei verknüpften Plänen den Konto-Inflow (inkl. VL und Zinsen), nicht den Giro-Abgang.
6. Eine Istmeldung setzt `accumulated` auf den gemeldeten Wert (Differenzbuchung), erzeugt keine Kontobewegung und ist in Posting-Liste/Audit als Korrektur erkennbar.
7. Die Istmeldung-Aktion ist bei konto-verknüpften Plänen nicht verfügbar bzw. wird abgewiesen.
8. Nach Entfernen aller Konto-Links eines Plans fallen `accumulatedAmount` und Defizitanalyse auf die bisherige Posting-basierte Auswertung zurück.
9. Die Budgetauswertung liefert für konto-verknüpfte Pläne unveränderte Istwerte aus den plan-verknüpften Postings; Bankpostings verknüpfter Konten erscheinen nicht als Sparplan-Budgetactuals; Vorzeichenlogik bleibt unverändert.
10. Verknüpfungen und Istmeldung-Postings überstehen Backup und Restore.

## Explizite Nicht-Ziele

- **Keine Änderung der Budgetauswertungen:** Weder Quelle noch Vorzeichen der Budgetsicht werden angepasst; `informal` wirkt nicht auf Budgets.
- **Keine separate Konto-/Einnahmen-Budgetquelle:** Zinsen, VL oder sonstige Kontoeingänge werden nicht als eigenständige Budget-Actuals modelliert.
- **Keine automatische Zuordnung von Bankpostings zu Sparplänen** über den Konto-Link hinaus (Kontobuchungen bleiben Kontobuchungen).
- **Keine Aufteilungslogik für Sammelkonten** (keine Prozent-, Betrags- oder Prioritätenschlüssel).
- **Keine Umbuchung/Migration historischer Posting-Beträge** beim Setzen eines Konto-Links.
- **Kein Istmeldung-Override für konto-verknüpfte Pläne.**
- Keine Änderungen an Wertpapier-, Kontakt- oder sonstigen Posting-Domänen außerhalb des beschriebenen Umfangs.

## Offene Fragen / getroffene Annahmen

Die im Issue genannten offenen Punkte wurden mangels Nutzerantwort fachlich konservativ als Annahmen festgelegt:

| Offener Punkt (Issue) | Getroffene Annahme |
|---|---|
| Migration bestehender plan-verknüpfter Postings: „real" oder rückwirkend informell? | Bestehende plan-verknüpfte Postings bleiben unverändert bestehen. Bei gesetztem Konto-Link werden Funding-Postings für Plananalysen als informell ausgewertet; historische Beträge werden nicht umgebucht. |
| Sammelkonto-Semantik: Aufteilung des Saldos (Prozent, Betrag, Priorität)? | Keine Aufteilung. `accumulatedAmount` jedes verknüpften Plans ist die Summe der verknüpften Kontosalden; ein Sammelkonto trägt seinen vollen Saldo zu jedem Plan bei. |
| Istmeldung auch bei konto-verknüpften Plänen (Override bis zur nächsten Synchronisation)? | Nein. Istmeldung ist nur für Pläne ohne verknüpfte Konten zulässig; bei Konto-Link ist der Kontostand die alleinige Ist-Quelle. |
| Mehrere Konten pro Plan: additive Summe oder gewichtete Zuordnung? | Additive Summe der Kontosalden; keine Gewichtung. |

**Weiterhin offen (Umsetzungsdetail, keine Fachlichkeit):**

- Konkrete technische Repräsentation der Informalität (explizites Flag am Posting, Zuordnungsart oder Ableitung aus „plan-verknüpftes Posting + gesetzter Konto-Link"). Fachlich verbindlich ist nur das Auswertungsergebnis gemäß FA-2.
- Ob der Konto-Inflow für die Defizitanalyse alle Zuflüsse des Kontos oder nur Beitrags-artige Zuflüsse (Einzüge, VL, Zinsen) umfasst; konservativ wird der vollständige Inflow herangezogen, wie im Issue formuliert („das Giro sieht die VL-40 nie; nur das Vertragskonto enthält den vollständigen Beitragsstrom").
