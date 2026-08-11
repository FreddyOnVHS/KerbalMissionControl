using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Guidance;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Build 12.2.1 Guidance Director UI cleanup.
    ///
    /// Display-only. All guidance, burn execution, interlock, and maneuver
    /// calculations remain Engine-owned.
    /// </summary>
    public sealed class GuidancePage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private const int HeaderOffset = 68;
        private const int Gap = 16;
        private const int Padding = 14;
        private const int PanelTitleHeight = 38;
        private const int InnerGap = 10;

        public string Name
        {
            get { return "GUIDANCE / GNC"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get { return Size.Empty; }
        }

        public MissionPageContentProfile ContentProfile
        {
            get { return MissionPageContentProfile.DenseEngineering; }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            MissionPageLayout layout =
                new MissionPageLayout(context);

            layout.DrawHeader(
                Name,
                "GNC / CREW ADVISORY");

            GuidanceSolutionModel guidance =
                GetLatestGuidance();

            Rectangle content =
                context.ContentBounds;

            int top =
                content.Top + HeaderOffset;

            int height =
                Math.Max(
                    1,
                    content.Bottom - top - 4);

            int leftWidth =
                Math.Max(
                    520,
                    (int)(content.Width * 0.47));

            Rectangle fdaiBounds =
                new Rectangle(
                    content.Left,
                    top,
                    leftWidth,
                    height);

            Rectangle directorBounds =
                new Rectangle(
                    fdaiBounds.Right + Gap,
                    top,
                    Math.Max(
                        1,
                        content.Right -
                        fdaiBounds.Right -
                        Gap),
                    height);

            DrawGuidanceSphere(
                context,
                fdaiBounds,
                guidance);

            DrawDirector(
                context,
                directorBounds,
                guidance);
        }

        private static GuidanceSolutionModel GetLatestGuidance()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Guidance == null)
            {
                return null;
            }

            return result.Snapshot.Guidance;
        }

        private static void DrawGuidanceSphere(
            MissionRenderContext context,
            Rectangle bounds,
            GuidanceSolutionModel guidance)
        {
            DrawPanelFrame(
                context,
                bounds,
                "FDAI / MANEUVER VECTOR");

            Rectangle inner =
                Rectangle.FromLTRB(
                    bounds.Left + Padding,
                    bounds.Top + 46,
                    bounds.Right - Padding,
                    bounds.Bottom - Padding);

            int diameter =
                Math.Max(
                    180,
                    Math.Min(
                        inner.Width - 24,
                        inner.Height - 205));

            Rectangle sphere =
                new Rectangle(
                    inner.Left +
                    (inner.Width - diameter) / 2,
                    inner.Top + 16,
                    diameter,
                    diameter);

            using (Pen outer =
                new Pen(
                    context.PhosphorColor,
                    2.0f))
            using (Pen grid =
                new Pen(
                    context.DimPhosphorColor,
                    1.0f))
            using (Pen nose =
                new Pen(
                    Color.White,
                    2.0f))
            using (SolidBrush targetBrush =
                new SolidBrush(
                    Color.LimeGreen))
            {
                context.Graphics.DrawEllipse(
                    outer,
                    sphere);

                context.Graphics.DrawLine(
                    grid,
                    sphere.Left,
                    sphere.Top + sphere.Height / 2,
                    sphere.Right,
                    sphere.Top + sphere.Height / 2);

                context.Graphics.DrawLine(
                    grid,
                    sphere.Left + sphere.Width / 2,
                    sphere.Top,
                    sphere.Left + sphere.Width / 2,
                    sphere.Bottom);

                context.Graphics.DrawEllipse(
                    grid,
                    sphere.Left + sphere.Width / 4,
                    sphere.Top,
                    sphere.Width / 2,
                    sphere.Height);

                context.Graphics.DrawEllipse(
                    grid,
                    sphere.Left,
                    sphere.Top + sphere.Height / 4,
                    sphere.Width,
                    sphere.Height / 2);

                int cx =
                    sphere.Left +
                    sphere.Width / 2;

                int cy =
                    sphere.Top +
                    sphere.Height / 2;

                context.Graphics.DrawLine(
                    nose,
                    cx - 18,
                    cy,
                    cx + 18,
                    cy);

                context.Graphics.DrawLine(
                    nose,
                    cx,
                    cy - 18,
                    cx,
                    cy + 18);

                context.Graphics.DrawEllipse(
                    nose,
                    cx - 8,
                    cy - 8,
                    16,
                    16);

                if (guidance != null &&
                    guidance.ManeuverVectorAvailable)
                {
                    double x =
                        Clamp(
                            guidance.ManeuverRightComponent,
                            -1.0,
                            1.0);

                    double y =
                        Clamp(
                            guidance.ManeuverReferenceForwardComponent,
                            -1.0,
                            1.0);

                    int radius =
                        Math.Max(
                            1,
                            sphere.Width / 2 - 18);

                    int tx =
                        cx +
                        (int)Math.Round(
                            x * radius);

                    int ty =
                        cy -
                        (int)Math.Round(
                            y * radius);

                    context.Graphics.FillEllipse(
                        targetBrush,
                        tx - 7,
                        ty - 7,
                        14,
                        14);

                    context.Graphics.DrawEllipse(
                        outer,
                        tx - 13,
                        ty - 13,
                        26,
                        26);
                }
            }

            int textY =
                sphere.Bottom + 14;

            DrawCentered(
                context,
                guidance != null &&
                guidance.ManeuverVectorAvailable
                    ? "GREEN CUE = COMMANDED MANEUVER VECTOR"
                    : "MANEUVER VECTOR UNAVAILABLE",
                inner.Left,
                inner.Right,
                textY,
                guidance != null &&
                guidance.ManeuverVectorAvailable
                    ? context.PhosphorColor
                    : Color.Orange);

            textY += 26;

            DrawCentered(
                context,
                guidance != null
                    ? "ALIGN ERROR " +
                      FormatAngle(
                          guidance.AlignmentErrorDegrees) +
                      "   LAT " +
                      FormatSignedAngle(
                          guidance.LateralErrorDegrees) +
                      "   VERT " +
                      FormatSignedAngle(
                          guidance.VerticalErrorDegrees)
                    : "ALIGN ERROR ---",
                inner.Left,
                inner.Right,
                textY,
                context.PhosphorColor);

            textY += 32;

            DrawStatusBand(
                context,
                Rectangle.FromLTRB(
                    inner.Left + 30,
                    textY,
                    inner.Right - 30,
                    textY + 32),
                guidance != null &&
                guidance.ExecutionAuthorized
                    ? "EXECUTION INTERLOCK: GO"
                    : "EXECUTION INTERLOCK: INHIBIT",
                guidance != null &&
                guidance.ExecutionAuthorized
                    ? Color.LimeGreen
                    : Color.Orange);

            textY += 42;

            DrawBurnBar(
                context,
                Rectangle.FromLTRB(
                    inner.Left + 30,
                    textY,
                    inner.Right - 30,
                    textY + 42),
                guidance);
        }

        private static void DrawDirector(
            MissionRenderContext context,
            Rectangle bounds,
            GuidanceSolutionModel guidance)
        {
            DrawPanelFrame(
                context,
                bounds,
                "GUIDANCE DIRECTOR");

            int left =
                bounds.Left + Padding;

            int right =
                bounds.Right - Padding;

            int top =
                bounds.Top + 48;

            DrawStatusBand(
                context,
                Rectangle.FromLTRB(
                    left,
                    top,
                    right,
                    top + 36),
                guidance != null
                    ? guidance.Status
                    : "GUIDANCE UNAVAILABLE",
                GetStatusColor(
                    guidance));

            int cardsTop =
                top + 46;

            int cardsHeight =
                Math.Max(
                    230,
                    (int)(bounds.Height * 0.48));

            int columnGap =
                10;

            int columnWidth =
                Math.Max(
                    1,
                    (right -
                     left -
                     columnGap) / 2);

            Rectangle maneuverBox =
                new Rectangle(
                    left,
                    cardsTop,
                    columnWidth,
                    cardsHeight / 2 - 5);

            Rectangle executionBox =
                new Rectangle(
                    maneuverBox.Right + columnGap,
                    cardsTop,
                    columnWidth,
                    maneuverBox.Height);

            Rectangle timingBox =
                new Rectangle(
                    left,
                    maneuverBox.Bottom + InnerGap,
                    columnWidth,
                    cardsHeight / 2 - 5);

            Rectangle burnBox =
                new Rectangle(
                    timingBox.Right + columnGap,
                    timingBox.Top,
                    columnWidth,
                    timingBox.Height);

            DrawCompactGroup(
                context,
                maneuverBox,
                "MANEUVER",
                new[]
                {
                    new FieldPair(
                        "PLAN",
                        guidance != null
                            ? guidance.PlanId
                            : "---"),

                    new FieldPair(
                        "MODE",
                        guidance != null
                            ? guidance.Mode
                            : "---"),

                    new FieldPair(
                        "ATTITUDE",
                        guidance != null
                            ? guidance.AttitudeReference
                            : "---")
                });

            DrawCompactGroup(
                context,
                executionBox,
                "EXECUTION",
                new[]
                {
                    new FieldPair(
                        "NODE",
                        guidance != null
                            ? guidance.NodeState
                            : "---"),

                    new FieldPair(
                        "INTERLOCK",
                        guidance != null &&
                        guidance.ExecutionAuthorized
                            ? "GO"
                            : "INHIBIT"),

                    new FieldPair(
                        "STATUS",
                        guidance != null
                            ? guidance.Status
                            : "---")
                });

            DrawCompactGroup(
                context,
                timingBox,
                "TIMING",
                new[]
                {
                    new FieldPair(
                        "NODE",
                        guidance != null
                            ? FormatDuration(
                                guidance.TimeToNodeSeconds)
                            : "---"),

                    new FieldPair(
                        "IGNITION",
                        guidance != null
                            ? FormatDuration(
                                guidance.TimeToIgnitionSeconds)
                            : "---"),

                    new FieldPair(
                        "BURN EST",
                        guidance != null
                            ? FormatSeconds(
                                guidance.BurnDurationSeconds)
                            : "---")
                });

            DrawBurnPerformanceGroup(
                context,
                burnBox,
                guidance);

            int lowerTop =
                timingBox.Bottom +
                InnerGap;

            int lowerHeight =
                Math.Max(
                    120,
                    bounds.Bottom -
                    lowerTop -
                    Padding);

            int propulsionWidth =
                Math.Max(
                    220,
                    (int)((right - left) * 0.34));

            Rectangle propulsionBox =
                new Rectangle(
                    left,
                    lowerTop,
                    propulsionWidth,
                    lowerHeight);

            Rectangle commandBox =
                new Rectangle(
                    propulsionBox.Right +
                    columnGap,
                    lowerTop,
                    Math.Max(
                        1,
                        right -
                        propulsionBox.Right -
                        columnGap),
                    lowerHeight);

            DrawPropulsionGroup(
                context,
                propulsionBox,
                guidance);

            DrawCrewCommandGroup(
                context,
                commandBox,
                guidance);
        }

        private static void DrawCompactGroup(
            MissionRenderContext context,
            Rectangle bounds,
            string title,
            FieldPair[] fields)
        {
            DrawSubPanel(
                context,
                bounds,
                title);

            int y =
                bounds.Top +
                PanelTitleHeight +
                10;

            int labelX =
                bounds.Left + 12;

            int valueX =
                bounds.Left +
                Math.Max(
                    90,
                    (int)(bounds.Width * 0.38));

            int valueRight =
                bounds.Right - 10;

            for (int index = 0;
                 index < fields.Length;
                 index++)
            {
                DrawCompactField(
                    context,
                    fields[index].Label,
                    fields[index].Value,
                    labelX,
                    valueX,
                    valueRight,
                    y);

                y += 32;
            }
        }

        private static void DrawBurnPerformanceGroup(
            MissionRenderContext context,
            Rectangle bounds,
            GuidanceSolutionModel guidance)
        {
            DrawSubPanel(
                context,
                bounds,
                "BURN PERFORMANCE");

            int centerX =
                bounds.Left +
                bounds.Width / 2;

            int y =
                bounds.Top +
                PanelTitleHeight +
                10;

            using (SolidBrush dimBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Font bigFont =
                new Font(
                    "Consolas",
                    16.0f,
                    FontStyle.Bold))
            using (StringFormat centered =
                new StringFormat())
            {
                centered.Alignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    "REMAINING DV",
                    context.SmallFont,
                    dimBrush,
                    new RectangleF(
                        bounds.Left + 8,
                        y,
                        bounds.Width - 16,
                        38),
                    centered);

                y += 32;

                context.Graphics.DrawString(
                    guidance != null
                        ? FormatDeltaV(
                            guidance.RemainingDeltaVMetersPerSecond)
                        : "---",
                    bigFont,
                    valueBrush,
                    new RectangleF(
                        bounds.Left + 8,
                        y,
                        bounds.Width - 16,
                        30),
                    centered);
            }

            y += 44;

            int labelX =
                bounds.Left + 12;

            int valueX =
                bounds.Left +
                Math.Max(
                    90,
                    (int)(bounds.Width * 0.50));

            int valueRight =
                bounds.Right - 10;

            DrawCompactField(
                context,
                "DELIVERED",
                guidance != null
                    ? FormatDeltaV(
                        guidance.DeliveredDeltaVMetersPerSecond)
                    : "---",
                labelX,
                valueX,
                valueRight,
                y);

            y += 30;

            DrawCompactField(
                context,
                "PLANNED",
                guidance != null
                    ? FormatDeltaV(
                        guidance.PlannedDeltaVMetersPerSecond)
                    : "---",
                labelX,
                valueX,
                valueRight,
                y);

            y += 30;

            DrawCompactField(
                context,
                "PROGRESS",
                guidance != null
                    ? FormatPercent(
                        guidance.BurnProgressPercent)
                    : "---",
                labelX,
                valueX,
                valueRight,
                y);
        }

        private static void DrawPropulsionGroup(
            MissionRenderContext context,
            Rectangle bounds,
            GuidanceSolutionModel guidance)
        {
            DrawSubPanel(
                context,
                bounds,
                "PROPULSION");

            int labelX =
                bounds.Left + 12;

            int valueX =
                bounds.Left +
                Math.Max(
                    88,
                    (int)(bounds.Width * 0.42));

            int valueRight =
                bounds.Right - 10;

            int y =
                bounds.Top +
                PanelTitleHeight +
                12;

            DrawCompactField(
                context,
                "THRUST",
                guidance != null
                    ? FormatThrust(
                        guidance.LiveThrustKilonewtons)
                    : "---",
                labelX,
                valueX,
                valueRight,
                y);

            y += 32;

            DrawCompactField(
                context,
                "ACCEL",
                guidance != null
                    ? FormatAcceleration(
                        guidance.LiveAccelerationMetersPerSecondSquared)
                    : "---",
                labelX,
                valueX,
                valueRight,
                y);

            y += 32;

            DrawCompactField(
                context,
                "ENGINE",
                guidance != null &&
                guidance.ProducingThrust
                    ? "THRUSTING"
                    : "IDLE",
                labelX,
                valueX,
                valueRight,
                y);

            y += 32;

            DrawCompactField(
                context,
                "BURN",
                guidance != null &&
                guidance.BurnComplete
                    ? "COMPLETE"
                    : guidance != null &&
                      guidance.BurnActive
                        ? "ACTIVE"
                        : "STANDBY",
                labelX,
                valueX,
                valueRight,
                y);
        }

        private static void DrawCrewCommandGroup(
            MissionRenderContext context,
            Rectangle bounds,
            GuidanceSolutionModel guidance)
        {
            DrawSubPanel(
                context,
                bounds,
                "CREW COMMAND");

            Rectangle inner =
                Rectangle.FromLTRB(
                    bounds.Left + 12,
                    bounds.Top + PanelTitleHeight + 8,
                    bounds.Right - 12,
                    bounds.Bottom - 12);

            Color color =
                GetStatusColor(
                    guidance);

            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (SolidBrush dimBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (Font commandFont =
                new Font(
                    "Consolas",
                    14.0f,
                    FontStyle.Bold))
            using (StringFormat centered =
                new StringFormat())
            {
                centered.Alignment =
                    StringAlignment.Center;

                centered.LineAlignment =
                    StringAlignment.Center;

                int commandHeight =
                    Math.Max(
                        44,
                        inner.Height - 64);

                Rectangle commandRect =
                    new Rectangle(
                        inner.Left,
                        inner.Top,
                        inner.Width,
                        commandHeight);

                context.Graphics.DrawString(
                    guidance != null
                        ? Safe(
                            guidance.Command)
                        : "AWAIT GUIDANCE",
                    commandFont,
                    brush,
                    commandRect,
                    centered);

                Rectangle throttleRect =
                    new Rectangle(
                        inner.Left,
                        commandRect.Bottom + 2,
                        inner.Width,
                        34);

                context.Graphics.DrawString(
                    guidance != null
                        ? Safe(
                            guidance.ThrottleAdvisory)
                        : "THROTTLE 0%",
                    context.SmallFont,
                    brush,
                    throttleRect,
                    centered);

                Rectangle noteRect =
                    new Rectangle(
                        inner.Left,
                        throttleRect.Bottom + 2,
                        inner.Width,
                        30);

                context.Graphics.DrawString(
                    "ADVISORY ONLY - NO AUTOPILOT",
                    context.SmallFont,
                    dimBrush,
                    noteRect,
                    centered);
            }
        }

        private static void DrawCompactField(
            MissionRenderContext context,
            string label,
            string value,
            int labelX,
            int valueX,
            int valueRight,
            int y)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelX,
                    y);

                /*
                 * Do not constrain CRT text to a short RectangleF.
                 * The actual rendered terminal font is taller than the
                 * nominal SmallFont point size on some DPI/display settings,
                 * which caused the tops/bottoms of values to be clipped.
                 */
                context.Graphics.DrawString(
                    Safe(
                        value),
                    context.SmallFont,
                    valueBrush,
                    valueX,
                    y);
            }
        }

        private static void DrawSubPanel(
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

                int separatorY =
                    bounds.Top +
                    PanelTitleHeight;

                context.Graphics.DrawLine(
                    pen,
                    bounds.Left,
                    separatorY,
                    bounds.Right,
                    separatorY);
            }
        }

        private static void DrawBurnBar(
            MissionRenderContext context,
            Rectangle bounds,
            GuidanceSolutionModel guidance)
        {
            double percent =
                guidance != null &&
                IsFinite(
                    guidance.BurnProgressPercent)
                    ? Clamp(
                        guidance.BurnProgressPercent,
                        0.0,
                        100.0)
                    : 0.0;

            Color color =
                guidance != null &&
                guidance.BurnComplete
                    ? Color.LimeGreen
                    : guidance != null &&
                      guidance.BurnActive
                        ? context.PhosphorColor
                        : context.DimPhosphorColor;

            using (Pen pen =
                new Pen(
                    color,
                    1.0f))
            using (SolidBrush fill =
                new SolidBrush(
                    color))
            using (SolidBrush textBrush =
                new SolidBrush(
                    color))
            using (StringFormat format =
                new StringFormat())
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds);

                int fillWidth =
                    (int)Math.Round(
                        (bounds.Width - 4) *
                        (percent / 100.0));

                if (fillWidth > 0)
                {
                    context.Graphics.FillRectangle(
                        fill,
                        bounds.Left + 2,
                        bounds.Top + 2,
                        fillWidth,
                        Math.Max(
                            1,
                            bounds.Height - 4));
                }

                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                string text =
                    guidance != null &&
                    guidance.BurnActive
                        ? "BURN " +
                          FormatPercent(
                              guidance.BurnProgressPercent) +
                          "   REM " +
                          FormatDeltaV(
                              guidance.RemainingDeltaVMetersPerSecond)
                        : guidance != null &&
                          guidance.BurnComplete
                            ? "MANEUVER COMPLETE"
                            : "BURN EXECUTION STANDBY";

                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    textBrush,
                    bounds,
                    format);
            }
        }

        private static Color GetStatusColor(
            GuidanceSolutionModel guidance)
        {
            if (guidance == null)
            {
                return Color.Orange;
            }

            if (guidance.BurnComplete)
            {
                return Color.LimeGreen;
            }

            if (!guidance.ExecutionAuthorized ||
                string.Equals(
                    guidance.Status,
                    "BURN INHIBITED",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    guidance.Status,
                    "ATTITUDE ERROR",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Color.Orange;
            }

            return Color.LimeGreen;
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
                    bounds.Left + 12,
                    bounds.Top + 10);
            }
        }

        private static void DrawStatusBand(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color)
        {
            using (Pen pen =
                new Pen(
                    color,
                    1.0f))
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.DrawRectangle(
                    pen,
                    bounds);

                context.Graphics.DrawString(
                    Safe(
                        text),
                    context.SmallFont,
                    brush,
                    bounds,
                    format);
            }
        }

        private static void DrawCentered(
            MissionRenderContext context,
            string text,
            int left,
            int right,
            int y,
            Color color)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    Safe(
                        text),
                    context.SmallFont,
                    brush,
                    new RectangleF(
                        left,
                        y,
                        Math.Max(
                            1,
                            right - left),
                        24),
                    format);
            }
        }

        private static string FormatDuration(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "---";
            }

            string sign =
                seconds < 0.0
                    ? "-"
                    : string.Empty;

            int total =
                (int)Math.Floor(
                    Math.Abs(seconds));

            return
                sign +
                (total / 60).ToString("00") +
                ":" +
                (total % 60).ToString("00");
        }

        private static string FormatDeltaV(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString("0.00") + " M/S"
                    : "---";
        }

        private static string FormatPercent(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString("0.0") + "%"
                    : "---";
        }

        private static string FormatThrust(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString("0.00") + " KN"
                    : "---";
        }

        private static string FormatAcceleration(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString("0.00") + " M/S2"
                    : "---";
        }

        private static string FormatSeconds(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString("0.00") + " S"
                    : "---";
        }

        private static string FormatAngle(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString("0.00") + " DEG"
                    : "---";
        }

        private static string FormatSignedAngle(
            double value)
        {
            return
                IsFinite(value)
                    ? value.ToString(
                        "+0.00;-0.00;0.00") +
                      " DEG"
                    : "---";
        }

        private static string Safe(
            string value)
        {
            return
                string.IsNullOrWhiteSpace(
                    value)
                    ? "---"
                    : value.Trim()
                        .ToUpperInvariant();
        }

        private static double Clamp(
            double value,
            double min,
            double max)
        {
            return
                Math.Max(
                    min,
                    Math.Min(
                        max,
                        value));
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

        private sealed class FieldPair
        {
            public FieldPair(
                string label,
                string value)
            {
                Label =
                    label ?? string.Empty;

                Value =
                    value ?? string.Empty;
            }

            public string Label { get; private set; }
            public string Value { get; private set; }
        }
    }
}
