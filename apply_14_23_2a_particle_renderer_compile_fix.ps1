param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

$path = Join-Path $RepoRoot "KMC.Plugin\Topology\VesselTopologyBuilder.cs"

if (-not (Test-Path $path)) {
    throw "Missing VesselTopologyBuilder.cs"
}

$text = [System.IO.File]::ReadAllText($path)

$old = @'
                    if (renderer == null ||
                        renderer is ParticleSystemRenderer ||
                        renderer is LineRenderer ||
                        renderer is TrailRenderer)
                    {
                        continue;
                    }
'@

$new = @'
                    if (renderer == null)
                    {
                        continue;
                    }

                    /*
                     * Avoid hard references to optional Unity split modules
                     * such as UnityEngine.ParticleSystemModule. KSP's managed
                     * assembly set forwards ParticleSystemRenderer there, but
                     * KMC.Plugin does not otherwise need that module.
                     */
                    string rendererTypeName =
                        renderer.GetType().Name;

                    if (string.Equals(
                            rendererTypeName,
                            "ParticleSystemRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            rendererTypeName,
                            "LineRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            rendererTypeName,
                            "TrailRenderer",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
'@

if ($text.Contains($new)) {
    Write-Host "KMC 14.23.2a compile fix is already applied."
    exit 0
}

if (-not $text.Contains($old)) {
    throw "Expected 14.23.2 renderer filter block was not found. No file changed."
}

$text = $text.Replace($old, $new)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.23.2a ParticleSystemRenderer compile fix."
Write-Host "No new Unity assembly reference is required."
