using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Guidance;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public sealed class GuidancePage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private const int HeaderOffset = 68;
        private const int Gap = 16;
        private const int Padding = 14;

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
                Math.Max(1, content.Bottom - top - 4);

            int leftWidth =
                Math.Max(
                    520,
                    (int)(content.Width * 0.48));

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
                        inner.Height - 130));

            Rectangle sphere =
                new Rectangle(
                    inner.Left +
                    (inner.Width - diameter) / 2,
                    inner.Top + 20,
                    diameter,
                    diameter);

            using (Pen outer =
                new Pen(context.PhosphorColor, 2.0f))
            using (Pen grid =
                new Pen(context.DimPhosphorColor, 1.0f))
            using (Pen nose =
                new Pen(Color.White, 2.0f))
            using (SolidBrush targetBrush =
                new SolidBrush(Color.LimeGreen))
            {
                context.Graphics.DrawEllipse(outer, sphere);

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
                    sphere.Left + sphere.Width / 2;
                int cy =
                    sphere.Top + sphere.Height / 2;

                context.Graphics.DrawLine(nose, cx - 18, cy, cx + 18, cy);
                context.Graphics.DrawLine(nose, cx, cy - 18, cx, cy + 18);
                context.Graphics.DrawEllipse(nose, cx - 8, cy - 8, 16, 16);

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
                        (int)Math.Round(x * radius);

                    int ty =
                        cy -
                        (int)Math.Round(y * radius);

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
                sphere.Bottom + 18;

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

            textY += 30;

            DrawCentered(
                context,
                guidance != null
                    ? "ALIGN ERROR " +
                      FormatAngle(guidance.AlignmentErrorDegrees) +
                      "   LAT " +
                      FormatSignedAngle(guidance.LateralErrorDegrees) +
                      "   VERT " +
                      FormatSignedAngle(guidance.VerticalErrorDegrees)
                    : "ALIGN ERROR ---",
                inner.Left,
                inner.Right,
                textY,
                context.PhosphorColor);
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

            int y =
                bounds.Top + 50;

            DrawStatusBand(
                context,
                Rectangle.FromLTRB(
                    left,
                    y,
                    right,
                    y + 38),
                guidance != null
                    ? guidance.Status
                    : "GUIDANCE UNAVAILABLE",
                guidance != null &&
                guidance.Available
                    ? Color.LimeGreen
                    : Color.Orange);

            y += 54;

            DrawField(context, "MODE", guidance != null ? guidance.Mode : "---", left, right, ref y);
            DrawField(context, "PLAN", guidance != null ? guidance.PlanId : "---", left, right, ref y);
            DrawField(context, "COMMAND", guidance != null ? guidance.Command : "AWAIT GUIDANCE", left, right, ref y);
            DrawField(context, "ATTITUDE", guidance != null ? guidance.AttitudeReference : "---", left, right, ref y);
            DrawField(context, "THROTTLE", guidance != null ? guidance.ThrottleAdvisory : "THROTTLE 0%", left, right, ref y);

            y += 8;

            DrawField(context, "TIME TO NODE", guidance != null ? FormatDuration(guidance.TimeToNodeSeconds) : "---", left, right, ref y);
            DrawField(context, "TIME TO IGN", guidance != null ? FormatDuration(guidance.TimeToIgnitionSeconds) : "---", left, right, ref y);
            DrawField(context, "PLANNED DV", guidance != null ? FormatDeltaV(guidance.PlannedDeltaVMetersPerSecond) : "---", left, right, ref y);
            DrawField(context, "BURN TIME", guidance != null ? FormatSeconds(guidance.BurnDurationSeconds) : "---", left, right, ref y);

            y += 12;

            DrawStatusBand(
                context,
                Rectangle.FromLTRB(
                    left,
                    y,
                    right,
                    y + 46),
                guidance != null
                    ? guidance.Command
                    : "AWAIT GUIDANCE",
                guidance != null &&
                guidance.Available
                    ? context.PhosphorColor
                    : Color.Orange);

            y += 62;

            using (SolidBrush dim =
                new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    "ADVISORY ONLY - NO AUTOPILOT / NO VEHICLE COMMAND",
                    context.SmallFont,
                    dim,
                    new RectangleF(
                        left,
                        y,
                        Math.Max(1, right - left),
                        24));
            }
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (Pen pen =
                new Pen(context.DimPhosphorColor, 1.0f))
            using (SolidBrush brush =
                new SolidBrush(context.DimPhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds.Left,
                    bounds.Top,
                    Math.Max(0, bounds.Width - 1),
                    Math.Max(0, bounds.Height - 1));

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    brush,
                    bounds.Left + 12,
                    bounds.Top + 10);
            }
        }

        private static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int left,
            int right,
            ref int y)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(context.PhosphorColor))
            using (StringFormat valueFormat =
                new StringFormat())
            {
                valueFormat.Alignment =
                    StringAlignment.Far;

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    left,
                    y);

                context.Graphics.DrawString(
                    Safe(value),
                    context.SmallFont,
                    valueBrush,
                    new RectangleF(
                        left + 160,
                        y,
                        Math.Max(1, right - left - 160),
                        24),
                    valueFormat);
            }

            y += 30;
        }

        private static void DrawStatusBand(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color)
        {
            using (Pen pen =
                new Pen(color, 1.0f))
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;
                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.DrawRectangle(pen, bounds);

                context.Graphics.DrawString(
                    Safe(text),
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
                new SolidBrush(color))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    Safe(text),
                    context.SmallFont,
                    brush,
                    new RectangleF(
                        left,
                        y,
                        Math.Max(1, right - left),
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
                seconds < 0.0 ? "-" : string.Empty;

            int total =
                (int)Math.Floor(Math.Abs(seconds));

            return
                sign +
                (total / 60).ToString("00") +
                ":" +
                (total % 60).ToString("00");
        }

        private static string FormatDeltaV(double value)
        {
            return IsFinite(value)
                ? value.ToString("0.00") + " M/S"
                : "---";
        }

        private static string FormatSeconds(double value)
        {
            return IsFinite(value)
                ? value.ToString("0.00") + " S"
                : "---";
        }

        private static string FormatAngle(double value)
        {
            return IsFinite(value)
                ? value.ToString("0.00") + " DEG"
                : "---";
        }

        private static string FormatSignedAngle(double value)
        {
            return IsFinite(value)
                ? value.ToString("+0.00;-0.00;0.00") + " DEG"
                : "---";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "---"
                : value.Trim().ToUpperInvariant();
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}
