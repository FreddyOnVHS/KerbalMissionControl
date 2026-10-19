# KMC 14.23.7 - depth-resolved external side view (experimental)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$files = @(
 'KMC.shared/Topology/VesselTopology.cs',
 'KMC.shared/Topology/VesselTopologyPacketCodec.cs',
 'KMC.Plugin/Topology/VesselTopologyBuilder.cs',
 'KMC.MissionControl/Pages/SchematicPage.cs',
 'Tools/NavigationTests/Program.cs'
)
foreach ($f in $files) { if (-not (Test-Path (Join-Path $root $f))) { throw "Missing $f; run in KMC root." } }
$sha = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sha -ne '1a574b6723b2f99f1aa5c587d6f368075ad74ef9') { throw "Unexpected git HEAD $sha. Need frozen 14.23.4c; local uncommitted 14.23.5/6 changes are allowed." }
$changes = @{}
foreach ($f in $files) { $changes[$f] = [System.IO.File]::ReadAllText((Join-Path $root $f)) }
function Replace-Once([string]$path,[string]$old,[string]$new) {
 $src=$changes[$path]
 $at=$src.IndexOf($old,[StringComparison]::Ordinal)
 if ($at -lt 0 -or $src.IndexOf($old,$at+$old.Length,[StringComparison]::Ordinal) -ge 0) { throw "Missing/ambiguous anchor in $path : $($old.Substring(0,[Math]::Min(70,$old.Length)))" }
 $changes[$path]=$src.Substring(0,$at)+$new+$src.Substring($at+$old.Length)
}
Replace-Once 'KMC.shared/Topology/VesselTopology.cs' @'
public List<VesselTopologyNode> Nodes { get; private set; }
'@ @'
public List<VesselTopologyNode> Nodes { get; private set; }
        // v5: compact KSP-side depth-resolved exterior side raster.
        public int SchematicWidth { get; set; }
        public int SchematicHeight { get; set; }
        public double SchematicMinH { get; set; }
        public double SchematicMaxH { get; set; }
        public double SchematicMinV { get; set; }
        public double SchematicMaxV { get; set; }
        // Pixel codes: 0 empty, 1 face, 2 exterior/part boundary.
        public byte[] SchematicPixels { get; set; }
'@
Replace-Once 'KMC.shared/Topology/VesselTopologyPacketCodec.cs' @'
public const int CurrentVersion = 4;
'@ @'
public const int CurrentVersion = 5;
'@
Replace-Once 'KMC.shared/Topology/VesselTopologyPacketCodec.cs' @'
                writer.Flush();
                return stream.ToArray();
'@ @'
                WriteSchematic(writer, topology);
                writer.Flush();
                return stream.ToArray();
'@
Replace-Once 'KMC.shared/Topology/VesselTopologyPacketCodec.cs' @'
                    topology = result;
                    return true;
'@ @'
                    if (version >= 5) ReadSchematic(reader, result);
                    topology = result;
                    return true;
'@
Replace-Once 'KMC.shared/Topology/VesselTopologyPacketCodec.cs' @'
        private static void WriteNode(
'@ @'
        private static void WriteSchematic(BinaryWriter writer, VesselTopology model)
        {
            byte[] pixels = model.SchematicPixels;
            bool valid = pixels != null && model.SchematicWidth > 0 &&
                model.SchematicHeight > 0 && model.SchematicWidth <= 256 &&
                model.SchematicHeight <= 384 &&
                pixels.Length == model.SchematicWidth * model.SchematicHeight;
            writer.Write(valid);
            if (!valid) return;
            writer.Write(model.SchematicWidth);
            writer.Write(model.SchematicHeight);
            writer.Write(model.SchematicMinH);
            writer.Write(model.SchematicMaxH);
            writer.Write(model.SchematicMinV);
            writer.Write(model.SchematicMaxV);
            // Three-byte RLE: 1 byte code and 2 byte run length.
            using (MemoryStream compressed = new MemoryStream())
            using (BinaryWriter runs = new BinaryWriter(compressed))
            {
                for (int pos = 0; pos < pixels.Length;)
                {
                    byte value = pixels[pos];
                    int count = 1;
                    while (pos + count < pixels.Length &&
                           pixels[pos + count] == value && count < 65535)
                        count++;
                    runs.Write(value);
                    runs.Write((ushort)count);
                    pos += count;
                }
                runs.Flush();
                byte[] bytes = compressed.ToArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        private static void ReadSchematic(BinaryReader reader, VesselTopology model)
        {
            if (!reader.ReadBoolean()) return;
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            double minH = reader.ReadDouble();
            double maxH = reader.ReadDouble();
            double minV = reader.ReadDouble();
            double maxV = reader.ReadDouble();
            int len = reader.ReadInt32();
            if (width <= 0 || height <= 0 || width > 256 || height > 384 ||
                len < 0 || len > 300000 || len % 3 != 0 ||
                !(maxH > minH) || !(maxV > minV))
                throw new InvalidDataException("Invalid SCHEM raster header.");
            byte[] packed = reader.ReadBytes(len);
            if (packed.Length != len) throw new EndOfStreamException();
            byte[] pixels = new byte[width * height];
            int pos = 0;
            for (int i = 0; i < packed.Length; i += 3)
            {
                byte value = packed[i];
                int count = packed[i + 1] | (packed[i + 2] << 8);
                if (value > 2 || count == 0 || count > pixels.Length - pos)
                    throw new InvalidDataException("Invalid SCHEM RLE.");
                for (int k = 0; k < count; k++) pixels[pos++] = value;
            }
            if (pos != pixels.Length) throw new InvalidDataException("Incomplete SCHEM RLE.");
            model.SchematicWidth = width;
            model.SchematicHeight = height;
            model.SchematicMinH = minH;
            model.SchematicMaxH = maxH;
            model.SchematicMinV = minV;
            model.SchematicMaxV = maxV;
            model.SchematicPixels = pixels;
        }

        private static void WriteNode(
'@
Replace-Once 'KMC.Plugin/Topology/VesselTopologyBuilder.cs' @'
            VesselResourceAnalyzer.Analyze(
                vessel,
                topology);

            return topology;
'@ @'
            VesselResourceAnalyzer.Analyze(
                vessel,
                topology);

            // Rendering only. Any modded mesh failure preserves topology telemetry.
            try { BuildVisibleSideRaster(vessel, topology); }
            catch { topology.SchematicPixels = null; }

            return topology;
'@
Replace-Once 'KMC.Plugin/Topology/VesselTopologyBuilder.cs' @'
        private static VesselTopologyNode CreateNode(
'@ @'
        // 14.23.7: CPU z-buffer in vessel side plane (horizontal Z, vertical Y,
        // depth X looking toward -X). Store only visible faces and boundaries.
        private static void BuildVisibleSideRaster(Vessel vessel, VesselTopology topology)
        {
            Transform reference = vessel.ReferenceTransform;
            if (reference == null || topology.Nodes.Count == 0) return;
            double minH = double.PositiveInfinity, maxH = double.NegativeInfinity;
            double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;
            foreach (VesselTopologyNode node in topology.Nodes)
            {
                foreach (VesselOutlinePoint pt in node.SideOutlinePoints)
                {
                    minH = Math.Min(minH, pt.Horizontal);
                    maxH = Math.Max(maxH, pt.Horizontal);
                    minV = Math.Min(minV, pt.Vertical);
                    maxV = Math.Max(maxV, pt.Vertical);
                }
            }
            if (double.IsInfinity(minH) || double.IsInfinity(minV) ||
                maxH - minH < 0.001 || maxV - minV < 0.001) return;
            double padH = Math.Max(0.1, (maxH - minH) * 0.02);
            double padV = Math.Max(0.1, (maxV - minV) * 0.02);
            minH -= padH; maxH += padH; minV -= padV; maxV += padV;
            const int width = 160, height = 256;
            int[] owners = new int[width * height];
            float[] depth = new float[width * height];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.NegativeInfinity;
            int triangleBudget = 240000;
            foreach (Part part in vessel.parts)
            {
                if (part == null || triangleBudget <= 0) break;
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                if (renderers == null) continue;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy ||
                        renderer.GetComponentInParent<Part>() != part) continue;
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
                        int[] triangles = mesh.triangles;
                        if (vertices == null || triangles == null) continue;
                        Vector3[] points = new Vector3[vertices.Length];
                        for (int v = 0; v < vertices.Length; v++)
                            points[v] = reference.InverseTransformPoint(
                                meshTransform.TransformPoint(vertices[v]));
                        for (int t = 0; t + 2 < triangles.Length && triangleBudget > 0; t += 3)
                        {
                            triangleBudget--;
                            int a = triangles[t], b = triangles[t+1], c = triangles[t+2];
                            if (a < 0 || b < 0 || c < 0 || a >= points.Length ||
                                b >= points.Length || c >= points.Length) continue;
                            RasterTriangle(points[a], points[b], points[c],
                                (int)part.flightID, minH, maxH, minV, maxV,
                                width, height, depth, owners);
                        }
                    }
                    catch { /* unreadable or mod-provided mesh: nonfatal */ }
                }
            }
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

        private static void RasterTriangle(Vector3 a, Vector3 b, Vector3 c, int owner,
            double minH, double maxH, double minV, double maxV,
            int width, int height, float[] depths, int[] owners)
        {
            double ax = (a.z - minH) / (maxH - minH) * (width - 1);
            double bx = (b.z - minH) / (maxH - minH) * (width - 1);
            double cx = (c.z - minH) / (maxH - minH) * (width - 1);
            double ay = (maxV - a.y) / (maxV - minV) * (height - 1);
            double by = (maxV - b.y) / (maxV - minV) * (height - 1);
            double cy = (maxV - c.y) / (maxV - minV) * (height - 1);
            double area = (bx-ax)*(cy-ay) - (by-ay)*(cx-ax);
            if (Math.Abs(area) < 0.000001) return;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
            int x1 = Math.Min(width-1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
            int y1 = Math.Min(height-1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double wb = ((px-ax)*(cy-ay)-(py-ay)*(cx-ax))/area;
                    double wc = ((bx-ax)*(py-ay)-(by-ay)*(px-ax))/area;
                    double wa = 1.0 - wb - wc;
                    if (wa < -0.000001 || wb < -0.000001 || wc < -0.000001) continue;
                    float d = (float)(wa*a.x + wb*b.x + wc*c.x);
                    int i = y * width + x;
                    if (d > depths[i]) { depths[i] = d; owners[i] = owner; }
                }
        }

        private static VesselTopologyNode CreateNode(
'@
Replace-Once 'KMC.MissionControl/Pages/SchematicPage.cs' @'
            outlineParts = 0;
            fallbackParts = 0;

            Rectangle drawing =
'@ @'
            outlineParts = 0;
            fallbackParts = 0;

            // Prefer true KSP-side triangle z-buffer (transport v5).
            // Old packets and unreadable mesh cases use legacy polygons.
            if (DrawDepthResolvedSchematic(context, bounds, topology,
                    out outlineParts, out fallbackParts)) return;

            Rectangle drawing =
'@
Replace-Once 'KMC.MissionControl/Pages/SchematicPage.cs' @'
        private static void DrawPartOutline(
'@ @'
        private static bool DrawDepthResolvedSchematic(
            MissionRenderContext context, Rectangle bounds, VesselTopology topology,
            out int outlineParts, out int fallbackParts)
        {
            outlineParts = 0;
            fallbackParts = 0;
            byte[] pixels = topology.SchematicPixels;
            int width = topology.SchematicWidth;
            int height = topology.SchematicHeight;
            if (pixels == null || width <= 0 || height <= 0 ||
                pixels.Length != width * height) return false;
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
            outlineParts = topology.Nodes.Count;
            return true;
        }

        private static void DrawPartOutline(
'@
Replace-Once 'Tools/NavigationTests/Program.cs' @'
        Run("SCHEM side outline survives topology transport", VesselSchematicOutlineTransportRegression);
'@ @'
        Run("SCHEM side outline survives topology transport", VesselSchematicOutlineTransportRegression);
        Run("SCHEM v5 depth-resolved raster transport", SchematicRasterTransportRegression);
'@
Replace-Once 'Tools/NavigationTests/Program.cs' @'
    private static void Run(string name, Action test)
'@ @'
    private static void SchematicRasterTransportRegression()
    {
        VesselTopology topology = new VesselTopology();
        topology.SchematicWidth = 4;
        topology.SchematicHeight = 3;
        topology.SchematicMinH = -2;
        topology.SchematicMaxH = 2;
        topology.SchematicMinV = -3;
        topology.SchematicMaxV = 3;
        topology.SchematicPixels = new byte[] {0,0,2,0,0,1,1,2,0,0,2,0};
        byte[] packet = VesselTopologyPacketCodec.Encode(topology);
        VesselTopology decoded;
        Check(VesselTopologyPacketCodec.TryDecode(packet, out decoded), "SCHEM v5 packet rejected");
        Check(decoded.TransportVersion == 5 && decoded.SchematicWidth == 4 &&
            decoded.SchematicHeight == 3 && decoded.SchematicPixels != null &&
            decoded.SchematicPixels.Length == 12 && decoded.SchematicPixels[5] == 1 &&
            decoded.SchematicPixels[6] == 1 && decoded.SchematicPixels[10] == 2 &&
            decoded.SchematicMinH == -2 && decoded.SchematicMaxV == 3,
            "SCHEM raster was not preserved");
        // Old/empty geometry packets must still decode cleanly.
        topology.SchematicPixels = null;
        Check(VesselTopologyPacketCodec.TryDecode(VesselTopologyPacketCodec.Encode(topology), out decoded)
            && decoded.SchematicPixels == null, "SCHEM raster fallback failed");
    }

    private static void Run(string name, Action test)
'@
# All anchors passed. Stage on disk then replace with rollback on failure.
$backups = @{}
try {
 foreach ($f in $files) {
  $path = Join-Path $root $f
  $backups[$f] = [System.IO.File]::ReadAllBytes($path)
 }
 foreach ($f in $files) {
  [System.IO.File]::WriteAllText((Join-Path $root $f),$changes[$f],(New-Object System.Text.UTF8Encoding($false)))
 }
} catch {
 foreach ($f in $backups.Keys) { [System.IO.File]::WriteAllBytes((Join-Path $root $f),$backups[$f]) }
 throw
}
Write-Host '14.23.7 experimental depth-resolved SCHEM applied.'
Write-Warning 'Replace KMC.Plugin.dll and rebuild KMC.shared + MissionControl. Unreadable mesh geometry falls back to legacy silhouettes.'
