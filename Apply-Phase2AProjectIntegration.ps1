param(
    [string]$RepositoryRoot = "."
)

$ErrorActionPreference = "Stop"

$pluginProject =
    Join-Path $RepositoryRoot "KMC.Plugin\KMC.Plugin.csproj"

$missionProject =
    Join-Path $RepositoryRoot "KMC.MissionControl\KMC.MissionControl.csproj"

function AddEntryAfter {
    param(
        [string]$ProjectPath,
        [string]$Anchor,
        [string]$IncludePath,
        [string]$Entry
    )

    $text =
        Get-Content -LiteralPath $ProjectPath -Raw

    if ($text.Contains('Compile Include="' + $IncludePath + '"')) {
        Write-Host "Already included: $IncludePath"
        return
    }

    if (-not $text.Contains($Anchor)) {
        throw "Anchor not found in $ProjectPath : $Anchor"
    }

    $text =
        $text.Replace(
            $Anchor,
            $Anchor +
            [Environment]::NewLine +
            $Entry)

    Set-Content `
        -LiteralPath $ProjectPath `
        -Value $text `
        -Encoding UTF8

    Write-Host "Added: $IncludePath"
}

if (-not (Test-Path $pluginProject)) {
    throw "Plugin project not found: $pluginProject"
}

if (-not (Test-Path $missionProject)) {
    throw "Mission Control project not found: $missionProject"
}

Copy-Item $pluginProject ($pluginProject + ".phase2a.bak") -Force
Copy-Item $missionProject ($missionProject + ".phase2a.bak") -Force

AddEntryAfter `
    $pluginProject `
    '    <Compile Include="Topology\VesselPartClassifier.cs" />' `
    'Topology\VesselModuleDiscoveryAnalyzer.cs' `
    '    <Compile Include="Topology\VesselModuleDiscoveryAnalyzer.cs" />'

$pluginAnchor =
    '    <Compile Include="..\KMC.shared\Topology\VesselResourceState.cs">'

$moduleResourceEntry = @'
    <Compile Include="..\KMC.shared\Topology\VesselModuleResource.cs">
      <Link>Shared\Topology\VesselModuleResource.cs</Link>
    </Compile>
'@

$moduleDescriptorEntry = @'
    <Compile Include="..\KMC.shared\Topology\VesselModuleDescriptor.cs">
      <Link>Shared\Topology\VesselModuleDescriptor.cs</Link>
    </Compile>
'@

AddEntryAfter `
    $pluginProject `
    $pluginAnchor `
    '..\KMC.shared\Topology\VesselModuleResource.cs' `
    $moduleResourceEntry

AddEntryAfter `
    $pluginProject `
    $moduleResourceEntry `
    '..\KMC.shared\Topology\VesselModuleDescriptor.cs' `
    $moduleDescriptorEntry

$missionAnchor =
    '    <Compile Include="..\KMC.shared\Topology\VesselResourceState.cs"><Link>Shared\Topology\VesselResourceState.cs</Link></Compile>'

$missionModuleResource =
    '    <Compile Include="..\KMC.shared\Topology\VesselModuleResource.cs"><Link>Shared\Topology\VesselModuleResource.cs</Link></Compile>'

$missionModuleDescriptor =
    '    <Compile Include="..\KMC.shared\Topology\VesselModuleDescriptor.cs"><Link>Shared\Topology\VesselModuleDescriptor.cs</Link></Compile>'

AddEntryAfter `
    $missionProject `
    $missionAnchor `
    '..\KMC.shared\Topology\VesselModuleResource.cs' `
    $missionModuleResource

AddEntryAfter `
    $missionProject `
    $missionModuleResource `
    '..\KMC.shared\Topology\VesselModuleDescriptor.cs' `
    $missionModuleDescriptor

Write-Host ""
Write-Host "Phase 2A project integration complete."
