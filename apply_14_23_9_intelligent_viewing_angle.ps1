# KMC 14.23.9 - stable automatic viewing angle for depth-resolved SCHEM
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$path = 'KMC.Plugin/Topology/VesselTopologyBuilder.cs'
$testPath = 'Tools/NavigationTests/Program.cs'
foreach ($p in @($path,$testPath)) { if (-not (Test-Path (Join-Path $root $p))) { throw "Missing $p; run from repository root." } }
$head = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne '1a574b6723b2f99f1aa5c587d6f368075ad74ef9') { throw "Expected 14.23.4c GitHub HEAD with unpushed 14.23.8 changes; got $head" }
$src = [IO.File]::ReadAllText((Join-Path $root $path))
$test = [IO.File]::ReadAllText((Join-Path $root $testPath))
$start = '        private static void BuildVisibleSideRaster(Vessel vessel, VesselTopology topology)'
$end = '        private static void RasterTriangle(Vector3 a, Vector3 b, Vector3 c, int owner,'
$i = $src.IndexOf($start,[StringComparison]::Ordinal)
$j = $src.IndexOf($end,[StringComparison]::Ordinal)
if ($i -lt 0 -or $j -le $i -or $src.IndexOf($start,$i+1,[StringComparison]::Ordinal) -ge 0) { throw 'Expected unique 14.23.8 raster method not found' }
$old = $src.Substring($i,$j-$i)
if (-not $old.Contains('const int width = 192, height = 320;') -or -not $old.Contains('RasterTriangle(points[a], points[b], points[c],') -or $src.Contains('ChooseSchematicViewingAngle')) { throw 'Wrong or already modified SCHEM raster version; no changes made' }
$newRaster = @'
        // 14.23.9: Choose a stable view from vessel-relative part geometry.
        // Solid boosters are prioritized so paired radial stages do not overlap.
        // This is a structural heuristic, not full multiview visibility scoring.
        private static double ChooseSchematicViewingAngle(VesselTopology topology)
        {
            if (topology == null || topology.Nodes == null || topology.Nodes.Count == 0)
                return 0.0;
            double centerX = 0.0, centerZ = 0.0;
            int count = 0;
            foreach (VesselTopologyNode node in topology.Nodes)
            {
                if (double.IsNaN(node.VesselX) || double.IsNaN(node.VesselZ) ||
                    double.IsInfinity(node.VesselX) || double.IsInfinity(node.VesselZ)) continue;
                centerX += node.VesselX;
                centerZ += node.VesselZ;
                count++;
            }
            if (count == 0) return 0.0;
            centerX /= count;
            centerZ /= count;
            double bestAngle = 0.0, bestScore = double.NegativeInfinity;
            for (int candidate = 0; candidate < 12; candidate++)
            {
                double angle = candidate * Math.PI / 12.0;
                double sine = Math.Sin(angle), cosine = Math.Cos(angle);
                double score = 0.0;
                foreach (VesselTopologyNode node in topology.Nodes)
                {
                    double dx = node.VesselX - centerX, dz = node.VesselZ - centerZ;
                    if (double.IsNaN(dx) || double.IsNaN(dz) ||
                        double.IsInfinity(dx) || double.IsInfinity(dz)) continue;
                    double projected = dx * sine + dz * cosine;
                    double radial = Math.Sqrt(dx * dx + dz * dz);
                    if (radial < 0.05) continue;
                    double weight = node.Category == VesselNodeCategory.SolidBooster ? 5.0 : 1.0;
                    score += weight * Math.Abs(projected);
                }
                // Ties deliberately retain earlier candidates (side-view first).
                if (score > bestScore + 1.0e-7)
                {
                    bestScore = score;
                    bestAngle = angle;
                }
            }
            return bestAngle;
        }

        private static Vector3 RotateSchematicPoint(Vector3 point, double sine, double cosine)
        {
            return new Vector3(
                (float)(point.x * cosine - point.z * sine),
                point.y,
                (float)(point.x * sine + point.z * cosine));
        }

        private static void BuildVisibleSideRaster(Vessel vessel, VesselTopology topology)
        {
            Transform reference = vessel.ReferenceTransform;
            if (reference == null || topology.Nodes.Count == 0) return;
            double angle = ChooseSchematicViewingAngle(topology);
            double sine = Math.Sin(angle), cosine = Math.Cos(angle);
            const int width = 192, height = 320;
            const int triangleLimit = 240000;
            // Cache projected triangles for one depth pass; do not serialize meshes.
            List<Vector3> triangles = new List<Vector3>();
            List<int> ownersByTriangle = new List<int>();
            double minH = double.PositiveInfinity, maxH = double.NegativeInfinity;
            double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;
            foreach (Part part in vessel.parts)
            {
                if (part == null || ownersByTriangle.Count >= triangleLimit) break;
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                if (renderers == null) continue;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy ||
                        renderer.GetComponentInParent<Part>() != part) continue;
                    string typeName = renderer.GetType().Name;
                    if (typeName == "ParticleSystemRenderer" || typeName == "LineRenderer" ||
                        typeName == "TrailRenderer") continue;
                    Mesh mesh = null;
                    Transform meshTransform = renderer.transform;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                    else
                    {
                        SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                        if (skin != null) mesh = skin.sharedMesh;
                    }
                    if (mesh == null || !mesh.isReadable) continue;
                    try
                    {
                        Vector3[] vertices = mesh.vertices;
                        int[] indices = mesh.triangles;
                        if (vertices == null || indices == null) continue;
                        Vector3[] points = new Vector3[vertices.Length];
                        for (int v = 0; v < vertices.Length; v++)
                        {
                            Vector3 local = reference.InverseTransformPoint(
                                meshTransform.TransformPoint(vertices[v]));
                            points[v] = RotateSchematicPoint(local, sine, cosine);
                        }
                        for (int t = 0; t + 2 < indices.Length && ownersByTriangle.Count < triangleLimit; t += 3)
                        {
                            int a = indices[t], b = indices[t+1], c = indices[t+2];
                            if (a < 0 || b < 0 || c < 0 || a >= points.Length ||
                                b >= points.Length || c >= points.Length) continue;
                            Vector3 va = points[a], vb = points[b], vc = points[c];
                            if (float.IsNaN(va.z) || float.IsNaN(vb.z) || float.IsNaN(vc.z) ||
                                float.IsNaN(va.y) || float.IsNaN(vb.y) || float.IsNaN(vc.y) ||
                                float.IsInfinity(va.z) || float.IsInfinity(vb.z) || float.IsInfinity(vc.z) ||
                                float.IsInfinity(va.y) || float.IsInfinity(vb.y) || float.IsInfinity(vc.y)) continue;
                            triangles.Add(va); triangles.Add(vb); triangles.Add(vc);
                            ownersByTriangle.Add((int)part.flightID);
                            minH = Math.Min(minH, Math.Min(va.z, Math.Min(vb.z, vc.z)));
                            maxH = Math.Max(maxH, Math.Max(va.z, Math.Max(vb.z, vc.z)));
                            minV = Math.Min(minV, Math.Min(va.y, Math.Min(vb.y, vc.y)));
                            maxV = Math.Max(maxV, Math.Max(va.y, Math.Max(vb.y, vc.y)));
                        }
                    }
                    catch { /* modded or unreadable meshes are nonfatal */ }
                }
            }
            if (ownersByTriangle.Count == 0 || !(maxH > minH) || !(maxV > minV)) return;
            double padH = Math.Max(0.1, (maxH - minH) * 0.02);
            double padV = Math.Max(0.1, (maxV - minV) * 0.02);
            minH -= padH; maxH += padH; minV -= padV; maxV += padV;
            int[] owners = new int[width * height];
            float[] depth = new float[width * height];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.NegativeInfinity;
            for (int t = 0; t < ownersByTriangle.Count; t++)
                RasterTriangle(triangles[3*t], triangles[3*t+1], triangles[3*t+2],
                    ownersByTriangle[t], minH, maxH, minV, maxV,
                    width, height, depth, owners);
            byte[] pixels = new byte[owners.Length];
            int visible = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (owners[i] == 0) continue;
                    visible++;
                    int id = owners[i];
                    bool boundary = x == 0 || x == width-1 || y == 0 || y == height-1 ||
                        owners[i-1] != id || owners[i+1] != id ||
                        owners[i-width] != id || owners[i+width] != id;
                    pixels[i] = boundary ? (byte)2 : (byte)1;
                }
            if (visible == 0) return;
            topology.SchematicWidth = width;
            topology.SchematicHeight = height;
            topology.SchematicMinH = minH;
            topology.SchematicMaxH = maxH;
            topology.SchematicMinV = minV;
            topology.SchematicMaxV = maxV;
            topology.SchematicPixels = pixels;
        }

'@
$newRaster = $newRaster.Replace("`r`n","`n")
$modified = $src.Substring(0,$i) + $newRaster + $src.Substring($j)
$anchor = '    private static void SchematicResolutionRegression()'
if (-not $test.Contains($anchor) -or $test.Contains('SchematicAngleSelectionRegression')) { throw 'Expected 14.23.8 regression anchors missing or patch already applied' }
$runAnchor = '        Run("SCHEM higher-resolution exterior raster presentation", SchematicResolutionRegression);'
if (-not $test.Contains($runAnchor)) { throw 'Expected 14.23.8 test registration missing' }
$modifiedTest = $test.Replace($runAnchor, $runAnchor + "`n" + '        Run("SCHEM deterministic radial viewing angle", SchematicAngleSelectionRegression);')
$method = @'
    private static void SchematicAngleSelectionRegression()
    {
        string builder = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.Plugin", "Topology", "VesselTopologyBuilder.cs"));
        Check(builder.Contains("ChooseSchematicViewingAngle(topology)") &&
              builder.Contains("VesselNodeCategory.SolidBooster") &&
              builder.Contains("RotateSchematicPoint(local, sine, cosine)") &&
              builder.Contains("const int width = 192, height = 320;") &&
              builder.Contains("RasterTriangle(triangles[3*t]"),
              "SCHEM automatic viewing-angle selection or depth rendering regressed");
    }

'@
$modifiedTest = $modifiedTest.Replace($anchor, $method + $anchor)
# Preflight completed; stage both files and keep restorable backups.
$backup = Join-Path $root ('patch_backups/14.23.9_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
[IO.Directory]::CreateDirectory($backup) | Out-Null
$items = @(@($path,$modified),@($testPath,$modifiedTest))
$staged = @()
try {
    foreach ($entry in $items) {
        $target = Join-Path $root $entry[0]
        $saved = Join-Path $backup ($entry[0].Replace('/','__'))
        [IO.File]::Copy($target,$saved,$true)
        $tmp = $target + '.kmc14239.tmp'
        [IO.File]::WriteAllText($tmp,$entry[1],(New-Object System.Text.UTF8Encoding($false)))
        $staged += $tmp
    }
    $written = @()
    try {
        foreach ($entry in $items) {
            $target = Join-Path $root $entry[0]
            [IO.File]::Copy(($target + '.kmc14239.tmp'),$target,$true)
            $written += $entry[0]
        }
    } catch {
        foreach ($relative in $written) { [IO.File]::Copy((Join-Path $backup ($relative.Replace('/','__'))),(Join-Path $root $relative),$true) }
        throw
    }
} finally { foreach ($tmp in $staged) { if (Test-Path $tmp) { Remove-Item $tmp -Force } } }
Write-Host 'KMC 14.23.9 applied. Stable structural-angle selection + original depth z-buffer.'
Write-Host "Backups: $backup"
Write-Host 'Run NavigationTests, rebuild Plugin and MissionControl as needed, and replace KMC.Plugin.dll.'
Write-Warning 'Experimental: evaluate actual Ariane rocket appearance before pushing.'
