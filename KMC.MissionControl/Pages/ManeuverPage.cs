using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Guidance;
using KMC.Engine.Maneuver;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Maneuver planning review / uplink / synchronization display.
    /// Build 13.6 replaces the large validation card with the live stock-KSP
    /// maneuver queue while preserving compact current-plan evidence.
    /// </summary>
    public sealed class ManeuverPage : IMissionPage, IMissionPageCanvasProvider
    {
        public string Name { get { return "MANEUVER PLANNING"; } }
        public Size PreferredVirtualCanvasSize { get { return Size.Empty; } }
        public MissionPageContentProfile ContentProfile { get { return MissionPageContentProfile.DenseEngineering; } }

        public void Draw(MissionRenderContext context, MissionTelemetry telemetry)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            MissionPageLayout layout = new MissionPageLayout(context);
            layout.DrawHeader(Name, "FDO / REVIEW");

            ManeuverPlanModel plan;
            GuidanceSolutionModel guidance;
            GetLatestManeuverState(out plan, out guidance);

            if (plan == null)
            {
                layout.Row("PLAN STATUS", "PLAN UNAVAILABLE", "SOURCE", "ENGINE MANEUVER FOUNDATION");
                layout.Space();
                Rectangle emptyRegion = layout.ReserveRegion(
                    Math.Max(120, context.ContentBounds.Bottom - layout.CurrentY - 58));
                DrawActiveManeuvers(context, emptyRegion, null);
                DrawReviewBand(context, "NO ENGINE-OWNED MANEUVER PLAN AVAILABLE", false);
                return;
            }

            ManeuverUplinkStatusSnapshot uplink = ManeuverUplinkStatusStore.GetLatest();
            bool samePlan = !string.IsNullOrWhiteSpace(plan.PlanId) &&
                string.Equals(uplink.PlanId, plan.PlanId, StringComparison.Ordinal);
            bool manualComplete = guidance != null && guidance.BurnComplete &&
                string.Equals(guidance.PlanId, plan.PlanId, StringComparison.Ordinal) &&
                string.Equals(guidance.PostBurnResult, "DV COMPLETE", StringComparison.OrdinalIgnoreCase);

            string displayedState = !plan.Available ? "INHIBITED" :
                samePlan ? Safe(uplink.State) : manualComplete ? "COMPLETED" : "IDLE";

            layout.Row("PLAN ID", Safe(plan.PlanId), "STATUS", manualComplete ? "MANEUVER COMPLETE" : Safe(plan.Status));
            layout.Row("OBJECTIVE", Safe(plan.Objective), "AVAILABLE", manualComplete ? "NO" : plan.Available ? "YES" : "NO");
            layout.Space();
            layout.Row("NODE MET", FormatDuration(plan.NodeMissionTimeSeconds), "TIME TO NODE", FormatDuration(plan.TimeToNodeSeconds));
            layout.Row("NODE UT", plan.NodeUniversalTimeAvailable ? FormatDuration(plan.NodeUniversalTimeSeconds) : "UNAVAILABLE",
                       "IGNITION MET", FormatDuration(plan.IgnitionMissionTimeSeconds));
            layout.Row("BURN DURATION", FormatSeconds(plan.EstimatedBurnDurationSeconds), "IGNITION LEAD", FormatSeconds(plan.IgnitionLeadSeconds));
            layout.Space();
            layout.Row("PROGRADE DV", FormatDeltaV(plan.ProgradeDeltaVMetersPerSecond), "TOTAL DV", FormatDeltaV(plan.TotalDeltaVMetersPerSecond));
            layout.Row("NORMAL DV", FormatDeltaV(plan.NormalDeltaVMetersPerSecond), "RADIAL DV", FormatDeltaV(plan.RadialDeltaVMetersPerSecond));
            layout.Space();
            layout.Row("PREDICTED AP", FormatDistance(plan.PredictedApoapsisMeters), "PREDICTED PE", FormatDistance(plan.PredictedPeriapsisMeters));
            layout.Row("PREDICTED INC", FormatAngle(plan.PredictedInclinationDegrees), "PREDICTED ECC", FormatRatio(plan.PredictedEccentricity));
            layout.Row("PREDICTED PERIOD", FormatDuration(plan.PredictedPeriodSeconds), "UPLINK STATUS", displayedState);

            bool currentNodeState = (plan.Available || manualComplete) && samePlan && uplink.NodeStateTelemetryAvailable;
            if (currentNodeState)
            {
                layout.Row("KSP NODE UT", uplink.NodeExists ? FormatDuration(uplink.NodeUniversalTimeSeconds) : "---",
                           "KSP PROGRADE DV", uplink.NodeExists ? FormatDeltaV(uplink.ProgradeDeltaVMetersPerSecond) : "---");
                layout.Row("KSP NORMAL DV", uplink.NodeExists ? FormatDeltaV(uplink.NormalDeltaVMetersPerSecond) : "---",
                           "KSP RADIAL DV", uplink.NodeExists ? FormatDeltaV(uplink.RadialDeltaVMetersPerSecond) : "---");
            }

            Rectangle queueRegion = layout.ReserveRegion(
                Math.Max(120, context.ContentBounds.Bottom - layout.CurrentY - 58));
            DrawActiveManeuvers(context, queueRegion, plan);

            bool nodeVerified = samePlan && string.Equals(uplink.State, "NODE VERIFIED", StringComparison.OrdinalIgnoreCase);
            bool crewModified = samePlan && string.Equals(uplink.State, "CREW MODIFIED", StringComparison.OrdinalIgnoreCase);
            bool nodeRemoved = samePlan && string.Equals(uplink.State, "NODE REMOVED", StringComparison.OrdinalIgnoreCase);
            bool vesselNotActive = samePlan && string.Equals(uplink.State, "VESSEL NOT ACTIVE", StringComparison.OrdinalIgnoreCase);
            bool nodeLoaded = samePlan && string.Equals(uplink.State, "NODE LOADED", StringComparison.OrdinalIgnoreCase);
            bool missed = string.Equals(plan.Status, "MANEUVER WINDOW MISSED", StringComparison.OrdinalIgnoreCase);
            bool retroPending = string.Equals(plan.Status, "RETROGRADE GUIDANCE PENDING", StringComparison.OrdinalIgnoreCase);

            string footer;
            if (manualComplete) footer = nodeRemoved ? "MANUAL MANEUVER COMPLETE - NODE REMOVED" : "MANUAL MANEUVER COMPLETE - REMOVE NODE";
            else if (missed) footer = "MANEUVER WINDOW MISSED - CLICK COMPUTE TO REPLAN";
            else if (retroPending) footer = "RETROGRADE GUIDANCE REQUIRED - UPLINK INHIBITED";
            else if (!plan.Available) footer = "PLAN NOT AVAILABLE - REVIEW ENGINE EVIDENCE";
            else if (nodeVerified) footer = "NODE VERIFIED - KSP MATCHES " + Safe(plan.PlanId);
            else if (crewModified) footer = "CREW MODIFIED NODE - KSP STATE DIFFERS FROM PLAN";
            else if (nodeRemoved) footer = "NODE REMOVED - UPLOAD REQUIRED";
            else if (vesselNotActive) footer = "TRACKED NODE VESSEL NOT ACTIVE";
            else if (nodeLoaded) footer = "NODE LOADED - VERIFYING KSP NODE";
            else if (samePlan && string.Equals(uplink.State, "AWAITING ACK", StringComparison.OrdinalIgnoreCase)) footer = "UPLINK SENT - AWAITING PLUGIN ACK";
            else if (!plan.NodeUniversalTimeAvailable) footer = "WAITING FOR KSP UNIVERSAL TIME - UPLINK INHIBITED";
            else footer = "PLAN READY - CLICK UPLOAD MNV";

            bool healthy = manualComplete || (plan.Available && plan.NodeUniversalTimeAvailable && !crewModified && !nodeRemoved && !vesselNotActive);
            DrawReviewBand(context, footer, healthy);
        }

        private static void DrawActiveManeuvers(MissionRenderContext context, Rectangle bounds, ManeuverPlanModel plan)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            ManeuverInventorySnapshot inventory = ManeuverInventoryStore.GetLatest();
            ManeuverUplinkStatusSnapshot uplink = ManeuverUplinkStatusStore.GetLatest();
            int left = bounds.Left + 10;
            int right = bounds.Right - 10;
            int top = bounds.Top + 8;

            using (Pen border = new Pen(context.DimPhosphorColor, 1.0f))
            using (SolidBrush labelBrush = new SolidBrush(context.DimPhosphorColor))
            using (SolidBrush valueBrush = new SolidBrush(context.PhosphorColor))
            using (SolidBrush nextBrush = new SolidBrush(Color.LimeGreen))
            {
                context.Graphics.DrawRectangle(border, bounds.Left, bounds.Top,
                    Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));
                context.Graphics.DrawString("ACTIVE MANEUVERS", context.SmallFont, labelBrush, left, top);

                DateTime now = DateTime.UtcNow;
                bool fresh = inventory != null && inventory.ReceivedUtc != DateTime.MinValue &&
                    now - inventory.ReceivedUtc <= TimeSpan.FromSeconds(2.0);

                string summary = fresh
                    ? inventory.Nodes.Count.ToString() + " KSP NODE(S) / " + Safe(inventory.VesselName)
                    : "KSP NODE INVENTORY WAITING";
                context.Graphics.DrawString(summary, context.SmallFont, labelBrush,
                    Math.Max(left + 260, right - 420), top);

                int headerY = top + 30;
                int width = Math.Max(1, right - left);
                int xNum = left;
                int xTime = left + (int)(width * 0.05);
                int xUt = left + (int)(width * 0.20);
                int xVector = left + (int)(width * 0.35);
                int xDv = left + (int)(width * 0.57);
                int xState = left + (int)(width * 0.72);
                int xPlan = left + (int)(width * 0.84);

                context.Graphics.DrawString("#", context.SmallFont, labelBrush, xNum, headerY);
                context.Graphics.DrawString("TIME TO NODE", context.SmallFont, labelBrush, xTime, headerY);
                context.Graphics.DrawString("NODE UT", context.SmallFont, labelBrush, xUt, headerY);
                context.Graphics.DrawString("VECTOR", context.SmallFont, labelBrush, xVector, headerY);
                context.Graphics.DrawString("DELTA-V", context.SmallFont, labelBrush, xDv, headerY);
                context.Graphics.DrawString("STATE", context.SmallFont, labelBrush, xState, headerY);
                context.Graphics.DrawString("KMC PLAN", context.SmallFont, labelBrush, xPlan, headerY);
                int headerDividerY = headerY + 30;
                context.Graphics.DrawLine(border, left, headerDividerY, right, headerDividerY);

                int y = headerDividerY + 8;
                int evidenceReserve = 34;
                int maxRows = Math.Max(1, (bounds.Bottom - evidenceReserve - y - 4) / 28);

                if (!fresh || inventory.Nodes.Count == 0)
                {
                    context.Graphics.DrawString(fresh ? "NO MANEUVER NODES ON ACTIVE VESSEL" : "WAITING FOR KMC-MNVI1 TELEMETRY",
                        context.SmallFont, valueBrush, left, y);
                }
                else
                {
                    for (int index = 0; index < inventory.Nodes.Count && index < maxRows; index++)
                    {
                        ManeuverInventoryNode node = inventory.Nodes[index];
                        double tNode = node.NodeUniversalTimeSeconds - inventory.UniversalTimeSeconds;
                        bool currentMatch = IsCurrentPlanNode(node, plan, uplink);
                        string state = currentMatch ? Safe(uplink.State) : index == 0 ? "NEXT" : "PLANNED";
                        string planText = currentMatch && plan != null ? ShortPlanId(plan.PlanId) : "---";
                        Brush rowBrush = index == 0 ? nextBrush : valueBrush;

                        context.Graphics.DrawString((index + 1).ToString(), context.SmallFont, rowBrush, xNum, y);
                        context.Graphics.DrawString(FormatCountdown(tNode), context.SmallFont, rowBrush, xTime, y);
                        context.Graphics.DrawString(FormatDuration(node.NodeUniversalTimeSeconds), context.SmallFont, rowBrush, xUt, y);
                        context.Graphics.DrawString(ManeuverInventoryFormatting.DescribeVector(node), context.SmallFont, rowBrush, xVector, y);
                        context.Graphics.DrawString(FormatDeltaV(ManeuverInventoryFormatting.TotalDeltaV(node)), context.SmallFont, rowBrush, xDv, y);
                        context.Graphics.DrawString(state, context.SmallFont, rowBrush, xState, y);
                        context.Graphics.DrawString(planText, context.SmallFont, rowBrush, xPlan, y);
                        y += 28;
                    }

                    if (inventory.Nodes.Count > maxRows)
                    {
                        context.Graphics.DrawString("+ " + (inventory.Nodes.Count - maxRows).ToString() + " MORE NODE(S)",
                            context.SmallFont, labelBrush, left, y);
                    }
                }

                int evidenceY = bounds.Bottom - 27;
                context.Graphics.DrawLine(border, left, evidenceY - 9, right, evidenceY - 9);
                string evidence = "EVIDENCE: ---";
                if (plan != null && plan.Evidence != null && plan.Evidence.Count > 0 && !string.IsNullOrWhiteSpace(plan.Evidence[0]))
                {
                    evidence = "EVIDENCE: " + plan.Evidence[0].Trim().ToUpperInvariant();
                }
                context.Graphics.DrawString(evidence, context.SmallFont, labelBrush,
                    new RectangleF(left, evidenceY, Math.Max(1, right - left), 24));
            }
        }

        private static bool IsCurrentPlanNode(ManeuverInventoryNode node, ManeuverPlanModel plan, ManeuverUplinkStatusSnapshot uplink)
        {
            if (node == null || plan == null || uplink == null || !uplink.NodeExists ||
                !string.Equals(plan.PlanId, uplink.PlanId, StringComparison.Ordinal)) return false;

            return Math.Abs(node.NodeUniversalTimeSeconds - uplink.NodeUniversalTimeSeconds) <= 0.25 &&
                   Math.Abs(node.ProgradeDeltaVMetersPerSecond - uplink.ProgradeDeltaVMetersPerSecond) <= 0.05 &&
                   Math.Abs(node.NormalDeltaVMetersPerSecond - uplink.NormalDeltaVMetersPerSecond) <= 0.05 &&
                   Math.Abs(node.RadialDeltaVMetersPerSecond - uplink.RadialDeltaVMetersPerSecond) <= 0.05;
        }

        private static string ShortPlanId(string planId)
        {
            if (string.IsNullOrWhiteSpace(planId)) return "---";
            string value = planId.Trim().ToUpperInvariant();
            return value.Length <= 12 ? value : "..." + value.Substring(value.Length - 9);
        }

        private static string FormatCountdown(double seconds)
        {
            if (!IsFinite(seconds)) return "---";
            if (seconds < 0.0) return "PAST " + FormatDuration(Math.Abs(seconds));
            return "T+ " + FormatDuration(seconds);
        }

        private static void GetLatestManeuverState(out ManeuverPlanModel plan, out GuidanceSolutionModel guidance)
        {
            plan = null;
            guidance = null;
            AnalysisPipelineResult result;
            if (!EngineeringSnapshotStore.TryGetLatest(out result) || result == null || result.Snapshot == null) return;
            plan = result.Snapshot.ManeuverPlan;
            guidance = result.Snapshot.Guidance;
        }

        private static void DrawReviewBand(MissionRenderContext context, string text, bool available)
        {
            Rectangle bounds = context.ContentBounds;
            Rectangle band = new Rectangle(bounds.Left + 28, bounds.Bottom - 42, Math.Max(1, bounds.Width - 56), 30);
            Color color = available ? context.PhosphorColor : Color.Orange;
            using (Pen pen = new Pen(color, 1.0f))
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                context.Graphics.DrawRectangle(pen, band);
                context.Graphics.DrawString(Safe(text), context.SmallFont, brush, band, format);
            }
        }

        private static string Safe(string value) { return string.IsNullOrWhiteSpace(value) ? "---" : value.Trim().ToUpperInvariant(); }
        private static string FormatDistance(double meters) { if (!IsFinite(meters)) return "---"; return Math.Abs(meters) >= 1000.0 ? (meters / 1000.0).ToString("0.000") + " KM" : meters.ToString("0") + " M"; }
        private static string FormatDeltaV(double value) { return IsFinite(value) ? value.ToString("0.00") + " M/S" : "---"; }
        private static string FormatSeconds(double value) { return IsFinite(value) ? value.ToString("0.00") + " S" : "---"; }
        private static string FormatAngle(double value) { return IsFinite(value) ? value.ToString("0.000") + " DEG" : "---"; }
        private static string FormatRatio(double value) { return IsFinite(value) ? value.ToString("0.000000") : "---"; }
        private static string FormatDuration(double seconds)
        {
            if (!IsFinite(seconds)) return "---";
            bool negative = seconds < 0.0;
            int total = (int)Math.Floor(Math.Abs(seconds));
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            string value = hours > 0 ? hours.ToString("00") + ":" + minutes.ToString("00") + ":" + secs.ToString("00") : minutes.ToString("00") + ":" + secs.ToString("00");
            return negative ? "-" + value : value;
        }
        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
