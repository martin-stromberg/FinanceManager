# Enums

## Vorhandene Enums (Referenz)

Es existiert **kein** `SecurityTxtFormat`-Enum im Projekt. Der Enum wird neu angelegt.

Vergleichbare vorhandene Enums (zur Orientierung):

### `HolidayProviderKind`
Datei: `src/FinanceManager.Domain/Notifications/HolidayProviderKind.cs`

Wird als Domain-Enum für Benachrichtigungseinstellungen verwendet und zeigt das übliche Ablageort-Muster für Enums in `FinanceManager.Domain`.

### `BackgroundTaskStatus`, `BackgroundTaskType`
Dateien: `src/FinanceManager.Shared/Dtos/Admin/BackgroundTaskStatus.cs`, `BackgroundTaskType.cs`

Zeigen, dass Enums auch im `FinanceManager.Shared`-Namespace abgelegt werden können, wenn sie in DTOs referenziert werden.

---

## Fehlende Enums

| Enum | Fehlende Werte | Geplanter Ablageort |
|------|----------------|---------------------|
| `SecurityTxtFormat` | `PlainText`, `Markdown`, `Html` | `FinanceManager.Application` oder `FinanceManager.Shared` |
