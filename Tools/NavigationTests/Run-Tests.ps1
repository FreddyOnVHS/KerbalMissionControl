param([string]$MSBuildPath)
$ErrorActionPreference = 'Stop'
if (!$MSBuildPath) {
    $vswhere = "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe"
    if (Test-Path $vswhere) {
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
}
if (!$MSBuildPath -or !(Test-Path $MSBuildPath)) { throw 'Pass -MSBuildPath with the Visual Studio MSBuild.exe location.' }

& $MSBuildPath "$PSScriptRoot/NavigationTests.csproj" /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot/bin/Release/NavigationTests.exe"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $MSBuildPath "$PSScriptRoot/TransferSearchTests.csproj" /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot/bin/Release/TransferSearchTests.exe"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $MSBuildPath "$PSScriptRoot/LambertEjectionTests.csproj" /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot/bin/Release/LambertEjectionTests.exe"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $MSBuildPath "$PSScriptRoot/ParkingAwareSearchTests.csproj" /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot/bin/Release/ParkingAwareSearchTests.exe"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $MSBuildPath "$PSScriptRoot/StateVectorPropagationTests.csproj" /p:Configuration=Release /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "$PSScriptRoot/bin/Release/StateVectorPropagationTests.exe"
exit $LASTEXITCODE
