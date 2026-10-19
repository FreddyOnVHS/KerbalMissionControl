# KMC 14.23.6b - Correct the missing DrawSchematicLayer method closure from 14.23.6a.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location).Path 'KMC.MissionControl/Pages/SchematicPage.cs'
if (-not (Test-Path -LiteralPath $path)) { throw "File not found: $path" }
$source = [System.IO.File]::ReadAllText($path)
if (-not $source.Contains('14.23.6a prototype: clip secondary detail')) { throw 'Expected 14.23.6a source marker missing. File was not modified.' }
if (-not $source.Contains('private static void DrawSchematicLayer(')) { throw 'DrawSchematicLayer method missing. File was not modified.' }
$nl = if ($source.Contains("`r`n")) { "`r`n" } else { "`n" }
$old = @'
                }
        }

        private static void DrawPartOutline(
'@.Replace("`n",$nl)
$new = @'
                }
            }
        }

        private static void DrawPartOutline(
'@.Replace("`n",$nl)
$idx = $source.IndexOf($old, [StringComparison]::Ordinal)
if ($idx -lt 0 -or $source.IndexOf($old,$idx+$old.Length,[StringComparison]::Ordinal) -ge 0) {
    throw 'Expected unique broken method closure not found. No changes made.'
}
$updated = $source.Substring(0,$idx) + $new + $source.Substring($idx+$old.Length)
$temp = Join-Path (Split-Path $path) ('.schem-14.23.6b-'+[guid]::NewGuid().ToString('N')+'.tmp')
try {
    [System.IO.File]::WriteAllText($temp,$updated,(New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temp -Destination $path -Force
} finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
Write-Host '14.23.6b applied: restored missing DrawSchematicLayer closing brace.' -ForegroundColor Green
