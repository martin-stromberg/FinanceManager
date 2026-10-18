# Enums

## `GoldenCrossPhase`
Datei: `FinanceManager.Domain/Securities/GoldenCrossPhase.cs`

| Wert | Bedeutung |
|------|-----------|
| `InsufficientData = 0` | Zu wenig Kursdaten für beide Durchschnitte |
| `Far = 1` | Kurzfristiger Durchschnitt klar unter dem langfristigen |
| `Approaching = 2` | Annäherung an ein Goldenes Kreuz |
| `Crossed = 3` | Goldenes Kreuz erreicht |

## Enums in `GoldenCrossAnalyzer.cs` (Application)
Datei: `FinanceManager.Application/Securities/GoldenCross/GoldenCrossAnalyzer.cs`

| Enum | Werte | Bedeutung |
|------|-------|-----------|
| `GoldenCrossEntryScenario` | `Early`, `Average`, `Late` | Einstiegsszenarien |
| `GoldenCrossWindowStatus` | `Upcoming`, `Open`, `Closed` | Lage des Fensters zum aktuellen Kurs |
| `GoldenCrossRetestStatus` | `None`, `InProgress`, `Successful`, `Failed` | Retest-Ergebnis |
| `GoldenCrossLine` | `Short`, `Long` | Getestete Durchschnittslinie |
| `GoldenCrossTrendStructure` | `Unknown`, `StableUptrend`, `WeakUptrend`, `Sideways` | Kursstruktur seit Kreuzung |

## DTO-Enums in `GoldenCrossDto.cs` (Shared)
Datei: `FinanceManager.Shared/Dtos/Securities/GoldenCrossDto.cs`

1:1-Spiegel der Domain/Analyzer-Enums mit Suffix `Dto` und identischen numerischen Werten (`GoldenCrossPhaseDto`, `GoldenCrossEntryScenarioDto`, `GoldenCrossWindowStatusDto`, `GoldenCrossRetestStatusDto`, `GoldenCrossLineDto`, `GoldenCrossTrendStructureDto`). Mapping per `(DtoEnum)(int)domainEnum`; abgesichert durch `GoldenCrossDtoEnumMappingTests`.

## Notification-Enums (Referenz)
Datei: `FinanceManager.Domain/Notifications/` — `NotificationType` (`EventDriven`, `SystemAlert`, …), `NotificationTarget` (`HomePage`, …). Werden von `INotificationWriter.CreateForUserAsync` verwendet.
