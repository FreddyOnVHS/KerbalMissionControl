param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) { throw "Missing file: $path" }
    return [System.IO.File]::ReadAllText($path)
}
function WriteText([string]$path,[string]$text) {
    [System.IO.File]::WriteAllText($path,$text,[System.Text.UTF8Encoding]::new($false))
}

$nodePath = Join-Path $RepoRoot "KMC.shared\Topology\VesselTopologyNode.cs"
$codecPath = Join-Path $RepoRoot "KMC.shared\Topology\VesselTopologyPacketCodec.cs"
$builderPath = Join-Path $RepoRoot "KMC.Plugin\Topology\VesselTopologyBuilder.cs"
$pagePath = Join-Path $RepoRoot "KMC.MissionControl\Pages\SchematicPage.cs"
$testPath = Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$node = ReadText $nodePath
$codec = ReadText $codecPath
$builder = ReadText $builderPath
$page = ReadText $pagePath
$tests = ReadText $testPath

# Preflight all expected anchors before writing anything.
if (-not $node.Contains("public double VesselZ { get; set; }")) {
    throw "VesselTopologyNode baseline anchor missing."
}
if (-not $codec.Contains("public const int CurrentVersion = 2;")) {
    throw "VesselTopologyPacketCodec is not at expected transport version 2."
}
if (-not $builder.Contains("ReadVesselPosition(")) {
    throw "VesselTopologyBuilder position anchor missing."
}
if (-not $page.Contains("public sealed class SchematicPage")) {
    throw "14.23.1 SchematicPage is not present. Apply 14.23.1 first."
}
if (-not $tests.Contains('Run("SCHEM vessel topology pipeline preserves projected part positions"')) {
    throw "14.23.1 regression marker missing. Apply 14.23.1/14.23.1a first."
}

# ---------------------------------------------------------------------------
# Shared topology model: oriented local bounds + vessel-relative part axes.
# ---------------------------------------------------------------------------
if (-not $node.Contains("public bool GeometryAvailable { get; set; }")) {
    $anchor = @'
        public double VesselZ { get; set; }
'@

    $addition = @'
        public double VesselZ { get; set; }

        // Build 14.23.2 — approximate part geometry.
        // Bounds center/size are expressed in the Part transform's local
        // coordinates. Right/Up/Forward are the Part local axes expressed in
        // the vessel ReferenceTransform frame.
        public bool GeometryAvailable { get; set; }
        public double BoundsCenterX { get; set; }
        public double BoundsCenterY { get; set; }
        public double BoundsCenterZ { get; set; }
        public double BoundsSizeX { get; set; }
        public double BoundsSizeY { get; set; }
        public double BoundsSizeZ { get; set; }

        public double AxisRightX { get; set; }
        public double AxisRightY { get; set; }
        public double AxisRightZ { get; set; }

        public double AxisUpX { get; set; }
        public double AxisUpY { get; set; }
        public double AxisUpZ { get; set; }

        public double AxisForwardX { get; set; }
        public double AxisForwardY { get; set; }
        public double AxisForwardZ { get; set; }
'@
    $node = $node.Replace($anchor,$addition)
}

# ---------------------------------------------------------------------------
# Packet codec v3. v1/v2 remain readable; geometry defaults unavailable.
# ---------------------------------------------------------------------------
$codec = $codec.Replace(
    "public const int CurrentVersion = 2;",
    "public const int CurrentVersion = 3;")

if (-not $codec.Contains("writer.Write(node.GeometryAvailable);")) {
    $anchor = @'
            writer.Write(node.VesselZ);
            writer.Write(node.StructuralDepth);
'@
    $replacement = @'
            writer.Write(node.VesselZ);

            writer.Write(node.GeometryAvailable);
            writer.Write(node.BoundsCenterX);
            writer.Write(node.BoundsCenterY);
            writer.Write(node.BoundsCenterZ);
            writer.Write(node.BoundsSizeX);
            writer.Write(node.BoundsSizeY);
            writer.Write(node.BoundsSizeZ);
            writer.Write(node.AxisRightX);
            writer.Write(node.AxisRightY);
            writer.Write(node.AxisRightZ);
            writer.Write(node.AxisUpX);
            writer.Write(node.AxisUpY);
            writer.Write(node.AxisUpZ);
            writer.Write(node.AxisForwardX);
            writer.Write(node.AxisForwardY);
            writer.Write(node.AxisForwardZ);

            writer.Write(node.StructuralDepth);
'@
    if (-not $codec.Contains($anchor)) {
        throw "Codec WriteNode geometry anchor missing."
    }
    $codec = $codec.Replace($anchor,$replacement)
}

if (-not $codec.Contains("if (version >= 3)")) {
    $old = @'
                    VesselZ = reader.ReadDouble(),
                    StructuralDepth = reader.ReadInt32(),
'@
    $new = @'
                    VesselZ = reader.ReadDouble()
                };

            if (version >= 3)
            {
                node.GeometryAvailable =
                    reader.ReadBoolean();

                node.BoundsCenterX =
                    reader.ReadDouble();
                node.BoundsCenterY =
                    reader.ReadDouble();
                node.BoundsCenterZ =
                    reader.ReadDouble();

                node.BoundsSizeX =
                    reader.ReadDouble();
                node.BoundsSizeY =
                    reader.ReadDouble();
                node.BoundsSizeZ =
                    reader.ReadDouble();

                node.AxisRightX =
                    reader.ReadDouble();
                node.AxisRightY =
                    reader.ReadDouble();
                node.AxisRightZ =
                    reader.ReadDouble();

                node.AxisUpX =
                    reader.ReadDouble();
                node.AxisUpY =
                    reader.ReadDouble();
                node.AxisUpZ =
                    reader.ReadDouble();

                node.AxisForwardX =
                    reader.ReadDouble();
                node.AxisForwardY =
                    reader.ReadDouble();
                node.AxisForwardZ =
                    reader.ReadDouble();
            }

            node.StructuralDepth =
                reader.ReadInt32();

            node.SymmetryGroupId =
                reader.ReadUInt32();

            node.BranchRootPartId =
                reader.ReadUInt32();

            node.ActivationStage =
                reader.ReadInt32();

            node.SeparationStage =
                reader.ReadInt32();

            node.IsSeparationBoundary =
                reader.ReadBoolean();

            node.WillSeparateOnNextStage =
                reader.ReadBoolean();

            node.AllowsCrossFeed =
                reader.ReadBoolean();

            /*
             * The properties above were moved out of the initializer in v3
             * so v1/v2 packets can still be decoded without consuming fields
             * that do not exist in those transports.
             */
'@
    if (-not $codec.Contains($old)) {
        throw "Codec ReadNode initializer anchor missing."
    }

    # Replace through the old tail of initializer fields.
    $start = $codec.IndexOf($old)
    $tail = @'
                    SymmetryGroupId = reader.ReadUInt32(),
                    BranchRootPartId = reader.ReadUInt32(),
                    ActivationStage = reader.ReadInt32(),
                    SeparationStage = reader.ReadInt32(),
                    IsSeparationBoundary = reader.ReadBoolean(),
                    WillSeparateOnNextStage = reader.ReadBoolean(),
                    AllowsCrossFeed = reader.ReadBoolean()
                };
'@
    $tailStart = $codec.IndexOf($tail,$start)
    if ($tailStart -lt 0) {
        throw "Codec ReadNode initializer tail missing."
    }
    $removeLength = ($tailStart + $tail.Length) - $start
    $codec = $codec.Remove($start,$removeLength).Insert($start,$new)
}

# ---------------------------------------------------------------------------
# KSP plugin: derive local renderer bounds + part orientation.
# ---------------------------------------------------------------------------
if (-not $builder.Contains("ReadVesselGeometry(")) {
    $anchor = @'
            ReadVesselPosition(
                vessel,
                part,
                node);

            return node;
'@
    $replacement = @'
            ReadVesselPosition(
                vessel,
                part,
                node);

            ReadVesselGeometry(
                vessel,
                part,
                node);

            return node;
'@
    if (-not $builder.Contains($anchor)) {
        throw "Builder CreateNode anchor missing."
    }
    $builder = $builder.Replace($anchor,$replacement)
}

if (-not $builder.Contains("private static void ReadVesselGeometry(")) {
    $marker = @'
        private static VesselAttachmentType GetAttachmentType(
'@
    if (-not $builder.Contains($marker)) {
        throw "Builder geometry insertion anchor missing."
    }

    $helper = @'
        private static void ReadVesselGeometry(
            Vessel vessel,
            Part part,
            VesselTopologyNode node)
        {
            if (vessel == null ||
                part == null ||
                node == null ||
                part.transform == null)
            {
                return;
            }

            try
            {
                Transform reference =
                    vessel.ReferenceTransform;

                if (reference == null)
                {
                    return;
                }

                Vector3 right =
                    reference.InverseTransformDirection(
                        part.transform.right).normalized;

                Vector3 up =
                    reference.InverseTransformDirection(
                        part.transform.up).normalized;

                Vector3 forward =
                    reference.InverseTransformDirection(
                        part.transform.forward).normalized;

                Renderer[] renderers =
                    part.GetComponentsInChildren<Renderer>(
                        true);

                if (renderers == null ||
                    renderers.Length == 0)
                {
                    return;
                }

                bool hasBounds =
                    false;

                Vector3 localMin =
                    Vector3.zero;

                Vector3 localMax =
                    Vector3.zero;

                for (int index = 0;
                     index < renderers.Length;
                     index++)
                {
                    Renderer renderer =
                        renderers[index];

                    if (renderer == null ||
                        renderer is ParticleSystemRenderer ||
                        renderer is LineRenderer ||
                        renderer is TrailRenderer)
                    {
                        continue;
                    }

                    Bounds bounds =
                        renderer.bounds;

                    Vector3 center =
                        bounds.center;

                    Vector3 extents =
                        bounds.extents;

                    for (int cornerIndex = 0;
                         cornerIndex < 8;
                         cornerIndex++)
                    {
                        Vector3 world =
                            center +
                            new Vector3(
                                (cornerIndex & 1) != 0
                                    ? extents.x
                                    : -extents.x,
                                (cornerIndex & 2) != 0
                                    ? extents.y
                                    : -extents.y,
                                (cornerIndex & 4) != 0
                                    ? extents.z
                                    : -extents.z);

                        Vector3 local =
                            part.transform.InverseTransformPoint(
                                world);

                        if (!hasBounds)
                        {
                            localMin =
                                local;

                            localMax =
                                local;

                            hasBounds =
                                true;
                        }
                        else
                        {
                            localMin =
                                Vector3.Min(
                                    localMin,
                                    local);

                            localMax =
                                Vector3.Max(
                                    localMax,
                                    local);
                        }
                    }
                }

                if (!hasBounds)
                {
                    return;
                }

                Vector3 size =
                    localMax -
                    localMin;

                if (!IsFinite(size.x) ||
                    !IsFinite(size.y) ||
                    !IsFinite(size.z) ||
                    size.sqrMagnitude < 1.0e-8f)
                {
                    return;
                }

                Vector3 centerLocal =
                    (localMin + localMax) *
                    0.5f;

                node.GeometryAvailable =
                    true;

                node.BoundsCenterX =
                    centerLocal.x;
                node.BoundsCenterY =
                    centerLocal.y;
                node.BoundsCenterZ =
                    centerLocal.z;

                node.BoundsSizeX =
                    Math.Abs(size.x);
                node.BoundsSizeY =
                    Math.Abs(size.y);
                node.BoundsSizeZ =
                    Math.Abs(size.z);

                node.AxisRightX =
                    right.x;
                node.AxisRightY =
                    right.y;
                node.AxisRightZ =
                    right.z;

                node.AxisUpX =
                    up.x;
                node.AxisUpY =
                    up.y;
                node.AxisUpZ =
                    up.z;

                node.AxisForwardX =
                    forward.x;
                node.AxisForwardY =
                    forward.y;
                node.AxisForwardZ =
                    forward.z;
            }
            catch
            {
                /*
                 * Geometry is presentation data only. A modded part with a
                 * renderer edge case must never break topology telemetry.
                 */
                node.GeometryAvailable =
                    false;
            }
        }

'@
    $builder = $builder.Replace($marker,$helper + $marker)
}

# ---------------------------------------------------------------------------
# Mission Control SCHEM: project oriented boxes when geometry is available.
# ---------------------------------------------------------------------------

# Replace DrawVessel with geometry-aware implementation, preserving the rest
# of 14.23.1 page/UI.
$startMarker = "        private void DrawVessel("
$endMarker = "        private void Project("

$start = $page.IndexOf($startMarker)
$end = $page.IndexOf($endMarker,$start)

if ($start -lt 0 -or $end -lt 0) {
    throw "Could not locate 14.23.1 DrawVessel block."
}

$newDrawVessel = @'
        private void DrawVessel(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology)
        {
            Rectangle drawing =
                Rectangle.FromLTRB(
                    bounds.Left + 28,
                    bounds.Top +
                        PanelHeaderHeight +
                        54,
                    bounds.Right - 28,
                    bounds.Bottom - 28);

            if (drawing.Width <= 10 ||
                drawing.Height <= 10)
            {
                return;
            }

            Dictionary<uint, VesselTopologyNode> nodes =
                new Dictionary<uint, VesselTopologyNode>();

            Dictionary<uint, PointF> centers =
                new Dictionary<uint, PointF>();

            Dictionary<uint, PointF[]> projectedBoxes =
                new Dictionary<uint, PointF[]>();

            double minA =
                double.PositiveInfinity;

            double maxA =
                double.NegativeInfinity;

            double minB =
                double.PositiveInfinity;

            double maxB =
                double.NegativeInfinity;

            // First pass computes true projected extents from oriented bounds.
            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                nodes[node.PartId] =
                    node;

                if (node.GeometryAvailable)
                {
                    VesselPoint3[] corners =
                        BuildVesselBoxCorners(
                            node);

                    for (int cornerIndex = 0;
                         cornerIndex < corners.Length;
                         cornerIndex++)
                    {
                        double a;
                        double b;

                        Project(
                            corners[cornerIndex],
                            out a,
                            out b);

                        ExpandRange(
                            a,
                            b,
                            ref minA,
                            ref maxA,
                            ref minB,
                            ref maxB);
                    }
                }
                else
                {
                    double a;
                    double b;

                    Project(
                        node,
                        out a,
                        out b);

                    ExpandRange(
                        a,
                        b,
                        ref minA,
                        ref maxA,
                        ref minB,
                        ref maxB);
                }
            }

            if (!IsFinite(minA) ||
                !IsFinite(maxA) ||
                !IsFinite(minB) ||
                !IsFinite(maxB))
            {
                DrawWaiting(
                    context,
                    bounds);

                return;
            }

            double rangeA =
                Math.Max(
                    0.5,
                    maxA - minA);

            double rangeB =
                Math.Max(
                    0.5,
                    maxB - minB);

            double scale =
                Math.Min(
                    drawing.Width /
                        (rangeA * 1.12),
                    drawing.Height /
                        (rangeB * 1.12));

            double centerA =
                (minA + maxA) /
                2.0;

            double centerB =
                (minB + maxB) /
                2.0;

            float centerX =
                drawing.Left +
                drawing.Width /
                2.0f;

            float centerY =
                drawing.Top +
                drawing.Height /
                2.0f;

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                double centerProjectedA;
                double centerProjectedB;

                Project(
                    node,
                    out centerProjectedA,
                    out centerProjectedB);

                centers[node.PartId] =
                    ToScreenPoint(
                        centerProjectedA,
                        centerProjectedB,
                        centerA,
                        centerB,
                        scale,
                        centerX,
                        centerY);

                if (!node.GeometryAvailable)
                {
                    continue;
                }

                VesselPoint3[] corners =
                    BuildVesselBoxCorners(
                        node);

                PointF[] screenCorners =
                    new PointF[corners.Length];

                for (int cornerIndex = 0;
                     cornerIndex < corners.Length;
                     cornerIndex++)
                {
                    double a;
                    double b;

                    Project(
                        corners[cornerIndex],
                        out a,
                        out b);

                    screenCorners[cornerIndex] =
                        ToScreenPoint(
                            a,
                            b,
                            centerA,
                            centerB,
                            scale,
                            centerX,
                            centerY);
                }

                projectedBoxes[node.PartId] =
                    screenCorners;
            }

            // Structural center lines remain as a faint topology reference.
            using (Pen structural =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor),
                    1.0f))
            {
                for (int index = 0;
                     index < topology.Nodes.Count;
                     index++)
                {
                    VesselTopologyNode node =
                        topology.Nodes[index];

                    if (node == null ||
                        !node.HasParent)
                    {
                        continue;
                    }

                    PointF child;
                    PointF parent;

                    if (centers.TryGetValue(
                            node.PartId,
                            out child) &&
                        centers.TryGetValue(
                            node.ParentPartId,
                            out parent))
                    {
                        context.Graphics.DrawLine(
                            structural,
                            parent,
                            child);
                    }
                }
            }

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                bool isRoot =
                    topology.HasRootPart &&
                    node.PartId ==
                        topology.RootPartId;

                PointF[] box;

                if (projectedBoxes.TryGetValue(
                        node.PartId,
                        out box))
                {
                    DrawProjectedBox(
                        context,
                        box,
                        node,
                        isRoot);
                }
                else
                {
                    PointF point;

                    if (centers.TryGetValue(
                            node.PartId,
                            out point))
                    {
                        DrawPartGlyph(
                            context,
                            point,
                            node,
                            isRoot);
                    }
                }
            }

            DrawAxes(
                context,
                drawing);
        }

        private static void ExpandRange(
            double a,
            double b,
            ref double minA,
            ref double maxA,
            ref double minB,
            ref double maxB)
        {
            if (!IsFinite(a) ||
                !IsFinite(b))
            {
                return;
            }

            minA =
                Math.Min(
                    minA,
                    a);

            maxA =
                Math.Max(
                    maxA,
                    a);

            minB =
                Math.Min(
                    minB,
                    b);

            maxB =
                Math.Max(
                    maxB,
                    b);
        }

        private static PointF ToScreenPoint(
            double a,
            double b,
            double centerA,
            double centerB,
            double scale,
            float centerX,
            float centerY)
        {
            return
                new PointF(
                    centerX +
                        (float)(
                            (a - centerA) *
                            scale),

                    centerY -
                        (float)(
                            (b - centerB) *
                            scale));
        }

        private static VesselPoint3[] BuildVesselBoxCorners(
            VesselTopologyNode node)
        {
            double halfX =
                Math.Max(
                    0.001,
                    node.BoundsSizeX * 0.5);

            double halfY =
                Math.Max(
                    0.001,
                    node.BoundsSizeY * 0.5);

            double halfZ =
                Math.Max(
                    0.001,
                    node.BoundsSizeZ * 0.5);

            VesselPoint3 localCenterOffset =
                Add(
                    Scale(
                        Right(node),
                        node.BoundsCenterX),
                    Add(
                        Scale(
                            Up(node),
                            node.BoundsCenterY),
                        Scale(
                            Forward(node),
                            node.BoundsCenterZ)));

            VesselPoint3 center =
                new VesselPoint3(
                    node.VesselX +
                        localCenterOffset.X,
                    node.VesselY +
                        localCenterOffset.Y,
                    node.VesselZ +
                        localCenterOffset.Z);

            VesselPoint3 right =
                Scale(
                    Right(node),
                    halfX);

            VesselPoint3 up =
                Scale(
                    Up(node),
                    halfY);

            VesselPoint3 forward =
                Scale(
                    Forward(node),
                    halfZ);

            VesselPoint3[] result =
                new VesselPoint3[8];

            for (int index = 0;
                 index < 8;
                 index++)
            {
                VesselPoint3 corner =
                    center;

                corner =
                    Add(
                        corner,
                        Scale(
                            right,
                            (index & 1) != 0
                                ? 1.0
                                : -1.0));

                corner =
                    Add(
                        corner,
                        Scale(
                            up,
                            (index & 2) != 0
                                ? 1.0
                                : -1.0));

                corner =
                    Add(
                        corner,
                        Scale(
                            forward,
                            (index & 4) != 0
                                ? 1.0
                                : -1.0));

                result[index] =
                    corner;
            }

            return result;
        }

        private static void DrawProjectedBox(
            MissionRenderContext context,
            PointF[] corners,
            VesselTopologyNode node,
            bool isRoot)
        {
            if (corners == null ||
                corners.Length != 8)
            {
                return;
            }

            Color color =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        isRoot
                            ? 230
                            : 180,
                        color),
                    isRoot
                        ? 2.0f
                        : 1.15f))
            {
                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    0,
                    1);
                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    0,
                    2);
                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    0,
                    4);

                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    1,
                    3);
                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    1,
                    5);

                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    2,
                    3);
                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    2,
                    6);

                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    3,
                    7);

                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    4,
                    5);
                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    4,
                    6);

                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    5,
                    7);

                DrawBoxEdge(
                    context,
                    pen,
                    corners,
                    6,
                    7);
            }
        }

        private static void DrawBoxEdge(
            MissionRenderContext context,
            Pen pen,
            PointF[] corners,
            int left,
            int right)
        {
            context.Graphics.DrawLine(
                pen,
                corners[left],
                corners[right]);
        }

        private static VesselPoint3 Right(
            VesselTopologyNode node)
        {
            return
                new VesselPoint3(
                    node.AxisRightX,
                    node.AxisRightY,
                    node.AxisRightZ);
        }

        private static VesselPoint3 Up(
            VesselTopologyNode node)
        {
            return
                new VesselPoint3(
                    node.AxisUpX,
                    node.AxisUpY,
                    node.AxisUpZ);
        }

        private static VesselPoint3 Forward(
            VesselTopologyNode node)
        {
            return
                new VesselPoint3(
                    node.AxisForwardX,
                    node.AxisForwardY,
                    node.AxisForwardZ);
        }

        private static VesselPoint3 Add(
            VesselPoint3 left,
            VesselPoint3 right)
        {
            return
                new VesselPoint3(
                    left.X + right.X,
                    left.Y + right.Y,
                    left.Z + right.Z);
        }

        private static VesselPoint3 Scale(
            VesselPoint3 value,
            double scale)
        {
            return
                new VesselPoint3(
                    value.X * scale,
                    value.Y * scale,
                    value.Z * scale);
        }

'@

$page =
    $page.Substring(0,$start) +
    $newDrawVessel +
    $page.Substring($end)

# Add point projection overload and compact 3D struct.
if (-not $page.Contains("private void Project(`r`n            VesselPoint3")) {
    $projectMarker = @'
        private void Project(
            VesselTopologyNode node,
'@
    $projectPos = $page.IndexOf($projectMarker)
    if ($projectPos -lt 0) {
        throw "Schematic Project(node) method missing."
    }

    $overload = @'
        private void Project(
            VesselPoint3 point,
            out double a,
            out double b)
        {
            if (_view ==
                SchematicView.Front)
            {
                a =
                    point.X;

                b =
                    point.Y;

                return;
            }

            if (_view ==
                SchematicView.Top)
            {
                a =
                    point.X;

                b =
                    point.Z;

                return;
            }

            a =
                point.Z;

            b =
                point.Y;
        }

'@

    $page =
        $page.Substring(0,$projectPos) +
        $overload +
        $page.Substring($projectPos)
}

if (-not $page.Contains("private struct VesselPoint3")) {
    $marker = @'
        private static string Safe(
'@
    if (-not $page.Contains($marker)) {
        throw "Schematic helper insertion anchor missing."
    }

    $struct = @'
        private struct VesselPoint3
        {
            public VesselPoint3(
                double x,
                double y,
                double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double X;
            public double Y;
            public double Z;
        }

'@
    $page = $page.Replace($marker,$struct + $marker)
}

$page = $page.Replace(
    '"FOUNDATION VIEW"',
    '"ORIENTED PART BOUNDS"')

$page = $page.Replace(
    '"PART CENTERS +"',
    '"PART BOXES +"')

# ---------------------------------------------------------------------------
# Tests: packet v3 geometry and production source wiring.
# ---------------------------------------------------------------------------
if (-not $tests.Contains('Run("SCHEM oriented geometry survives topology transport"')) {
    $anchor = '        Run("SCHEM vessel topology pipeline preserves projected part positions", VesselSchematicFoundationRegression);'
    if (-not $tests.Contains($anchor)) {
        throw "14.23.1 SCHEM test registration missing."
    }

    $tests = $tests.Replace(
        $anchor,
        $anchor + "`r`n" +
        '        Run("SCHEM oriented geometry survives topology transport", VesselSchematicGeometryRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicGeometryRegression()")) {
    $anchor = "    private static void ManeuverUplinkDesiredPeriapsis()"
    if (-not $tests.Contains($anchor)) {
        throw "NavigationTests insertion anchor missing."
    }

    $method = @'
    private static void VesselSchematicGeometryRegression()
    {
        VesselTopology topology =
            new VesselTopology
            {
                VesselId = "SCHEM-GEOMETRY",
                VesselName = "Geometry Test",
                RootPartId = 10U,
                HasRootPart = true,
                PartCount = 1,
                CurrentStage = 2,
                Revision = 9
            };

        VesselTopologyNode node =
            new VesselTopologyNode
            {
                PartId = 10U,
                PartTitle = "Oriented Part",
                VesselX = 1.0,
                VesselY = 2.0,
                VesselZ = 3.0,
                GeometryAvailable = true,
                BoundsCenterX = 0.1,
                BoundsCenterY = -0.2,
                BoundsCenterZ = 0.3,
                BoundsSizeX = 1.25,
                BoundsSizeY = 2.5,
                BoundsSizeZ = 3.75,
                AxisRightX = 0.0,
                AxisRightY = 1.0,
                AxisRightZ = 0.0,
                AxisUpX = -1.0,
                AxisUpY = 0.0,
                AxisUpZ = 0.0,
                AxisForwardX = 0.0,
                AxisForwardY = 0.0,
                AxisForwardZ = 1.0
            };

        topology.Nodes.Add(
            node);

        byte[] encoded =
            VesselTopologyPacketCodec.Encode(
                topology);

        VesselTopology decoded;

        Check(
            VesselTopologyPacketCodec.TryDecode(
                encoded,
                out decoded),
            "SCHEM geometry topology decode failed");

        Check(
            decoded != null &&
            decoded.TransportVersion >= 3 &&
            decoded.Nodes.Count == 1 &&
            decoded.Nodes[0].GeometryAvailable,
            "SCHEM geometry availability/version lost");

        VesselTopologyNode actual =
            decoded.Nodes[0];

        Near(actual.BoundsCenterX, 0.1);
        Near(actual.BoundsCenterY, -0.2);
        Near(actual.BoundsCenterZ, 0.3);
        Near(actual.BoundsSizeX, 1.25);
        Near(actual.BoundsSizeY, 2.5);
        Near(actual.BoundsSizeZ, 3.75);
        Near(actual.AxisRightY, 1.0);
        Near(actual.AxisUpX, -1.0);
        Near(actual.AxisForwardZ, 1.0);

        string builder =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.Plugin",
                    "Topology",
                    "VesselTopologyBuilder.cs"));

        string page =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Pages",
                    "SchematicPage.cs"));

        Check(
            builder.IndexOf(
                "GetComponentsInChildren<Renderer>",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "InverseTransformDirection",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "BoundsSizeX",
                StringComparison.Ordinal) >= 0,
            "KSP SCHEM geometry capture missing");

        Check(
            page.IndexOf(
                "BuildVesselBoxCorners",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "DrawProjectedBox",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "AxisForward",
                StringComparison.Ordinal) >= 0,
            "Mission Control oriented-box renderer missing");
    }


'@

    $tests = $tests.Replace(
        $anchor,
        $method + $anchor)
}

# ---------------------------------------------------------------------------
# Write only after all transformations succeeded.
# ---------------------------------------------------------------------------
WriteText $nodePath $node
WriteText $codecPath $codec
WriteText $builderPath $builder
WriteText $pagePath $page
WriteText $testPath $tests

Write-Host "Applied KMC 14.23.2 Part Geometry / Orientation."
Write-Host "Topology transport upgraded to v3 with backward-compatible v1/v2 decode."
Write-Host "KSP.Plugin MUST be rebuilt and replaced for geometry fields to be populated."
