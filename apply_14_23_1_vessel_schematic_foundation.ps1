param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) { throw "Missing file: $path" }
    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path,[string]$text) {
    $directory = [System.IO.Path]::GetDirectoryName($path)
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    [System.IO.File]::WriteAllText($path,$text,[System.Text.UTF8Encoding]::new($false))
}

# ---------------------------------------------------------------------------
# Verify baseline.
# ---------------------------------------------------------------------------
$mainPath = Join-Path $RepoRoot "KMC.MissionControl\MainForm.cs"
$projectPath = Join-Path $RepoRoot "KMC.MissionControl\KMC.MissionControl.csproj"
$testPath = Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$main = ReadText $mainPath
$project = ReadText $projectPath
$tests = ReadText $testPath

if (-not $main.Contains('"MAP",') -or
    -not $tests.Contains('Run("post-burn cleanup and queue handoff are explicit"')) {
    throw "Expected 14.22.76 baseline markers were not found. No files written."
}

# ---------------------------------------------------------------------------
# New source files.
# ---------------------------------------------------------------------------
$receiverSource = @'
using KMC.Shared.Topology;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace KMC.MissionControl.Telemetry
{
    internal sealed class VesselSchematicSnapshot
    {
        public VesselTopology Topology { get; set; }
        public DateTime ReceivedUtc { get; set; }
    }

    internal static class VesselSchematicTelemetryStore
    {
        private static readonly object SyncRoot =
            new object();

        private static VesselSchematicSnapshot _latest;

        public static void Publish(
            VesselTopology topology,
            DateTime receivedUtc)
        {
            if (topology == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _latest =
                    new VesselSchematicSnapshot
                    {
                        Topology = topology,
                        ReceivedUtc = receivedUtc
                    };
            }
        }

        public static VesselSchematicSnapshot GetLatest()
        {
            lock (SyncRoot)
            {
                return _latest;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest = null;
            }
        }
    }

    internal sealed class VesselSchematicTelemetryReceiver :
        IDisposable
    {
        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        public event Action<VesselTopology> SnapshotReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            _client =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Loopback,
                        VesselTopologyPacketCodec.TopologyPort));

            _running = true;

            _thread =
                new Thread(
                    ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KMC Vessel Schematic Telemetry"
                };

            _thread.Start();
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender =
                        new IPEndPoint(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        _client.Receive(
                            ref sender);

                    VesselTopology topology;

                    if (!VesselTopologyPacketCodec.TryDecode(
                            data,
                            out topology) ||
                        topology == null)
                    {
                        continue;
                    }

                    DateTime receivedUtc =
                        DateTime.UtcNow;

                    VesselSchematicTelemetryStore.Publish(
                        topology,
                        receivedUtc);

                    Action<VesselTopology> handler =
                        SnapshotReceived;

                    if (handler != null)
                    {
                        handler(topology);
                    }
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch
                {
                    if (!_running)
                    {
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            _running = false;

            if (_client != null)
            {
                try
                {
                    _client.Close();
                }
                catch
                {
                }

                _client = null;
            }

            if (_thread != null &&
                _thread.IsAlive)
            {
                _thread.Join(250);
            }

            _thread = null;
        }
    }
}
'@

$pageSource = @'
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Telemetry;
using KMC.Shared.Topology;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Pages
{
    public sealed class SchematicPage :
        IMissionPage,
        IMissionPageCanvasProvider,
        IMissionPagePointerInput
    {
        private const int HeaderOffset = 70;
        private const int Gap = 14;
        private const int PanelHeaderHeight = 34;

        private enum SchematicView
        {
            Front = 0,
            Side = 1,
            Top = 2
        }

        private SchematicView _view =
            SchematicView.Side;

        private Rectangle _frontTab;
        private Rectangle _sideTab;
        private Rectangle _topTab;

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
                    280,
                    (int)(content.Width * 0.24));

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
                "ACTIVE VESSEL");

            DrawPanelFrame(
                context,
                statusBounds,
                "SCHEMATIC STATUS");

            DrawViewTabs(
                context,
                viewBounds);

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
                    DateTime.MinValue);

                return;
            }

            DrawVessel(
                context,
                viewBounds,
                snapshot.Topology);

            DrawStatus(
                context,
                statusBounds,
                snapshot.Topology,
                snapshot.ReceivedUtc);
        }

        public bool PointerDown(
            PointF virtualPoint,
            MouseButtons button)
        {
            if (button != MouseButtons.Left)
            {
                return false;
            }

            Point point =
                Point.Round(
                    virtualPoint);

            if (_frontTab.Contains(point))
            {
                _view =
                    SchematicView.Front;

                return true;
            }

            if (_sideTab.Contains(point))
            {
                _view =
                    SchematicView.Side;

                return true;
            }

            if (_topTab.Contains(point))
            {
                _view =
                    SchematicView.Top;

                return true;
            }

            return false;
        }

        public bool PointerMove(
            PointF virtualPoint,
            MouseButtons buttons)
        {
            return false;
        }

        public bool PointerUp(
            PointF virtualPoint,
            MouseButtons button)
        {
            return false;
        }

        public bool PointerWheel(
            PointF virtualPoint,
            int delta)
        {
            return false;
        }

        private void DrawViewTabs(
            MissionRenderContext context,
            Rectangle bounds)
        {
            int tabTop =
                bounds.Top +
                PanelHeaderHeight +
                10;

            int tabWidth =
                112;

            int tabHeight =
                30;

            int left =
                bounds.Left +
                14;

            _frontTab =
                new Rectangle(
                    left,
                    tabTop,
                    tabWidth,
                    tabHeight);

            _sideTab =
                new Rectangle(
                    _frontTab.Right + 8,
                    tabTop,
                    tabWidth,
                    tabHeight);

            _topTab =
                new Rectangle(
                    _sideTab.Right + 8,
                    tabTop,
                    tabWidth,
                    tabHeight);

            DrawViewTab(
                context,
                _frontTab,
                "FRONT",
                _view == SchematicView.Front);

            DrawViewTab(
                context,
                _sideTab,
                "SIDE",
                _view == SchematicView.Side);

            DrawViewTab(
                context,
                _topTab,
                "TOP",
                _view == SchematicView.Top);
        }

        private static void DrawViewTab(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            bool active)
        {
            Color color =
                active
                    ? context.PhosphorColor
                    : context.DimPhosphorColor;

            using (Pen pen =
                new Pen(
                    color,
                    active
                        ? 1.8f
                        : 1.0f))
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (StringFormat format =
                new StringFormat())
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds);

                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    brush,
                    bounds,
                    format);
            }
        }

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

            Dictionary<uint, PointF> points =
                new Dictionary<uint, PointF>();

            double minA =
                double.PositiveInfinity;

            double maxA =
                double.NegativeInfinity;

            double minB =
                double.PositiveInfinity;

            double maxB =
                double.NegativeInfinity;

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

                double a;
                double b;

                Project(
                    node,
                    out a,
                    out b);

                if (!IsFinite(a) ||
                    !IsFinite(b))
                {
                    continue;
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

                double a;
                double b;

                Project(
                    node,
                    out a,
                    out b);

                if (!IsFinite(a) ||
                    !IsFinite(b))
                {
                    continue;
                }

                points[node.PartId] =
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

            using (Pen structural =
                new Pen(
                    context.DimPhosphorColor,
                    1.2f))
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

                    if (points.TryGetValue(
                            node.PartId,
                            out child) &&
                        points.TryGetValue(
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

                PointF point;

                if (node == null ||
                    !points.TryGetValue(
                        node.PartId,
                        out point))
                {
                    continue;
                }

                bool isRoot =
                    topology.HasRootPart &&
                    node.PartId ==
                        topology.RootPartId;

                DrawPartGlyph(
                    context,
                    point,
                    node,
                    isRoot);
            }

            DrawAxes(
                context,
                drawing);
        }

        private void Project(
            VesselTopologyNode node,
            out double a,
            out double b)
        {
            if (_view ==
                SchematicView.Front)
            {
                a =
                    node.VesselX;

                b =
                    node.VesselY;

                return;
            }

            if (_view ==
                SchematicView.Top)
            {
                a =
                    node.VesselX;

                b =
                    node.VesselZ;

                return;
            }

            a =
                node.VesselZ;

            b =
                node.VesselY;
        }

        private static void DrawPartGlyph(
            MissionRenderContext context,
            PointF point,
            VesselTopologyNode node,
            bool isRoot)
        {
            Color color =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            float size =
                isRoot
                    ? 14.0f
                    : 10.0f;

            using (Pen pen =
                new Pen(
                    color,
                    isRoot
                        ? 2.0f
                        : 1.4f))
            {
                if (node.Category ==
                    VesselNodeCategory.Engine ||
                    node.Category ==
                    VesselNodeCategory.SolidBooster)
                {
                    PointF[] triangle =
                    {
                        new PointF(
                            point.X,
                            point.Y - size),

                        new PointF(
                            point.X - size * 0.75f,
                            point.Y + size * 0.75f),

                        new PointF(
                            point.X + size * 0.75f,
                            point.Y + size * 0.75f)
                    };

                    context.Graphics.DrawPolygon(
                        pen,
                        triangle);

                    return;
                }

                if (node.Category ==
                    VesselNodeCategory.Command)
                {
                    PointF[] diamond =
                    {
                        new PointF(
                            point.X,
                            point.Y - size),

                        new PointF(
                            point.X + size,
                            point.Y),

                        new PointF(
                            point.X,
                            point.Y + size),

                        new PointF(
                            point.X - size,
                            point.Y)
                    };

                    context.Graphics.DrawPolygon(
                        pen,
                        diamond);

                    return;
                }

                if (node.Category ==
                    VesselNodeCategory.SolarPanel)
                {
                    context.Graphics.DrawLine(
                        pen,
                        point.X - size * 1.5f,
                        point.Y,
                        point.X + size * 1.5f,
                        point.Y);

                    context.Graphics.DrawLine(
                        pen,
                        point.X,
                        point.Y - size * 0.6f,
                        point.X,
                        point.Y + size * 0.6f);

                    return;
                }

                RectangleF box =
                    new RectangleF(
                        point.X - size * 0.65f,
                        point.Y - size * 0.65f,
                        size * 1.3f,
                        size * 1.3f);

                if (node.Category ==
                    VesselNodeCategory.FuelTank)
                {
                    context.Graphics.DrawEllipse(
                        pen,
                        box);
                }
                else
                {
                    context.Graphics.DrawRectangle(
                        pen,
                        box.X,
                        box.Y,
                        box.Width,
                        box.Height);
                }
            }
        }

        private void DrawAxes(
            MissionRenderContext context,
            Rectangle drawing)
        {
            string horizontal;
            string vertical;

            if (_view ==
                SchematicView.Front)
            {
                horizontal = "X";
                vertical = "Y";
            }
            else if (_view ==
                SchematicView.Top)
            {
                horizontal = "X";
                vertical = "Z";
            }
            else
            {
                horizontal = "Z";
                vertical = "Y";
            }

            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    horizontal,
                    context.SmallFont,
                    brush,
                    drawing.Right - 18,
                    drawing.Bottom - 22);

                context.Graphics.DrawString(
                    vertical,
                    context.SmallFont,
                    brush,
                    drawing.Left + 4,
                    drawing.Top + 2);
            }
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
                        60,
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
                    "WAITING FOR ACTIVE VESSEL TOPOLOGY",
                    context.LargeFont,
                    brush,
                    message,
                    format);
            }
        }

        private void DrawStatus(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology,
            DateTime receivedUtc)
        {
            int x =
                bounds.Left + 14;

            int valueX =
                bounds.Left +
                Math.Max(
                    105,
                    (int)(bounds.Width * 0.44));

            int y =
                bounds.Top +
                PanelHeaderHeight +
                16;

            DrawField(
                context,
                "LINK",
                topology != null
                    ? "TOPOLOGY ONLINE"
                    : "WAITING",
                x,
                valueX,
                y);

            y += 30;

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

            y += 30;

            DrawField(
                context,
                "PARTS",
                topology != null
                    ? topology.PartCount.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 30;

            DrawField(
                context,
                "STAGE",
                topology != null
                    ? topology.CurrentStage.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 30;

            DrawField(
                context,
                "REV",
                topology != null
                    ? topology.Revision.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 30;

            DrawField(
                context,
                "VIEW",
                _view.ToString().ToUpperInvariant(),
                x,
                valueX,
                y);

            y += 30;

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
                    "FOUNDATION VIEW",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 24;

                context.Graphics.DrawString(
                    "PART CENTERS +",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 22;

                context.Graphics.DrawString(
                    "STRUCTURAL LINKS",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 34;

                context.Graphics.DrawString(
                    "FRONT / SIDE / TOP",
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

WriteText (Join-Path $RepoRoot "KMC.MissionControl\Telemetry\VesselSchematicTelemetry.cs") $receiverSource
WriteText (Join-Path $RepoRoot "KMC.MissionControl\Pages\SchematicPage.cs") $pageSource

# ---------------------------------------------------------------------------
# MissionControl project includes.
# ---------------------------------------------------------------------------
if (-not $project.Contains('Telemetry\VesselSchematicTelemetry.cs')) {
    $anchor = '<Compile Include="Telemetry\OrbitMapTelemetryReceiver.cs" />'
    if (-not $project.Contains($anchor)) {
        throw "Could not locate MissionControl telemetry compile anchor."
    }

    $project = $project.Replace(
        $anchor,
        '<Compile Include="Telemetry\VesselSchematicTelemetry.cs" />' + $anchor)
}

if (-not $project.Contains('Pages\SchematicPage.cs')) {
    $anchor = '<Compile Include="Pages\SystemsPage.cs" />'
    if (-not $project.Contains($anchor)) {
        throw "Could not locate MissionControl page compile anchor."
    }

    $project = $project.Replace(
        $anchor,
        $anchor + '<Compile Include="Pages\SchematicPage.cs" />')
}

WriteText $projectPath $project

# ---------------------------------------------------------------------------
# MainForm receiver ownership + SCHEM navigation.
# ---------------------------------------------------------------------------
$main = ReadText $mainPath

if (-not $main.Contains('private readonly VesselSchematicTelemetryReceiver')) {
    $anchor = @'
        private readonly OrbitMapTelemetryReceiver _orbitMapReceiver;
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate OrbitMap receiver field."
    }

    $main = $main.Replace(
        $anchor,
        $anchor +
        '        private readonly VesselSchematicTelemetryReceiver _vesselSchematicReceiver;' +
        "`r`n")
}

if (-not $main.Contains('private bool _schematicPageActive;')) {
    $anchor = @'
        private bool _mapPageActive;
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate map-active field."
    }

    $main = $main.Replace(
        $anchor,
        $anchor +
        '        private bool _schematicPageActive;' +
        "`r`n")
}

if (-not $main.Contains('_vesselSchematicReceiver =')) {
    $anchor = @'
_orbitMapReceiver =
                new OrbitMapTelemetryReceiver();
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate OrbitMap receiver construction."
    }

    $main = $main.Replace(
        $anchor,
        $anchor + @'

            _vesselSchematicReceiver =
                new VesselSchematicTelemetryReceiver();
'@)
}

if (-not $main.Contains('_vesselSchematicReceiver.SnapshotReceived +=')) {
    $anchor = @'
_orbitMapReceiver.SnapshotReceived +=
                OnOrbitMapSnapshotReceived;
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate OrbitMap subscription."
    }

    $main = $main.Replace(
        $anchor,
        $anchor + @'

            _vesselSchematicReceiver.SnapshotReceived +=
                OnVesselSchematicSnapshotReceived;
'@)
}

if (-not $main.Contains('"SCHEM",')) {
    $anchor = @'
            _navigationBar.AddPage(
                "MAP",
                _mapPage);
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate MAP navigation entry."
    }

    $main = $main.Replace(
        $anchor,
        $anchor + @'

            _navigationBar.AddPage(
                "SCHEM",
                new SchematicPage());
'@)
}

if (-not $main.Contains('_schematicPageActive =')) {
    $anchor = @'
            _mapPageActive =
                string.Equals(
                    title,
                    "MAP",
                    StringComparison.OrdinalIgnoreCase);
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate MAP page-active assignment."
    }

    $main = $main.Replace(
        $anchor,
        $anchor + @'

            _schematicPageActive =
                string.Equals(
                    title,
                    "SCHEM",
                    StringComparison.OrdinalIgnoreCase);
'@)
}

if (-not $main.Contains('VesselSchematicTelemetryStore.Clear();')) {
    $anchor = @'
            OrbitMapSnapshotStore.Clear();
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate topology store startup anchor."
    }

    $main = $main.Replace(
        $anchor,
        $anchor +
        '                VesselSchematicTelemetryStore.Clear();' +
        "`r`n")
}

if (-not $main.Contains('_vesselSchematicReceiver.Start();')) {
    $anchor = @'
                _orbitMapReceiver.Start();
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate OrbitMap receiver startup."
    }

    $main = $main.Replace(
        $anchor,
        $anchor +
        '                _vesselSchematicReceiver.Start();' +
        "`r`n")
}

if (-not $main.Contains('private void OnVesselSchematicSnapshotReceived(')) {
    $anchor = @'
        private void OnOrbitMapSnapshotReceived(
'@
    $pos = $main.IndexOf($anchor)
    if ($pos -lt 0) {
        throw "Could not locate OrbitMap snapshot handler."
    }

    $handler = @'
        private void OnVesselSchematicSnapshotReceived(
            KMC.Shared.Topology.VesselTopology topology)
        {
            if (!_schematicPageActive ||
                IsDisposed ||
                !IsHandleCreated)
            {
                return;
            }

            BeginInvoke(
                (MethodInvoker)delegate
                {
                    if (_schematicPageActive &&
                        !_missionDisplay.IsDisposed)
                    {
                        _missionDisplay.RequestRender();
                    }
                });
        }

'@

    $main =
        $main.Substring(0,$pos) +
        $handler +
        $main.Substring($pos)
}

if (-not $main.Contains('_vesselSchematicReceiver.SnapshotReceived -=')) {
    $anchor = @'
            _orbitMapReceiver.SnapshotReceived -=
                OnOrbitMapSnapshotReceived;
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate OrbitMap unsubscription."
    }

    $main = $main.Replace(
        $anchor,
        $anchor + @'

            _vesselSchematicReceiver.SnapshotReceived -=
                OnVesselSchematicSnapshotReceived;
'@)
}

if (-not $main.Contains('_vesselSchematicReceiver.Dispose();')) {
    $anchor = @'
            _orbitMapReceiver.Dispose();
'@
    if (-not $main.Contains($anchor)) {
        throw "Could not locate OrbitMap receiver disposal."
    }

    $main = $main.Replace(
        $anchor,
        $anchor +
        '            _vesselSchematicReceiver.Dispose();' +
        "`r`n")
}

# Ensure shutdown store clear even if startup clear already exists.
$closingAnchor = @'
            OrbitMapSnapshotStore.Clear();
            ManeuverInventoryStore.Clear();
'@
if ($main.Contains($closingAnchor) -and
    -not $main.Contains(
        "            OrbitMapSnapshotStore.Clear();`r`n            VesselSchematicTelemetryStore.Clear();`r`n            ManeuverInventoryStore.Clear();")) {
    $main = $main.Replace(
        $closingAnchor,
        "            OrbitMapSnapshotStore.Clear();`r`n            VesselSchematicTelemetryStore.Clear();`r`n            ManeuverInventoryStore.Clear();")
}

WriteText $mainPath $main

# ---------------------------------------------------------------------------
# Regression coverage.
# ---------------------------------------------------------------------------
$tests = ReadText $testPath

if (-not $tests.Contains("using KMC.Shared.Topology;")) {
    $tests = $tests.Replace(
        "using KMC.Shared;`r`n",
        "using KMC.Shared;`r`nusing KMC.Shared.Topology;`r`n")
}

if (-not $tests.Contains('Run("SCHEM vessel topology pipeline preserves projected part positions"')) {
    $anchor = '        Run("post-burn cleanup and queue handoff are explicit", PostBurnCleanupQueueHandoffRegression);'
    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.22.76 test anchor."
    }

    $tests = $tests.Replace(
        $anchor,
        $anchor + "`r`n" +
        '        Run("SCHEM vessel topology pipeline preserves projected part positions", VesselSchematicFoundationRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicFoundationRegression()")) {
    $anchor = "    private static void ManeuverUplinkDesiredPeriapsis()"
    if (-not $tests.Contains($anchor)) {
        throw "Could not locate regression method insertion point."
    }

    $method = @'
    private static void VesselSchematicFoundationRegression()
    {
        VesselTopology topology =
            new VesselTopology
            {
                VesselId = "SCHEM-TEST",
                VesselName = "Schematic Test",
                RootPartId = 1U,
                HasRootPart = true,
                PartCount = 2,
                CurrentStage = 3,
                Revision = 7
            };

        topology.Nodes.Add(
            new VesselTopologyNode
            {
                PartId = 1U,
                PartTitle = "Root",
                VesselX = 1.25,
                VesselY = 2.5,
                VesselZ = -3.75
            });

        topology.Nodes.Add(
            new VesselTopologyNode
            {
                PartId = 2U,
                ParentPartId = 1U,
                HasParent = true,
                PartTitle = "Child",
                VesselX = -4.5,
                VesselY = 5.75,
                VesselZ = 6.25
            });

        byte[] encoded =
            VesselTopologyPacketCodec.Encode(
                topology);

        VesselTopology decoded;

        Check(
            VesselTopologyPacketCodec.TryDecode(
                encoded,
                out decoded),
            "SCHEM topology round trip failed");

        Check(
            decoded != null &&
            decoded.Nodes.Count == 2,
            "SCHEM topology node count lost");

        Near(
            decoded.Nodes[0].VesselX,
            1.25);

        Near(
            decoded.Nodes[0].VesselY,
            2.5);

        Near(
            decoded.Nodes[0].VesselZ,
            -3.75);

        Near(
            decoded.Nodes[1].VesselX,
            -4.5);

        Near(
            decoded.Nodes[1].VesselY,
            5.75);

        Near(
            decoded.Nodes[1].VesselZ,
            6.25);

        string main =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "MainForm.cs"));

        string page =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Pages",
                    "SchematicPage.cs"));

        string receiver =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.MissionControl",
                    "Telemetry",
                    "VesselSchematicTelemetry.cs"));

        Check(
            main.IndexOf(
                "\"SCHEM\"",
                StringComparison.Ordinal) >= 0 &&
            main.IndexOf(
                "new SchematicPage()",
                StringComparison.Ordinal) >= 0,
            "SCHEM navigation tab missing");

        Check(
            receiver.IndexOf(
                "VesselTopologyPacketCodec.TopologyPort",
                StringComparison.Ordinal) >= 0 &&
            receiver.IndexOf(
                "VesselTopologyPacketCodec.TryDecode",
                StringComparison.Ordinal) >= 0,
            "SCHEM does not consume existing KMC topology transport");

        Check(
            page.IndexOf(
                "\"FRONT\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"SIDE\"",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "\"TOP\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM orthographic view selectors missing");

        Check(
            page.IndexOf(
                "node.ParentPartId",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselX",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselY",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "node.VesselZ",
                StringComparison.Ordinal) >= 0,
            "SCHEM structural projection path incomplete");
    }


'@
    $tests = $tests.Replace(
        $anchor,
        $method + $anchor)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.23.1 Vessel Schematic Foundation."
Write-Host "SCHEM reuses the existing KMC vessel-topology UDP stream on port 5082."
Write-Host "No maneuver, guidance, propulsion, electrical, or KSP control logic was changed."
