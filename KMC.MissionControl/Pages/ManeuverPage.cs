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

            KmcManeuverPlanStore.Capture(plan);

            KmcManeuverPlanStore.RefreshLifecycle(
                ManeuverInventoryStore.GetLatest(),
                plan,
                guidance);

            ManeuverUplinkStatusSnapshot uplink =
                plan != null &&
                !string.IsNullOrWhiteSpace(plan.PlanId)
                    ? ManeuverUplinkStatusStore.GetForPlan(plan.PlanId)
                    : ManeuverUplinkStatusStore.GetLatest();
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

            int nextAvailableKmcSequence =
                nodeRemoved
                    ? GetNextAvailableKmcSequence()
                    : -1;

            string footer;
            if (manualComplete) footer = nodeRemoved ? "MANUAL MANEUVER COMPLETE - NODE REMOVED" : "MANUAL MANEUVER COMPLETE - REMOVE NODE";
            else if (missed) footer = "MANEUVER WINDOW MISSED - CLICK COMPUTE TO REPLAN";
            else if (retroPending) footer = "RETROGRADE GUIDANCE REQUIRED - UPLINK INHIBITED";
            else if (!plan.Available) footer = "PLAN NOT AVAILABLE - REVIEW ENGINE EVIDENCE";
            else if (nodeVerified) footer = "NODE VERIFIED - KSP MATCHES " + Safe(plan.PlanId);
            else if (crewModified) footer = "CREW MODIFIED NODE - KSP STATE DIFFERS FROM PLAN";
            else if (nodeRemoved && nextAvailableKmcSequence > 0)
                footer =
                    "NEXT KMC #" +
                    nextAvailableKmcSequence.ToString() +
                    " AVAILABLE - ACTIVATE NEXT";
            else if (nodeRemoved) footer = "NODE REMOVED - UPLOAD REQUIRED";
            else if (vesselNotActive) footer = "TRACKED NODE VESSEL NOT ACTIVE";
            else if (nodeLoaded) footer = "NODE LOADED - VERIFYING KSP NODE";
            else if (samePlan && string.Equals(uplink.State, "AWAITING ACK", StringComparison.OrdinalIgnoreCase)) footer = "UPLINK SENT - AWAITING PLUGIN ACK";
            else if (!plan.NodeUniversalTimeAvailable) footer = "WAITING FOR KSP UNIVERSAL TIME - UPLINK INHIBITED";
            else footer = "PLAN READY - CLICK UPLOAD MNV";

            bool healthy = manualComplete || (plan.Available && plan.NodeUniversalTimeAvailable && !crewModified && !nodeRemoved && !vesselNotActive);
            DrawReviewBand(context, footer, healthy);
        }

        /*
         * Build 13.10.1:
         * Presentation-only queue handoff helper. If the current active plan
         * has been removed, find the first remaining stock node that still
         * matches a retained KMC plan and return its permanent KMC sequence.
         */
        private static int GetNextAvailableKmcSequence()
        {
            ManeuverInventorySnapshot inventory =
                ManeuverInventoryStore.GetLatest();

            if (inventory == null ||
                inventory.Nodes == null ||
                inventory.Nodes.Count == 0 ||
                string.IsNullOrWhiteSpace(
                    inventory.VesselId))
            {
                return -1;
            }

            System.Collections.Generic.List<KmcQueuedManeuverPlan> retainedPlans =
                KmcManeuverPlanStore.GetAll();

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                KmcQueuedManeuverPlan retained =
                    FindRetainedPlanForNode(
                        inventory.Nodes[index],
                        inventory,
                        retainedPlans);

                if (retained == null)
                {
                    continue;
                }

                return
                    Math.Max(
                        1,
                        FindKmcPlanSequence(
                            retained.PlanId,
                            retainedPlans));
            }

            return -1;
        }

        private static void DrawActiveManeuvers(MissionRenderContext context, Rectangle bounds, ManeuverPlanModel plan)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            ManeuverInventorySnapshot inventory = ManeuverInventoryStore.GetLatest();
            ManeuverUplinkStatusSnapshot uplink =
                plan != null &&
                !string.IsNullOrWhiteSpace(plan.PlanId)
                    ? ManeuverUplinkStatusStore.GetForPlan(plan.PlanId)
                    : ManeuverUplinkStatusStore.GetLatest();
            System.Collections.Generic.List<KmcQueuedManeuverPlan> retainedPlans =
                KmcManeuverPlanStore.GetAll();

            string selectedPlanId =
                KmcManeuverPlanStore.GetSelectedPlanId();

            string activePlanId =
                ManeuverPlanPromotionStore.GetActivePlanId();

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

                string summary =
                    fresh
                        ? BuildQueueDirectorSummary(
                            inventory,
                            plan,
                            uplink,
                            retainedPlans)
                        : "KSP NODE INVENTORY WAITING";

                /*
                 * Build 13.7.1:
                 * Right-align the Queue Director summary inside a bounded
                 * rectangle instead of drawing from a fixed X origin. This
                 * keeps the complete "#x OF y" suffix visible at normal
                 * widescreen resolutions without changing queue semantics.
                 */
                using (StringFormat summaryFormat =
                    new StringFormat())
                {
                    summaryFormat.Alignment =
                        StringAlignment.Far;

                    summaryFormat.LineAlignment =
                        StringAlignment.Near;

                    RectangleF summaryBounds =
                        new RectangleF(
                            left + 300,
                            top,
                            Math.Max(
                                1,
                                right -
                                (left + 300)),
                            24);

                    context.Graphics.DrawString(
                        summary,
                        context.SmallFont,
                        labelBrush,
                        summaryBounds,
                        summaryFormat);
                }

                int headerY = top + 30;
                int width = Math.Max(1, right - left);
                int xNum = left;
                int xTime = left + (int)(width * 0.05);
                int xUt = left + (int)(width * 0.20);
                int xVector = left + (int)(width * 0.35);
                int xDv = left + (int)(width * 0.57);
                int xState = left + (int)(width * 0.70);
                int xPlan = left + (int)(width * 0.87);

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

                int lifecycleHeight =
                    retainedPlans.Count > 0 &&
                    bounds.Height >= 340
                        ? Math.Min(
                            170,
                            Math.Max(
                                118,
                                bounds.Height / 3))
                        : 0;

                int lifecycleTop =
                    lifecycleHeight > 0
                        ? bounds.Bottom -
                          evidenceReserve -
                          lifecycleHeight
                        : bounds.Bottom -
                          evidenceReserve;

                int maxRows =
                    Math.Max(
                        1,
                        (lifecycleTop - y - 8) / 28);

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
                        bool currentMatch =
                            IsCurrentPlanNode(
                                node,
                                plan,
                                uplink);

                        KmcQueuedManeuverPlan retainedMatch =
                            FindRetainedPlanForNode(
                                node,
                                inventory,
                                retainedPlans);

                        bool kmcOwned =
                            currentMatch ||
                            retainedMatch != null;

                        string matchedPlanId =
                            currentMatch &&
                            plan != null
                                ? plan.PlanId
                                : retainedMatch != null
                                    ? retainedMatch.PlanId
                                    : string.Empty;

                        int kmcSequence =
                            FindKmcPlanSequence(
                                matchedPlanId,
                                retainedPlans);

                        bool activeKmc =
                            kmcOwned &&
                            string.Equals(
                                matchedPlanId,
                                activePlanId,
                                StringComparison.Ordinal);

                        bool selectedKmc =
                            kmcOwned &&
                            string.Equals(
                                matchedPlanId,
                                selectedPlanId,
                                StringComparison.Ordinal);

                        string state =
                            DescribeQueueState(
                                index,
                                currentMatch,
                                kmcOwned,
                                activeKmc,
                                selectedKmc,
                                kmcSequence,
                                uplink);

                        string planText =
                            kmcOwned
                                ? "KMC #" +
                                  Math.Max(
                                      1,
                                      kmcSequence).ToString()
                                : "MANUAL";

                        Brush rowBrush =
                            index == 0 ||
                            kmcOwned
                                ? nextBrush
                                : valueBrush;

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

                if (lifecycleHeight > 0)
                {
                    DrawKmcPlanLifecycle(
                        context,
                        new Rectangle(
                            left,
                            lifecycleTop,
                            Math.Max(
                                1,
                                right - left),
                            lifecycleHeight),
                        retainedPlans);
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

        /*
         * Build 13.7 Maneuver Queue Director.
         *
         * KSP inventory owns chronological node order. Only the one stock
         * node that matches the current KMC plan/uplink may be described as
         * KMC-owned. Every other stock node is explicitly crew/manual.
         *
         * Build 13.10 adds explicit queue handoff awareness after the active
         * PlanId is confirmed NODE REMOVED. The next retained KMC node is
         * presented as available for crew activation, but is never promoted,
         * authorized, uploaded, or executed automatically.
         */
        /*
         * Build 13.11:
         * Retained KMC plans remain visible after their stock maneuver nodes
         * leave the live queue. This compact block is operational history
         * only and cannot authorize guidance or execution.
         */
        private static void DrawKmcPlanLifecycle(
            MissionRenderContext context,
            Rectangle bounds,
            System.Collections.Generic.List<KmcQueuedManeuverPlan> plans)
        {
            if (context == null ||
                plans == null ||
                plans.Count == 0 ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            using (Pen divider =
                new Pen(
                    context.DimPhosphorColor,
                    1.0f))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (SolidBrush completeBrush =
                new SolidBrush(
                    Color.LimeGreen))
            using (SolidBrush cautionBrush =
                new SolidBrush(
                    Color.Orange))
            {
                context.Graphics.DrawLine(
                    divider,
                    bounds.Left,
                    bounds.Top,
                    bounds.Right,
                    bounds.Top);

                int titleY =
                    bounds.Top + 7;

                context.Graphics.DrawString(
                    "KMC PLAN LIFECYCLE",
                    context.SmallFont,
                    labelBrush,
                    bounds.Left,
                    titleY);

                int headerY =
                    titleY + 27;

                int width =
                    Math.Max(
                        1,
                        bounds.Width);

                int xPlan =
                    bounds.Left;

                int xObjective =
                    bounds.Left +
                    (int)(width * 0.14);

                int xNodeUt =
                    bounds.Left +
                    (int)(width * 0.58);

                int xState =
                    bounds.Left +
                    (int)(width * 0.78);

                context.Graphics.DrawString(
                    "PLAN",
                    context.SmallFont,
                    labelBrush,
                    xPlan,
                    headerY);

                context.Graphics.DrawString(
                    "OBJECTIVE",
                    context.SmallFont,
                    labelBrush,
                    xObjective,
                    headerY);

                context.Graphics.DrawString(
                    "NODE UT",
                    context.SmallFont,
                    labelBrush,
                    xNodeUt,
                    headerY);

                context.Graphics.DrawString(
                    "LIFECYCLE",
                    context.SmallFont,
                    labelBrush,
                    xState,
                    headerY);

                int dividerY =
                    headerY + 25;

                context.Graphics.DrawLine(
                    divider,
                    bounds.Left,
                    dividerY,
                    bounds.Right,
                    dividerY);

                int availableRows =
                    Math.Max(
                        1,
                        (bounds.Bottom -
                         dividerY -
                         4) / 25);

                int firstIndex =
                    Math.Max(
                        0,
                        plans.Count -
                        availableRows);

                int y =
                    dividerY + 5;

                for (int index = firstIndex;
                     index < plans.Count;
                     index++)
                {
                    KmcQueuedManeuverPlan plan =
                        plans[index];

                    if (plan == null)
                    {
                        continue;
                    }

                    string lifecycle =
                        KmcManeuverPlanStore.DescribeLifecycle(
                            plan.LifecycleState);

                    Brush brush =
                        plan.LifecycleState ==
                            KmcManeuverLifecycleState.Complete
                            ? completeBrush
                            : plan.LifecycleState ==
                                  KmcManeuverLifecycleState.Removed ||
                              plan.LifecycleState ==
                                  KmcManeuverLifecycleState.Missed ||
                              plan.LifecycleState ==
                                  KmcManeuverLifecycleState.Modified
                                ? cautionBrush
                                : valueBrush;

                    context.Graphics.DrawString(
                        "KMC #" +
                        (index + 1).ToString(),
                        context.SmallFont,
                        brush,
                        xPlan,
                        y);

                    context.Graphics.DrawString(
                        Safe(
                            plan.Objective),
                        context.SmallFont,
                        brush,
                        xObjective,
                        y);

                    context.Graphics.DrawString(
                        FormatDuration(
                            plan.NodeUniversalTimeSeconds),
                        context.SmallFont,
                        brush,
                        xNodeUt,
                        y);

                    context.Graphics.DrawString(
                        lifecycle,
                        context.SmallFont,
                        brush,
                        xState,
                        y);

                    y += 25;
                }
            }
        }

        private static string BuildQueueDirectorSummary(
            ManeuverInventorySnapshot inventory,
            ManeuverPlanModel plan,
            ManeuverUplinkStatusSnapshot uplink,
            System.Collections.Generic.List<KmcQueuedManeuverPlan> retainedPlans)
        {
            if (inventory == null ||
                inventory.Nodes == null)
            {
                return "QUEUE DIRECTOR: WAITING";
            }

            int count =
                inventory.Nodes.Count;

            if (count == 0)
            {
                return
                    "QUEUE DIRECTOR: 0 KSP NODES / " +
                    Safe(inventory.VesselName);
            }

            string selectedPlanId =
                KmcManeuverPlanStore.GetSelectedPlanId();

            string activePlanId =
                ManeuverPlanPromotionStore.GetActivePlanId();

            int activeNodeIndex =
                FindNodeIndexForRetainedPlanId(
                    inventory,
                    retainedPlans,
                    activePlanId);

            int activeSequence =
                FindKmcPlanSequence(
                    activePlanId,
                    retainedPlans);

            if (activeNodeIndex >= 0 &&
                activeSequence > 0)
            {
                return
                    "QUEUE DIRECTOR: ACTIVE KMC #" +
                    activeSequence.ToString() +
                    " / NODE #" +
                    (activeNodeIndex + 1).ToString() +
                    " OF " +
                    count.ToString() +
                    " / NEXT #1";
            }

            ManeuverUplinkStatusSnapshot activePlanStatus =
                ManeuverUplinkStatusStore.GetForPlan(
                    activePlanId);

            bool activeNodeRemoved =
                activeNodeIndex < 0 &&
                activeSequence > 0 &&
                activePlanStatus != null &&
                string.Equals(
                    activePlanStatus.State,
                    "NODE REMOVED",
                    StringComparison.OrdinalIgnoreCase);

            if (activeNodeRemoved)
            {
                int nextKmcNodeIndex =
                    FindFirstRetainedKmcNodeIndex(
                        inventory,
                        retainedPlans,
                        plan,
                        uplink);

                if (nextKmcNodeIndex >= 0)
                {
                    KmcQueuedManeuverPlan nextKmcPlan =
                        FindRetainedPlanForNode(
                            inventory.Nodes[nextKmcNodeIndex],
                            inventory,
                            retainedPlans);

                    int nextKmcSequence =
                        nextKmcPlan != null
                            ? FindKmcPlanSequence(
                                nextKmcPlan.PlanId,
                                retainedPlans)
                            : -1;

                    return
                        "QUEUE DIRECTOR: NEXT KMC #" +
                        Math.Max(
                            1,
                            nextKmcSequence).ToString() +
                        " AVAILABLE / NODE #" +
                        (nextKmcNodeIndex + 1).ToString() +
                        " OF " +
                        count.ToString();
                }

                return
                    "QUEUE DIRECTOR: ACTIVE KMC #" +
                    activeSequence.ToString() +
                    " REMOVED / NO KMC PLAN AVAILABLE";
            }

            int selectedNodeIndex =
                FindNodeIndexForRetainedPlanId(
                    inventory,
                    retainedPlans,
                    selectedPlanId);

            int selectedSequence =
                FindKmcPlanSequence(
                    selectedPlanId,
                    retainedPlans);

            if (selectedNodeIndex >= 0 &&
                selectedSequence > 0)
            {
                return
                    "QUEUE DIRECTOR: SELECTING KMC #" +
                    selectedSequence.ToString() +
                    " / NODE #" +
                    (selectedNodeIndex + 1).ToString() +
                    " OF " +
                    count.ToString();
            }

            string nextVector =
                ManeuverInventoryFormatting.DescribeVector(
                    inventory.Nodes[0]);

            KmcQueuedManeuverPlan nextPlan =
                FindRetainedPlanForNode(
                    inventory.Nodes[0],
                    inventory,
                    retainedPlans);

            bool nextIsCurrent =
                IsCurrentPlanNode(
                    inventory.Nodes[0],
                    plan,
                    uplink);

            if (nextPlan != null ||
                nextIsCurrent)
            {
                string nextPlanId =
                    nextIsCurrent &&
                    plan != null
                        ? plan.PlanId
                        : nextPlan != null
                            ? nextPlan.PlanId
                            : string.Empty;

                int sequence =
                    FindKmcPlanSequence(
                        nextPlanId,
                        retainedPlans);

                return
                    "QUEUE DIRECTOR: NEXT #1 KMC #" +
                    Math.Max(
                        1,
                        sequence).ToString() +
                    " / " +
                    Safe(nextVector) +
                    " / " +
                    count.ToString() +
                    " TOTAL";
            }

            int firstKmcNodeIndex =
                FindFirstRetainedKmcNodeIndex(
                    inventory,
                    retainedPlans,
                    plan,
                    uplink);

            if (firstKmcNodeIndex >= 0)
            {
                return
                    "QUEUE DIRECTOR: NEXT #1 MANUAL / FIRST KMC NODE #" +
                    (firstKmcNodeIndex + 1).ToString() +
                    " OF " +
                    count.ToString();
            }

            return
                "QUEUE DIRECTOR: NEXT #1 " +
                Safe(nextVector) +
                " / MANUAL KSP / " +
                count.ToString() +
                " TOTAL";
        }

        private static string DescribeQueueState(
            int index,
            bool currentMatch,
            bool kmcOwned,
            bool activeKmc,
            bool selectedKmc,
            int kmcSequence,
            ManeuverUplinkStatusSnapshot uplink)
        {
            if (activeKmc)
            {
                bool verified =
                    uplink != null &&
                    string.Equals(
                        uplink.State,
                        "NODE VERIFIED",
                        StringComparison.OrdinalIgnoreCase);

                bool modified =
                    uplink != null &&
                    string.Equals(
                        uplink.State,
                        "CREW MODIFIED",
                        StringComparison.OrdinalIgnoreCase);

                if (modified)
                {
                    return "ACTIVE / MODIFIED";
                }

                if (verified)
                {
                    return "ACTIVE KMC";
                }

                return "ACTIVE / VERIFY";
            }

            if (selectedKmc)
            {
                return "SELECTED KMC";
            }

            if (kmcOwned)
            {
                return
                    index == 0
                        ? "NEXT KMC"
                        : "KMC QUEUED";
            }

            return
                index == 0
                    ? "NEXT / MANUAL"
                    : "MANUAL KSP";
        }

        private static KmcQueuedManeuverPlan FindRetainedPlanForNode(
            ManeuverInventoryNode node,
            ManeuverInventorySnapshot inventory,
            System.Collections.Generic.List<KmcQueuedManeuverPlan> plans)
        {
            if (node == null ||
                inventory == null ||
                plans == null)
            {
                return null;
            }

            for (int index = 0;
                 index < plans.Count;
                 index++)
            {
                KmcQueuedManeuverPlan plan =
                    plans[index];

                if (plan == null ||
                    !string.Equals(
                        plan.VesselId ?? string.Empty,
                        inventory.VesselId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (Math.Abs(
                        node.NodeUniversalTimeSeconds -
                        plan.NodeUniversalTimeSeconds) <= 0.25 &&
                    Math.Abs(
                        node.ProgradeDeltaVMetersPerSecond -
                        plan.ProgradeDeltaVMetersPerSecond) <= 0.05 &&
                    Math.Abs(
                        node.NormalDeltaVMetersPerSecond -
                        plan.NormalDeltaVMetersPerSecond) <= 0.05 &&
                    Math.Abs(
                        node.RadialDeltaVMetersPerSecond -
                        plan.RadialDeltaVMetersPerSecond) <= 0.05)
                {
                    return plan;
                }
            }

            return null;
        }

        private static int FindKmcPlanSequence(
            string planId,
            System.Collections.Generic.List<KmcQueuedManeuverPlan> plans)
        {
            if (string.IsNullOrWhiteSpace(
                    planId) ||
                plans == null)
            {
                return -1;
            }

            for (int index = 0;
                 index < plans.Count;
                 index++)
            {
                if (plans[index] != null &&
                    string.Equals(
                        plans[index].PlanId,
                        planId,
                        StringComparison.Ordinal))
                {
                    return index + 1;
                }
            }

            return -1;
        }

        private static int FindFirstRetainedKmcNodeIndex(
            ManeuverInventorySnapshot inventory,
            System.Collections.Generic.List<KmcQueuedManeuverPlan> plans,
            ManeuverPlanModel currentPlan,
            ManeuverUplinkStatusSnapshot uplink)
        {
            if (inventory == null ||
                inventory.Nodes == null)
            {
                return -1;
            }

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                ManeuverInventoryNode node =
                    inventory.Nodes[index];

                if (IsCurrentPlanNode(
                        node,
                        currentPlan,
                        uplink) ||
                    FindRetainedPlanForNode(
                        node,
                        inventory,
                        plans) != null)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindNodeIndexForRetainedPlanId(
            ManeuverInventorySnapshot inventory,
            System.Collections.Generic.List<KmcQueuedManeuverPlan> plans,
            string planId)
        {
            if (inventory == null ||
                inventory.Nodes == null ||
                plans == null ||
                string.IsNullOrWhiteSpace(
                    planId))
            {
                return -1;
            }

            KmcQueuedManeuverPlan plan =
                null;

            for (int index = 0;
                 index < plans.Count;
                 index++)
            {
                if (plans[index] != null &&
                    string.Equals(
                        plans[index].PlanId,
                        planId,
                        StringComparison.Ordinal))
                {
                    plan =
                        plans[index];

                    break;
                }
            }

            if (plan == null)
            {
                return -1;
            }

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                ManeuverInventoryNode node =
                    inventory.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                if (Math.Abs(
                        node.NodeUniversalTimeSeconds -
                        plan.NodeUniversalTimeSeconds) <= 0.25 &&
                    Math.Abs(
                        node.ProgradeDeltaVMetersPerSecond -
                        plan.ProgradeDeltaVMetersPerSecond) <= 0.05 &&
                    Math.Abs(
                        node.NormalDeltaVMetersPerSecond -
                        plan.NormalDeltaVMetersPerSecond) <= 0.05 &&
                    Math.Abs(
                        node.RadialDeltaVMetersPerSecond -
                        plan.RadialDeltaVMetersPerSecond) <= 0.05)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindCurrentPlanNodeIndex(
            ManeuverInventorySnapshot inventory,
            ManeuverPlanModel plan,
            ManeuverUplinkStatusSnapshot uplink)
        {
            if (inventory == null ||
                inventory.Nodes == null)
            {
                return -1;
            }

            for (int index = 0;
                 index < inventory.Nodes.Count;
                 index++)
            {
                if (IsCurrentPlanNode(
                        inventory.Nodes[index],
                        plan,
                        uplink))
                {
                    return index;
                }
            }

            return -1;
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
