# Schritt 3: Bankpostenart-Filter

## Anforderung

Bankberichte erhalten analog zum Wertpapier-Subtypfilter eine Auswahl der Bankpostenarten Eingang, Ausgang, Zinsen und Steuern. Die Auswahl gilt nur für Bankposten, wird in Favoriten gespeichert, in Backups übernommen und vom Aggregat- sowie Drilldown-Endpunkt identisch ausgewertet.

## Abgrenzung

- Keine neue Klassifikationslogik; es wird `Posting.PostingType` / `BankPostingType` aus Issue #542 verwendet.
- `Unspecified` ist bewusst nicht als Filterauswahl vorgesehen.
- Bei verknüpften Sparkonten als Sparplanquelle bleibt der Bankpostenartfilter auf Bankberichte beschränkt.
