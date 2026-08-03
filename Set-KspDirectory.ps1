param(
    [Parameter(Mandatory = $true)]
    [string]$KspFolder
)

$assemblyPath = Join-Path $KspFolder "KSP_x64_Data\Managed\Assembly-CSharp.dll"

if (-not (Test-Path $assemblyPath)) {
    throw "Assembly-CSharp.dll was not found under: $KspFolder"
}

[Environment]::SetEnvironmentVariable(
    "KSP_DIR",
    $KspFolder,
    "User"
)

Write-Host "KSP_DIR was set to:"
Write-Host $KspFolder
Write-Host ""
Write-Host "Close and reopen Visual Studio before rebuilding."
