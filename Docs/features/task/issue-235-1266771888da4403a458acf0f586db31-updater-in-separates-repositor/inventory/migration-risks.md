# Migrationsrisiken und offene Punkte

## Risiken

| Risiko | Auswirkung | Gegenmaßnahme |
|--------|------------|---------------|
| Externe API weicht von `SoftwareSchmiede.AutoUpdate` ab | Adapter und Registrierung kompilieren nicht | ZIP entpacken, Public API gegen aktuelle Nutzung abgleichen |
| Externer Namespace weicht ab | `using SoftwareSchmiede.AutoUpdate` bricht | Namespace-Migration zentral in `FinanceManager.Web/Services/Updates` und `ProgramExtensions.cs` durchführen |
| Falsche TFM im Release-Asset | `FinanceManager.Web` kann DLL nicht referenzieren | Kompatible DLL für `net10.0` oder kompatibles Standard-TFM wählen |
| Transitive Abhängigkeiten fehlen | Build oder Laufzeit startet nicht | ZIP-Inhalt vollständig einbinden oder notwendige PackageReferences ergänzen |
| Artefaktablage unter ausgeschlossenem Pfad | DLL wird nicht kopiert oder nicht gefunden | Ablage außerhalb `artifacts/` bevorzugen oder explizite MSBuild-Items setzen |
| Entfernte Tests reduzieren Abdeckung | Bibliotheksregressionen werden hier nicht mehr erkannt | Nur App-Integration hier testen; Bibliothekstests im externen Repo voraussetzen |
| Release-Workflow baut ganze Solution | Verwaiste Solution-Projekte blockieren Release | `dotnet sln remove` und `dotnet sln list` verifizieren |
| Publish-Output enthält externe DLL nicht | Update-Funktion fällt erst deployt aus | `Reference` mit korrektem `Private`/CopyLocal-Verhalten prüfen |

## Offene Punkte für Planung

1. Soll das unveränderte `release.zip`, die entpackte DLL oder beides eingecheckt werden?
2. Welcher Ablageort ist verbindlich für externe Release-Artefakte?
3. Welche Assembly im ZIP ist die zu referenzierende Bibliothek?
4. Hat die externe Bibliothek denselben Namespace `SoftwareSchmiede.AutoUpdate` oder einen neuen Namespace?
5. Muss eine Herkunfts-/Lizenzdatei neben dem Artefakt abgelegt werden?
6. Sollen README und CHANGELOG direkt in dieser Umsetzung aktualisiert werden?
7. Soll nach Entfernung des lokalen Testprojekts ein kleiner Smoke-Test für `UseAutoUpdate` in `FinanceManager.Tests` ergänzt werden, falls bestehende Tests nicht ausreichen?

## Empfohlene technische Entscheidungen

### Artefaktablage

Empfohlen ist ein versionierter, nicht als Build-Output behandelter Pfad:

```text
external/msTools.Updater/v0.2.0/
```

Begründung:

- `artifacts/` ist im Repository bereits als ausgeschlossener Build-/Arbeitsbereich behandelt.
- `external/` macht Herkunft und Zweck klar.
- Versionierter Unterordner erlaubt spätere Upgrades ohne unklare Überschreibung.

### MSBuild-Einbindung

Nach dem Entpacken sollte `FinanceManager.Web.csproj` eine explizite Referenz erhalten, z. B. sinngemäß:

```xml
<Reference Include="...">
  <HintPath>..\external\msTools.Updater\v0.2.0\lib\...\...\....dll</HintPath>
  <Private>true</Private>
</Reference>
```

Der konkrete `Include`-Name und `HintPath` hängen vom ZIP-Inhalt ab.

### Reihenfolge

1. Externes Asset herunterladen und Inhalt prüfen.
2. Neue Referenz in `FinanceManager.Web` ergänzen, ohne lokale Bibliothek sofort zu löschen.
3. Namespaces/API anpassen, bis Build grün ist.
4. Lokale Projekte aus Solution und Dateisystem entfernen.
5. Tests ausführen.
6. Dokumentation aktualisieren.

Diese Reihenfolge hält Fehlerquellen trennbar: erst externe API-Kompatibilität, dann Entfernung lokaler Projekte.
