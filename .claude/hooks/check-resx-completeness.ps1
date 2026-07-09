#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Claude PostToolUse hook: Prüft nach jeder Dateibearbeitung, ob alle Ressourcenschlüssel
    in sämtlichen .resx-Dateien desselben Verzeichnisses vorhanden sind.

.DESCRIPTION
    Dieser Hook wird automatisch von Claude nach jedem Write/Edit-Tool-Aufruf ausgeführt.
    Er liest den Tool-Call aus stdin (JSON), bestimmt die bearbeitete Datei und prüft,
    ob alle Schlüssel der Fallback-Datei (ohne Kultur-Suffix) auch in den sprachspezifischen
    Dateien (*.de.resx, *.en.resx, …) vorhanden sind – und umgekehrt.

    Ergebnis wird nach stdout geschrieben; bei fehlenden Schlüsseln werden diese aufgelistet.

.NOTES
    Wird als PostToolUse-Hook in .claude/settings.json registriert.
#>

param()

# ---- Eingabe von stdin lesen -----------------------------------------------
$inputJson = $null
try {
    $inputJson = $input | Out-String | ConvertFrom-Json -ErrorAction Stop
} catch {
    # Kein gültiges JSON oder leere Eingabe – Hook beendet sich ohne Fehler
    exit 0
}

# ---- Bearbeitete Datei ermitteln -------------------------------------------
$filePath = $inputJson.tool_input?.file_path ?? $inputJson.tool_input?.path ?? ""
if ([string]::IsNullOrWhiteSpace($filePath)) { exit 0 }

# Nur bei .resx-Dateien aktiv werden
if (-not $filePath.EndsWith(".resx", [System.StringComparison]::OrdinalIgnoreCase)) { exit 0 }

# ---- Alle .resx-Dateien in demselben Verzeichnis ermitteln -----------------
$dir = Split-Path $filePath -Parent
if (-not (Test-Path $dir)) { exit 0 }

$resxFiles = Get-ChildItem -Path $dir -Filter "*.resx" | Where-Object { $_.Name -notlike "*.Designer.resx" }
if ($resxFiles.Count -lt 2) { exit 0 }  # Nur eine Datei → nichts zu vergleichen

# ---- Schlüssel pro Datei einlesen ------------------------------------------
function Get-ResxKeys([string]$path) {
    try {
        [xml]$xml = Get-Content $path -Encoding UTF8
        return @($xml.root.data | Where-Object { $_.name } | ForEach-Object { $_.name })
    } catch {
        return @()
    }
}

$fileKeys = @{}
foreach ($f in $resxFiles) {
    $fileKeys[$f.Name] = Get-ResxKeys $f.FullName
}

# ---- Basis-Datei (ohne Kulturkürzel) finden --------------------------------
# Muster: "Pages.resx" ist Basis für "Pages.de.resx" und "Pages.en.resx"
$baseName = $resxFiles | Where-Object { $_.Name -notmatch '\.[a-z]{2}(-[A-Z]{2})?\.resx$' } | Select-Object -First 1
if (-not $baseName) { exit 0 }

$baseKeys = $fileKeys[$baseName.Name]
$cultureFiles = $resxFiles | Where-Object { $_.Name -ne $baseName.Name }

# ---- Fehlende Schlüssel ermitteln ------------------------------------------
$issues = [System.Collections.Generic.List[string]]::new()

foreach ($cf in $cultureFiles) {
    $cultureKeys = $fileKeys[$cf.Name]
    $missing = $baseKeys | Where-Object { $_ -notin $cultureKeys }
    $extra   = $cultureKeys | Where-Object { $_ -notin $baseKeys }

    foreach ($m in $missing) {
        $issues.Add("  FEHLT in $($cf.Name): $m")
    }
    foreach ($e in $extra) {
        $issues.Add("  EXTRA in $($cf.Name) (nicht in $($baseName.Name)): $e")
    }
}

# ---- Ausgabe ---------------------------------------------------------------
if ($issues.Count -gt 0) {
    $msg = @"
⚠️  Übersetzungsprüfung für Verzeichnis: $dir

Die folgenden Ressourcenschlüssel sind nicht in allen Sprachdateien vorhanden:

$($issues -join "`n")

Bitte ergänze die fehlenden Schlüssel in den jeweiligen .resx-Dateien.
"@
    # Als Claude-Hook-Antwort: Blockieren mit Fehlermeldung
    @{
        decision = "block"
        reason   = $msg
    } | ConvertTo-Json -Compress
    exit 2
}

# Alles in Ordnung – Hook gibt grünes Licht
@{
    decision = "approve"
    reason   = "Alle Ressourcenschlüssel sind in allen Sprachdateien vorhanden."
} | ConvertTo-Json -Compress
exit 0
