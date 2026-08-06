param(
    [string]$ProjectPath = ".\KMC.MissionControl.csproj"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

$project = Get-Content -LiteralPath $ProjectPath -Raw

$entries = @(
'    <Compile Include="Capabilities\CapabilityEnums.cs" />',
'    <Compile Include="Capabilities\CapabilityModels.cs" />',
'    <Compile Include="Capabilities\ResourceClassifier.cs" />',
'    <Compile Include="Capabilities\PartCapabilityClassifier.cs" />',
'    <Compile Include="Capabilities\VesselCapabilityBuilder.cs" />',
'    <Compile Include="Debugging\Capabilities\CapabilityDebuggerHost.cs" />',
'    <Compile Include="Debugging\Capabilities\CapabilityDebuggerForm.cs"><SubType>Form</SubType></Compile>'
)

$missing = @()

foreach ($entry in $entries) {
    $include = [regex]::Match(
        $entry,
        'Compile Include="([^"]+)"'
    ).Groups[1].Value

    $escapedInclude = [regex]::Escape($include)

    if ($project -notmatch ('<Compile Include="' + $escapedInclude + '"')) {
        $missing += $entry
    }
}

if ($missing.Count -eq 0) {
    Write-Host "All capability files are already included in the project."
    exit 0
}

$anchor = '    <Compile Include="Debugging\Electrical\ElectricalTopologyDebuggerHost.cs" />'

if (-not $project.Contains($anchor)) {
    throw "Could not find the expected Debugging/Electrical compile anchor."
}

$insertion = $anchor + [Environment]::NewLine +
             ($missing -join [Environment]::NewLine)

$project = $project.Replace(
    $anchor,
    $insertion
)

$backup = $ProjectPath + ".before-capability-fix.bak"
Copy-Item -LiteralPath $ProjectPath -Destination $backup -Force

Set-Content `
    -LiteralPath $ProjectPath `
    -Value $project `
    -Encoding UTF8

Write-Host ""
Write-Host "Capability project integration completed."
Write-Host "Backup created:"
Write-Host "  $backup"
Write-Host ""
Write-Host "Added compile entries:"
foreach ($entry in $missing) {
    Write-Host "  $entry"
}
