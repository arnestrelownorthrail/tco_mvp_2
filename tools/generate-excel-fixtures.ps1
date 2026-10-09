<#
  Erzeugt Referenzwerte für die Paritätstests, indem die Vertriebsversion der Excel
  per COM mit zufälligen Eingaben durchgerechnet wird.
  Die Excel wird als Kopie in %TEMP% geöffnet (Makros deaktiviert) und nicht gespeichert.

  Aufruf:  powershell -ExecutionPolicy Bypass -File tools\generate-excel-fixtures.ps1
#>
param(
    [string]$Workbook = (Join-Path $PSScriptRoot "..\excel\Traxx Shunter_TCO model_26.08._Vertriebsversion.xlsm"),
    [string]$Output = (Join-Path $PSScriptRoot "..\tests\TcoMvp.Tests\Fixtures\excel-scenarios.json"),
    [int]$RandomScenarios = 60,
    [int]$Seed = 4711
)
$ErrorActionPreference = "Stop"
$rng = New-Object System.Random $Seed
function Rnd([double]$min, [double]$max, [int]$digits = 2) { [math]::Round($min + $rng.NextDouble() * ($max - $min), $digits) }

$locoCols = [ordered]@{ "g1206" = "G"; "de18-3a" = "H"; "de18-5sh" = "I"; "vdm" = "J"; "traxx" = "K" }
$modelCols = [ordered]@{ "g1206" = "H"; "de18-3a" = "I"; "de18-5sh" = "J"; "vdm" = "K"; "vdm-light" = "L"; "traxx" = "M" }

function New-DefaultScenario($name) {
    [ordered]@{
        name = $name; loadFactor = 0.15
        inputs = [ordered]@{
            mileageKmPerYear = 40000; operatingHoursPerYear = 3000; rollingShare = 0.5; electricShare = 0.7
            shuntingShare = 0.5; dieselPricePerLiter = 1.8; electricityPricePerKwh = 0.15
            locomotives = [ordered]@{
                "g1206"    = [ordered]@{ dieselShuntingLitersPerKm = 4.8; dieselMainlineLitersPerKm = 3.5; dieselParkedLitersPerHour = 13.5 }
                "de18-3a"  = [ordered]@{ startStopShare = 0; dieselShuntingLitersPerKm = 6; dieselMainlineLitersPerKm = 3.5; dieselParkedLitersPerHour = 13 }
                "de18-5sh" = [ordered]@{ startStopShare = 0.5; dieselShuntingLitersPerKm = 5.4; dieselMainlineLitersPerKm = 3.5; dieselParkedLitersPerHour = 13 }
                "vdm"      = [ordered]@{ dieselShuntingLitersPerKm = 6.5; dieselMainlineLitersPerKm = 3.5; dieselParkedLitersPerHour = 13; kwhPerKm = 16; kwhPerHourParked = 30 }
                "traxx"    = [ordered]@{ startStopShare = 0.1; dieselShuntingLitersPerKm = 4.9; dieselMainlineLitersPerKm = 3.5; dieselParkedLitersPerHour = 7; kwhPerKm = 13; kwhPerHourParked = 30 }
            }
        }
    }
}

$scenarios = @()
$scenarios += New-DefaultScenario "Standardannahmen"

# Gezielte Fälle: exakte Tabellentreffer, Load factor, Bereichsgrenzen
$s = New-DefaultScenario "G1206/DE18 exakter Tabellentreffer 2000 h"; $s.inputs.operatingHoursPerYear = 2000; $scenarios += $s
$s = New-DefaultScenario "VDM exakter Matrixtreffer 50000 km / 1000 h"; $s.inputs.mileageKmPerYear = 50000; $s.inputs.operatingHoursPerYear = 2000; $s.inputs.electricShare = 0.5; $scenarios += $s
$s = New-DefaultScenario "Load factor 20 %"; $s.loadFactor = 0.2; $s.inputs.operatingHoursPerYear = 5000; $s.inputs.electricShare = 0.2; $scenarios += $s
$s = New-DefaultScenario "Load factor 25 %"; $s.loadFactor = 0.25; $s.inputs.operatingHoursPerYear = 5000; $s.inputs.electricShare = 0.2; $scenarios += $s
$s = New-DefaultScenario "Ohne Elektrifizierung"; $s.inputs.electricShare = 0; $scenarios += $s
$s = New-DefaultScenario "Voll elektrisch"; $s.inputs.electricShare = 1; $scenarios += $s
$s = New-DefaultScenario "Laufleistung außerhalb Traxx-Tabelle"; $s.inputs.mileageKmPerYear = 130000; $scenarios += $s
$s = New-DefaultScenario "Dieselstunden außerhalb Traxx-Tabelle"; $s.inputs.operatingHoursPerYear = 8000; $s.inputs.electricShare = 0; $s.inputs.locomotives.traxx.startStopShare = 0; $scenarios += $s
$s = New-DefaultScenario "Kleine Laufleistung"; $s.inputs.mileageKmPerYear = 5000; $s.inputs.operatingHoursPerYear = 800; $scenarios += $s

# Grenzfälle: Traxx-Preisbänder (XLOOKUP exakt / nächstgrößer), Nullwerte, extreme Anteile
foreach ($km in 37500, 37501, 42499, 42500, 77500, 77501, 122500, 122501) {
    $s = New-DefaultScenario "Bandgrenze $km km"; $s.inputs.mileageKmPerYear = $km; $scenarios += $s
}
$s = New-DefaultScenario "Null km und Null Stunden"; $s.inputs.mileageKmPerYear = 0; $s.inputs.operatingHoursPerYear = 0; $scenarios += $s
$s = New-DefaultScenario "Nur rollend"; $s.inputs.rollingShare = 1; $scenarios += $s
$s = New-DefaultScenario "Nur parkend"; $s.inputs.rollingShare = 0; $scenarios += $s
$s = New-DefaultScenario "Nur Rangieren"; $s.inputs.shuntingShare = 1; $scenarios += $s
$s = New-DefaultScenario "Nur Streckenfahrt"; $s.inputs.shuntingShare = 0; $scenarios += $s
$s = New-DefaultScenario "Energiepreise 0"; $s.inputs.dieselPricePerLiter = 0; $s.inputs.electricityPricePerKwh = 0; $scenarios += $s
$s = New-DefaultScenario "Start-Stop 100 % (negative Dieselparkstunden)"
$s.inputs.locomotives."de18-3a".startStopShare = 1; $s.inputs.locomotives."de18-5sh".startStopShare = 1; $s.inputs.locomotives.traxx.startStopShare = 1; $scenarios += $s
$s = New-DefaultScenario "Traxx knapp unter 3000 Wartungsstunden"; $s.inputs.electricShare = 0; $s.inputs.operatingHoursPerYear = 4000; $s.inputs.locomotives.traxx.startStopShare = 0; $scenarios += $s
$s = New-DefaultScenario "Traxx knapp über 3000 Wartungsstunden"; $s.inputs.electricShare = 0; $s.inputs.operatingHoursPerYear = 4100; $s.inputs.locomotives.traxx.startStopShare = 0; $scenarios += $s
$s = New-DefaultScenario "G1206 Tabellenwerte 2500 h"; $s.inputs.operatingHoursPerYear = 2500; $scenarios += $s
$s = New-DefaultScenario "G1206 Tabellenwerte 4000 h"; $s.inputs.operatingHoursPerYear = 4000; $scenarios += $s
$s = New-DefaultScenario "Sehr hohe Stunden 8760"; $s.inputs.operatingHoursPerYear = 8760; $scenarios += $s

for ($i = 1; $i -le $RandomScenarios; $i++) {
    $s = New-DefaultScenario "Zufall $i"
    $in = $s.inputs
    $in.mileageKmPerYear = [math]::Round((Rnd 5000 125000 0) / 500) * 500
    $in.operatingHoursPerYear = [math]::Round((Rnd 500 6000 0) / 50) * 50
    $in.rollingShare = Rnd 0.1 0.9
    $in.electricShare = Rnd 0 1
    $in.shuntingShare = Rnd 0 1
    $in.dieselPricePerLiter = Rnd 0.8 2.5
    $in.electricityPricePerKwh = Rnd 0.05 0.4 3
    $in.locomotives."de18-3a".startStopShare = Rnd 0 0.6
    $in.locomotives."de18-5sh".startStopShare = Rnd 0 0.8
    $in.locomotives.traxx.startStopShare = Rnd 0 0.5
    foreach ($id in $locoCols.Keys) {
        $l = $in.locomotives.$id
        $l.dieselShuntingLitersPerKm = Rnd 3 8 1
        $l.dieselMainlineLitersPerKm = Rnd 2 5 1
        $l.dieselParkedLitersPerHour = Rnd 5 15 1
        if ($l.Contains("kwhPerKm")) { $l.kwhPerKm = Rnd 10 20 1; $l.kwhPerHourParked = Rnd 20 40 0 }
    }
    $s.loadFactor = @(0.15, 0.15, 0.2, 0.25)[$rng.Next(4)]
    $scenarios += $s
}

$tmp = Join-Path $env:TEMP ("tco-fixture-" + [guid]::NewGuid() + ".xlsm")
Copy-Item -LiteralPath $Workbook -Destination $tmp
$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$excel.AutomationSecurity = 3   # msoAutomationSecurityForceDisable
try {
    $wb = $excel.Workbooks.Open($tmp)
    $bk = $wb.Worksheets.Item("Betriebskosten")
    $tm = $wb.Worksheets.Item("TCO Model")
    function V($ws, $addr) { $v = $ws.Range($addr).Value2; if ($v -is [int]) { if ($v -lt 0) { return $null } }; if ($v -is [double]) { return $v } ; return $null }

    foreach ($s in $scenarios) {
        $in = $s.inputs
        $bk.Range("F7").Value2 = [double]$in.mileageKmPerYear
        $bk.Range("F8").Value2 = [double]$in.operatingHoursPerYear
        $bk.Range("F9").Value2 = [double]$in.rollingShare
        $bk.Range("F14").Value2 = [double]$in.electricShare
        $bk.Range("F17").Value2 = [double]$in.shuntingShare
        $bk.Range("F29").Value2 = [double]$in.dieselPricePerLiter
        $bk.Range("F30").Value2 = [double]$in.electricityPricePerKwh
        foreach ($id in $locoCols.Keys) {
            $c = $locoCols[$id]; $l = $in.locomotives.$id
            if ($l.Contains("startStopShare")) { $bk.Range("${c}12").Value2 = [double]$l.startStopShare }
            $bk.Range("${c}22").Value2 = [double]$l.dieselShuntingLitersPerKm
            $bk.Range("${c}23").Value2 = [double]$l.dieselMainlineLitersPerKm
            $bk.Range("${c}24").Value2 = [double]$l.dieselParkedLitersPerHour
            if ($l.Contains("kwhPerKm")) { $bk.Range("${c}26").Value2 = [double]$l.kwhPerKm; $bk.Range("${c}27").Value2 = [double]$l.kwhPerHourParked }
        }
        $tm.Range("M41").Value2 = [double]$s.loadFactor
        $excel.CalculateFull()

        $customer = [ordered]@{}
        foreach ($id in $locoCols.Keys) {
            $c = $locoCols[$id]
            $customer[$id] = [ordered]@{
                rentAndMaintenance = V $bk "${c}34"; energyCost = V $bk "${c}35"
                totalOperatingCost = V $bk "${c}36"; differenceToReference = V $bk "${c}37"
            }
        }
        $model = [ordered]@{}
        foreach ($id in $modelCols.Keys) {
            $c = $modelCols[$id]
            $model[$id] = [ordered]@{
                dieselKm = V $tm "${c}65"; electricKm = V $tm "${c}68"
                dieselRollingHours = V $tm "${c}71"; dieselParkingHours = V $tm "${c}75"; electricParkingHours = V $tm "${c}77"
                vehicleCost = V $tm "${c}90"; dieselCost = V $tm "${c}97"; electricityCost = V $tm "${c}107"; energyCost = V $tm "${c}96"
                maintenanceHours = V $tm "${c}116"; maintenanceCost = V $tm "${c}115"; totalCost = V $tm "${c}123"
            }
        }
        $s.expectedCustomer = $customer
        $s.expectedModel = $model
        Write-Host ("{0,-45} Traxx gesamt: {1}" -f $s.name, $customer.traxx.totalOperatingCost)
    }
    $wb.Close($false)
}
finally {
    $excel.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null
$scenarios | ConvertTo-Json -Depth 10 | Out-File -FilePath $Output -Encoding utf8
Write-Host "Geschrieben: $Output ($($scenarios.Count) Szenarien)"
