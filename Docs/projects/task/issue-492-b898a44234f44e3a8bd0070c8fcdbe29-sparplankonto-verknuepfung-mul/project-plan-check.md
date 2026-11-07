# Projektplan-Prüfung – Sparplan↔Konto-Verknüpfung (Multi-Link), informelle Beitragsbuchungen und Istmeldung

**Issue:** #492
**Geprüfte Eingaben:** `requirement.md`, `inventory.md`, `project-plan.md`

## Ergebnis

**Status:** Projektplan vollständig

Alle Muss-Anforderungen (FA-1 bis FA-4) und alle zehn Akzeptanzkriterien aus `requirement.md` sind durch die Entwicklungsschritte abgedeckt. Die expliziten Nicht-Ziele werden eingehalten bzw. durch die Vorgehensentscheidungen und Schritt 10 aktiv abgesichert. Die Abhängigkeiten referenzieren ausschließlich frühere Schrittnummern und sind zyklenfrei. Die getroffenen Umsetzungsentscheidungen (Ableitung statt persistiertem `informal`-Flag, Marker statt neuem `PostingKind`, optionale Backup-Sektion, Istmeldung-Ausschluss aus Budget-Actuals) sind gegen `inventory.md` technisch plausibel begründet.

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderung (requirement.md) | Abdeckung im Projektplan | Abgedeckt |
|---|---|---|
| FA-1: n:m-Verknüpfung, owner-scoped, Duplikat-Abweisung | Schritt 1 (Join-Entity, Unique-Paarung, Owner-Prüfung, Service-Ops), Schritt 3 (API/ApiClient) | Ja |
| FA-1: `accumulatedAmount` = Summe verknüpfter Kontosalden, additiv, Sammelkonto voller Saldo je Plan | Schritt 2 | Ja |
| FA-1: `scheduleDeficit`/Kadenz aus Konto-Inflow | Schritt 2 | Ja |
| FA-1: Disbursements bleiben Auszahlungen, mindern `accumulated` nicht | Schritt 2 (Financing-Cycle-Erkennung bleibt, Kontostand enthält Realität) | Ja |
| FA-2: Funding-Postings informell in Plananalyse (nicht in `accumulated`/`expectedToDate`) | Schritt 2 (Analyse), Vorgehensentscheidung 2 (Ableitung statt Flag) | Ja |
| FA-2: `informal: true` in Postings-Ausgabe inkl. MCP | Schritt 4 (REST-DTOs, `fm_postings_query`, Exporte) | Ja |
| FA-2: keine Umbuchung historischer Beträge; nur Plananalyse, nicht Budgetsicht | Entscheidung 2 (Ableitung), Schritt 4 + Schritt 10 (Budget-AK) | Ja |
| FA-3: Istmeldung nur für Pläne ohne Konto-Link; Differenzbuchung ohne Kontobewegung | Schritt 5 (Endpunkt, Marker, `AccountId == null`, Abweisung bei Links), Schritt 8 (UI), Schritt 6 (MCP) | Ja |
| FA-3: Istmeldung als Korrekturbuchung in Posting-Liste/Audit erkennbar | Schritt 5 (Marker in Ausgabe inkl. MCP), Schritt 7 (UI-Kennzeichnung) | Ja |
| FA-4: Budgetsicht unverändert, keine Bankpostings als Plan-Actuals | Schritt 5 (Istmeldung-Ausschluss aus Actuals), Schritt 10 (Regressionsabsicherung) | Ja |
| AK 1: `accumulatedAmount` = Salden-Summe (Validierungsbeispiel Bausparen) | Schritt 2, AK 1 | Ja |
| AK 2: additive Summe, kein Sammelkonto-Split | Schritt 2, AK 2 | Ja |
| AK 3: Owner-Scoping, Duplikat-Abweisung | Schritt 1 (Service), Schritt 3 (API-Fehlercodes) | Ja |
| AK 4: `informal: true` inkl. MCP, kein Einfluss auf `accumulated`/`expectedToDate`, Funding-Nachweis bleibt | Schritte 2 + 4 | Ja |
| AK 5: `scheduleDeficit`/Kadenz aus Konto-Inflow (inkl. VL/Zinsen) | Schritt 2, AK 3 | Ja |
| AK 6: Istmeldung setzt `accumulated`, keine Kontobewegung, erkennbar | Schritt 5, AK 1–2 | Ja |
| AK 7: Istmeldung bei verknüpften Plänen abgewiesen/nicht verfügbar | Schritt 5 (Abweisung), Schritt 8 (UI-Deaktivierung), Schritt 6 (MCP-Validierung) | Ja |
| AK 8: Fallback auf Posting-Auswertung nach Entfernen aller Links | Schritt 2 (AK 5), Schritt 4 (AK 2), Schritt 7 (AK 3) | Ja |
| AK 9: Budget-Istwerte unverändert, keine Bankpostings als Actuals, Vorzeichen stabil | Schritt 10 (alle drei AK) | Ja |
| AK 10: Verknüpfungen + Istmeldung-Postings überstehen Backup/Restore | Schritt 9 (optionale Sektion, ID-Remapping, Marker im `PostingBackupDto`) | Ja |
| Nicht-Ziel: keine Budget-Änderung | Entscheidung 5, Schritte 5 + 10 | Eingehalten |
| Nicht-Ziel: keine separate Konto-/Einnahmen-Budgetquelle | Kein Schritt modelliert Budget-Actuals aus Kontoeingängen | Eingehalten |
| Nicht-Ziel: keine automatische Zuordnung Bankposting→Plan | Kein Schritt enthält Zuordnungslogik; Link wirkt nur aggregiert (Saldo/Inflow) | Eingehalten |
| Nicht-Ziel: keine Sammelkonto-Aufteilung | Entscheidung in Schritt 2 explizit „keine Aufteilung" | Eingehalten |
| Nicht-Ziel: keine Umbuchung historischer Postings | Entscheidung 2 (Ableitung statt Flag), Schritt 4 AK | Eingehalten |
| Nicht-Ziel: kein Istmeldung-Override bei verknüpften Plänen | Schritte 5, 6, 8 | Eingehalten |
| Nicht-Ziel: keine Änderungen an Wertpapier-/Kontakt-Domänen | Kein Schritt betrifft diese Bereiche | Eingehalten |

## Abhängigkeitsprüfung

- **Nur frühere Schrittnummern:** Alle deklarierten Abhängigkeiten verweisen ausschließlich auf kleinere Schrittnummern (max. Referenz: Schritt 7 in Schritt 8). Erfüllt.
- **Zyklenfrei:** Bei ausschließlich absteigenden Referenzen sind Zyklen strukturell ausgeschlossen. Erfüllt.
- **Fachliche Notwendigkeit:**
  - Schritt 2 → 1: zwingend (Analyse braucht die Join-Entity als Quellen-Kriterium).
  - Schritt 3 → 1, 2: 1 zwingend (Endpunkte auf Service-Ops); 2 notwendig, weil das AK den sofortigen Analyse-Effekt Ende-zu-Ende verlangt.
  - Schritt 4 → 2, 3: 2 zwingend (Informalität ist Auswertungseigenschaft des Link-Modus); 3 begründbar (Links werden für Tests/API-E2E über die neuen Endpunkte gesetzt). Die Kennzeichnung selbst hinge streng genommen nur an 1 + 2; die Referenz auf 3 ist nicht falsch, aber über das Minimum hinaus — siehe Hinweise.
  - Schritt 5 → 1, 3: 1 zwingend (Abweisung „Plan hat Links" braucht die Verknüpfung), 3 zwingend (Endpunkt-/ApiClient-Muster am Sparplan).
  - Schritt 6 → 3, 5: zwingend (MCP-Schreibops bauen auf den Fachpfaden für Links und Istmeldung auf).
  - Schritt 7 → 3, 4, 5: zwingend (UI nutzt Link-Endpunkte, `informal`-Ausgabe und Istmeldung-Marker).
  - Schritt 8 → 5, 7: zwingend (Aktion nutzt Istmeldung-Endpunkt und den Detail-Kontext aus 7).
  - Schritt 9 → 1, 5: zwingend (Exportiert werden Join-Entity und Marker-Feld).
  - Schritt 10 → 2, 4, 5: zwingend (regressiert werden die Wirkungen dieser Schritte).
- **Eigenständigkeit als Lifecycle-Eingabe:** Jeder Schritt enthält eine in sich verständliche fachliche Beschreibung, eigene Akzeptanzkriterien, Abhängigkeiten und betroffene Bereiche; die Vorgehensentscheidungen im Kopfteil liefern die übergreifenden Begründungen, auf die die Schritte implizit aufbauen. Erfüllt.

## Fehlende oder unvollständige Punkte

Keine. Sämtliche Anforderungen, Akzeptanzkriterien und Nicht-Ziele sind abgedeckt; die Schritte sind eigenständig beschrieben und technisch mit `inventory.md` konsistent (explizite Join-Entity statt Shadow-m:n, `dotnet ef`-Migration am etablierten Ort, ableitungsbasierte Informalität über `GroupId`, Marker auf `PostingKind.SavingsPlan` statt neuem Enum-Wert zur Vermeidung der dokumentierten Filter-Kaskade, `AccountId == null` als strukturelle Garantie „keine Kontobewegung", Ausschluss der Istmeldung aus den rein `SavingsPlanId`-gefilterten Budget-Actuals, abwärtskompatible optionale Backup-Sektion statt Pflichtabschnitt, ID-Remapping beim Import, ApiClient-Spiegelung für Integrationstests, MCP-Scope-/Descriptor-Anpassungen, UI-Anlehnung an `LinkedIbans`-Verwaltung und Flag-Spalten).

## Hinweise

- **Ableitungsheuristik `informal` (Entscheidung 2):** Die Heuristik „Buchungsgruppe enthält ein Bank-Posting, das die Speisung aus einem anderen Konto belegt" sollte in der Umsetzung prüfen, dass das Gruppen-Bankposting **nicht** auf einem der verknüpften Vertragskonten liegt — sonst könnte ein Plan-Posting, das direkt aus einer Buchung des Vertragskontos entsteht, fälschlich als informell klassifiziert werden. Der Plan formuliert „aus einem anderen Konto" bereits korrekt; die Präzisierung ist als Umsetzungsdetail zu beachten.
- **Schritt 5 ↔ Schritt 4:** Das AK „gilt nicht als informell" in Schritt 5 setzt die Ausgabe-Ableitung aus Schritt 4 voraus, die nicht als Abhängigkeit deklariert ist. Durch die vorgesehene sequentielle Abarbeitung (1→10) und die transitive Ordnung ist dies praktisch abgedeckt; eine explizite Abhängigkeit 5 → 4 wäre sauberer, ist aber nicht erforderlich.
- **Schritt 4 → 3:** Die Abhängigkeit ist über das fachliche Minimum hinaus deklariert (die Kennzeichnung hinge streng nur an 1 + 2). Unkritisch, da ohnehin sequenziell gearbeitet wird.
- **Validierungsbeispiel:** AK 1 der Anforderung nennt konkrete Zahlen (28.801,86 statt −760,74); Schritt 2 paraphrasiert nur („realer Kontosaldo statt negativer Posting-Summe"). Fachlich ausreichend; die konkreten Zahlen sollten in der Verifikation mitgeführt werden.
- **`SavingsPlanId`-Index:** `inventory.md` (Abschnitt 2.4) merkt an, dass kein Index auf `Posting.SavingsPlanId` existiert und neue Join-/Querypfade dies prüfenswert machen. Der Plan adressiert das nicht explizit — optionaler Performance-Aspekt, keine fachliche Lücke.
- **Join-Entity-Felder:** `requirement.md` nennt neben `SavingsPlanId`/`AccountId`/`OwnerUserId` auch „Erstellungs-/Metadaten"; Schritt 1 nennt diese nicht explizit. Durch die Projekt-Konvention (alle Entities führen Metadaten) als implizit abgedeckt gewertet.
- **Kein Istmeldung-Storno (Entscheidung 7):** Konsistent mit `inventory.md` (Owner-Auflösung nur über `AccountId`) und vom Requirement nicht gefordert. In der UI sollte die Storno-Aktion für Istmeldung-Postings ggf. deaktiviert werden, um einen Laufzeitfehler zu vermeiden — Detail für Schritt 7/8.
