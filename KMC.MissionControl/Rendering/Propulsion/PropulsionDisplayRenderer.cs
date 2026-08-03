using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Full-page propulsion display generator. The system-flow diagram and
    /// physical engine projection are rendered as coordinated operator views.
    /// </summary>
    public sealed class PropulsionDisplayRenderer
    {
        private readonly EngineClusterProjector _clusterProjector =
            new EngineClusterProjector();

        private readonly PropulsionSystemModelBuilder _systemBuilder =
            new PropulsionSystemModelBuilder();

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

                PropulsionSystemModel system =
                    _systemBuilder.Build(graph);

                EngineClusterProjection cluster =
                    _clusterProjector.Build(graph);

                int gap = 14;
                int upperHeight =
                    Math.Max(
                        210,
                        bounds.Height * 47 / 100);

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

        private static void DrawEngineCluster(
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

            int labelHeight = 38;

            Rectangle plot =
                new Rectangle(
                    bounds.Left + 12,
                    bounds.Top + labelHeight,
                    bounds.Width - 24,
                    bounds.Height -
                    labelHeight -
                    8);

            int diameter =
                Math.Max(
                    34,
                    Math.Min(
                        58,
                        Math.Min(
                            plot.Width,
                            plot.Height) /
                        Math.Max(
                            3,
                            (int)Math.Ceiling(
                                Math.Sqrt(
                                    cluster.Engines.Count)))));

            float radiusX =
                Math.Max(
                    0,
                    (plot.Width -
                     diameter -
                     16) /
                    2.0f);

            float radiusY =
                Math.Max(
                    0,
                    (plot.Height -
                     diameter -
                     16) /
                    2.0f);

            bool producing =
                telemetry != null &&
                telemetry
                    .ProducingThrustEngineCount >
                0;

            Color active =
                producing
                    ? Color.FromArgb(
                        255,
                        55,
                        255,
                        105)
                    : dimPhosphor;

            using (SolidBrush labelBrush =
                new SolidBrush(phosphor))
            using (SolidBrush detailBrush =
                new SolidBrush(dimPhosphor))
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
                    "  •  TOP VIEW",
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

                Rectangle engine =
                    new Rectangle(
                        x,
                        y,
                        diameter,
                        diameter);

                DrawEngineSymbol(
                    graphics,
                    engine,
                    point,
                    active,
                    labelFont,
                    smallFont);
            }
        }

        private static void DrawEngineSymbol(
            Graphics graphics,
            Rectangle bounds,
            EngineProjectionPoint point,
            Color color,
            Font numberFont,
            Font detailFont)
        {
            using (Pen outer =
                new Pen(color, 2.0f))
            using (Pen inner =
                new Pen(
                    Color.FromArgb(
                        110,
                        color),
                    1.0f))
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
                graphics.DrawEllipse(
                    outer,
                    bounds);

                Rectangle innerBounds =
                    Rectangle.Inflate(
                        bounds,
                        -6,
                        -6);

                graphics.DrawEllipse(
                    inner,
                    innerBounds);

                graphics.DrawString(
                    point.DisplayNumber
                        .ToString(),
                    numberFont,
                    brush,
                    bounds,
                    centered);

                Rectangle tag =
                    new Rectangle(
                        bounds.Left - 20,
                        bounds.Bottom + 1,
                        bounds.Width + 40,
                        17);

                graphics.DrawString(
                    Shorten(
                        point.DisplayName,
                        10),
                    detailFont,
                    brush,
                    tag,
                    centered);
            }
        }

        private static void DrawPerformance(
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
        }

        private static void DrawSystemFlow(
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

            int centerX =
                bounds.Left +
                bounds.Width / 2;

            Rectangle lfTank =
                new Rectangle(
                    bounds.Left + 16,
                    bounds.Top + 36,
                    132,
                    72);

            Rectangle oxTank =
                new Rectangle(
                    bounds.Right - 148,
                    bounds.Top + 36,
                    132,
                    72);

            Rectangle mixer =
                new Rectangle(
                    centerX - 48,
                    bounds.Top + 64,
                    96,
                    40);

            Rectangle valve =
                new Rectangle(
                    centerX - 40,
                    mixer.Bottom + 28,
                    80,
                    34);

            Rectangle chamber =
                new Rectangle(
                    centerX - 62,
                    valve.Bottom + 28,
                    124,
                    48);

            DrawTank(
                graphics,
                lfTank,
                "LIQUID FUEL",
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

            DrawTank(
                graphics,
                oxTank,
                "OXIDIZER",
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

            DrawFlow(
                graphics,
                new Point(
                    lfTank.Right,
                    lfTank.Top +
                    lfTank.Height / 2),
                new Point(
                    mixer.Left,
                    mixer.Top +
                    mixer.Height / 2),
                lf,
                "LF PUMP",
                smallFont);

            DrawFlow(
                graphics,
                new Point(
                    oxTank.Left,
                    oxTank.Top +
                    oxTank.Height / 2),
                new Point(
                    mixer.Right,
                    mixer.Top +
                    mixer.Height / 2),
                ox,
                "OX PUMP",
                smallFont);

            DrawBox(
                graphics,
                mixer,
                "MIXER",
                "LF / OX",
                phosphor,
                labelFont,
                smallFont);

            DrawVerticalFlow(
                graphics,
                mixer,
                valve,
                lf);

            DrawValve(
                graphics,
                valve,
                phosphor,
                smallFont);

            DrawVerticalFlow(
                graphics,
                valve,
                chamber,
                lf);

            DrawBox(
                graphics,
                chamber,
                "THRUST CHAMBER",
                system.TotalEngineCount +
                " ENGINES",
                Color.White,
                labelFont,
                smallFont);

            DrawNozzle(
                graphics,
                chamber,
                phosphor);

            if (system.HasMonopropellant)
            {
                Rectangle monoTank =
                    new Rectangle(
                        bounds.Right - 148,
                        chamber.Top,
                        132,
                        62);

                DrawTank(
                    graphics,
                    monoTank,
                    "MONOPROP",
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
            Color color)
        {
            using (Pen pen =
                new Pen(color, 2.0f))
            {
                pen.EndCap =
                    LineCap.ArrowAnchor;

                graphics.DrawLine(
                    pen,
                    from.Left +
                    from.Width / 2,
                    from.Bottom,
                    to.Left +
                    to.Width / 2,
                    to.Top);
            }
        }

        private static void DrawValve(
            Graphics graphics,
            Rectangle bounds,
            Color color,
            Font font)
        {
            Point center =
                new Point(
                    bounds.Left +
                    bounds.Width / 2,
                    bounds.Top +
                    bounds.Height / 2);

            Point[] left =
            {
                new Point(
                    bounds.Left,
                    bounds.Top + 4),
                center,
                new Point(
                    bounds.Left,
                    bounds.Bottom - 4)
            };

            Point[] right =
            {
                new Point(
                    bounds.Right,
                    bounds.Top + 4),
                center,
                new Point(
                    bounds.Right,
                    bounds.Bottom - 4)
            };

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
                graphics.DrawPolygon(
                    pen,
                    left);

                graphics.DrawPolygon(
                    pen,
                    right);

                graphics.DrawString(
                    "MAIN VALVE",
                    font,
                    brush,
                    new Rectangle(
                        bounds.Left - 12,
                        bounds.Bottom + 2,
                        bounds.Width + 24,
                        18),
                    centered);
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
