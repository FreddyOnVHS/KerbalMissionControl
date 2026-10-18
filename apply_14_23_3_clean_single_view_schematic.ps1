param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path,[string]$text) {
    $dir = [System.IO.Path]::GetDirectoryName($path)
    if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path $dir)) {
        [System.IO.Directory]::CreateDirectory($dir) | Out-Null
    }

    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

$nodePath = Join-Path $RepoRoot "KMC.shared\Topology\VesselTopologyNode.cs"
$codecPath = Join-Path $RepoRoot "KMC.shared\Topology\VesselTopologyPacketCodec.cs"
$sharedProjectPath = Join-Path $RepoRoot "KMC.shared\KMC.shared.csproj"
$pluginProjectPath = Join-Path $RepoRoot "KMC.Plugin\KMC.Plugin.csproj"
$builderPath = Join-Path $RepoRoot "KMC.Plugin\Topology\VesselTopologyBuilder.cs"
$pagePath = Join-Path $RepoRoot "KMC.MissionControl\Pages\SchematicPage.cs"
$testPath = Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"
$outlinePath = Join-Path $RepoRoot "KMC.shared\Topology\VesselOutlinePoint.cs"

$node = ReadText $nodePath
$codec = ReadText $codecPath
$sharedProject = ReadText $sharedProjectPath
$pluginProject = ReadText $pluginProjectPath
$builder = ReadText $builderPath
$page = ReadText $pagePath
$tests = ReadText $testPath

# ---------------------------------------------------------------------------
# Preflight expected local 14.23.2c state.
# ---------------------------------------------------------------------------
if (-not $node.Contains("public bool GeometryAvailable { get; set; }")) {
    throw "14.23.2 geometry fields are missing."
}

if (-not $codec.Contains("public const int CurrentVersion = 3;")) {
    throw "Expected topology transport v3 from 14.23.2c."
}

if (-not $builder.Contains("TryGetRendererLocalGeometryBounds(") -or
    -not $builder.Contains("AccumulateRendererLocalBounds(")) {
    throw "14.23.2c native local mesh bounds are missing."
}

if (-not $page.Contains("FRONT") -or
    -not $page.Contains("SIDE") -or
    -not $page.Contains("TOP")) {
    throw "Expected 14.23.2c multi-view SCHEM page."
}

if (-not $tests.Contains('Run("SCHEM geometry uses native local mesh bounds"')) {
    throw "14.23.2c regression marker missing."
}

# ---------------------------------------------------------------------------
# New shared outline point type.
# ---------------------------------------------------------------------------
$outlineSource = @'
namespace KMC.Shared.Topology
{
    public sealed class VesselOutlinePoint
    {
        public double Horizontal { get; set; }
        public double Vertical { get; set; }
    }
}
'@

WriteText $outlinePath $outlineSource

if (-not $sharedProject.Contains('Topology\VesselOutlinePoint.cs')) {
    $anchor = '<Compile Include="Topology\VesselTopologyNode.cs" />'

    if (-not $sharedProject.Contains($anchor)) {
        throw "Could not locate shared topology compile anchor."
    }

    $sharedProject =
        $sharedProject.Replace(
            $anchor,
            '<Compile Include="Topology\VesselOutlinePoint.cs" />' +
            "`r`n    " +
            $anchor)
}

if (-not $pluginProject.Contains('..\KMC.shared\Topology\VesselOutlinePoint.cs')) {
    $anchor = '<Compile Include="..\KMC.shared\Topology\VesselTopologyNode.cs"><Link>Shared\Topology\VesselTopologyNode.cs</Link></Compile>'

    if (-not $pluginProject.Contains($anchor)) {
        throw "Could not locate plugin shared topology link anchor."
    }

    $pluginProject =
        $pluginProject.Replace(
            $anchor,
            '<Compile Include="..\KMC.shared\Topology\VesselOutlinePoint.cs"><Link>Shared\Topology\VesselOutlinePoint.cs</Link></Compile>' +
            "`r`n    " +
            $anchor)
}

# ---------------------------------------------------------------------------
# VesselTopologyNode: per-part side-view outline.
# ---------------------------------------------------------------------------
if (-not $node.Contains("SideOutlinePoints")) {
    $ctorAnchor = @'
            Modules = new List<VesselModuleDescriptor>();
'@

    if (-not $node.Contains($ctorAnchor)) {
        throw "Could not locate VesselTopologyNode constructor anchor."
    }

    $node =
        $node.Replace(
            $ctorAnchor,
            $ctorAnchor +
            '            SideOutlinePoints = new List<VesselOutlinePoint>();' +
            "`r`n")

    $propAnchor = @'
        public List<VesselModuleDescriptor> Modules { get; private set; }
'@

    if (-not $node.Contains($propAnchor)) {
        throw "Could not locate VesselTopologyNode collection anchor."
    }

    $node =
        $node.Replace(
            $propAnchor,
            $propAnchor +
            '        public List<VesselOutlinePoint> SideOutlinePoints { get; private set; }' +
            "`r`n")
}

# ---------------------------------------------------------------------------
# Transport v4: append side outline after modules for each node.
# ---------------------------------------------------------------------------
$codec =
    $codec.Replace(
        "public const int CurrentVersion = 3;",
        "public const int CurrentVersion = 4;")

if (-not $codec.Contains("WriteOutlinePoints(")) {
    $writeAnchor = @'
            for (int index = 0;
                 index < node.Modules.Count;
                 index++)
            {
                WriteModule(
                    writer,
                    node.Modules[index]);
            }
'@

    if (-not $codec.Contains($writeAnchor)) {
        throw "Could not locate WriteNode module block."
    }

    $writeReplacement = $writeAnchor + @'

            WriteOutlinePoints(
                writer,
                node.SideOutlinePoints);
'@

    $codec =
        $codec.Replace(
            $writeAnchor,
            $writeReplacement)
}

if (-not $codec.Contains("if (version >= 4)")) {
    $readAnchor = @'
            if (version >= 2)
            {
                int moduleCount =
                    ReadCount(reader, 1000);

                for (int index = 0;
                     index < moduleCount;
                     index++)
                {
                    node.Modules.Add(
                        ReadModule(reader));
                }
            }

            return node;
'@

    if (-not $codec.Contains($readAnchor)) {
        throw "Could not locate ReadNode module block."
    }

    $readReplacement = @'
            if (version >= 2)
            {
                int moduleCount =
                    ReadCount(reader, 1000);

                for (int index = 0;
                     index < moduleCount;
                     index++)
                {
                    node.Modules.Add(
                        ReadModule(reader));
                }
            }

            if (version >= 4)
            {
                ReadOutlinePoints(
                    reader,
                    node.SideOutlinePoints);
            }

            return node;
'@

    $codec =
        $codec.Replace(
            $readAnchor,
            $readReplacement)
}

if (-not $codec.Contains("private static void WriteOutlinePoints(")) {
    $marker = @'
        private static void WriteModule(
'@

    if (-not $codec.Contains($marker)) {
        throw "Could not locate codec helper insertion point."
    }

    $helpers = @'
        private static void WriteOutlinePoints(
            BinaryWriter writer,
            System.Collections.Generic.IList<VesselOutlinePoint> points)
        {
            writer.Write(
                points != null
                    ? points.Count
                    : 0);

            if (points == null)
            {
                return;
            }

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                writer.Write(
                    points[index].Horizontal);

                writer.Write(
                    points[index].Vertical);
            }
        }

        private static void ReadOutlinePoints(
            BinaryReader reader,
            System.Collections.Generic.IList<VesselOutlinePoint> points)
        {
            int count =
                ReadCount(
                    reader,
                    128);

            for (int index = 0;
                 index < count;
                 index++)
            {
                points.Add(
                    new VesselOutlinePoint
                    {
                        Horizontal =
                            reader.ReadDouble(),

                        Vertical =
                            reader.ReadDouble()
                    });
            }
        }

'@

    $codec =
        $codec.Replace(
            $marker,
            $helpers + $marker)
}

# ---------------------------------------------------------------------------
# Plugin: build one clean side-view convex hull per Part from native meshes.
# ---------------------------------------------------------------------------
if (-not $builder.Contains("BuildSideOutline(")) {
    $anchor = @'
            ReadVesselGeometry(
                vessel,
                part,
                node);

            return node;
'@

    if (-not $builder.Contains($anchor)) {
        throw "Could not locate geometry call in CreateNode."
    }

    $replacement = @'
            ReadVesselGeometry(
                vessel,
                part,
                node);

            BuildSideOutline(
                vessel,
                part,
                node);

            return node;
'@

    $builder =
        $builder.Replace(
            $anchor,
            $replacement)
}

if (-not $builder.Contains("private static void BuildSideOutline(")) {
    $marker = @'
        private static bool TryGetRendererLocalGeometryBounds(
'@

    if (-not $builder.Contains($marker)) {
        throw "Could not locate geometry helper insertion point."
    }

    $helpers = @'
        private static void BuildSideOutline(
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

            Transform reference =
                vessel.ReferenceTransform;

            if (reference == null)
            {
                return;
            }

            List<OutlineSample> samples =
                new List<OutlineSample>();

            try
            {
                Renderer[] renderers =
                    part.GetComponentsInChildren<Renderer>(
                        true);

                if (renderers == null)
                {
                    return;
                }

                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer =
                        renderers[rendererIndex];

                    if (renderer == null)
                    {
                        continue;
                    }

                    Part rendererOwner =
                        renderer.GetComponentInParent<Part>();

                    if (rendererOwner != part)
                    {
                        continue;
                    }

                    string typeName =
                        renderer.GetType().Name;

                    if (string.Equals(
                            typeName,
                            "ParticleSystemRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            typeName,
                            "LineRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            typeName,
                            "TrailRenderer",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddRendererOutlineSamples(
                        renderer,
                        reference,
                        samples);
                }
            }
            catch
            {
                return;
            }

            if (samples.Count < 3)
            {
                return;
            }

            List<OutlineSample> hull =
                BuildConvexHull(
                    samples);

            if (hull.Count < 3)
            {
                return;
            }

            hull =
                ReduceHull(
                    hull,
                    24);

            for (int index = 0;
                 index < hull.Count;
                 index++)
            {
                node.SideOutlinePoints.Add(
                    new VesselOutlinePoint
                    {
                        Horizontal =
                            hull[index].Horizontal,

                        Vertical =
                            hull[index].Vertical
                    });
            }
        }

        private static void AddRendererOutlineSamples(
            Renderer renderer,
            Transform reference,
            IList<OutlineSample> samples)
        {
            if (renderer == null ||
                reference == null ||
                samples == null)
            {
                return;
            }

            string typeName =
                renderer.GetType().Name;

            if (string.Equals(
                    typeName,
                    "SkinnedMeshRenderer",
                    StringComparison.Ordinal))
            {
                try
                {
                    System.Reflection.PropertyInfo sharedMeshProperty =
                        renderer.GetType().GetProperty(
                            "sharedMesh");

                    object mesh =
                        sharedMeshProperty != null
                            ? sharedMeshProperty.GetValue(
                                renderer,
                                null)
                            : null;

                    if (mesh != null)
                    {
                        AddMeshVertices(
                            mesh,
                            renderer.transform,
                            reference,
                            samples);

                        return;
                    }
                }
                catch
                {
                }
            }

            try
            {
                Component meshFilter =
                    renderer.GetComponent(
                        "MeshFilter");

                if (meshFilter == null)
                {
                    return;
                }

                System.Reflection.PropertyInfo sharedMeshProperty =
                    meshFilter.GetType().GetProperty(
                        "sharedMesh");

                object mesh =
                    sharedMeshProperty != null
                        ? sharedMeshProperty.GetValue(
                            meshFilter,
                            null)
                        : null;

                if (mesh == null)
                {
                    return;
                }

                AddMeshVertices(
                    mesh,
                    meshFilter.transform,
                    reference,
                    samples);
            }
            catch
            {
            }
        }

        private static void AddMeshVertices(
            object mesh,
            Transform geometryTransform,
            Transform reference,
            IList<OutlineSample> samples)
        {
            if (mesh == null ||
                geometryTransform == null ||
                reference == null)
            {
                return;
            }

            try
            {
                System.Reflection.PropertyInfo verticesProperty =
                    mesh.GetType().GetProperty(
                        "vertices");

                if (verticesProperty == null)
                {
                    return;
                }

                Vector3[] vertices =
                    verticesProperty.GetValue(
                        mesh,
                        null) as
                    Vector3[];

                if (vertices == null ||
                    vertices.Length == 0)
                {
                    return;
                }

                int stride =
                    Math.Max(
                        1,
                        vertices.Length /
                        512);

                for (int index = 0;
                     index < vertices.Length;
                     index += stride)
                {
                    Vector3 world =
                        geometryTransform.TransformPoint(
                            vertices[index]);

                    Vector3 vesselPoint =
                        reference.InverseTransformPoint(
                            world);

                    if (!IsFinite(vesselPoint.y) ||
                        !IsFinite(vesselPoint.z))
                    {
                        continue;
                    }

                    samples.Add(
                        new OutlineSample(
                            vesselPoint.z,
                            vesselPoint.y));
                }
            }
            catch
            {
            }
        }

        private static List<OutlineSample> BuildConvexHull(
            IList<OutlineSample> source)
        {
            List<OutlineSample> points =
                new List<OutlineSample>();

            for (int index = 0;
                 index < source.Count;
                 index++)
            {
                points.Add(
                    source[index]);
            }

            points.Sort(
                delegate(
                    OutlineSample left,
                    OutlineSample right)
                {
                    int horizontal =
                        left.Horizontal.CompareTo(
                            right.Horizontal);

                    return horizontal != 0
                        ? horizontal
                        : left.Vertical.CompareTo(
                            right.Vertical);
                });

            List<OutlineSample> unique =
                new List<OutlineSample>();

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                if (unique.Count == 0 ||
                    Math.Abs(
                        points[index].Horizontal -
                        unique[unique.Count - 1].Horizontal) >
                        1.0e-5 ||
                    Math.Abs(
                        points[index].Vertical -
                        unique[unique.Count - 1].Vertical) >
                        1.0e-5)
                {
                    unique.Add(
                        points[index]);
                }
            }

            if (unique.Count <= 2)
            {
                return unique;
            }

            List<OutlineSample> lower =
                new List<OutlineSample>();

            for (int index = 0;
                 index < unique.Count;
                 index++)
            {
                while (lower.Count >= 2 &&
                       Cross(
                           lower[lower.Count - 2],
                           lower[lower.Count - 1],
                           unique[index]) <= 0.0)
                {
                    lower.RemoveAt(
                        lower.Count - 1);
                }

                lower.Add(
                    unique[index]);
            }

            List<OutlineSample> upper =
                new List<OutlineSample>();

            for (int index = unique.Count - 1;
                 index >= 0;
                 index--)
            {
                while (upper.Count >= 2 &&
                       Cross(
                           upper[upper.Count - 2],
                           upper[upper.Count - 1],
                           unique[index]) <= 0.0)
                {
                    upper.RemoveAt(
                        upper.Count - 1);
                }

                upper.Add(
                    unique[index]);
            }

            lower.RemoveAt(
                lower.Count - 1);

            upper.RemoveAt(
                upper.Count - 1);

            lower.AddRange(
                upper);

            return lower;
        }

        private static List<OutlineSample> ReduceHull(
            IList<OutlineSample> hull,
            int maximumPoints)
        {
            List<OutlineSample> result =
                new List<OutlineSample>();

            if (hull == null ||
                hull.Count == 0)
            {
                return result;
            }

            if (hull.Count <= maximumPoints)
            {
                for (int index = 0;
                     index < hull.Count;
                     index++)
                {
                    result.Add(
                        hull[index]);
                }

                return result;
            }

            double step =
                (double)hull.Count /
                maximumPoints;

            for (int index = 0;
                 index < maximumPoints;
                 index++)
            {
                int sourceIndex =
                    Math.Min(
                        hull.Count - 1,
                        (int)Math.Floor(
                            index * step));

                result.Add(
                    hull[sourceIndex]);
            }

            return result;
        }

        private static double Cross(
            OutlineSample origin,
            OutlineSample left,
            OutlineSample right)
        {
            return
                (left.Horizontal -
                 origin.Horizontal) *
                (right.Vertical -
                 origin.Vertical) -
                (left.Vertical -
                 origin.Vertical) *
                (right.Horizontal -
                 origin.Horizontal);
        }

        private struct OutlineSample
        {
            public OutlineSample(
                double horizontal,
                double vertical)
            {
                Horizontal =
                    horizontal;

                Vertical =
                    vertical;
            }

            public double Horizontal;
            public double Vertical;
        }

'@

    $builder =
        $builder.Replace(
            $marker,
            $helpers + $marker)
}

# ---------------------------------------------------------------------------
# Mission Control: one clean SIDE schematic using transmitted convex outlines.
# ---------------------------------------------------------------------------
$pageSource = @'
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Telemetry;
using KMC.Shared.Topology;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Pages
{
    public sealed class SchematicPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private const int HeaderOffset = 70;
        private const int Gap = 14;
        private const int PanelHeaderHeight = 34;

        public string Name
        {
            get { return "VESSEL SCHEMATIC"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get { return Size.Empty; }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return MissionPageContentProfile.DenseEngineering;
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            MissionPageLayout layout =
                new MissionPageLayout(
                    context);

            layout.DrawHeader(
                Name,
                "VEHICLE / STRUCTURE");

            Rectangle content =
                context.ContentBounds;

            int top =
                content.Top +
                HeaderOffset;

            int sideWidth =
                Math.Max(
                    260,
                    (int)(content.Width * 0.22));

            Rectangle viewBounds =
                new Rectangle(
                    content.Left,
                    top,
                    Math.Max(
                        1,
                        content.Width -
                        sideWidth -
                        Gap),
                    Math.Max(
                        1,
                        content.Bottom -
                        top -
                        4));

            Rectangle statusBounds =
                new Rectangle(
                    viewBounds.Right +
                    Gap,
                    top,
                    Math.Max(
                        1,
                        content.Right -
                        viewBounds.Right -
                        Gap),
                    viewBounds.Height);

            DrawPanelFrame(
                context,
                viewBounds,
                "ACTIVE VESSEL / SIDE");

            DrawPanelFrame(
                context,
                statusBounds,
                "SCHEMATIC STATUS");

            VesselSchematicSnapshot snapshot =
                VesselSchematicTelemetryStore.GetLatest();

            if (snapshot == null ||
                snapshot.Topology == null ||
                snapshot.Topology.Nodes == null ||
                snapshot.Topology.Nodes.Count == 0)
            {
                DrawWaiting(
                    context,
                    viewBounds);

                DrawStatus(
                    context,
                    statusBounds,
                    null,
                    DateTime.MinValue,
                    0,
                    0);

                return;
            }

            int outlineParts;
            int fallbackParts;

            DrawVessel(
                context,
                viewBounds,
                snapshot.Topology,
                out outlineParts,
                out fallbackParts);

            DrawStatus(
                context,
                statusBounds,
                snapshot.Topology,
                snapshot.ReceivedUtc,
                outlineParts,
                fallbackParts);
        }

        private static void DrawVessel(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology,
            out int outlineParts,
            out int fallbackParts)
        {
            outlineParts = 0;
            fallbackParts = 0;

            Rectangle drawing =
                Rectangle.FromLTRB(
                    bounds.Left + 34,
                    bounds.Top +
                        PanelHeaderHeight +
                        24,
                    bounds.Right - 34,
                    bounds.Bottom - 30);

            double minH =
                double.PositiveInfinity;

            double maxH =
                double.NegativeInfinity;

            double minV =
                double.PositiveInfinity;

            double maxV =
                double.NegativeInfinity;

            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null)
                {
                    continue;
                }

                if (node.SideOutlinePoints != null &&
                    node.SideOutlinePoints.Count >= 3)
                {
                    outlineParts++;

                    for (int pointIndex = 0;
                         pointIndex < node.SideOutlinePoints.Count;
                         pointIndex++)
                    {
                        VesselOutlinePoint point =
                            node.SideOutlinePoints[pointIndex];

                        Expand(
                            point.Horizontal,
                            point.Vertical,
                            ref minH,
                            ref maxH,
                            ref minV,
                            ref maxV);
                    }
                }
                else
                {
                    fallbackParts++;

                    Expand(
                        node.VesselZ,
                        node.VesselY,
                        ref minH,
                        ref maxH,
                        ref minV,
                        ref maxV);
                }
            }

            if (!IsFinite(minH) ||
                !IsFinite(maxH) ||
                !IsFinite(minV) ||
                !IsFinite(maxV))
            {
                DrawWaiting(
                    context,
                    bounds);

                return;
            }

            double rangeH =
                Math.Max(
                    0.5,
                    maxH - minH);

            double rangeV =
                Math.Max(
                    0.5,
                    maxV - minV);

            double scale =
                Math.Min(
                    drawing.Width /
                        (rangeH * 1.08),
                    drawing.Height /
                        (rangeV * 1.08));

            double centerH =
                (minH + maxH) /
                2.0;

            double centerV =
                (minV + maxV) /
                2.0;

            float screenCenterX =
                drawing.Left +
                drawing.Width /
                2.0f;

            float screenCenterY =
                drawing.Top +
                drawing.Height /
                2.0f;

            context.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null)
                {
                    continue;
                }

                bool isRoot =
                    topology.HasRootPart &&
                    node.PartId ==
                        topology.RootPartId;

                if (node.SideOutlinePoints != null &&
                    node.SideOutlinePoints.Count >= 3)
                {
                    PointF[] polygon =
                        new PointF[
                            node.SideOutlinePoints.Count];

                    for (int pointIndex = 0;
                         pointIndex < node.SideOutlinePoints.Count;
                         pointIndex++)
                    {
                        VesselOutlinePoint point =
                            node.SideOutlinePoints[pointIndex];

                        polygon[pointIndex] =
                            ToScreen(
                                point.Horizontal,
                                point.Vertical,
                                centerH,
                                centerV,
                                scale,
                                screenCenterX,
                                screenCenterY);
                    }

                    DrawPartOutline(
                        context,
                        polygon,
                        node,
                        isRoot);
                }
                else
                {
                    PointF center =
                        ToScreen(
                            node.VesselZ,
                            node.VesselY,
                            centerH,
                            centerV,
                            scale,
                            screenCenterX,
                            screenCenterY);

                    DrawFallbackGlyph(
                        context,
                        center,
                        node,
                        isRoot);
                }
            }
        }

        private static void DrawPartOutline(
            MissionRenderContext context,
            PointF[] polygon,
            VesselTopologyNode node,
            bool isRoot)
        {
            if (polygon == null ||
                polygon.Length < 3)
            {
                return;
            }

            Color outlineColor =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            int fillAlpha =
                IsMajorPart(node)
                    ? 26
                    : 10;

            int lineAlpha =
                IsMajorPart(node)
                    ? 220
                    : 135;

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        fillAlpha,
                        outlineColor)))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        lineAlpha,
                        outlineColor),
                    isRoot
                        ? 2.0f
                        : IsMajorPart(node)
                            ? 1.5f
                            : 1.0f))
            {
                context.Graphics.FillPolygon(
                    fill,
                    polygon);

                context.Graphics.DrawPolygon(
                    pen,
                    polygon);
            }
        }

        private static bool IsMajorPart(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            return
                node.Category ==
                    VesselNodeCategory.Command ||
                node.Category ==
                    VesselNodeCategory.Engine ||
                node.Category ==
                    VesselNodeCategory.SolidBooster ||
                node.Category ==
                    VesselNodeCategory.FuelTank ||
                node.Category ==
                    VesselNodeCategory.SolarPanel ||
                node.Category ==
                    VesselNodeCategory.DockingPort ||
                node.IsSeparationBoundary;
        }

        private static void DrawFallbackGlyph(
            MissionRenderContext context,
            PointF point,
            VesselTopologyNode node,
            bool isRoot)
        {
            float size =
                isRoot
                    ? 7.0f
                    : 4.0f;

            Color color =
                isRoot
                    ? Color.White
                    : context.DimPhosphorColor;

            using (Pen pen =
                new Pen(
                    color,
                    isRoot
                        ? 1.8f
                        : 1.0f))
            {
                context.Graphics.DrawEllipse(
                    pen,
                    point.X - size,
                    point.Y - size,
                    size * 2.0f,
                    size * 2.0f);
            }
        }

        private static PointF ToScreen(
            double horizontal,
            double vertical,
            double centerH,
            double centerV,
            double scale,
            float screenCenterX,
            float screenCenterY)
        {
            return
                new PointF(
                    screenCenterX +
                        (float)(
                            (horizontal - centerH) *
                            scale),

                    screenCenterY -
                        (float)(
                            (vertical - centerV) *
                            scale));
        }

        private static void Expand(
            double horizontal,
            double vertical,
            ref double minH,
            ref double maxH,
            ref double minV,
            ref double maxV)
        {
            if (!IsFinite(horizontal) ||
                !IsFinite(vertical))
            {
                return;
            }

            minH =
                Math.Min(
                    minH,
                    horizontal);

            maxH =
                Math.Max(
                    maxH,
                    horizontal);

            minV =
                Math.Min(
                    minV,
                    vertical);

            maxV =
                Math.Max(
                    maxV,
                    vertical);
        }

        private static void DrawWaiting(
            MissionRenderContext context,
            Rectangle bounds)
        {
            Rectangle message =
                Rectangle.FromLTRB(
                    bounds.Left + 24,
                    bounds.Top +
                        PanelHeaderHeight +
                        40,
                    bounds.Right - 24,
                    bounds.Bottom - 24);

            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    "WAITING FOR ACTIVE VESSEL SCHEMATIC",
                    context.LargeFont,
                    brush,
                    message,
                    format);
            }
        }

        private static void DrawStatus(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology,
            DateTime receivedUtc,
            int outlineParts,
            int fallbackParts)
        {
            int x =
                bounds.Left + 14;

            int valueX =
                bounds.Left +
                Math.Max(
                    100,
                    (int)(bounds.Width * 0.43));

            int y =
                bounds.Top +
                PanelHeaderHeight +
                16;

            DrawField(
                context,
                "LINK",
                topology != null
                    ? "SCHEM ONLINE"
                    : "WAITING",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "VESSEL",
                topology != null
                    ? Safe(
                        topology.VesselName)
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "PARTS",
                topology != null
                    ? topology.PartCount.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "OUTLINES",
                topology != null
                    ? outlineParts.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "FALLBACK",
                topology != null
                    ? fallbackParts.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "STAGE",
                topology != null
                    ? topology.CurrentStage.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "REV",
                topology != null
                    ? topology.Revision.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "VIEW",
                "SIDE",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "AGE",
                receivedUtc != DateTime.MinValue
                    ? Math.Max(
                        0.0,
                        (DateTime.UtcNow -
                         receivedUtc).TotalSeconds)
                        .ToString("0.0") +
                      " S"
                    : "---",
                x,
                valueX,
                y);

            y += 48;

            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    "MESH HULL SCHEMATIC",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 24;

                context.Graphics.DrawString(
                    "NO STRUCTURAL LINKS",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 22;

                context.Graphics.DrawString(
                    "SINGLE SIDE VIEW",
                    context.SmallFont,
                    brush,
                    x,
                    y);
            }
        }

        private static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int labelX,
            int valueX,
            int y)
        {
            using (SolidBrush dim =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush bright =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    dim,
                    labelX,
                    y);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    bright,
                    valueX,
                    y);
            }
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (Pen pen =
                new Pen(
                    context.DimPhosphorColor,
                    1.0f))
            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds.Left,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width - 1),
                    Math.Max(
                        0,
                        bounds.Height - 1));

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    brush,
                    bounds.Left + 10,
                    bounds.Top + 8);

                context.Graphics.DrawLine(
                    pen,
                    bounds.Left,
                    bounds.Top +
                        PanelHeaderHeight,
                    bounds.Right,
                    bounds.Top +
                        PanelHeaderHeight);
            }
        }

        private static string Safe(
            string value)
        {
            return
                string.IsNullOrWhiteSpace(
                    value)
                    ? "---"
                    : value.Trim();
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }
    }
}
'@

WriteText $pagePath $pageSource

# ---------------------------------------------------------------------------
# Tests: v4 outline transport + single-view/no structural links.
# ---------------------------------------------------------------------------
if (-not $tests.Contains('Run("SCHEM side outline survives topology transport"')) {
    $anchor =
        '        Run("SCHEM geometry uses native local mesh bounds", VesselSchematicNativeBoundsRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.23.2c regression registration."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("SCHEM side outline survives topology transport", VesselSchematicOutlineTransportRegression);' +
            "`r`n" +
            '        Run("SCHEM uses one clean side-view renderer", VesselSchematicSingleViewRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicOutlineTransportRegression()")) {
    $anchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests insertion point."
    }

    $methods = @'
    private static void VesselSchematicOutlineTransportRegression()
    {
        VesselTopology topology =
            new VesselTopology
            {
                VesselId = "SCHEM-OUTLINE",
                VesselName = "Outline Test",
                RootPartId = 1U,
                HasRootPart = true,
                PartCount = 1,
                Revision = 11
            };

        VesselTopologyNode node =
            new VesselTopologyNode
            {
                PartId = 1U,
                PartTitle = "Hull"
            };

        node.SideOutlinePoints.Add(
            new VesselOutlinePoint
            {
                Horizontal = -2.0,
                Vertical = -1.0
            });

        node.SideOutlinePoints.Add(
            new VesselOutlinePoint
            {
                Horizontal = 2.0,
                Vertical = -1.0
            });

        node.SideOutlinePoints.Add(
            new VesselOutlinePoint
            {
                Horizontal = 0.0,
                Vertical = 3.0
            });

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
            "SCHEM outline transport decode failed");

        Check(
            decoded != null &&
            decoded.TransportVersion >= 4 &&
            decoded.Nodes.Count == 1 &&
            decoded.Nodes[0].SideOutlinePoints.Count == 3,
            "SCHEM outline collection lost");

        Near(
            decoded.Nodes[0]
                .SideOutlinePoints[0]
                .Horizontal,
            -2.0);

        Near(
            decoded.Nodes[0]
                .SideOutlinePoints[2]
                .Vertical,
            3.0);
    }

    private static void VesselSchematicSingleViewRegression()
    {
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
                "BuildConvexHull",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "SideOutlinePoints.Add",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "vertices",
                StringComparison.Ordinal) >= 0,
            "SCHEM mesh-outline generation missing");

        Check(
            page.IndexOf(
                "\"ACTIVE VESSEL / SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"SINGLE SIDE VIEW\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM single side view missing");

        Check(
            page.IndexOf(
                "DrawPolygon",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "FillPolygon",
                StringComparison.Ordinal) >= 0,
            "SCHEM clean polygon renderer missing");

        Check(
            page.IndexOf(
                "PointerDown",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) < 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) < 0,
            "SCHEM legacy multi-view controls remain");
    }


'@

    $tests =
        $tests.Replace(
            $anchor,
            $methods + $anchor)
}

# ---------------------------------------------------------------------------
# Write updated existing files only after all mutations succeeded.
# ---------------------------------------------------------------------------
WriteText $nodePath $node
WriteText $codecPath $codec
WriteText $sharedProjectPath $sharedProject
WriteText $pluginProjectPath $pluginProject
WriteText $builderPath $builder
WriteText $testPath $tests

Write-Host "Applied KMC 14.23.3 Clean Single-View Schematic."
Write-Host "SCHEM now transmits simplified per-Part side-view mesh hulls."
Write-Host "FRONT/SIDE/TOP controls and structural-link rendering were removed."
Write-Host "KMC.Plugin.dll MUST be rebuilt and replaced."
