<#
  End-to-End-Abgleich: Excel (Vertriebsversion, Reiter "Betriebskosten") gegen die laufende Web-App.
  Für jedes Szenario werden dieselben Eingaben in Excel eingetragen und per POST /api/calculate
  an die Web-App geschickt; anschließend werden Zeile 34–37 je Lok verglichen.

  Voraussetzung: Web-App läuft (dotnet run --project src/TcoMvp.Web --urls http://localhost:5080)
  Aufruf:        powershell -ExecutionPolicy Bypass -File tools\compare-excel-webapp.ps1
#>
param(
    [string]$Workbook = (Join-Path $PSScriptRoot "..\excel\Traxx Shunter_TCO model_26.08._NR_Vertrieb 05.10.2026.xlsm"),
    [string]$BaseUrl = "http://localhost:5080",
    [string]$Report = (Join-Path $PSScriptRoot "..\reports\excel-vs-webapp.csv"),
    [int]$RandomScenarios = 30,
    [int]$Seed = 2026,
    [double]$Tolerance = 0.005   # Euro
)
$ErrorActionPreference = "Stop"
$rng = New-Object System.Random $Seed
function Rnd([double]$min, [double]$max, [int]$digits = 2) { [math]::Round($min + $rng.NextDouble() * ($max - $min), $digits) }

$locoCols = [ordered]@{ "g1206" = "G"; "de18-3a" = "H"; "de18-5sh" = "I"; "vdm" = "J"; "traxx" = "K" }
$rows = [ordered]@{ rentAndMaintenance = 34; energyCost = 35; totalOperatingCost = 36; differenceToReference = 37 }

$defaults = Invoke-RestMethod "$BaseUrl/api/model" | Select-Object -ExpandProperty defaults
function Copy-Defaults { $defaults | ConvertTo-Json -Depth 10 | ConvertFrom-Json }

$scenarios = @()
$s = Copy-Defaults; $scenarios += ,@("Standardannahmen", $s)
$s = Copy-Defaults; $s.operatingHoursPerYear = 2000; $scenarios += ,@("Tabellentreffer 2000 h", $s)
$s = Copy-Defaults; $s.mileageKmPerYear = 50000; $s.operatingHoursPerYear = 2000; $s.electricShare = 0.5; $scenarios += ,@("VDM-Matrixtreffer", $s)
$s = Copy-Defaults; $s.electricShare = 0; $scenarios += ,@("Ohne Elektrifizierung", $s)
$s = Copy-Defaults; $s.electricShare = 1; $scenarios += ,@("Voll elektrisch", $s)
$s = Copy-Defaults; $s.mileageKmPerYear = 130000; $scenarios += ,@("Traxx-Tabelle überschritten", $s)
for ($i = 1; $i -le $RandomScenarios; $i++) {
    $s = Copy-Defaults
    $s.mileageKmPerYear = [math]::Round((Rnd 5000 120000 0) / 500) * 500
    $s.operatingHoursPerYear = [math]::Round((Rnd 500 5500 0) / 50) * 50
    $s.rollingShare = Rnd 0.1 0.9; $s.electricShare = Rnd 0 1; $s.shuntingShare = Rnd 0 1
    $s.dieselPricePerLiter = Rnd 0.8 2.5; $s.electricityPricePerKwh = Rnd 0.05 0.4 3
    foreach ($id in $locoCols.Keys) {
        $l = $s.locomotives.$id
        if ($l.PSObject.Properties.Name -contains "startStopShare") { $l.startStopShare = Rnd 0 0.6 }
        $l.dieselShuntingLitersPerKm = Rnd 3 8 1; $l.dieselMainlineLitersPerKm = Rnd 2 5 1; $l.dieselParkedLitersPerHour = Rnd 5 15 1
        if ($l.PSObject.Properties.Name -contains "kwhPerKm") { $l.kwhPerKm = Rnd 10 20 1; $l.kwhPerHourParked = Rnd 20 40 0 }
    }
    $scenarios += ,@("Zufall $i", $s)
}

$tmp = Join-Path $env:TEMP ("tco-compare-" + [guid]::NewGuid() + ".xlsm")
Copy-Item -LiteralPath $Workbook -Destination $tmp
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false; $excel.DisplayAlerts = $false; $excel.AutomationSecurity = 3
$out = New-Object System.Collections.Generic.List[object]
try {
    $wb = $excel.Workbooks.Open($tmp)
    $bk = $wb.Worksheets.Item("Betriebskosten")
    foreach ($pair in $scenarios) {
        $name = $pair[0]; $in = $pair[1]
        $bk.Range("F7").Value2 = [double]$in.mileageKmPerYear
        $bk.Range("F8").Value2 = [double]$in.operatingHoursPerYear
        $bk.Range("F9").Value2 = [double]$in.rollingShare
        $bk.Range("F14").Value2 = [double]$in.electricShare
        $bk.Range("F17").Value2 = [double]$in.shuntingShare
        $bk.Range("F29").Value2 = [double]$in.dieselPricePerLiter
        $bk.Range("F30").Value2 = [double]$in.electricityPricePerKwh
        foreach ($id in $locoCols.Keys) {
            $c = $locoCols[$id]; $l = $in.locomotives.$id; $p = $l.PSObject.Properties.Name
            if ($p -contains "startStopShare") { $bk.Range("${c}12").Value2 = [double]$l.startStopShare }
            $bk.Range("${c}22").Value2 = [double]$l.dieselShuntingLitersPerKm
            $bk.Range("${c}23").Value2 = [double]$l.dieselMainlineLitersPerKm
            $bk.Range("${c}24").Value2 = [double]$l.dieselParkedLitersPerHour
            if ($p -contains "kwhPerKm") { $bk.Range("${c}26").Value2 = [double]$l.kwhPerKm; $bk.Range("${c}27").Value2 = [double]$l.kwhPerHourParked }
        }
        $excel.CalculateFull()

        $web = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/calculate" -ContentType "application/json" `
            -Body ([Text.Encoding]::UTF8.GetBytes(($in | ConvertTo-Json -Depth 10)))

        foreach ($id in $locoCols.Keys) {
            $w = $web.locomotives | Where-Object { $_.id -eq $id }
            foreach ($key in $rows.Keys) {
                if ($id -eq "traxx" -and $key -eq "differenceToReference") { continue }
                $xv = $bk.Range("$($locoCols[$id])$($rows[$key])").Value2
                $xl = if ($xv -is [double]) { $xv } else { $null }   # #NV kommt als Int32-Fehlercode
                $wv = $w.$key
                $ok = if ($null -eq $xl) { $null -eq $wv } elseif ($null -eq $wv) { $false } else { [math]::Abs($xl - $wv) -le $Tolerance }
                $out.Add([pscustomobject]@{
                    Szenario = $name; Lok = $id; Zeile = $key
                    Excel = if ($null -eq $xl) { "#NV" } else { [math]::Round($xl, 4) }
                    WebApp = if ($null -eq $wv) { "n. v." } else { [math]::Round([double]$wv, 4) }
                    Abweichung = if ($null -ne $xl -and $null -ne $wv) { [math]::Abs($xl - $wv) } else { 0 }
                    OK = $ok
                })
            }
        }
    }
    $wb.Close($false)
}
finally {
    $excel.Quit(); [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path (Split-Path $Report) | Out-Null
$out | Export-Csv -Path $Report -NoTypeInformation -Delimiter ";" -Encoding UTF8

$fail = @($out | Where-Object { -not $_.OK })
Write-Host ""
Write-Host "Szenarien: $($scenarios.Count)   Vergleiche: $($out.Count)   Abweichungen > $Tolerance EUR: $($fail.Count)"
Write-Host ("Größte Abweichung: {0:E2} EUR" -f ($out | Measure-Object Abweichung -Maximum).Maximum)
Write-Host "Bericht: $Report"
if ($fail.Count -gt 0) { $fail | Format-Table -AutoSize | Out-String | Write-Host; exit 1 }
