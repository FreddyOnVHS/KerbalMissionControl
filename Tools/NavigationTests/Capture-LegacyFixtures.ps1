# Run with Windows PowerShell 5.1 against an UNMODIFIED 14.22.32 Release build.
# Captured fixture values are committed; do not regenerate against the new planner.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../..").Path
Add-Type -Path "$repo/KMC.MissionControl/bin/Release/KMC.shared.dll"
$assembly = [Reflection.Assembly]::LoadFrom("$repo/KMC.MissionControl/bin/Release/KMC.MissionControl.exe")
$map = $assembly.GetType('KMC.MissionControl.Pages.MapPage')
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$window = $map.GetMethod('TryCalculateTransferWindow', $flags)
$ejection = $map.GetMethod('TryCalculateParkingOrbitEjection', $flags)
if (!$window -or !$ejection) { throw 'Expected unmodified 14.22.32 MapPage methods.' }
$fixtures = @()
for ($index = 0; $index -lt 8; $index++) {
    $mu = 1e12; $r1 = 1e7; $r2 = 1.8e7
    if ($index % 2 -eq 1) { $r1 = 1.8e7; $r2 = 1e7 }
    $origin = [KMC.Shared.OrbitMapBody]::new()
    $origin.Name = 'Origin'; $origin.ParentName = 'Primary'; $origin.GravParameter = 3.5e8
    $destination = [KMC.Shared.OrbitMapBody]::new()
    $destination.Name = 'Destination'; $destination.ParentName = 'Primary'
    foreach ($body in @($origin, $destination)) {
        $a = if ($body -eq $origin) { $r1 } else { $r2 }
        $o = [KMC.Shared.OrbitMapOrbit]::new()
        $o.SemiMajorAxisMeters = $a
        $o.PeriodSeconds = 2 * [Math]::PI * [Math]::Sqrt($a * $a * $a / $mu)
        $o.Eccentricity = 0.02 * $index
        $o.MeanAnomalyAtEpochRadians = 0.23 + 0.4 * $index
        if ($body -eq $destination) { $o.MeanAnomalyAtEpochRadians += 0.8 }
        $o.EpochUniversalTimeSeconds = 1234; $o.InclinationDegrees = 2 * $index
        $o.LongitudeOfAscendingNodeDegrees = 17 * $index; $o.ArgumentOfPeriapsisDegrees = 11 * $index
        $body.Orbit = $o
    }
    $packet = [KMC.Shared.OrbitMapPacket]::new()
    $packet.ReferenceBodyRadiusMeters = 60000; $packet.UniversalTimeSeconds = 6000 + 1000 * $index
    $park = [KMC.Shared.OrbitMapOrbit]::new()
    $park.SemiMajorAxisMeters = 70000 + 1000 * $index; $park.Eccentricity = 0.005 * $index
    $park.InclinationDegrees = $index; $park.MeanAnomalyAtEpochRadians = 0.3 * $index
    $park.EpochUniversalTimeSeconds = 100
    $park.LongitudeOfAscendingNodeDegrees = 3 * $index; $park.ArgumentOfPeriapsisDegrees = 7 * $index
    $packet.ActiveOrbit = $park
    $args1 = [object[]]@($origin, $destination, $packet.UniversalTimeSeconds, $null)
    if (!$window.Invoke($null, $args1)) { throw 'Window failed' }
    $args2 = [object[]]@($packet, $origin, $args1[3], $null)
    if (!$ejection.Invoke($null, $args2)) { throw 'Ejection failed' }
    $row = [ordered]@{Index = $index}
    foreach ($solution in @($args1[3], $args2[3])) {
        foreach ($field in $solution.GetType().GetFields()) { $row[$field.Name] = $field.GetValue($solution) }
    }
    $fixtures += [pscustomobject]$row
}
$fixtures | Export-Csv "$PSScriptRoot/Fixtures/legacy-14.22.32.csv" -NoTypeInformation
$fixtures | Select-Object Index, DepartureUniversalTimeSeconds, EjectionDeltaVMetersPerSecond, BurnUniversalTimeSeconds
