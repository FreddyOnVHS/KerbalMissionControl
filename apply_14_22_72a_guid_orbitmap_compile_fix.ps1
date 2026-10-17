param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$path =
    Join-Path $RepoRoot "KMC.Engine\Orbit\VelocityVectorTelemetryModel.cs"

if (-not (Test-Path $path)) {
    throw "VelocityVectorTelemetryModel.cs not found at $path"
}

$text =
    [System.IO.File]::ReadAllText($path)

if (-not $text.Contains("using System.Collections.Generic;")) {
    $needle = "using System;"

    if (-not $text.Contains($needle)) {
        throw "Could not locate using System; in VelocityVectorTelemetryModel.cs"
    }

    $text =
        $text.Replace(
            $needle,
            "using System;`r`nusing System.Collections.Generic;")

    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

Write-Host "Applied KMC 14.22.72a compile fix."
