# Anforderung: Anzeige der Versionsnummer im Programmmenü

## Fachliche Zusammenfassung

Die `LoginStatus.razor` Komponente, die im Footer-Bereich des Programmmenüs angezeigt wird, zeigt derzeit die Benutzer-ID (`CurrentUser.UserId`) an. Diese Anzeige soll durch die aktuelle Versionsnummer des Programms ersetzt werden. Die Versionsnummer wird aus der `release-metadata.json` Datei bereitgestellt, die durch den bereits existierenden `IInstalledReleaseMetadataProvider` gelesen wird.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- `LoginStatus.razor` – Die Komponente, die das Menü-Fußbereich-Element rendert und die Benutzer-ID anzeigt

### Services
- `IInstalledReleaseMetadataProvider` (bereits vorhanden) – Stellt die `InstalledReleaseMetadataDto` mit der Versionsinformation bereit
- `CurrentUserService` – Derzeit zur Benutzer-Authentifizierung und Benutzer-ID-Zugriff verwendet

### DTOs
- `InstalledReleaseMetadataDto` (bereits vorhanden) – Enthält Versionsinformation und wurde bereits implementiert

### Tests
- Test für `LoginStatus.razor`, um sicherzustellen, dass die Versionsnummer korrekt angezeigt wird
- ggf. Anpassung bestehender Tests für die Komponente

## Implementierungsansatz

1. **Abhängigkeit hinzufügen**: Die `LoginStatus.razor` Komponente muss `IInstalledReleaseMetadataProvider` per Dependency Injection erhhalten.

2. **Versionsinformation laden**: In der `OnInitializedAsync()` oder `OnAfterRenderAsync()` Methode die Versionsinformation asynchron laden, indem `IInstalledReleaseMetadataProvider.GetAsync()` aufgerufen wird.

3. **UI aktualisieren**: Statt `@CurrentUser.UserId` wird `@_versionInfo?.Version` angezeigt (oder ein entsprechender Fallback-Text, falls keine Version vorhanden ist).

4. **Benutzer-Authentifizierung bewahren**: Die Logout-Funktionalität und LoginStatus-Komponenten-Struktur bleiben erhalten; nur die angezeigte Information wird ausgetauscht.

## Konfiguration

- Keine neue Konfiguration erforderlich. Die bestehende `release-metadata.json` wird verwendet.
- Die Komponente sollte resilient sein gegenüber fehlender oder ungültiger `release-metadata.json` (z. B. Fallback-Text wie "Version unbekannt" oder leere Anzeige).

## Offene Fragen

1. **Fallback-Verhalten**: Wie soll die Komponente reagieren, wenn `release-metadata.json` nicht existiert oder die `Version` Eigenschaft `null` ist?
   - Option A: Leerer String angezeigt
   - Option B: Platzhalter-Text (z. B. "Version unbekannt")
   - Option C: "N/A" oder ähnliches
   
2. **Format der Versionsnummer**: Soll die Versionsnummer mit Präfix angezeigt werden (z. B. "v1.2.3") oder nur als Zahl (z. B. "1.2.3")?

3. **Beibehaltung der Benutzer-ID-Anzeige**: Soll die Benutzer-ID dennoch angezeigt werden, ggf. an anderer Stelle oder in einem Tooltip?

4. **Styling/Layout**: Benötigt die Versionsanzeige ein anderes CSS-Styling als die bisherige Benutzer-ID-Anzeige?
