$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$page = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
$planner = Join-Path $root 'KMC.Engine\Navigation\MoonTransferPlanner.cs'
if (!(Test-Path $page) -or !(Test-Path $planner)) { throw 'Run from the KMC repository root; source files not found.' }
$source = [IO.File]::ReadAllText($page)
$engine = [IO.File]::ReadAllText($planner)
if (!$engine.Contains('LambertAndSoiMilliseconds') -or !$engine.Contains('CorrectionMilliseconds') -or !$engine.Contains('TotalPlanningMilliseconds')) { throw '14.24.8 Engine timing fields are absent. Apply 14.24.8 first.' }
if ($source.Contains('SOLVER PERFORMANCE / 14.24.8a')) { throw '14.24.8a is already applied. No changes made.' }
$anchor = @'
                    context.Graphics.DrawString("DEPART IN " +
                        FormatTransferInterval(Math.Max(0,
                            c.Injection.DepartureUniversalTimeSeconds -
                            packet.UniversalTimeSeconds)),
                        context.SmallFont, dim, x, y); y += lineHeight;
'@
$addition = @'
                    // SOLVER PERFORMANCE / 14.24.8a — completed-plan timings.
                    // Values are computed in KMC.Engine; drawing never solves.
                    context.Graphics.DrawString("SOLVER PERFORMANCE",
                        context.SmallFont, dim, x, y); y += lineHeight;
                    context.Graphics.DrawString("LAMBERT + SOI  " +
                        _moonPreview.LambertAndSoiMilliseconds.ToString() + " ms",
                        context.SmallFont, dim, x, y); y += lineHeight;
                    context.Graphics.DrawString("PE CORRECTION  " +
                        _moonPreview.CorrectionMilliseconds.ToString() + " ms",
                        context.SmallFont, dim, x, y); y += lineHeight;
                    context.Graphics.DrawString("TOTAL  " +
                        _moonPreview.TotalPlanningMilliseconds.ToString() + " ms",
                        context.SmallFont, dim, x, y); y += lineHeight;
'@
$start = $source.IndexOf('        private void DrawMoonTransferPreview(')
$end = $source.IndexOf('        private void StartMoonTransferCalculation()', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Expected moon preview method not found. No changes made.' }
$method = $source.Substring($start, $end - $start)
if (!$method.Contains($anchor)) { throw 'Expected departure-time drawing anchor not found. No changes made.' }
if ($method.Split(@($anchor), [StringSplitOptions]::None).Length -ne 2) { throw 'Ambiguous drawing anchor. No changes made.' }
$method = $method.Replace($anchor, $anchor + [Environment]::NewLine + $addition)
$patched = $source.Substring(0, $start) + $method + $source.Substring($end)
$backupFolder = Join-Path $env:TEMP ('KMC_14_24_8a_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
Copy-Item $page (Join-Path $backupFolder 'MapPage.cs')
[IO.File]::WriteAllText($page, $patched, (New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.8a Moon Solver Timing UI applied (Mission Control only).'
Write-Host "Backup: $backupFolder"
Write-Host 'No KSP game-folder DLL replacement required. Build Mission Control and run NavigationTests.'
