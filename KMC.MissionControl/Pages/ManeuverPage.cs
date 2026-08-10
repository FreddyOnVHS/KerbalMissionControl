using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Maneuver;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Build 11.2 maneuver planning review / uplink display.
    ///
    /// This page consumes the Engine-owned ManeuverPlanModel and uplink ACK status.
    /// It never performs or duplicates orbital / maneuver calculations.
    /// </summary>
    public sealed class ManeuverPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        public string Name
        {
            get { return "MANEUVER PLANNING"; }
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
                "FDO / REVIEW");

            ManeuverPlanModel plan =
                GetLatestPlan();

            if (plan == null)
            {
                layout.Row(
                    "PLAN STATUS",
                    "PLAN UNAVAILABLE",
                    "SOURCE",
                    "ENGINE MANEUVER FOUNDATION");

                layout.Space();

                DrawReviewBand(
                    context,
                    "NO ENGINE-OWNED MANEUVER PLAN AVAILABLE",
                    false);

                return;
            }

            layout.Row(
                "PLAN ID",
                Safe(plan.PlanId),
                "STATUS",
                Safe(plan.Status));

            layout.Row(
                "OBJECTIVE",
                Safe(plan.Objective),
                "AVAILABLE",
                plan.Available ? "YES" : "NO");

            layout.Space();

            layout.Row(
                "NODE MET",
                FormatDuration(plan.NodeMissionTimeSeconds),
                "TIME TO NODE",
                FormatDuration(plan.TimeToNodeSeconds));

            layout.Row(
                "NODE UT",
                plan.NodeUniversalTimeAvailable
                    ? FormatDuration(plan.NodeUniversalTimeSeconds)
                    : "UNAVAILABLE",
                "IGNITION MET",
                FormatDuration(plan.IgnitionMissionTimeSeconds));

            layout.Row(
                "BURN DURATION",
                FormatSeconds(plan.EstimatedBurnDurationSeconds),
                "IGNITION LEAD",
                FormatSeconds(plan.IgnitionLeadSeconds));

            layout.Space();

            layout.Row(
                "PROGRADE DV",
                FormatDeltaV(plan.ProgradeDeltaVMetersPerSecond),
                "TOTAL DV",
                FormatDeltaV(plan.TotalDeltaVMetersPerSecond));

            layout.Row(
                "NORMAL DV",
                FormatDeltaV(plan.NormalDeltaVMetersPerSecond),
                "RADIAL DV",
                FormatDeltaV(plan.RadialDeltaVMetersPerSecond));

            layout.Space();

            layout.Row(
                "PREDICTED AP",
                FormatDistance(plan.PredictedApoapsisMeters),
                "PREDICTED PE",
                FormatDistance(plan.PredictedPeriapsisMeters));

            layout.Row(
                "PREDICTED INC",
                FormatAngle(plan.PredictedInclinationDegrees),
                "PREDICTED ECC",
                FormatRatio(plan.PredictedEccentricity));

            ManeuverUplinkStatusSnapshot uplink =
                ManeuverUplinkStatusStore.GetLatest();

            layout.Row(
                "PREDICTED PERIOD",
                FormatDuration(plan.PredictedPeriodSeconds),
                "UPLINK STATUS",
                Safe(uplink.State));

            Rectangle reviewRegion =
                layout.ReserveRegion(
                    Math.Max(
                        90,
                        context.ContentBounds.Bottom -
                        layout.CurrentY -
                        12));

            DrawEvidence(
                context,
                reviewRegion,
                plan);

            string footer;

            bool nodeLoaded =
                string.Equals(
                    uplink.State,
                    "NODE LOADED",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    uplink.PlanId,
                    plan.PlanId,
                    StringComparison.Ordinal);

            if (nodeLoaded)
            {
                footer =
                    "NODE LOADED - PLUGIN ACKNOWLEDGED " +
                    Safe(plan.PlanId);
            }
            else if (string.Equals(
                         uplink.State,
                         "AWAITING ACK",
                         StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(
                         uplink.PlanId,
                         plan.PlanId,
                         StringComparison.Ordinal))
            {
                footer =
                    "UPLINK SENT - AWAITING PLUGIN ACK";
            }
            else if (!plan.NodeUniversalTimeAvailable)
            {
                footer =
                    "WAITING FOR KSP UNIVERSAL TIME - UPLINK INHIBITED";
            }
            else if (plan.Available)
            {
                footer =
                    "PLAN READY - CLICK UPLOAD MNV";
            }
            else
            {
                footer =
                    "PLAN NOT AVAILABLE - REVIEW ENGINE EVIDENCE";
            }

            DrawReviewBand(
                context,
                footer,
                plan.Available && plan.NodeUniversalTimeAvailable);
        }

        private static ManeuverPlanModel GetLatestPlan()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.ManeuverPlan == null)
            {
                return null;
            }

            return result.Snapshot.ManeuverPlan;
        }

        private static void DrawEvidence(
            MissionRenderContext context,
            Rectangle bounds,
            ManeuverPlanModel plan)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            int left = bounds.Left + 10;
            int top = bounds.Top + 8;
            int right = bounds.Right - 10;

            using (Pen border =
                new Pen(
                    context.DimPhosphorColor,
                    1.0f))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds.Left,
                    bounds.Top,
                    Math.Max(0, bounds.Width - 1),
                    Math.Max(0, bounds.Height - 1));

                context.Graphics.DrawString(
                    "VALIDATION / EVIDENCE",
                    context.SmallFont,
                    labelBrush,
                    left,
                    top);

                int y = top + 26;

                if (plan.Evidence == null ||
                    plan.Evidence.Count == 0)
                {
                    context.Graphics.DrawString(
                        "---",
                        context.SmallFont,
                        valueBrush,
                        left,
                        y);

                    return;
                }

                for (int index = 0;
                     index < plan.Evidence.Count &&
                     index < 5;
                     index++)
                {
                    string evidence =
                        plan.Evidence[index];

                    if (string.IsNullOrWhiteSpace(evidence))
                    {
                        continue;
                    }

                    string line =
                        (index + 1).ToString() +
                        ". " +
                        evidence.Trim().ToUpperInvariant();

                    RectangleF lineBounds =
                        new RectangleF(
                            left,
                            y,
                            Math.Max(1, right - left),
                            24);

                    context.Graphics.DrawString(
                        line,
                        context.SmallFont,
                        valueBrush,
                        lineBounds);

                    y += 24;

                    if (y >
                        bounds.Bottom - 24)
                    {
                        break;
                    }
                }
            }
        }

        private static void DrawReviewBand(
            MissionRenderContext context,
            string text,
            bool available)
        {
            Rectangle bounds =
                context.ContentBounds;

            Rectangle band =
                new Rectangle(
                    bounds.Left + 28,
                    bounds.Bottom - 42,
                    Math.Max(1, bounds.Width - 56),
                    30);

            Color color =
                available
                    ? context.PhosphorColor
                    : Color.Orange;

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
                    band);

                context.Graphics.DrawString(
                    Safe(text),
                    context.SmallFont,
                    brush,
                    band,
                    format);
            }
        }

        private static string Safe(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "---"
                : value.Trim().ToUpperInvariant();
        }

        private static string FormatDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            if (Math.Abs(meters) >= 1000.0)
            {
                return
                    (meters / 1000.0)
                        .ToString("0.000") +
                    " KM";
            }

            return
                meters.ToString("0") +
                " M";
        }

        private static string FormatDeltaV(
            double metersPerSecond)
        {
            return IsFinite(metersPerSecond)
                ? metersPerSecond.ToString("0.00") + " M/S"
                : "---";
        }

        private static string FormatSeconds(
            double seconds)
        {
            return IsFinite(seconds)
                ? seconds.ToString("0.00") + " S"
                : "---";
        }

        private static string FormatAngle(
            double degrees)
        {
            return IsFinite(degrees)
                ? degrees.ToString("0.000") + " DEG"
                : "---";
        }

        private static string FormatRatio(
            double value)
        {
            return IsFinite(value)
                ? value.ToString("0.000000")
                : "---";
        }

        private static string FormatDuration(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "---";
            }

            bool negative =
                seconds < 0.0;

            double absolute =
                Math.Abs(seconds);

            int wholeSeconds =
                (int)Math.Floor(absolute);

            int hours =
                wholeSeconds / 3600;

            int minutes =
                (wholeSeconds % 3600) / 60;

            int secs =
                wholeSeconds % 60;

            string value =
                hours > 0
                    ? hours.ToString("00") + ":" +
                      minutes.ToString("00") + ":" +
                      secs.ToString("00")
                    : minutes.ToString("00") + ":" +
                      secs.ToString("00");

            return negative
                ? "-" + value
                : value;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
