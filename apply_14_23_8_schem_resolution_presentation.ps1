# KMC 14.23.8 - higher resolution depth raster and smoother SCHEM presentation
# Apply only after 14.23.7, before pushing. Changes limited to SCHEM source and tests.
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$paths = @('KMC.Plugin/Topology/VesselTopologyBuilder.cs','KMC.MissionControl/Pages/SchematicPage.cs','Tools/NavigationTests/Program.cs')
foreach ($p in $paths) { if (-not (Test-Path (Join-Path $root $p))) { throw "Missing $p. Run from KMC repository root." } }
$head = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne '1a574b6723b2f99f1aa5c587d6f368075ad74ef9') { throw "Unexpected HEAD: $head. This patch expects frozen 14.23.4c with local 14.23.7." }
$changes = @{}
foreach ($p in $paths) { $changes[$p] = [IO.File]::ReadAllText((Join-Path $root $p)) }
function Replace-Once([string]$path,[string]$old,[string]$new) {
  $src = $changes[$path]
  $i = $src.IndexOf($old,[StringComparison]::Ordinal)
  if ($i -lt 0 -or $src.IndexOf($old,$i+$old.Length,[StringComparison]::Ordinal) -ge 0) { throw "Missing or repeated 14.23.7 anchor in $path : $($old.Substring(0,[Math]::Min(65,$old.Length)))" }
  $changes[$path] = $src.Substring(0,$i) + $new + $src.Substring($i+$old.Length)
}
Replace-Once 'KMC.Plugin/Topology/VesselTopologyBuilder.cs' @'
            const int width = 160, height = 256;
'@ @'
            // Higher raster resolution improves diagonals while preserving v5 transport.
            // Keep within codec width <= 256 and height <= 384.
            const int width = 192, height = 320;
'@
Replace-Once 'KMC.MissionControl/Pages/SchematicPage.cs' @'
            Rectangle drawing = Rectangle.FromLTRB(bounds.Left + 34,
                bounds.Top + PanelHeaderHeight + 24,
                bounds.Right - 34, bounds.Bottom - 30);
            if (drawing.Width <= 0 || drawing.Height <= 0) return false;
            double realW = topology.SchematicMaxH - topology.SchematicMinH;
            double realH = topology.SchematicMaxV - topology.SchematicMinV;
            if (!(realW > 0) || !(realH > 0)) return false;
            double scale = Math.Min(drawing.Width / realW, drawing.Height / realH);
            float pixelW = (float)(scale * realW / (width - 1));
            float pixelH = (float)(scale * realH / (height - 1));
            float left = drawing.Left + (drawing.Width - (float)(realW * scale)) * 0.5f;
            float top = drawing.Top + (drawing.Height - (float)(realH * scale)) * 0.5f;
            using (SolidBrush face = new SolidBrush(Color.FromArgb(55, context.PhosphorColor)))
            using (SolidBrush line = new SolidBrush(Color.FromArgb(235, context.PhosphorColor)))
            {
                for (int y = 0; y < height; y++)
                {
                    int x = 0;
                    while (x < width)
                    {
                        byte value = pixels[y * width + x];
                        if (value == 0) { x++; continue; }
                        int start = x++;
                        while (x < width && pixels[y * width + x] == value) x++;
                        context.Graphics.FillRectangle(value == 2 ? line : face,
                            left + start * pixelW, top + y * pixelH,
                            Math.Max(0.2f, (x-start) * pixelW), Math.Max(0.2f, pixelH));
                    }
                }
            }
'@ @'
            // Use slightly more of the page height. No crop/overzoom.
            Rectangle drawing = Rectangle.FromLTRB(bounds.Left + 22,
                bounds.Top + PanelHeaderHeight + 12,
                bounds.Right - 22, bounds.Bottom - 12);
            if (drawing.Width <= 0 || drawing.Height <= 0) return false;
            double realW = topology.SchematicMaxH - topology.SchematicMinH;
            double realH = topology.SchematicMaxV - topology.SchematicMinV;
            if (!(realW > 0) || !(realH > 0)) return false;
            double scale = Math.Min(drawing.Width / realW, drawing.Height / realH);
            float imageW = (float)(realW * scale);
            float imageH = (float)(realH * scale);
            float left = drawing.Left + (drawing.Width - imageW) * 0.5f;
            float top = drawing.Top + (drawing.Height - imageH) * 0.5f;

            // Build an ARGB surface once per received raster draw, then let GDI+
            // interpolate the *whole* raster instead of growing individual pixels.
            // Empty pixels remain transparent and the panel background shows through.
            using (Bitmap bitmap = new Bitmap(width, height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                Color phosphor = context.PhosphorColor;
                int face = Color.FromArgb(55, phosphor).ToArgb();
                int line = Color.FromArgb(235, phosphor).ToArgb();
                int[] argb = new int[width * height];
                for (int i = 0; i < argb.Length; i++)
                    argb[i] = pixels[i] == 2 ? line : pixels[i] == 1 ? face : 0;
                System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    if (data.Stride != width * 4)
                        throw new InvalidOperationException("Unexpected schematic bitmap stride.");
                    System.Runtime.InteropServices.Marshal.Copy(argb, 0,
                        data.Scan0, argb.Length);
                }
                finally { bitmap.UnlockBits(data); }
                System.Drawing.Drawing2D.InterpolationMode previousInterpolation =
                    context.Graphics.InterpolationMode;
                System.Drawing.Drawing2D.PixelOffsetMode previousOffset =
                    context.Graphics.PixelOffsetMode;
                try
                {
                    context.Graphics.InterpolationMode =
                        System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    context.Graphics.PixelOffsetMode =
                        System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    context.Graphics.DrawImage(bitmap,
                        new RectangleF(left, top, imageW, imageH),
                        new RectangleF(0, 0, width, height), GraphicsUnit.Pixel);
                }
                finally
                {
                    context.Graphics.InterpolationMode = previousInterpolation;
                    context.Graphics.PixelOffsetMode = previousOffset;
                }
            }
'@
Replace-Once 'Tools/NavigationTests/Program.cs' @'
        Run("SCHEM v5 depth-resolved raster transport", SchematicRasterTransportRegression);
'@ @'
        Run("SCHEM v5 depth-resolved raster transport", SchematicRasterTransportRegression);
        Run("SCHEM higher-resolution exterior raster presentation", SchematicResolutionRegression);
'@
Replace-Once 'Tools/NavigationTests/Program.cs' @'
    private static void SchematicRasterTransportRegression()
'@ @'
    private static void SchematicResolutionRegression()
    {
        string plugin = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.Plugin", "Topology", "VesselTopologyBuilder.cs"));
        string page = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.MissionControl", "Pages", "SchematicPage.cs"));
        Check(plugin.Contains("const int width = 192, height = 320;"),
            "SCHEM raster dimensions regressed");
        Check(page.Contains("InterpolationMode.HighQualityBilinear") &&
              page.Contains("DrawImage(bitmap") &&
              page.Contains("context.Graphics.InterpolationMode = previousInterpolation"),
            "SCHEM bitmap smoothing or graphics-state restoration missing");
    }

    private static void SchematicRasterTransportRegression()
'@
# No writes until all anchors have passed. Preserve backups in a dedicated folder.
$backup = Join-Path $root ('patch_backups/14.23.8_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
[IO.Directory]::CreateDirectory($backup) | Out-Null
$staged = @{}
try {
  foreach ($p in $paths) {
    $source = Join-Path $root $p
    $targetBackup = Join-Path $backup ($p.Replace('/','__'))
    [IO.File]::Copy($source,$targetBackup,$true)
    $temp = $source + '.kmc14238.tmp'
    [IO.File]::WriteAllText($temp,$changes[$p],(New-Object System.Text.UTF8Encoding($false)))
    $staged[$p] = $temp
  }
  $written = @()
  try {
    foreach ($p in $paths) {
      [IO.File]::Copy($staged[$p],(Join-Path $root $p),$true)
      $written += $p
    }
  } catch {
    foreach ($p in $written) { [IO.File]::Copy((Join-Path $backup ($p.Replace('/','__'))),(Join-Path $root $p),$true) }
    throw
  }
} finally {
  foreach ($temp in $staged.Values) { if (Test-Path $temp) { Remove-Item $temp -Force } }
}
Write-Host 'KMC 14.23.8 applied: 192x320 depth raster, bitmap smoothing, slightly larger schematic.'
Write-Host "Backups: $backup"
Write-Host 'Run NavigationTests. Rebuild KMC.Plugin.dll and MissionControl; replace plugin DLL in KSP.'
Write-Warning 'Experimental local build. Do not push until runtime comparison and tests pass.'
