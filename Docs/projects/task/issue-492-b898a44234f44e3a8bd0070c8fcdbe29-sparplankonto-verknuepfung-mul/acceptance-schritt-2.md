# Abnahmeprüfung – Entwicklungsschritt 2

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 2 ist in Commit `64afbfef` enthalten) gegen Schritt 2 in `project-plan.md` (Zeilen 64–78) sowie `requirement.md` (FA-1, AK 1/2/5/8). Code-Review plus Testlauf.

- **AK 1 – `accumulatedAmount` = Summe verknüpfter Kontosalden:** Zentrale Quellenwahl in `FinanceManager.Infrastructure/Savings/SavingsPlanLinkAnalysis.cs` (`GetCurrentAmountAsync`, Zeilen 58–76): Bei Links `Sum(CurrentBalance)` über die verknüpften Konten, sonst Posting-Summe (`Kind == PostingKind.SavingsPlan`, optional `BookingDate <= bound`). Verbraucher: `SavingsPlanService.GetAsync` (Zeile 75, mit `today`-Bound wie bisher) und `AnalyzeAsync` (Zeilen 225–234). Das Bauspar-Validierungsbeispiel ist über `AnalyzeAsync_LinkedPlan_AccumulatedFromAccountBalance` abgedeckt (Posting-Summe negativ, Kontosaldo positiv → `accumulatedAmount` = Kontosaldo).

- **AK 2 – additive Summe / Sammelkonto ohne Aufteilung:** Die Salden werden ungewichtet addiert (`SumAsync` in `SavingsPlanLinkAnalysis.cs:63–65` und `SavingsPlanService.cs:232–234`); ein Sammelkonto trägt seinen vollen Saldo zu jedem verknüpften Plan bei, da die Links rein planseitig abgefragt werden. Belegt durch `AnalyzeAsync_TwoLinkedAccounts_AccumulatedIsAdditiveSum` und `AnalyzeAsync_CollectionAccount_FullBalanceForEachPlan`.

- **AK 3 – `scheduleDeficit`/Kadenz aus Konto-Inflow, keine Doppelzählung:** `AnalyzeAsync` (Zeilen 236–288) baut die History im Link-Modus aus **allen positiven** `PostingKind.Bank`-Postings der verknüpften Konten (voller Zufluss inkl. Einzüge, VL, Zinsen — konsistent mit Vorgehensentscheidung 6) plus nicht-informellen Plan-Postings. Informelle Funding-Postings (Gruppen-Bankposting auf **nicht** verknüpftem Konto, Zeilen 268–276) werden übersprungen — keine Doppelzählung, da derselbe Geldfluss bereits über Saldo/Inflow erfasst ist. Positive Plan-Postings, deren Bank-Gegenposting auf einem verknüpften Konto liegt, werden ebenfalls übersprungen (Zeilen 277–282). `expectedToDate`/`scheduleDeficit`/Kadenz laufen über denselben `ComputeSchedule`-Pfad wie bisher. Disbursements (Plan-Postings ohne Bank-Gegenposting) bleiben in der History und speisen die Financing-Cycle-Erkennung (getestet: `AnalyzeAsync_DisbursementWithoutBankCounterpart_StillDetected`).

- **AK 4 – einheitliche „current"-Quelle für alle Ist-Verbraucher:** Alle drei Bestands-Stellen in `StatementDraftService.cs` sind umgestellt: `SAVINGSPLAN_GOAL_*`/`ARCHIVE_MISMATCH`-Validierung (Zeile ~2127), `SAVINGSPLAN_DUE`-Hinweis (Zeile ~2164 — ergänzend zählt ein positiver Inflow auf einem verknüpften Konto als „Beitrag in diesem Monat", Zeilen 2169–2181) und Auto-Archivierung `current == target` (Zeile ~2625). Repo-weite Suche: kein weiterer direkter „current"-Summenpfad auf Plan-Postings verblieben.

- **AK 5 – Fallback ohne Links:** Der `else`-Zweig von `AnalyzeAsync` (Zeilen 290–299) und der Fallback in `GetCurrentAmountAsync` entsprechen exakt dem bisherigen posting-basierten Verhalten; nach Entfernen des letzten Links greift er automatisch (Ableitung, kein Datenupdate) — getestet in `AnalyzeAsync_AfterRemovingLastLink_FallsBackToPostingSum`.

- **Testnachweis:** `dotnet test FinanceManager.Tests --filter "...SavingsPlanLinkedAnalysisTests..."` → **33/33 Tests bestanden** (inkl. `GetAsync_LinkedPlan_CurrentAmountFromBalance`). Build ohne Fehler.

- **Randnotiz (keine Abweichung):** Ein **negatives** Plan-Posting, dessen Gruppe ein Bankposting auf einem nicht verknüpften Konto enthält, wird ebenfalls als informell klassifiziert und komplett übersprungen (Zeilen 271–276) — der Abfluss auf dem Fremdkonto erscheint dann weder als Inflow noch als Disbursement. Das ist konsistent mit der Informal-Definition („dokumentiert nur den Geldfluss"), kann aber eine Auszahlung untertreiben, falls jemals eine Disbursement-Buchung auf ein Fremdkonto gebucht würde. Fachlich unübliches Szenario, kein Requirement-Bruch.

- **`hasPostingThisMonth`-Erweiterung:** Die `SAVINGSPLAN_DUE`-Prüfung zählt Konto-Inflow als Monatsbeitrag — fachlich korrekt und im Sinne der einheitlichen Quellenwahl; geringfügig über den Wortlaut des AK hinaus, aber zweckdienlich.
