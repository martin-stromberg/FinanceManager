# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine.

## Zusammenfassung

- Gesamt: 1005
- Bestanden: 1005
- Fehlgeschlagen: 0
- Übersprungen: 0

### Testlauf nach Kategorie

| Kategorie | Tests | Bestanden | Fehlgeschlagen | Zeit |
|-----------|-------|-----------|-----------------|------|
| FinanceManager.Tests.Integration | 101 | 101 | 0 | 28,33 s |
| FinanceManager.Tests | 881 | 881 | 0 | 60,90 s |
| FinanceManager.Tests.E2E | 23 | 23 | 0 | 70,02 s |

## Testabdeckung

**Gesamte Abdeckung (Zeilen):** 13.74 %
**Gesamte Abdeckung (Branches):** 29.13 %

| Paket | Zeilenabdeckung |
|-------|-----------------|
| FinanceManager.Infrastructure | 7.90 % |
| FinanceManager.Web | 30.57 % |
| FinanceManager.Shared | 34.79 % |
| FinanceManager.Domain | 67.75 % |
| FinanceManager.Application | 77.27 % |

## Fehlende Tests

Basierend auf der Coverage-Analyse:

**Quelle:** Coverage-Daten

### Kritische Abdeckungslücken (< 50 %)

- `FinanceManager.Infrastructure` — 7.90% Zeilenabdeckung (kritisch)

### Mittlere Abdeckungslücken (50–80 %)

- `FinanceManager.Web` — 30.57% Zeilenabdeckung
- `FinanceManager.Shared` — 34.79% Zeilenabdeckung
- `FinanceManager.Domain` — 67.75% Zeilenabdeckung

### Zufriedenstellende Abdeckung (> 80 %)

- `FinanceManager.Application` — 77.27% Zeilenabdeckung (nahe an Ziel)

## Hinweise

1. Die Gesamt-Zeilenabdeckung von 13.74% ist relativ niedrig. Dies deutet darauf hin, dass viele Quelldateien derzeit nicht oder nur minimal durch Tests abgedeckt sind.
2. Das FinanceManager.Infrastructure-Projekt (7.90% Abdeckung) benötigt prioritäre Aufmerksamkeit für zusätzliche Tests.
3. Die Test-Suite läuft zuverlässig ohne Fehler durch, was auf gute Test-Stabilität hinweist.
4. Es ist zu empfehlen, Integrations- und Unit-Tests für die unterabgedeckten Module zu entwickeln, insbesondere für kritische Business-Logik in Infrastructure und Web-Projekten.

## Coverage-Artefakte

- FinanceManager.Tests: `FinanceManager.Tests\TestResults\496dc213-d36e-47b7-8092-daf6be682327\coverage.cobertura.xml`
- FinanceManager.Tests.E2E: `FinanceManager.Tests.E2E\TestResults\ad6ca5a6-b48a-4a2c-8b43-0563f5d16b63\coverage.cobertura.xml`
