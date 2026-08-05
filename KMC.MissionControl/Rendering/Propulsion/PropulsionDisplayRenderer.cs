using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.MissionControl.Models;
using KMC.MissionControl.Telemetry;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Full-page propulsion display generator. The system-flow diagram and
    /// physical engine projection are rendered as coordinated operator views.
    /// </summary>
    public sealed class PropulsionDisplayRenderer
    {
        private static readonly object ValveAnimationSync =
            new object();

        private static bool _lastValveTargetOpen;

        private static bool _valveAnimationInitialized;

        private static DateTime _valveTransitionStartedUtc =
            DateTime.MinValue;

        private const double ValveTransitionSeconds =
            0.30;

        private enum LiquidPropulsionState
        {
            Unavailable = 0,
            Idle = 1,
            Ignition = 2,
            Running = 3,
            Flameout = 4
        }

        private sealed class LiquidPropulsionSnapshot
        {
            public LiquidPropulsionState State { get; set; }

            public int InstalledCount { get; set; }

            public int ProducingCount { get; set; }

            public double ThrottleFraction { get; set; }

            public bool FlowActive
            {
                get
                {
                    /*
                     * Pump rotation represents real or commanded propellant
                     * flow. A flameout is shown as a fault, not as a normally
                     * operating pump.
                     */
                    return
                        State ==
                            LiquidPropulsionState.Ignition ||
                        State ==
                            LiquidPropulsionState.Running;
                }
            }

            public bool ValveOpen
            {
                get
                {
                    return
                        FlowActive ||
                        State ==
                            LiquidPropulsionState.Flameout;
                }
            }
        }

        // Topology-dependent analysis is supplied by the shared cache.

        public void Draw(
            Graphics graphics,
            Rectangle bounds,
            PropulsionRenderGraph graph,
            MissionTelemetry telemetry,
            Font titleFont,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            if (graphics == null ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            GraphicsState state =
                graphics.Save();

            try
            {
                graphics.SmoothingMode =
                    SmoothingMode.AntiAlias;

                DrawBackground(
                    graphics,
                    bounds,
                    dimPhosphor);

                if (graph == null)
                {
                    DrawCentered(
                        graphics,
                        bounds,
                        "AWAITING VESSEL TOPOLOGY",
                        titleFont,
                        dimPhosphor);
                    return;
                }

                int liveCurrentStage =
                    telemetry != null
                        ? telemetry.CurrentStage
                        : graph.CurrentStage;

                PropulsionAnalysis analysis =
                    PropulsionAnalysisCache.GetOrBuild(
                        graph,
                        liveCurrentStage);

                PropulsionSystemModel system =
                    analysis.SystemModel;

                EngineClusterProjection cluster =
                    analysis.EngineCluster;

                int gap = 14;
                int upperHeight =
                    Math.Max(
                        190,
                        bounds.Height * 36 / 100);

                Rectangle clusterPanel =
                    new Rectangle(
                        bounds.Left,
                        bounds.Top,
                        bounds.Width * 48 / 100,
                        upperHeight);

                Rectangle performancePanel =
                    new Rectangle(
                        clusterPanel.Right + gap,
                        bounds.Top,
                        bounds.Right -
                        clusterPanel.Right -
                        gap,
                        upperHeight);

                Rectangle flowPanel =
                    new Rectangle(
                        bounds.Left,
                        clusterPanel.Bottom + gap,
                        bounds.Width,
                        bounds.Bottom -
                        clusterPanel.Bottom -
                        gap);

                DrawPanelFrame(
                    graphics,
                    clusterPanel,
                    "PHYSICAL ENGINE CLUSTER",
                    labelFont,
                    phosphor,
                    dimPhosphor);

                DrawPanelFrame(
                    graphics,
                    performancePanel,
                    "PROPULSION PERFORMANCE",
                    labelFont,
                    phosphor,
                    dimPhosphor);

                DrawPanelFrame(
                    graphics,
                    flowPanel,
                    "PROPELLANT FLOW / STAGE SYSTEM",
                    labelFont,
                    phosphor,
                    dimPhosphor);

                DrawEngineCluster(
                    graphics,
                    Rectangle.Inflate(
                        clusterPanel,
                        -18,
                        -48),
                    cluster,
                    telemetry,
                    labelFont,
                    smallFont,
                    phosphor,
                    dimPhosphor);

                DrawPerformance(
                    graphics,
                    Rectangle.Inflate(
                        performancePanel,
                        -18,
                        -48),
                    graph,
                    system,
                    telemetry,
                    labelFont,
                    smallFont,
                    phosphor,
                    dimPhosphor);

                DrawSystemFlow(
                    graphics,
                    Rectangle.Inflate(
                        flowPanel,
                        -18,
                        -48),
                    system,
                    telemetry,
                    labelFont,
                    smallFont,
                    phosphor,
                    dimPhosphor);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        internal static void DrawEngineCluster(
            Graphics graphics,
            Rectangle bounds,
            EngineClusterProjection cluster,
            MissionTelemetry telemetry,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            if (cluster == null ||
                cluster.Engines.Count == 0)
            {
                DrawCentered(
                    graphics,
                    bounds,
                    "NO ENGINE CLUSTER",
                    labelFont,
                    dimPhosphor);

                return;
            }

            int headerHeight =
                38;

            /*
             * The cleanup build uses one compact status row only. Removing
             * the legend and mode row gives the engine-bell plot substantially
             * more vertical space.
             */
            int statusHeight =
                Math.Max(
                    48,
                    Math.Min(
                        60,
                        bounds.Height / 6));

            Rectangle plot =
                new Rectangle(
                    bounds.Left + 12,
                    bounds.Top + headerHeight,
                    bounds.Width - 24,
                    Math.Max(
                        1,
                        bounds.Height -
                        headerHeight -
                        statusHeight -
                        10));

            Rectangle status =
                new Rectangle(
                    bounds.Left + 4,
                    plot.Bottom + 8,
                    bounds.Width - 8,
                    statusHeight - 8);

            /*
             * Begin with a desired symbol size, then reduce it when the
             * projected engine centers are close enough that circles would
             * touch. This keeps dense vertical and radial arrangements clear.
             */
            int desiredDiameter =
                Math.Max(
                    34,
                    Math.Min(
                        54,
                        Math.Min(
                            plot.Width,
                            plot.Height) /
                        Math.Max(
                            3,
                            (int)Math.Ceiling(
                                Math.Sqrt(
                                    cluster.Engines.Count)))));

            float provisionalRadiusX =
                Math.Max(
                    0,
                    (plot.Width -
                     desiredDiameter -
                     24) /
                    2.0f);

            float provisionalRadiusY =
                Math.Max(
                    0,
                    (plot.Height -
                     desiredDiameter -
                     24) /
                    2.0f);

            int collisionSafeDiameter =
                CalculateCollisionSafeDiameter(
                    cluster,
                    plot,
                    provisionalRadiusX,
                    provisionalRadiusY,
                    desiredDiameter);

            int diameter =
                Math.Max(
                    28,
                    Math.Min(
                        desiredDiameter,
                        collisionSafeDiameter));

            float radiusX =
                Math.Max(
                    0,
                    (plot.Width -
                     diameter -
                     24) /
                    2.0f);

            float radiusY =
                Math.Max(
                    0,
                    (plot.Height -
                     diameter -
                     24) /
                    2.0f);

            bool anyProducing =
                telemetry != null &&
                telemetry
                    .ProducingThrustEngineCount >
                0;

            Color normal =
                anyProducing
                    ? Color.FromArgb(
                        255,
                        55,
                        255,
                        105)
                    : dimPhosphor;

            using (SolidBrush labelBrush =
                new SolidBrush(
                    phosphor))
            using (SolidBrush detailBrush =
                new SolidBrush(
                    dimPhosphor))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.DrawString(
                    cluster.DisplayName,
                    labelFont,
                    labelBrush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top,
                        bounds.Width,
                        21),
                    centered);

                graphics.DrawString(
                    "STAGE " +
                    cluster.ActivationStage
                        .ToString("00") +
                    "  •  ENGINE BELL VIEW",
                    smallFont,
                    detailBrush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 20,
                        bounds.Width,
                        18),
                    centered);
            }

            DrawCrosshair(
                graphics,
                plot,
                dimPhosphor);

            for (int index = 0;
                 index < cluster.Engines.Count;
                 index++)
            {
                EngineProjectionPoint point =
                    cluster.Engines[index];

                int x =
                    (int)
                    (plot.Left +
                     plot.Width / 2.0f +
                     point.NormalizedX *
                     radiusX -
                     diameter / 2.0f);

                int y =
                    (int)
                    (plot.Top +
                     plot.Height / 2.0f +
                     point.NormalizedY *
                     radiusY -
                     diameter / 2.0f);

                DrawEngineSymbol(
                    graphics,
                    new Rectangle(
                        x,
                        y,
                        diameter,
                        diameter),
                    point,
                    normal,
                    labelFont,
                    smallFont);
            }

            DrawEngineClusterStatus(
                graphics,
                status,
                cluster,
                telemetry,
                smallFont,
                phosphor,
                dimPhosphor);
        }

        private static int CalculateCollisionSafeDiameter(
            EngineClusterProjection cluster,
            Rectangle plot,
            float radiusX,
            float radiusY,
            int desiredDiameter)
        {
            if (cluster == null ||
                cluster.Engines == null ||
                cluster.Engines.Count < 2)
            {
                return desiredDiameter;
            }

            double minimumDistance =
                double.MaxValue;

            for (int leftIndex = 0;
                 leftIndex < cluster.Engines.Count;
                 leftIndex++)
            {
                EngineProjectionPoint left =
                    cluster.Engines[leftIndex];

                double leftX =
                    plot.Left +
                    plot.Width / 2.0 +
                    left.NormalizedX *
                    radiusX;

                double leftY =
                    plot.Top +
                    plot.Height / 2.0 +
                    left.NormalizedY *
                    radiusY;

                for (int rightIndex =
                        leftIndex + 1;
                     rightIndex <
                        cluster.Engines.Count;
                     rightIndex++)
                {
                    EngineProjectionPoint right =
                        cluster.Engines[rightIndex];

                    double rightX =
                        plot.Left +
                        plot.Width / 2.0 +
                        right.NormalizedX *
                        radiusX;

                    double rightY =
                        plot.Top +
                        plot.Height / 2.0 +
                        right.NormalizedY *
                        radiusY;

                    double deltaX =
                        rightX -
                        leftX;

                    double deltaY =
                        rightY -
                        leftY;

                    double distance =
                        Math.Sqrt(
                            deltaX * deltaX +
                            deltaY * deltaY);

                    minimumDistance =
                        Math.Min(
                            minimumDistance,
                            distance);
                }
            }

            if (minimumDistance ==
                double.MaxValue)
            {
                return desiredDiameter;
            }

            /*
             * Keep at least 8 pixels between neighboring symbols.
             */
            return Math.Max(
                28,
                (int)Math.Floor(
                    minimumDistance -
                    8.0));
        }

        private static void DrawEngineClusterStatus(
            Graphics graphics,
            Rectangle bounds,
            EngineClusterProjection cluster,
            MissionTelemetry telemetry,
            Font font,
            Color phosphor,
            Color dimPhosphor)
        {
            int producing =
                0;

            int flameout =
                0;

            double currentThrust =
                0.0;

            double maximumThrust =
                0.0;

            for (int index = 0;
                 index < cluster.Engines.Count;
                 index++)
            {
                EngineStateTelemetry state =
                    EngineStateTelemetryStore.GetEngine(
                        cluster.Engines[index].PartId);

                if (state == null)
                {
                    continue;
                }

                currentThrust +=
                    Math.Max(
                        0.0,
                        state.CurrentThrust);

                maximumThrust +=
                    Math.Max(
                        0.0,
                        state.MaximumThrust);

                if (state.OperatingState ==
                    EngineOperatingState.Producing)
                {
                    producing++;
                }

                if (state.OperatingState ==
                    EngineOperatingState.Flameout)
                {
                    flameout++;
                }
            }

            double thrustFraction =
                maximumThrust > 0.0001
                    ? Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            currentThrust /
                            maximumThrust))
                    : 0.0;

            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        115,
                        dimPhosphor),
                    1.0f))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    dimPhosphor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    phosphor))
            using (SolidBrush faultBrush =
                new SolidBrush(
                    flameout > 0
                        ? Color.FromArgb(
                            255,
                            255,
                            75,
                            75)
                        : phosphor))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center,

                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.DrawRectangle(
                    border,
                    bounds);

                int cellWidth =
                    Math.Max(
                        1,
                        bounds.Width / 4);

                Rectangle activeCell =
                    new Rectangle(
                        bounds.Left,
                        bounds.Top,
                        cellWidth,
                        bounds.Height);

                Rectangle thrustCell =
                    new Rectangle(
                        activeCell.Right,
                        bounds.Top,
                        cellWidth,
                        bounds.Height);

                Rectangle faultCell =
                    new Rectangle(
                        thrustCell.Right,
                        bounds.Top,
                        cellWidth,
                        bounds.Height);

                Rectangle stageCell =
                    new Rectangle(
                        faultCell.Right,
                        bounds.Top,
                        bounds.Right -
                        faultCell.Right,
                        bounds.Height);

                DrawStatusCell(
                    graphics,
                    activeCell,
                    "ACTIVE",
                    producing.ToString("00") +
                    "/" +
                    cluster.Engines.Count
                        .ToString("00"),
                    font,
                    labelBrush,
                    valueBrush,
                    centered);

                DrawStatusCell(
                    graphics,
                    thrustCell,
                    "THRUST",
                    (thrustFraction * 100.0)
                        .ToString("0") +
                    "%",
                    font,
                    labelBrush,
                    valueBrush,
                    centered);

                DrawStatusCell(
                    graphics,
                    faultCell,
                    "FAULTS",
                    flameout.ToString("00"),
                    font,
                    labelBrush,
                    faultBrush,
                    centered);

                DrawStatusCell(
                    graphics,
                    stageCell,
                    "STAGE",
                    telemetry != null
                        ? telemetry.CurrentStage
                            .ToString("00")
                        : cluster.ActivationStage
                            .ToString("00"),
                    font,
                    labelBrush,
                    valueBrush,
                    centered);

                graphics.DrawLine(
                    border,
                    activeCell.Right,
                    bounds.Top,
                    activeCell.Right,
                    bounds.Bottom);

                graphics.DrawLine(
                    border,
                    thrustCell.Right,
                    bounds.Top,
                    thrustCell.Right,
                    bounds.Bottom);

                graphics.DrawLine(
                    border,
                    faultCell.Right,
                    bounds.Top,
                    faultCell.Right,
                    bounds.Bottom);
            }
        }

        private static void DrawStatusCell(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string value,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            StringFormat centered)
        {
            int topPadding =
                2;

            int gap =
                2;

            int labelHeight =
                Math.Max(
                    14,
                    (bounds.Height -
                     topPadding -
                     gap) *
                    42 /
                    100);

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left + 3,
                    bounds.Top + topPadding,
                    Math.Max(
                        1,
                        bounds.Width - 6),
                    labelHeight);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left + 3,
                    labelBounds.Bottom + gap,
                    Math.Max(
                        1,
                        bounds.Width - 6),
                    Math.Max(
                        1,
                        bounds.Bottom -
                        labelBounds.Bottom -
                        gap -
                        2));

            graphics.DrawString(
                label,
                font,
                labelBrush,
                labelBounds,
                centered);

            graphics.DrawString(
                value,
                font,
                valueBrush,
                valueBounds,
                centered);
        }

        private static void DrawEngineSymbol(
            Graphics graphics,
            Rectangle bounds,
            EngineProjectionPoint point,
            Color color,
            Font numberFont,
            Font detailFont)
        {
            EngineStateTelemetry state =
                point != null
                    ? EngineStateTelemetryStore.GetEngine(
                        point.PartId)
                    : null;

            Color stateColor =
                ResolveEngineStateColor(
                    color,
                    state);

            bool producing =
                state != null &&
                state.OperatingState ==
                    EngineOperatingState.Producing;

            string identifier =
                CreateEngineTag(
                    point);

            float identifierSize =
                CalculateIdentifierFontSize(
                    detailFont,
                    identifier,
                    bounds.Width);

            using (Font identifierFont =
                new Font(
                    detailFont.FontFamily,
                    identifierSize,
                    FontStyle.Bold,
                    GraphicsUnit.Point))
            using (Pen outer =
                new Pen(
                    stateColor,
                    producing
                        ? 3.0f
                        : 2.0f))
            using (Pen inner =
                new Pen(
                    Color.FromArgb(
                        producing
                            ? 190
                            : 100,
                        stateColor),
                    1.0f))
            using (SolidBrush brush =
                new SolidBrush(
                    stateColor))
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        48,
                        stateColor)))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center,

                    Trimming =
                        StringTrimming.EllipsisCharacter,

                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                if (producing)
                {
                    graphics.FillEllipse(
                        fill,
                        bounds);
                }

                graphics.DrawEllipse(
                    outer,
                    bounds);

                Rectangle innerBounds =
                    Rectangle.Inflate(
                        bounds,
                        -5,
                        -5);

                if (innerBounds.Width > 4 &&
                    innerBounds.Height > 4)
                {
                    graphics.DrawEllipse(
                        inner,
                        innerBounds);
                }

                /*
                 * The stable mission identifier is the only label. It is
                 * rendered inside the symbol, eliminating both the duplicate
                 * display number and all external label collisions.
                 */
                graphics.DrawString(
                    identifier,
                    identifierFont,
                    brush,
                    Rectangle.Inflate(
                        bounds,
                        -3,
                        -3),
                    centered);

                if (state != null &&
                    state.OperatingState ==
                        EngineOperatingState.Flameout)
                {
                    graphics.DrawLine(
                        outer,
                        bounds.Left + 7,
                        bounds.Top + 7,
                        bounds.Right - 7,
                        bounds.Bottom - 7);

                    graphics.DrawLine(
                        outer,
                        bounds.Right - 7,
                        bounds.Top + 7,
                        bounds.Left + 7,
                        bounds.Bottom - 7);
                }
            }
        }

        private static float CalculateIdentifierFontSize(
            Font baseFont,
            string identifier,
            int symbolWidth)
        {
            int characterCount =
                string.IsNullOrEmpty(
                    identifier)
                    ? 1
                    : identifier.Length;

            float size =
                Math.Max(
                    8.0f,
                    baseFont.SizeInPoints);

            if (characterCount >= 4)
            {
                size =
                    Math.Min(
                        size,
                        Math.Max(
                            8.0f,
                            symbolWidth /
                            5.2f));
            }
            else
            {
                size =
                    Math.Min(
                        size + 1.0f,
                        Math.Max(
                            8.0f,
                            symbolWidth /
                            4.2f));
            }

            return size;
        }

        private static Color ResolveEngineStateColor(Color normal, EngineStateTelemetry state)
        {
            if(state==null)return Color.FromArgb(135,normal);
            switch(state.OperatingState)
            {
                case EngineOperatingState.Producing:return normal;
                case EngineOperatingState.Ignited:return Color.FromArgb(255,255,190,60);
                case EngineOperatingState.Flameout:return Color.FromArgb(255,255,75,75);
                case EngineOperatingState.Shutdown:return Color.FromArgb(85,normal);
                case EngineOperatingState.Armed:return Color.FromArgb(145,normal);
                default:return Color.FromArgb(115,normal);
            }
        }

        private static string CreateEngineTag(
            EngineProjectionPoint point)
        {
            if (point == null)
            {
                return "E--";
            }

            if (!string.IsNullOrWhiteSpace(
                    point.Identifier))
            {
                return point.Identifier;
            }

            return CreateEnginePrefix(
                    point.DisplayName) +
                point.DisplayNumber
                    .ToString("00");
        }

        private static string CreateEnginePrefix(
            string name)
        {
            if (string.IsNullOrWhiteSpace(
                    name))
            {
                return "E";
            }

            string upper =
                name.Trim()
                    .ToUpperInvariant();

            if (upper.StartsWith("THUMPER", StringComparison.Ordinal))
            {
                return "T";
            }

            if (upper.StartsWith("KICKBACK", StringComparison.Ordinal))
            {
                return "K";
            }

            if (upper.StartsWith("SEPARATRON", StringComparison.Ordinal))
            {
                return "S";
            }

            if (upper.StartsWith("SKIPPER", StringComparison.Ordinal))
            {
                return "SK";
            }

            if (upper.StartsWith("TERRIER", StringComparison.Ordinal))
            {
                return "TR";
            }

            if (upper.StartsWith("SWIVEL", StringComparison.Ordinal))
            {
                return "SW";
            }

            if (upper.StartsWith("RELIANT", StringComparison.Ordinal))
            {
                return "R";
            }

            return upper.Substring(
                0,
                Math.Min(
                    2,
                    upper.Length));
        }

        internal static void DrawPerformance(
            Graphics graphics,
            Rectangle bounds,
            PropulsionRenderGraph graph,
            PropulsionSystemModel system,
            MissionTelemetry telemetry,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            int row = 31;
            int y = bounds.Top;

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "CURRENT STAGE",
                telemetry != null
                    ? telemetry.CurrentStage
                        .ToString("00")
                    : "--",
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "ENGINE COUNT",
                system.TotalEngineCount
                    .ToString("00"),
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "PRODUCING",
                telemetry != null
                    ? telemetry
                        .ProducingThrustEngineCount
                        .ToString("00")
                    : "--",
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "THROTTLE",
                telemetry != null
                    ? FormatPercent(
                        telemetry.Throttle)
                    : "---",
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "THRUST",
                telemetry != null
                    ? Format(
                        telemetry.CurrentThrust,
                        "0.0",
                        " kN")
                    : "---",
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "TWR",
                telemetry != null
                    ? Format(
                        telemetry
                            .ThrustToWeightRatio,
                        "0.00",
                        "")
                    : "---",
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "AVG ISP",
                telemetry != null
                    ? Format(
                        telemetry
                            .AverageSpecificImpulse,
                        "0.0",
                        " s")
                    : "---",
                labelFont,
                dimPhosphor,
                phosphor);

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "GRAPH REV",
                graph.TopologyRevision
                    .ToString(),
                labelFont,
                dimPhosphor,
                phosphor);

            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        100,
                        dimPhosphor)))
            {
                graphics.DrawLine(
                    pen,
                    bounds.Left,
                    y + 4,
                    bounds.Right,
                    y + 4);
            }

            y += 18;

            string state =
                GetEngineState(
                    telemetry);

            Color stateColor =
                state == "RUNNING"
                    ? Color.FromArgb(
                        255,
                        55,
                        255,
                        105)
                    : state == "FLAMEOUT"
                        ? Color.FromArgb(
                            255,
                            255,
                            75,
                            55)
                        : dimPhosphor;

            DrawValueRow(
                graphics,
                bounds,
                ref y,
                row,
                "SYSTEM STATUS",
                state,
                labelFont,
                dimPhosphor,
                stateColor);

            bool activeFuelEmpty =
                telemetry != null &&
                telemetry.StageLiquidFuelCapacity > 0.0 &&
                telemetry.StageLiquidFuelAmount <= 0.0001;

            bool activeOxEmpty =
                telemetry != null &&
                telemetry.StageOxidizerCapacity > 0.0 &&
                telemetry.StageOxidizerAmount <= 0.0001;

            if (activeFuelEmpty ||
                activeOxEmpty)
            {
                DrawValueRow(
                    graphics,
                    bounds,
                    ref y,
                    row,
                    "ACTIVE FEED",
                    "NO USABLE PROPELLANT",
                    labelFont,
                    dimPhosphor,
                    Color.FromArgb(
                        255,
                        255,
                        75,
                        55));
            }
        }

        internal static void DrawSystemFlow(
            Graphics graphics,
            Rectangle bounds,
            PropulsionSystemModel system,
            MissionTelemetry telemetry,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            Color lf =
                Color.FromArgb(
                    255,
                    45,
                    225,
                    85);

            Color ox =
                Color.FromArgb(
                    255,
                    35,
                    205,
                    255);

            Color mono =
                Color.FromArgb(
                    255,
                    255,
                    145,
                    30);

            Color solid =
                Color.FromArgb(
                    255,
                    255,
                    150,
                    35);

            SolidFuelTelemetrySnapshot solidFuel =
                SolidFuelTelemetryResolver.GetSnapshot();

            SrbBankAlertSnapshot srbAlerts =
                SrbBankAlertTracker.Update(
                    solidFuel,
                    DateTime.UtcNow);

            bool showSolidBoosters =
                srbAlerts.ShouldDisplay;

            LiquidPropulsionSnapshot liquidState =
                ResolveLiquidPropulsionState(
                    system,
                    telemetry);

            DateTime animationNowUtc =
                DateTime.UtcNow;

            double valveOpenFraction =
                GetValveOpenFraction(
                    liquidState.ValveOpen,
                    animationNowUtc);

            Color lfFlowColor =
                ResolveLiquidFeedColor(
                    liquidState,
                    lf,
                    dimPhosphor);

            Color oxFlowColor =
                ResolveLiquidFeedColor(
                    liquidState,
                    ox,
                    dimPhosphor);

            Color liquidCenterColor =
                ResolveLiquidCenterColor(
                    liquidState,
                    phosphor,
                    dimPhosphor);

            int centerX =
                bounds.Left +
                bounds.Width / 2;

            Rectangle lfTank =
                new Rectangle(
                    bounds.Left + 16,
                    bounds.Top + 28,
                    166,
                    118);

            Rectangle oxTank =
                new Rectangle(
                    bounds.Right - 182,
                    bounds.Top + 28,
                    166,
                    118);

            /*
             * Give the center LF/OX schematic more breathing room. All
             * downstream elements continue to anchor to these rectangles, so
             * the valve, liquid-propulsion block, nozzle, and SRB banks remain
             * aligned as one assembly.
             */
            Rectangle mixer =
                new Rectangle(
                    centerX - 52,
                    bounds.Top + 64,
                    104,
                    46);

            Rectangle valve =
                new Rectangle(
                    centerX - 42,
                    mixer.Bottom + 38,
                    84,
                    36);

            Rectangle chamber =
                new Rectangle(
                    centerX - 82,
                    valve.Bottom + 40,
                    164,
                    64);

            if (showSolidBoosters)
            {
                DrawSolidBoosterPair(
                    graphics,
                    bounds,
                    mixer,
                    chamber,
                    srbAlerts,
                    solid,
                    labelFont,
                    smallFont);
            }

            DrawSplitTank(
                graphics,
                lfTank,
                "LIQUID FUEL",
                Fraction(
                    telemetry != null
                        ? telemetry
                            .StageLiquidFuelAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry
                            .StageLiquidFuelCapacity
                        : 0.0),
                Fraction(
                    telemetry != null
                        ? telemetry
                            .TotalLiquidFuelAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry
                            .TotalLiquidFuelCapacity
                        : 0.0),
                lf,
                labelFont,
                smallFont);

            DrawSplitTank(
                graphics,
                oxTank,
                "OXIDIZER",
                Fraction(
                    telemetry != null
                        ? telemetry
                            .StageOxidizerAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry
                            .StageOxidizerCapacity
                        : 0.0),
                Fraction(
                    telemetry != null
                        ? telemetry
                            .TotalOxidizerAmount
                        : 0.0,
                    telemetry != null
                        ? telemetry
                            .TotalOxidizerCapacity
                        : 0.0),
                ox,
                labelFont,
                smallFont);

            DrawPumpFlow(
                graphics,
                new Point(
                    lfTank.Right,
                    lfTank.Top +
                    lfTank.Height / 2),
                new Point(
                    mixer.Left,
                    mixer.Top +
                    mixer.Height / 2),
                lfFlowColor,
                "LF",
                liquidState,
                animationNowUtc,
                smallFont);

            DrawPumpFlow(
                graphics,
                new Point(
                    oxTank.Left,
                    oxTank.Top +
                    oxTank.Height / 2),
                new Point(
                    mixer.Right,
                    mixer.Top +
                    mixer.Height / 2),
                oxFlowColor,
                "OX",
                liquidState,
                animationNowUtc,
                smallFont);

            DrawBox(
                graphics,
                mixer,
                "MIXER",
                "LF / OX",
                liquidCenterColor,
                labelFont,
                smallFont);

            DrawMixerDetail(
                graphics,
                mixer,
                liquidCenterColor);

            DrawVerticalFlow(
                graphics,
                mixer,
                valve,
                liquidCenterColor,
                liquidState,
                animationNowUtc);

            DrawValve(
                graphics,
                valve,
                liquidCenterColor,
                valveOpenFraction,
                smallFont);

            DrawVerticalFlow(
                graphics,
                valve,
                chamber,
                liquidCenterColor,
                liquidState,
                animationNowUtc);

            int liquidEngineCount =
                liquidState.InstalledCount;

            DrawBox(
                graphics,
                chamber,
                "LIQUID PROPULSION",
                liquidEngineCount.ToString("00") +
                (liquidEngineCount == 1
                    ? " ENGINE"
                    : " ENGINES") +
                "  " +
                GetLiquidStateLabel(
                    liquidState.State),
                liquidCenterColor,
                labelFont,
                smallFont);

            DrawNozzle(
                graphics,
                chamber,
                liquidCenterColor);

            if (liquidState.State ==
                LiquidPropulsionState.Flameout)
            {
                DrawLiquidFaultAnnunciator(
                    graphics,
                    chamber,
                    animationNowUtc,
                    smallFont);
            }

            if (system.HasMonopropellant)
            {
                Rectangle monoTank =
                    new Rectangle(
                        bounds.Right - 182,
                        chamber.Top - 6,
                        166,
                        104);

                DrawSplitTank(
                    graphics,
                    monoTank,
                    "MONOPROP",
                    Fraction(
                        telemetry != null
                            ? telemetry
                                .StageMonopropellantAmount
                            : 0.0,
                        telemetry != null
                            ? telemetry
                                .StageMonopropellantCapacity
                            : 0.0),
                    Fraction(
                        telemetry != null
                            ? telemetry
                                .TotalMonopropellantAmount
                            : 0.0,
                        telemetry != null
                            ? telemetry
                                .TotalMonopropellantCapacity
                            : 0.0),
                    mono,
                    smallFont,
                    smallFont);

                Rectangle rcs =
                    new Rectangle(
                        monoTank.Left - 96,
                        monoTank.Top + 14,
                        72,
                        34);

                DrawBox(
                    graphics,
                    rcs,
                    "RCS",
                    system.RcsThrusterCount +
                    " JETS",
                    mono,
                    smallFont,
                    smallFont);

                DrawFlow(
                    graphics,
                    new Point(
                        monoTank.Left,
                        monoTank.Top +
                        monoTank.Height / 2),
                    new Point(
                        rcs.Right,
                        rcs.Top +
                        rcs.Height / 2),
                    mono,
                    string.Empty,
                    smallFont);
            }

            DrawStageRail(
                graphics,
                bounds,
                system,
                smallFont,
                dimPhosphor);
        }

        private static void DrawMixerDetail(
            Graphics graphics,
            Rectangle mixer,
            Color color)
        {
            Rectangle detail =
                new Rectangle(
                    mixer.Left + 8,
                    mixer.Top + 21,
                    mixer.Width - 16,
                    Math.Max(
                        8,
                        mixer.Height - 27));

            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        135,
                        color),
                    1.0f))
            {
                int middleY =
                    detail.Top +
                    detail.Height /
                    2;

                graphics.DrawLine(
                    pen,
                    detail.Left,
                    detail.Top,
                    detail.Right,
                    detail.Bottom);

                graphics.DrawLine(
                    pen,
                    detail.Left,
                    detail.Bottom,
                    detail.Right,
                    detail.Top);

                graphics.DrawLine(
                    pen,
                    detail.Left,
                    middleY,
                    detail.Right,
                    middleY);
            }
        }

        private static LiquidPropulsionSnapshot
            ResolveLiquidPropulsionState(
                PropulsionSystemModel system,
                MissionTelemetry telemetry)
        {
            LiquidPropulsionSnapshot snapshot =
                new LiquidPropulsionSnapshot();

            if (system == null)
            {
                snapshot.State =
                    LiquidPropulsionState.Unavailable;

                return snapshot;
            }

            snapshot.InstalledCount =
                Math.Max(
                    0,
                    system.LiquidEngineCount);

            if (snapshot.InstalledCount <= 0)
            {
                snapshot.State =
                    LiquidPropulsionState.Unavailable;

                return snapshot;
            }

            /*
             * KSP can continue reporting a throttleable engine as Ignited
             * after the player returns throttle to zero. The schematic treats
             * zero throttle as no commanded flow regardless of that flag.
             */
            snapshot.ThrottleFraction =
                telemetry != null
                    ? Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            telemetry.Throttle))
                    : 0.0;

            bool thrustCommanded =
                snapshot.ThrottleFraction >
                    0.005;

            int producing =
                0;

            int ignited =
                0;

            int flameout =
                0;

            double liquidThrust =
                0.0;

            for (int index = 0;
                 index < system.LiquidEnginePartIds.Count;
                 index++)
            {
                EngineStateTelemetry engine =
                    EngineStateTelemetryStore.GetEngine(
                        system.LiquidEnginePartIds[index]);

                if (engine == null)
                {
                    continue;
                }

                liquidThrust +=
                    Math.Max(
                        0.0,
                        engine.CurrentThrust);

                switch (engine.OperatingState)
                {
                    case EngineOperatingState.Producing:
                        producing++;
                        break;

                    case EngineOperatingState.Ignited:
                        ignited++;
                        break;

                    case EngineOperatingState.Flameout:
                        flameout++;
                        break;
                }
            }

            snapshot.ProducingCount =
                producing;

            bool measurableLiquidThrust =
                liquidThrust >
                    0.05;

            if (!thrustCommanded &&
                !measurableLiquidThrust)
            {
                snapshot.State =
                    LiquidPropulsionState.Idle;
            }
            else if (flameout > 0 &&
                     thrustCommanded &&
                     !measurableLiquidThrust)
            {
                snapshot.State =
                    LiquidPropulsionState.Flameout;
            }
            else if (producing > 0 ||
                     measurableLiquidThrust)
            {
                snapshot.State =
                    LiquidPropulsionState.Running;
            }
            else if (thrustCommanded &&
                     ignited > 0)
            {
                snapshot.State =
                    LiquidPropulsionState.Ignition;
            }
            else
            {
                snapshot.State =
                    LiquidPropulsionState.Idle;
            }

            return snapshot;
        }

        private static Color ResolveLiquidFeedColor(
            LiquidPropulsionSnapshot state,
            Color normal,
            Color dim)
        {
            if (state == null)
            {
                return dim;
            }

            switch (state.State)
            {
                case LiquidPropulsionState.Running:
                    /*
                     * Preserve each feed's identity:
                     * LF stays green and OX stays cyan.
                     */
                    return normal;

                case LiquidPropulsionState.Ignition:
                    return Color.FromArgb(
                        255,
                        255,
                        205,
                        75);

                case LiquidPropulsionState.Flameout:
                    return CreateFaultPulseColor(
                        DateTime.UtcNow);

                case LiquidPropulsionState.Idle:
                    /*
                     * Inactive hardware is a cool, subdued version of its
                     * normal color rather than SRB-like orange.
                     */
                    return Color.FromArgb(
                        92,
                        normal);

                default:
                    return dim;
            }
        }

        private static Color ResolveLiquidCenterColor(
            LiquidPropulsionSnapshot state,
            Color normal,
            Color dim)
        {
            if (state == null)
            {
                return dim;
            }

            switch (state.State)
            {
                case LiquidPropulsionState.Running:
                    return normal;

                case LiquidPropulsionState.Ignition:
                    return Color.FromArgb(
                        255,
                        255,
                        205,
                        75);

                case LiquidPropulsionState.Flameout:
                    return CreateFaultPulseColor(
                        DateTime.UtcNow);

                case LiquidPropulsionState.Idle:
                    return Color.FromArgb(
                        115,
                        dim);

                default:
                    return dim;
            }
        }

        private static string GetLiquidStateLabel(
            LiquidPropulsionState state)
        {
            switch (state)
            {
                case LiquidPropulsionState.Running:
                    return "RUNNING";

                case LiquidPropulsionState.Ignition:
                    return "IGNITION";

                case LiquidPropulsionState.Flameout:
                    return "FLAMEOUT";

                case LiquidPropulsionState.Idle:
                    return "IDLE";

                default:
                    return "NO ENGINES";
            }
        }

        private static double GetValveOpenFraction(
            bool targetOpen,
            DateTime nowUtc)
        {
            lock (ValveAnimationSync)
            {
                if (!_valveAnimationInitialized)
                {
                    _valveAnimationInitialized =
                        true;

                    _lastValveTargetOpen =
                        targetOpen;

                    _valveTransitionStartedUtc =
                        nowUtc -
                        TimeSpan.FromSeconds(
                            ValveTransitionSeconds);
                }
                else if (_lastValveTargetOpen !=
                         targetOpen)
                {
                    _lastValveTargetOpen =
                        targetOpen;

                    _valveTransitionStartedUtc =
                        nowUtc;
                }

                double progress =
                    Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            (nowUtc -
                             _valveTransitionStartedUtc)
                                .TotalSeconds /
                            ValveTransitionSeconds));

                /*
                 * Smoothstep avoids an abrupt mechanical snap at either end.
                 */
                progress =
                    progress *
                    progress *
                    (3.0 -
                     2.0 *
                     progress);

                return targetOpen
                    ? progress
                    : 1.0 -
                      progress;
            }
        }

        private static void DrawHorizontalFlowPackets(
            Graphics graphics,
            Point start,
            Point end,
            Color color,
            double throttle,
            DateTime nowUtc,
            bool reverse)
        {
            int length =
                Math.Abs(
                    end.X -
                    start.X);

            if (length <
                18)
            {
                return;
            }

            int packetCount =
                Math.Max(
                    2,
                    Math.Min(
                        7,
                        length /
                        54));

            double speed =
                0.35 +
                1.65 *
                throttle;

            double phase =
                (nowUtc.TimeOfDay
                    .TotalSeconds *
                 speed) %
                1.0;

            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        225,
                        color)))
            {
                for (int index = 0;
                     index < packetCount;
                     index++)
                {
                    double fraction =
                        (phase +
                         index /
                         (double)packetCount) %
                        1.0;

                    if (reverse)
                    {
                        fraction =
                            1.0 -
                            fraction;
                    }

                    int x =
                        (int)Math.Round(
                            start.X +
                            (end.X -
                             start.X) *
                            fraction);

                    int y =
                        start.Y;

                    int radius =
                        throttle >
                        0.66
                            ? 3
                            : 2;

                    graphics.FillEllipse(
                        brush,
                        x - radius,
                        y - radius,
                        radius * 2,
                        radius * 2);
                }
            }
        }

        private static void DrawVerticalFlowPackets(
            Graphics graphics,
            Point start,
            Point end,
            Color color,
            double throttle,
            DateTime nowUtc)
        {
            int length =
                Math.Abs(
                    end.Y -
                    start.Y);

            if (length <
                14)
            {
                return;
            }

            int packetCount =
                Math.Max(
                    1,
                    Math.Min(
                        4,
                        length /
                        32));

            double speed =
                0.35 +
                1.65 *
                throttle;

            double phase =
                (nowUtc.TimeOfDay
                    .TotalSeconds *
                 speed) %
                1.0;

            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        225,
                        color)))
            {
                for (int index = 0;
                     index < packetCount;
                     index++)
                {
                    double fraction =
                        (phase +
                         index /
                         (double)packetCount) %
                        1.0;

                    int x =
                        start.X;

                    int y =
                        (int)Math.Round(
                            start.Y +
                            (end.Y -
                             start.Y) *
                            fraction);

                    int radius =
                        throttle >
                        0.66
                            ? 3
                            : 2;

                    graphics.FillEllipse(
                        brush,
                        x - radius,
                        y - radius,
                        radius * 2,
                        radius * 2);
                }
            }
        }

        private static Color CreateFaultPulseColor(
            DateTime nowUtc)
        {
            double pulse =
                0.5 +
                0.5 *
                Math.Sin(
                    nowUtc.TimeOfDay
                        .TotalSeconds *
                    Math.PI *
                    4.0);

            return Color.FromArgb(
                255,
                255,
                (int)Math.Round(
                    35 +
                    70 *
                    pulse),
                (int)Math.Round(
                    30 +
                    35 *
                    pulse));
        }

        private static void DrawLiquidFaultAnnunciator(
            Graphics graphics,
            Rectangle chamber,
            DateTime nowUtc,
            Font font)
        {
            Color fault =
                CreateFaultPulseColor(
                    nowUtc);

            Rectangle bounds =
                new Rectangle(
                    chamber.Left - 8,
                    chamber.Bottom + 34,
                    chamber.Width + 16,
                    24);

            using (Pen pen =
                new Pen(
                    fault,
                    2.0f))
            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        42,
                        fault)))
            using (SolidBrush text =
                new SolidBrush(
                    fault))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.FillRectangle(
                    brush,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);

                graphics.DrawString(
                    "LIQUID FLOW FAULT",
                    font,
                    text,
                    bounds,
                    centered);
            }
        }

        private static void DrawPumpFlow(
            Graphics graphics,
            Point start,
            Point end,
            Color color,
            string label,
            LiquidPropulsionSnapshot liquidState,
            DateTime nowUtc,
            Font font)
        {
            bool active =
                liquidState != null &&
                liquidState.FlowActive;

            double throttle =
                liquidState != null
                    ? liquidState.ThrottleFraction
                    : 0.0;

            double vibration =
                active &&
                liquidState.State ==
                    LiquidPropulsionState.Running
                    ? Math.Sin(
                        nowUtc.TimeOfDay
                            .TotalSeconds *
                        26.0) *
                      Math.Min(
                          1.0,
                          throttle)
                    : 0.0;

            int vibrationOffset =
                (int)Math.Round(
                    vibration);

            int middleX =
                (start.X + end.X) /
                2;

            Point pumpCenter =
                new Point(
                    middleX,
                    start.Y +
                    vibrationOffset);

            int pumpRadius =
                22;

            Color pumpColor =
                active
                    ? color
                    : Color.FromArgb(
                        145,
                        color);

            float pipeWidth =
                active
                    ? (float)(
                        1.7 +
                        2.1 *
                        throttle)
                    : 1.2f;

            using (Pen pipePen =
                new Pen(
                    pumpColor,
                    pipeWidth))
            using (Pen pumpPen =
                new Pen(
                    pumpColor,
                    active
                        ? (float)(
                            2.0 +
                            1.0 *
                            throttle)
                        : 1.4f))
            using (SolidBrush brush =
                new SolidBrush(
                    pumpColor))
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        active
                            ? (int)(
                                32 +
                                54 *
                                throttle)
                            : 16,
                        pumpColor)))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center,

                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                pipePen.EndCap =
                    active
                        ? LineCap.ArrowAnchor
                        : LineCap.Flat;

                Point firstPipeEnd =
                    new Point(
                        pumpCenter.X -
                        pumpRadius,
                        start.Y);

                Point secondPipeStart =
                    new Point(
                        pumpCenter.X +
                        pumpRadius,
                        start.Y);

                graphics.DrawLine(
                    pipePen,
                    start,
                    firstPipeEnd);

                graphics.DrawLines(
                    pipePen,
                    new[]
                    {
                        secondPipeStart,
                        new Point(
                            end.X,
                            start.Y),
                        end
                    });

                if (active)
                {
                    DrawHorizontalFlowPackets(
                        graphics,
                        start,
                        firstPipeEnd,
                        pumpColor,
                        throttle,
                        nowUtc,
                        false);

                    DrawHorizontalFlowPackets(
                        graphics,
                        secondPipeStart,
                        new Point(
                            end.X,
                            start.Y),
                        pumpColor,
                        throttle,
                        nowUtc,
                        start.X > end.X);
                }

                Rectangle pumpBounds =
                    new Rectangle(
                        pumpCenter.X -
                        pumpRadius,
                        pumpCenter.Y -
                        pumpRadius,
                        pumpRadius * 2,
                        pumpRadius * 2);

                graphics.FillEllipse(
                    fill,
                    pumpBounds);

                graphics.DrawEllipse(
                    pumpPen,
                    pumpBounds);

                DrawPumpImpeller(
                    graphics,
                    pumpCenter,
                    pumpRadius - 6,
                    pumpPen,
                    active,
                    throttle,
                    nowUtc);

                Rectangle labelBounds =
                    new Rectangle(
                        pumpBounds.Left - 20,
                        pumpBounds.Top - 24,
                        pumpBounds.Width + 40,
                        20);

                graphics.DrawString(
                    label + " PUMP",
                    font,
                    brush,
                    labelBounds,
                    centered);
            }
        }

        private static void DrawPumpImpeller(
            Graphics graphics,
            Point center,
            int radius,
            Pen pen,
            bool active,
            double throttle,
            DateTime nowUtc)
        {
            int bladeCount =
                8;

            /*
             * Approximately 0.4 rotations/second near idle command and
             * 4 rotations/second at full throttle.
             */
            double rotationsPerSecond =
                active
                    ? 0.4 +
                      3.6 *
                      Math.Max(
                          0.0,
                          Math.Min(
                              1.0,
                              throttle))
                    : 0.0;

            double phase =
                active
                    ? nowUtc.TimeOfDay
                        .TotalSeconds *
                      rotationsPerSecond *
                      Math.PI *
                      2.0
                    : 0.0;

            for (int index = 0;
                 index < bladeCount;
                 index++)
            {
                double angle =
                    phase +
                    index *
                    Math.PI *
                    2.0 /
                    bladeCount;

                double curvedAngle =
                    angle +
                    0.42;

                Point inner =
                    new Point(
                        center.X +
                        (int)Math.Round(
                            Math.Cos(angle) *
                            radius *
                            0.22),

                        center.Y +
                        (int)Math.Round(
                            Math.Sin(angle) *
                            radius *
                            0.22));

                Point outer =
                    new Point(
                        center.X +
                        (int)Math.Round(
                            Math.Cos(curvedAngle) *
                            radius),

                        center.Y +
                        (int)Math.Round(
                            Math.Sin(curvedAngle) *
                            radius));

                graphics.DrawLine(
                    pen,
                    inner,
                    outer);
            }

            graphics.DrawEllipse(
                pen,
                new Rectangle(
                    center.X - 3,
                    center.Y - 3,
                    6,
                    6));
        }

        private static void DrawFlow(
            Graphics graphics,
            Point start,
            Point end,
            Color color,
            string label,
            Font font)
        {
            int middleX =
                (start.X + end.X) / 2;

            using (Pen pen =
                new Pen(color, 2.0f))
            using (SolidBrush brush =
                new SolidBrush(color))
            {
                pen.EndCap =
                    LineCap.ArrowAnchor;

                graphics.DrawLines(
                    pen,
                    new[]
                    {
                        start,
                        new Point(
                            middleX,
                            start.Y),
                        new Point(
                            middleX,
                            end.Y),
                        end
                    });

                if (!string.IsNullOrEmpty(
                        label))
                {
                    graphics.DrawString(
                        label,
                        font,
                        brush,
                        middleX - 28,
                        Math.Min(
                            start.Y,
                            end.Y) - 17);
                }
            }
        }

        private static void DrawVerticalFlow(
            Graphics graphics,
            Rectangle from,
            Rectangle to,
            Color color,
            LiquidPropulsionSnapshot liquidState,
            DateTime nowUtc)
        {
            bool active =
                liquidState != null &&
                liquidState.FlowActive;

            double throttle =
                liquidState != null
                    ? liquidState.ThrottleFraction
                    : 0.0;

            Point start =
                new Point(
                    from.Left +
                    from.Width / 2,
                    from.Bottom);

            Point end =
                new Point(
                    to.Left +
                    to.Width / 2,
                    to.Top);

            using (Pen pen =
                new Pen(
                    color,
                    active
                        ? (float)(
                            1.7 +
                            2.1 *
                            throttle)
                        : 1.2f))
            {
                pen.EndCap =
                    active
                        ? LineCap.ArrowAnchor
                        : LineCap.Flat;

                graphics.DrawLine(
                    pen,
                    start,
                    end);
            }

            if (active)
            {
                DrawVerticalFlowPackets(
                    graphics,
                    start,
                    end,
                    color,
                    throttle,
                    nowUtc);
            }
        }

        private static void DrawValve(
            Graphics graphics,
            Rectangle bounds,
            Color color,
            double openFraction,
            Font font)
        {
            openFraction =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        openFraction));

            Point center =
                new Point(
                    bounds.Left +
                    bounds.Width / 2,
                    bounds.Top +
                    bounds.Height / 2);

            int separation =
                (int)Math.Round(
                    openFraction *
                    Math.Max(
                        4,
                        bounds.Width /
                        9));

            Point leftTip =
                new Point(
                    center.X -
                    separation,
                    center.Y);

            Point rightTip =
                new Point(
                    center.X +
                    separation,
                    center.Y);

            Point[] left =
            {
                new Point(
                    bounds.Left,
                    bounds.Top + 4),
                leftTip,
                new Point(
                    bounds.Left,
                    bounds.Bottom - 4)
            };

            Point[] right =
            {
                new Point(
                    bounds.Right,
                    bounds.Top + 4),
                rightTip,
                new Point(
                    bounds.Right,
                    bounds.Bottom - 4)
            };

            bool visuallyOpen =
                openFraction >=
                    0.98;

            using (Pen pen =
                new Pen(
                    color,
                    visuallyOpen
                        ? 2.0f
                        : 1.4f))
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.DrawPolygon(
                    pen,
                    left);

                graphics.DrawPolygon(
                    pen,
                    right);

                if (openFraction <
                    0.05)
                {
                    graphics.DrawLine(
                        pen,
                        center.X,
                        bounds.Top + 3,
                        center.X,
                        bounds.Bottom - 3);
                }

                string valveLabel;

                if (openFraction <=
                    0.02)
                {
                    valveLabel =
                        "MAIN VALVE CLOSED";
                }
                else if (openFraction >=
                         0.98)
                {
                    valveLabel =
                        "MAIN VALVE OPEN";
                }
                else
                {
                    valveLabel =
                        "MAIN VALVE TRANSIT";
                }

                graphics.DrawString(
                    valveLabel,
                    font,
                    brush,
                    new Rectangle(
                        bounds.Left - 34,
                        bounds.Bottom + 2,
                        bounds.Width + 68,
                        18),
                    centered);
            }
        }

        /// <summary>
        /// Draws scalable SRB bank schematics rather than one decorative
        /// booster body per side. The total booster count is divided as evenly
        /// as possible between Bank A and Bank B.
        /// </summary>
        private static void DrawSolidBoosterPair(
            Graphics graphics,
            Rectangle flowBounds,
            Rectangle mixer,
            Rectangle chamber,
            SrbBankAlertSnapshot alerts,
            Color color,
            Font labelFont,
            Font smallFont)
        {
            int bankWidth =
                Math.Max(
                    210,
                    Math.Min(
                        300,
                        flowBounds.Width /
                        5));

            int bankHeight =
                Math.Max(
                    142,
                    Math.Min(
                        176,
                        flowBounds.Height *
                        38 /
                        100));

            int bankTop =
                Math.Max(
                    chamber.Bottom + 38,
                    mixer.Bottom + 128);

            int maximumTop =
                flowBounds.Bottom -
                bankHeight -
                16;

            bankTop =
                Math.Min(
                    bankTop,
                    maximumTop);

            int horizontalGap =
                Math.Max(
                    80,
                    flowBounds.Width /
                    12);

            Rectangle bankA =
                new Rectangle(
                    mixer.Left -
                    horizontalGap -
                    bankWidth,
                    bankTop,
                    bankWidth,
                    bankHeight);

            Rectangle bankB =
                new Rectangle(
                    mixer.Right +
                    horizontalGap,
                    bankTop,
                    bankWidth,
                    bankHeight);

            if (bankA.Left <
                flowBounds.Left + 34)
            {
                bankA.X =
                    flowBounds.Left + 34;
            }

            if (bankB.Right >
                flowBounds.Right - 34)
            {
                bankB.X =
                    flowBounds.Right -
                    34 -
                    bankB.Width;
            }

            DrawSolidBoosterBank(
                graphics,
                bankA,
                "SRB BANK A",
                alerts.BankA,
                color,
                labelFont,
                smallFont);

            DrawSolidBoosterBank(
                graphics,
                bankB,
                "SRB BANK B",
                alerts.BankB,
                color,
                labelFont,
                smallFont);
        }

        private static void DrawSolidBoosterBank(
            Graphics graphics,
            Rectangle bounds,
            string title,
            SrbBankAlertBankSnapshot bank,
            Color color,
            Font labelFont,
            Font smallFont)
        {
            if (bank == null ||
                bank.State ==
                    SrbBankAlertState.Offline)
            {
                return;
            }

            if (bank.State ==
                SrbBankAlertState.Separate)
            {
                DrawSrbAlertBank(
                    graphics,
                    bounds,
                    title,
                    "SEPARATE",
                    bank.FlashOn,
                    color,
                    labelFont,
                    smallFont);

                return;
            }

            if (bank.State ==
                SrbBankAlertState.Separated)
            {
                DrawSeparatedSrbBank(
                    graphics,
                    bounds,
                    title,
                    labelFont,
                    smallFont);

                return;
            }

            double fraction =
                Fraction(
                    bank.Amount,
                    bank.Capacity);

            Color activeColor =
                bank.Burning
                    ? color
                    : Color.FromArgb(
                        125,
                        color);

            Rectangle titleBounds =
                new Rectangle(
                    bounds.Left + 6,
                    bounds.Top + 4,
                    bounds.Width - 12,
                    22);

            Rectangle countBounds =
                new Rectangle(
                    bounds.Left + 6,
                    titleBounds.Bottom,
                    bounds.Width - 12,
                    18);

            Rectangle dotArea =
                new Rectangle(
                    bounds.Left + 16,
                    countBounds.Bottom + 6,
                    bounds.Width - 32,
                    Math.Max(
                        40,
                        bounds.Height - 92));

            Rectangle meter =
                new Rectangle(
                    bounds.Left + 12,
                    bounds.Bottom - 24,
                    bounds.Width - 24,
                    10);

            /*
             * Give the numeric fuel readout enough ascent/descent room.
             * The previous 18-pixel rectangle clipped the tops of the
             * monospace digits at some DPI/font combinations.
             */
            Rectangle percentBounds =
                new Rectangle(
                    bounds.Left + 6,
                    meter.Top - 29,
                    bounds.Width - 12,
                    26);

            using (Font readoutFont =
                new Font(
                    smallFont.FontFamily,
                    Math.Max(
                        10.0f,
                        smallFont.SizeInPoints),
                    FontStyle.Bold,
                    GraphicsUnit.Point))
            using (Pen outline =
                new Pen(
                    Color.FromArgb(
                        150,
                        color),
                    1.2f))
            using (Pen meterOutline =
                new Pen(
                    color,
                    1.2f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    activeColor))
            using (SolidBrush dimBrush =
                new SolidBrush(
                    Color.FromArgb(
                        175,
                        color)))
            using (SolidBrush meterFill =
                new SolidBrush(
                    Color.FromArgb(
                        bank.Burning
                            ? 180
                            : 105,
                        color)))
            using (StringFormat centered =
                new StringFormat(
                    StringFormat.GenericTypographic)
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center,
                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.DrawRectangle(
                    outline,
                    bounds);

                graphics.DrawString(
                    title,
                    labelFont,
                    titleBrush,
                    titleBounds,
                    centered);

                graphics.DrawString(
                    bank.BoosterCount.ToString("00") +
                    (bank.BoosterCount == 1
                        ? " BOOSTER"
                        : " BOOSTERS"),
                    smallFont,
                    dimBrush,
                    countBounds,
                    centered);

                DrawBoosterDots(
                    graphics,
                    dotArea,
                    bank.BoosterCount,
                    bank.Burning,
                    color);

                graphics.DrawString(
                    bank.Amount.ToString("0.0") +
                    " / " +
                    bank.Capacity.ToString("0.0") +
                    "   " +
                    (fraction * 100.0)
                        .ToString("0") +
                    "%",
                    readoutFont,
                    titleBrush,
                    percentBounds,
                    centered);

                graphics.DrawRectangle(
                    meterOutline,
                    meter);

                int fillWidth =
                    (int)Math.Round(
                        fraction *
                        Math.Max(
                            0,
                            meter.Width - 2));

                if (fillWidth > 0)
                {
                    graphics.FillRectangle(
                        meterFill,
                        new Rectangle(
                            meter.Left + 1,
                            meter.Top + 1,
                            fillWidth,
                            Math.Max(
                                1,
                                meter.Height - 2)));
                }
            }
        }

        private static void DrawSeparatedSrbBank(
            Graphics graphics,
            Rectangle bounds,
            string title,
            Font labelFont,
            Font smallFont)
        {
            Color confirmation =
                Color.FromArgb(
                    255,
                    55,
                    255,
                    105);

            using (Pen outline =
                new Pen(
                    confirmation,
                    2.5f))
            using (SolidBrush background =
                new SolidBrush(
                    Color.FromArgb(
                        34,
                        confirmation)))
            using (SolidBrush brush =
                new SolidBrush(
                    confirmation))
            using (Font confirmationFont =
                new Font(
                    labelFont.FontFamily,
                    Math.Max(
                        13.0f,
                        labelFont.SizeInPoints *
                        1.35f),
                    FontStyle.Bold,
                    GraphicsUnit.Point))
            using (StringFormat centered =
                new StringFormat(
                    StringFormat.GenericTypographic)
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center,
                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.FillRectangle(
                    background,
                    bounds);

                graphics.DrawRectangle(
                    outline,
                    bounds);

                graphics.DrawString(
                    title,
                    smallFont,
                    brush,
                    new Rectangle(
                        bounds.Left + 6,
                        bounds.Top + 5,
                        bounds.Width - 12,
                        24),
                    centered);

                graphics.DrawString(
                    "SEPARATION\nCONFIRMED",
                    confirmationFont,
                    brush,
                    new Rectangle(
                        bounds.Left + 8,
                        bounds.Top + 36,
                        bounds.Width - 16,
                        bounds.Height - 46),
                    centered);
            }
        }

        private static void DrawSrbAlertBank(
            Graphics graphics,
            Rectangle bounds,
            string title,
            string alert,
            bool flashOn,
            Color color,
            Font labelFont,
            Font smallFont)
        {
            Color alertColor =
                flashOn
                    ? Color.FromArgb(
                        255,
                        255,
                        75,
                        45)
                    : Color.FromArgb(
                        95,
                        color);

            using (Pen outline =
                new Pen(
                    alertColor,
                    flashOn
                        ? 3.0f
                        : 1.2f))
            using (SolidBrush background =
                new SolidBrush(
                    Color.FromArgb(
                        flashOn
                            ? 42
                            : 8,
                        alertColor)))
            using (SolidBrush brush =
                new SolidBrush(
                    alertColor))
            using (Font alertFont =
                new Font(
                    labelFont.FontFamily,
                    Math.Max(
                        13.0f,
                        labelFont.SizeInPoints *
                        1.35f),
                    FontStyle.Bold,
                    GraphicsUnit.Point))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center,
                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.FillRectangle(
                    background,
                    bounds);

                graphics.DrawRectangle(
                    outline,
                    bounds);

                graphics.DrawString(
                    title,
                    smallFont,
                    brush,
                    new Rectangle(
                        bounds.Left + 6,
                        bounds.Top + 5,
                        bounds.Width - 12,
                        22),
                    centered);

                graphics.DrawString(
                    flashOn
                        ? alert
                        : string.Empty,
                    alertFont,
                    brush,
                    new Rectangle(
                        bounds.Left + 8,
                        bounds.Top + 34,
                        bounds.Width - 16,
                        bounds.Height - 42),
                    centered);
            }
        }

        private static void DrawBoosterDots(
            Graphics graphics,
            Rectangle bounds,
            int boosterCount,
            bool burning,
            Color color)
        {
            if (boosterCount <= 0)
            {
                using (SolidBrush emptyBrush =
                    new SolidBrush(
                        Color.FromArgb(
                            95,
                            color)))
                using (StringFormat centered =
                    new StringFormat
                    {
                        Alignment =
                            StringAlignment.Center,

                        LineAlignment =
                            StringAlignment.Center
                    })
                using (Font emptyFont =
                    new Font(
                        FontFamily.GenericMonospace,
                        9.0f,
                        FontStyle.Bold,
                        GraphicsUnit.Point))
                {
                    graphics.DrawString(
                        "--",
                        emptyFont,
                        emptyBrush,
                        bounds,
                        centered);
                }

                return;
            }

            int columns =
                boosterCount <= 4
                    ? boosterCount
                    : (int)Math.Ceiling(
                        boosterCount /
                        2.0);

            int rows =
                boosterCount <= 4
                    ? 1
                    : 2;

            int horizontalSpacing =
                Math.Max(
                    22,
                    bounds.Width /
                    Math.Max(
                        1,
                        columns));

            int verticalSpacing =
                Math.Max(
                    22,
                    bounds.Height /
                    Math.Max(
                        1,
                        rows));

            int diameter =
                Math.Max(
                    12,
                    Math.Min(
                        22,
                        Math.Min(
                            horizontalSpacing - 8,
                            verticalSpacing - 8)));

            Color dotColor =
                burning
                    ? color
                    : Color.FromArgb(
                        115,
                        color);

            using (Pen dotPen =
                new Pen(
                    dotColor,
                    burning
                        ? 2.2f
                        : 1.4f))
            using (SolidBrush dotFill =
                new SolidBrush(
                    Color.FromArgb(
                        burning
                            ? 105
                            : 28,
                        dotColor)))
            {
                int remaining =
                    boosterCount;

                for (int row = 0;
                     row < rows &&
                     remaining > 0;
                     row++)
                {
                    int itemsThisRow =
                        rows == 1
                            ? remaining
                            : Math.Min(
                                columns,
                                remaining);

                    int rowWidth =
                        itemsThisRow *
                        horizontalSpacing;

                    int startX =
                        bounds.Left +
                        (bounds.Width -
                         rowWidth) /
                        2 +
                        (horizontalSpacing -
                         diameter) /
                        2;

                    int centerY =
                        bounds.Top +
                        (row + 1) *
                        bounds.Height /
                        (rows + 1);

                    for (int column = 0;
                         column < itemsThisRow;
                         column++)
                    {
                        Rectangle dot =
                            new Rectangle(
                                startX +
                                column *
                                horizontalSpacing,
                                centerY -
                                diameter /
                                2,
                                diameter,
                                diameter);

                        graphics.FillEllipse(
                            dotFill,
                            dot);

                        graphics.DrawEllipse(
                            dotPen,
                            dot);
                    }

                    remaining -=
                        itemsThisRow;
                }
            }
        }

        private static void DrawSolidBooster(
            Graphics graphics,
            Rectangle bounds,
            bool burning,
            Color color,
            Font labelFont,
            Font smallFont)
        {
            using (Pen outline =
                new Pen(
                    color,
                    2.0f))
            using (Pen detail =
                new Pen(
                    Color.FromArgb(
                        135,
                        color),
                    1.0f))
            using (SolidBrush textBrush =
                new SolidBrush(
                    color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                Point[] body =
                {
                    new Point(
                        bounds.Left +
                        bounds.Width / 2,
                        bounds.Top),
                    new Point(
                        bounds.Right - 5,
                        bounds.Top +
                        bounds.Width / 2),
                    new Point(
                        bounds.Right - 5,
                        bounds.Bottom - 28),
                    new Point(
                        bounds.Left + 5,
                        bounds.Bottom - 28),
                    new Point(
                        bounds.Left + 5,
                        bounds.Top +
                        bounds.Width / 2)
                };

                graphics.DrawPolygon(
                    outline,
                    body);

                graphics.DrawLine(
                    detail,
                    bounds.Left + 5,
                    bounds.Top +
                    bounds.Width / 2 +
                    10,
                    bounds.Right - 5,
                    bounds.Top +
                    bounds.Width / 2 +
                    10);

                graphics.DrawLine(
                    detail,
                    bounds.Left + 5,
                    bounds.Bottom - 48,
                    bounds.Right - 5,
                    bounds.Bottom - 48);

                Rectangle nozzle =
                    new Rectangle(
                        bounds.Left +
                        bounds.Width / 4,
                        bounds.Bottom - 28,
                        bounds.Width / 2,
                        20);

                graphics.DrawRectangle(
                    outline,
                    nozzle);

                Point[] bell =
                {
                    new Point(
                        nozzle.Left,
                        nozzle.Bottom),
                    new Point(
                        nozzle.Right,
                        nozzle.Bottom),
                    new Point(
                        bounds.Right - 4,
                        bounds.Bottom),
                    new Point(
                        bounds.Left + 4,
                        bounds.Bottom)
                };

                graphics.DrawPolygon(
                    outline,
                    bell);

                GraphicsState state =
                    graphics.Save();

                try
                {
                    graphics.TranslateTransform(
                        bounds.Left +
                        bounds.Width / 2.0f,
                        bounds.Top +
                        bounds.Height / 2.0f);

                    graphics.RotateTransform(
                        -90.0f);

                    Rectangle label =
                        new Rectangle(
                            -bounds.Height / 2 + 28,
                            -bounds.Width / 2,
                            bounds.Height - 56,
                            bounds.Width);

                    graphics.DrawString(
                        "SOLID FUEL",
                        smallFont,
                        textBrush,
                        label,
                        centered);
                }
                finally
                {
                    graphics.Restore(
                        state);
                }

                if (burning)
                {
                    DrawBoosterFlame(
                        graphics,
                        bounds,
                        color);
                }
            }
        }

        private static void DrawBoosterFlame(
            Graphics graphics,
            Rectangle booster,
            Color color)
        {
            Rectangle flame =
                new Rectangle(
                    booster.Left +
                    booster.Width / 4,
                    booster.Bottom + 3,
                    booster.Width / 2,
                    Math.Max(
                        18,
                        booster.Width / 2));

            Point[] flameShape =
            {
                new Point(
                    flame.Left,
                    flame.Top),
                new Point(
                    flame.Right,
                    flame.Top),
                new Point(
                    flame.Left +
                    flame.Width / 2,
                    flame.Bottom)
            };

            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        160,
                        color)))
            using (Pen pen =
                new Pen(
                    color,
                    1.5f))
            {
                graphics.FillPolygon(
                    brush,
                    flameShape);

                graphics.DrawPolygon(
                    pen,
                    flameShape);
            }
        }

        private static void DrawIndividualSolidFuelBar(
            Graphics graphics,
            Rectangle bounds,
            string title,
            double amount,
            double capacity,
            Color color,
            Font smallFont)
        {
            double fraction =
                Fraction(
                    amount,
                    capacity);

            /*
             * Large, readable four-zone layout:
             *
             *  Title                    Percent
             *  Amount / Capacity
             *  Fuel meter
             *
             * Each rectangle is separated vertically and the title/percent
             * share a row without overlapping.
             */
            Rectangle titleBounds =
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 5,
                    bounds.Width * 62 / 100,
                    21);

            Rectangle percentBounds =
                new Rectangle(
                    titleBounds.Right,
                    bounds.Top + 5,
                    bounds.Right -
                    titleBounds.Right -
                    8,
                    21);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 29,
                    bounds.Width - 16,
                    21);

            Rectangle meter =
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Bottom - 17,
                    bounds.Width - 16,
                    10);

            int fillWidth =
                (int)Math.Round(
                    fraction *
                    Math.Max(
                        0,
                        meter.Width - 2));

            float readoutSize =
                Math.Max(
                    10.5f,
                    smallFont.SizeInPoints);

            using (Font readoutFont =
                new Font(
                    smallFont.FontFamily,
                    readoutSize,
                    FontStyle.Bold,
                    GraphicsUnit.Point))
            using (Pen outline =
                new Pen(
                    color,
                    1.4f))
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        165,
                        color)))
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (StringFormat leftAligned =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Near,
                    LineAlignment =
                        StringAlignment.Center,
                    Trimming =
                        StringTrimming.EllipsisCharacter,
                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            using (StringFormat rightAligned =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Far,
                    LineAlignment =
                        StringAlignment.Center,
                    Trimming =
                        StringTrimming.EllipsisCharacter,
                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center,
                    Trimming =
                        StringTrimming.EllipsisCharacter,
                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.DrawRectangle(
                    outline,
                    bounds);

                graphics.DrawString(
                    title,
                    readoutFont,
                    brush,
                    titleBounds,
                    leftAligned);

                graphics.DrawString(
                    (fraction * 100.0)
                        .ToString("0") +
                    "%",
                    readoutFont,
                    brush,
                    percentBounds,
                    rightAligned);

                graphics.DrawString(
                    amount.ToString("0.0") +
                    " / " +
                    capacity.ToString("0.0"),
                    readoutFont,
                    brush,
                    valueBounds,
                    centered);

                graphics.DrawRectangle(
                    outline,
                    meter);

                if (fillWidth > 0)
                {
                    graphics.FillRectangle(
                        fill,
                        new Rectangle(
                            meter.Left + 1,
                            meter.Top + 1,
                            fillWidth,
                            Math.Max(
                                1,
                                meter.Height - 2)));
                }
            }
        }

        private static void DrawSplitTank(
            Graphics graphics,
            Rectangle bounds,
            string title,
            double activeFraction,
            double totalFraction,
            Color color,
            Font titleFont,
            Font detailFont)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        180,
                        3,
                        16,
                        22)))
            using (Pen pen =
                new Pen(color, 1.7f))
            using (SolidBrush titleBrush =
                new SolidBrush(color))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    Color.FromArgb(
                        180,
                        color)))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);

                graphics.DrawString(
                    title,
                    titleFont,
                    titleBrush,
                    new Rectangle(
                        bounds.Left + 3,
                        bounds.Top + 5,
                        bounds.Width - 6,
                        22),
                    centered);

                int dividerY =
                    bounds.Top + 31;

                graphics.DrawLine(
                    pen,
                    bounds.Left + 8,
                    dividerY,
                    bounds.Right - 8,
                    dividerY);

                Rectangle activeBar =
                    new Rectangle(
                        bounds.Left + 10,
                        bounds.Top + 54,
                        bounds.Width - 20,
                        13);

                Rectangle totalBar =
                    new Rectangle(
                        bounds.Left + 10,
                        bounds.Top + 91,
                        bounds.Width - 20,
                        10);

                DrawLevelBar(
                    graphics,
                    activeBar,
                    activeFraction,
                    color,
                    90);

                DrawLevelBar(
                    graphics,
                    totalBar,
                    totalFraction,
                    color,
                    45);

                graphics.DrawString(
                    "ACTIVE  " +
                    (activeFraction * 100.0)
                        .ToString("0") +
                    "%",
                    detailFont,
                    activeFraction <= 0.0001
                        ? Brushes.Red
                        : titleBrush,
                    new Rectangle(
                        bounds.Left + 7,
                        bounds.Top + 33,
                        bounds.Width - 14,
                        20),
                    centered);

                graphics.DrawString(
                    "TOTAL   " +
                    (totalFraction * 100.0)
                        .ToString("0") +
                    "%",
                    detailFont,
                    labelBrush,
                    new Rectangle(
                        bounds.Left + 7,
                        bounds.Top + 70,
                        bounds.Width - 14,
                        19),
                    centered);
            }
        }

        private static void DrawLevelBar(
            Graphics graphics,
            Rectangle bounds,
            double fraction,
            Color color,
            int alpha)
        {
            using (Pen frame =
                new Pen(
                    Color.FromArgb(
                        150,
                        color),
                    1.0f))
            using (SolidBrush level =
                new SolidBrush(
                    Color.FromArgb(
                        alpha,
                        color)))
            {
                graphics.DrawRectangle(
                    frame,
                    bounds);

                Rectangle filled =
                    new Rectangle(
                        bounds.Left + 1,
                        bounds.Top + 1,
                        Math.Max(
                            0,
                            (int)
                            ((bounds.Width - 1) *
                             Math.Max(
                                 0.0,
                                 Math.Min(
                                     1.0,
                                     fraction)))),
                        Math.Max(
                            0,
                            bounds.Height - 1));

                graphics.FillRectangle(
                    level,
                    filled);
            }
        }

        private static void DrawTank(
            Graphics graphics,
            Rectangle bounds,
            string title,
            double fraction,
            Color color,
            Font titleFont,
            Font detailFont)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        180,
                        3,
                        16,
                        22)))
            using (SolidBrush level =
                new SolidBrush(
                    Color.FromArgb(
                        60,
                        color)))
            using (Pen pen =
                new Pen(color, 1.6f))
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                Rectangle levelBounds =
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Bottom -
                        2 -
                        (int)
                        ((bounds.Height - 4) *
                         fraction),
                        bounds.Width - 4,
                        (int)
                        ((bounds.Height - 4) *
                         fraction));

                graphics.FillRectangle(
                    level,
                    levelBounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);

                graphics.DrawString(
                    title,
                    titleFont,
                    brush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 7,
                        bounds.Width,
                        22),
                    centered);

                graphics.DrawString(
                    (fraction * 100.0)
                        .ToString("0") +
                    "%",
                    detailFont,
                    brush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 36,
                        bounds.Width,
                        20),
                    centered);
            }
        }

        private static void DrawBox(
            Graphics graphics,
            Rectangle bounds,
            string title,
            string detail,
            Color color,
            Font titleFont,
            Font detailFont)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        190,
                        3,
                        16,
                        22)))
            using (Pen pen =
                new Pen(color, 1.4f))
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);

                graphics.DrawString(
                    title,
                    titleFont,
                    brush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 1,
                        bounds.Width,
                        bounds.Height / 2),
                    centered);

                graphics.DrawString(
                    detail,
                    detailFont,
                    brush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top +
                        bounds.Height / 2,
                        bounds.Width,
                        bounds.Height / 2),
                    centered);
            }
        }

        private static void DrawNozzle(
            Graphics graphics,
            Rectangle chamber,
            Color color)
        {
            int centerX =
                chamber.Left +
                chamber.Width / 2;

            Point[] nozzle =
            {
                new Point(
                    centerX - 24,
                    chamber.Bottom),
                new Point(
                    centerX + 24,
                    chamber.Bottom),
                new Point(
                    centerX + 41,
                    chamber.Bottom + 31),
                new Point(
                    centerX - 41,
                    chamber.Bottom + 31)
            };

            using (Pen pen =
                new Pen(color, 1.5f))
            {
                graphics.DrawPolygon(
                    pen,
                    nozzle);

                graphics.DrawLine(
                    pen,
                    centerX - 51,
                    chamber.Bottom + 37,
                    centerX + 51,
                    chamber.Bottom + 37);
            }
        }

        private static void DrawStageRail(
            Graphics graphics,
            Rectangle bounds,
            PropulsionSystemModel system,
            Font font,
            Color color)
        {
            int x =
                bounds.Left + 4;

            int y =
                bounds.Top + 134;

            using (SolidBrush brush =
                new SolidBrush(color))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        120,
                        color)))
            {
                graphics.DrawString(
                    "SEP STAGES",
                    font,
                    brush,
                    x,
                    y);

                y += 22;

                int count =
                    Math.Min(
                        5,
                        system.SeparationStages.Count);

                for (int index = 0;
                     index < count;
                     index++)
                {
                    string stage =
                        system
                            .SeparationStages[index]
                            .ToString("00");

                    graphics.DrawString(
                        stage,
                        font,
                        brush,
                        x,
                        y);

                    graphics.DrawLine(
                        pen,
                        x + 26,
                        y + 7,
                        x + 72,
                        y + 7);

                    y += 23;
                }
            }
        }

        private static void DrawValueRow(
            Graphics graphics,
            Rectangle bounds,
            ref int y,
            int rowHeight,
            string label,
            string value,
            Font font,
            Color labelColor,
            Color valueColor)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(labelColor))
            using (SolidBrush valueBrush =
                new SolidBrush(valueColor))
            {
                graphics.DrawString(
                    label,
                    font,
                    labelBrush,
                    bounds.Left,
                    y);

                SizeF valueSize =
                    graphics.MeasureString(
                        value,
                        font);

                graphics.DrawString(
                    value,
                    font,
                    valueBrush,
                    bounds.Right -
                    valueSize.Width,
                    y);
            }

            y += rowHeight;
        }

        private static void DrawCrosshair(
            Graphics graphics,
            Rectangle bounds,
            Color color)
        {
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        55,
                        color),
                    1.0f))
            {
                graphics.DrawEllipse(
                    pen,
                    Rectangle.Inflate(
                        bounds,
                        -18,
                        -18));

                graphics.DrawLine(
                    pen,
                    bounds.Left +
                    bounds.Width / 2,
                    bounds.Top + 8,
                    bounds.Left +
                    bounds.Width / 2,
                    bounds.Bottom - 8);

                graphics.DrawLine(
                    pen,
                    bounds.Left + 8,
                    bounds.Top +
                    bounds.Height / 2,
                    bounds.Right - 8,
                    bounds.Top +
                    bounds.Height / 2);
            }
        }

        private static void DrawPanelFrame(
            Graphics graphics,
            Rectangle bounds,
            string title,
            Font font,
            Color phosphor,
            Color dimPhosphor)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        70,
                        2,
                        14,
                        20)))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        130,
                        dimPhosphor),
                    1.4f))
            using (SolidBrush brush =
                new SolidBrush(phosphor))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    border,
                    bounds);

                graphics.DrawString(
                    title,
                    font,
                    brush,
                    bounds.Left + 14,
                    bounds.Top + 12);

                graphics.DrawLine(
                    border,
                    bounds.Left + 14,
                    bounds.Top + 39,
                    bounds.Right - 14,
                    bounds.Top + 39);
            }
        }

        private static void DrawBackground(
            Graphics graphics,
            Rectangle bounds,
            Color dimPhosphor)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        45,
                        1,
                        10,
                        15)))
            using (Pen grid =
                new Pen(
                    Color.FromArgb(
                        15,
                        dimPhosphor)))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                for (int x = bounds.Left;
                     x < bounds.Right;
                     x += 42)
                {
                    graphics.DrawLine(
                        grid,
                        x,
                        bounds.Top,
                        x,
                        bounds.Bottom);
                }

                for (int y = bounds.Top;
                     y < bounds.Bottom;
                     y += 42)
                {
                    graphics.DrawLine(
                        grid,
                        bounds.Left,
                        y,
                        bounds.Right,
                        y);
                }
            }
        }

        private static void DrawCentered(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color)
        {
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.DrawString(
                    text,
                    font,
                    brush,
                    bounds,
                    centered);
            }
        }

        private static string GetEngineState(
            MissionTelemetry telemetry)
        {
            if (telemetry == null ||
                telemetry.EngineCount <= 0)
            {
                return "NO ENGINES";
            }

            if (telemetry.FlameoutEngineCount > 0)
            {
                return "FLAMEOUT";
            }

            if (telemetry
                .ProducingThrustEngineCount >
                0)
            {
                return "RUNNING";
            }

            if (telemetry.IgnitedEngineCount > 0)
            {
                return "IGNITED";
            }

            return "STANDBY";
        }

        private static string FormatPercent(
            double value)
        {
            return
                (Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        value)) *
                 100.0)
                .ToString("0") +
                "%";
        }

        private static string Format(
            double value,
            string format,
            string suffix)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return "---";
            }

            return Math.Max(
                    0.0,
                    value)
                .ToString(format) +
                suffix;
        }

        private static double Fraction(
            double amount,
            double capacity)
        {
            if (capacity <= 0.0)
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    amount / capacity));
        }

        private static string Shorten(
            string value,
            int maximum)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length <= maximum)
            {
                return value ?? string.Empty;
            }

            return value.Substring(
                0,
                Math.Max(
                    1,
                    maximum - 1)) +
                "…";
        }
    }
}
