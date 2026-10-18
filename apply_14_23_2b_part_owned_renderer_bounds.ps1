param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

$path = Join-Path $RepoRoot "KMC.Plugin\Topology\VesselTopologyBuilder.cs"

if (-not (Test-Path $path)) {
    throw "Missing VesselTopologyBuilder.cs"
}

$text = [System.IO.File]::ReadAllText($path)

$old = @'
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

                    Bounds bounds =
                        renderer.bounds;
'@

$new = @'
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

                    /*
                     * GetComponentsInChildren<Renderer>() on a Part transform
                     * can include renderer hierarchies belonging to attached
                     * child Parts. Only measure renderers whose nearest Part
                     * ancestor is the Part currently being sampled.
                     */
                    Part rendererOwner =
                        renderer.GetComponentInParent<Part>();

                    if (rendererOwner != part)
                    {
                        continue;
                    }

                    Bounds bounds =
                        renderer.bounds;
'@

if ($text.Contains($new)) {
    Write-Host "KMC 14.23.2b part-owned renderer bounds fix is already applied."
    exit 0
}

if (-not $text.Contains($old)) {
    throw "Expected 14.23.2a renderer block was not found. No file changed."
}

$text = $text.Replace($old, $new)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.23.2b Part-Owned Renderer Bounds."
Write-Host "Each Part now measures only renderer geometry owned by that Part."
