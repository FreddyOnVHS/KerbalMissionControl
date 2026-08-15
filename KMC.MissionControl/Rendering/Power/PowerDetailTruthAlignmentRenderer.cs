using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.14.2B truth-alignment layer for POWER 2/2.
    ///
    /// POWER 2/2 originally predated the switched A/B/ESS distribution model
    /// and mixed three kinds of evidence without making the distinction clear:
    ///
    /// - live KSP ElectricCharge telemetry,
    /// - topology-snapshot storage/staging analysis,
    /// - KMC synthetic switched-distribution truth.
    ///
    /// This renderer redraws the remaining legacy/coarse panels so each one is
    /// explicit about which truth it represents and never presents stale
    /// topology EC amounts as current live storage.
    /// </summary>
    internal static class PowerDetailTruthAlignmentRenderer
    {
        private static readonly Color Healthy =
            Color.FromArgb(112, 202, 154);

        private static readonly Color Advisory =
            Color.FromArgb(232, 188, 84);

        private static readonly Color Warning =
            Color.FromArgb(236, 142, 66);

        private static readonly Color Critical =
            Color.FromArgb(236, 92, 76);

        private static readonly Color Dead =
            Color.FromArgb(110, 125, 120);

        private static readonly Color PanelFill =
            Color.FromArgb(255, 7, 18, 20);

        public static void Draw(
            MissionRenderContext context,
            AnalysisPipelineResult engineering)
        {
            if (context == null ||
                engineering == null ||
                engineering.Snapshot == null ||
                engineering.Snapshot.Power == null)
            {
                return;
            }

            var power =
                engineering.Snapshot.Power;

            SyntheticElectricalDistributionModel distribution =
                engineering.Snapshot.SpacecraftSystems != null
                    ? engineering.Snapshot.SpacecraftSystems.ElectricalDistribution
                    : null;

            Rectangle area =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 80);

            int gap =
                Math.Max(
                    12,
                    area.Width / 160);

            int topHeight =
                area.Height * 31 / 100;

            int middleHeight =
                area.Height * 43 / 100;

            int bottomHeight =
                area.Height -
                topHeight -
                middleHeight -
                gap * 2;

            int sourceWidth =
                area.Width * 25 / 100;

            int loadWidth =
                area.Width * 31 / 100;

            Rectangle sources =
                new Rectangle(
                    area.Left,
                    area.Top,
                    sourceWidth,
                    topHeight);

            Rectangle loads =
                new Rectangle(
                    area.Right - loadWidth,
                    area.Top,
                    loadWidth,
                    topHeight);

            int storageWidth =
                area.Width * 61 / 100;

            Rectangle storage =
                new Rectangle(
                    area.Left,
                    sources.Bottom + gap,
                    storageWidth,
                    middleHeight);

            Rectangle procedure =
                new Rectangle(
                    storage.Right + gap,
                    sources.Bottom + gap,
                    area.Right -
                    storage.Right -
                    gap,
                    middleHeight);

            int recoveryWidth =
                area.Width * 52 / 100;

            Rectangle currentState =
                new Rectangle(
                    area.Left,
                    storage.Bottom + gap,
                    recoveryWidth,
                    bottomHeight);

            Rectangle stageRisk =
                new Rectangle(
                    currentState.Right + gap,
                    storage.Bottom + gap,
                    area.Right -
                    currentState.Right -
                    gap,
                    bottomHeight);

            DrawNativeConsumerPanel(
                context,
                loads,
                power.Attribution,
                power.LoadShedding,
                distribution);

            DrawLiveStoragePanel(
                context,
                storage,
                power.Diagnostic,
                power.Flow,
                power.ElectricalNetwork != null
                    ? power.ElectricalNetwork.Storage
                    : null);

            DrawCurrentActionPanel(
                context,
                procedure,
                distribution,
                power.Diagnostic,
                power.Flow);

            DrawCurrentStatePanel(
                context,
                currentState,
                distribution,
                power.Flow);

            DrawStageRiskPanel(
                context,
                stageRisk,
                power.Diagnostic,
                power.ElectricalNetwork != null
                    ? power.ElectricalNetwork.Storage
                    : null);
        }

        private static void DrawNativeConsumerPanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalAttributionModel attribution,
            ElectricalLoadSheddingModel shedding,
            SyntheticElectricalDistributionModel distribution)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "NATIVE KSP CONSUMERS / POTENTIAL");

            Graphics g =
                context.Graphics;

            int row =
                RowHeight(
                    context);

            int y =
                body.Top;

            string consumers =
                attribution != null
                    ? attribution.ConsumerCount.ToString()
                    : "--";

            string currentKnown =
                attribution != null
                    ? attribution.KnownCurrentConsumerCount.ToString()
                    : "--";

            string currentRate =
                attribution != null
                    ? attribution.KnownCurrentConsumptionEcPerSecond
                        .ToString("0.###") + " EC/S"
                    : "--";

            string maximumRate =
                attribution != null
                    ? attribution.DeclaredMaximumConsumptionEcPerSecond
                        .ToString("0.###") + " EC/S"
                    : "--";

            y = DrawPair(
                context,
                body,
                y,
                "DISCOVERED",
                consumers,
                "CURRENT KNOWN",
                currentKnown,
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "KNOWN CURRENT",
                currentRate,
                "DECLARED MAX",
                maximumRate,
                context.PhosphorColor);

            string candidateCount =
                shedding != null
                    ? shedding.CandidateCount.ToString()
                    : "--";

            string potential =
                shedding != null
                    ? shedding.PotentialMaximumRecoverableEcPerSecond
                        .ToString("0.###") + " EC/S"
                    : "--";

            y = DrawPair(
                context,
                body,
                y,
                "NATIVE CANDIDATES",
                candidateCount,
                "POTENTIAL MAX",
                potential,
                Advisory);

            string kmc =
                distribution != null
                    ? distribution.KmcOwnedActiveLoadEcPerSecond
                        .ToString("0.000") + " EC/S"
                    : "--";

            y = DrawPair(
                context,
                body,
                y,
                "KMC SWITCHED LOAD",
                kmc,
                "DOMAIN",
                "SEPARATE MODEL",
                Healthy);

            if (attribution == null ||
                !attribution.TelemetryAvailable)
            {
                DrawText(
                    g,
                    new Rectangle(
                        body.Left,
                        y,
                        body.Width,
                        row),
                    "NATIVE CONSUMER ATTRIBUTION WAITING",
                    context,
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                return;
            }

            List<IGrouping<string, ElectricalAttributionEntry>> groups =
                attribution.Entries
                    .Where(
                        entry =>
                            entry != null &&
                            entry.Kind ==
                                ElectricalAttributionKind.Consumer)
                    .GroupBy(
                        entry =>
                            string.IsNullOrWhiteSpace(
                                entry.Category)
                                ? "OTHER"
                                : entry.Category)
                    .OrderBy(
                        group => group.Key)
                    .Take(3)
                    .ToList();

            for (int index = 0;
                 index < groups.Count &&
                 y + row <= body.Bottom;
                 index++)
            {
                IGrouping<string, ElectricalAttributionEntry> group =
                    groups[index];

                double max =
                    group
                        .Where(
                            entry =>
                                entry.MaximumRateKnown)
                        .Sum(
                            entry =>
                                entry.MaximumRateEcPerSecond);

                string value =
                    group.Count().ToString() +
                    "X  MAX " +
                    max.ToString("0.###") +
                    " EC/S";

                y = DrawPair(
                    context,
                    body,
                    y,
                    group.Key,
                    value,
                    string.Empty,
                    string.Empty,
                    context.PhosphorColor);
            }
        }

        private static void DrawLiveStoragePanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalFlowModel flow,
            ElectricalStorageModel topology)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "LIVE EC STORAGE / TOPOLOGY");

            int y =
                body.Top;

            bool live =
                diagnostic != null &&
                diagnostic.TelemetryAvailable;

            string stored =
                live
                    ? diagnostic.StoredEc.ToString("0.0") +
                      "/" +
                      diagnostic.CapacityEc.ToString("0.0") +
                      " EC"
                    : "--";

            string reserve =
                live
                    ? diagnostic.ReservePercent.ToString("0.0") +
                      "%"
                    : "--";

            y = DrawPair(
                context,
                body,
                y,
                "LIVE STORAGE",
                stored,
                "RESERVE",
                reserve,
                context.PhosphorColor);

            string net =
                flow != null &&
                flow.HasMeasuredNetStorageRate
                    ? SignedRate(
                        flow.NetStorageRateEcPerSecond)
                    : "--";

            string endurance =
                diagnostic != null &&
                diagnostic.HasEndurance
                    ? Duration(
                        diagnostic.EnduranceSeconds)
                    : "--";

            y = DrawPair(
                context,
                body,
                y,
                "NET FLOW",
                net,
                "ENDURANCE",
                endurance,
                context.PhosphorColor);

            string parts =
                topology != null
                    ? topology.Parts.Count.ToString()
                    : "--";

            string branches =
                topology != null
                    ? topology.BranchSections.Count.ToString()
                    : "--";

            y = DrawPair(
                context,
                body,
                y,
                "TOPOLOGY PARTS",
                parts,
                "BRANCHES",
                branches,
                context.DimPhosphorColor);

            int remaining =
                Math.Max(
                    0,
                    body.Bottom - y);

            Rectangle topologyArea =
                new Rectangle(
                    body.Left,
                    y + 4,
                    body.Width,
                    Math.Max(
                        0,
                        remaining - 4));

            DrawTopologyCapacity(
                context,
                topologyArea,
                topology);
        }

        private static void DrawTopologyCapacity(
            MissionRenderContext context,
            Rectangle bounds,
            ElectricalStorageModel topology)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            if (topology == null ||
                topology.StageSections == null ||
                topology.StageSections.Count == 0)
            {
                DrawCentered(
                    context,
                    bounds,
                    "NO STORAGE TOPOLOGY",
                    context.DimPhosphorColor);

                return;
            }

            List<ElectricalStorageStageSection> sections =
                topology.StageSections
                    .OrderByDescending(
                        section =>
                            section.IsRetainedSection)
                    .ThenByDescending(
                        section =>
                            section.SeparationStage)
                    .Take(5)
                    .ToList();

            int gap =
                10;

            int width =
                Math.Max(
                    100,
                    (bounds.Width -
                     gap *
                     Math.Max(
                         0,
                         sections.Count - 1)) /
                    Math.Max(
                        1,
                        sections.Count));

            int total =
                width *
                sections.Count +
                gap *
                Math.Max(
                    0,
                    sections.Count - 1);

            int x =
                bounds.Left +
                Math.Max(
                    0,
                    (bounds.Width -
                     total) /
                    2);

            for (int index = 0;
                 index < sections.Count;
                 index++)
            {
                ElectricalStorageStageSection section =
                    sections[index];

                Rectangle node =
                    new Rectangle(
                        x +
                        index *
                        (width + gap),
                        bounds.Top + 4,
                        width,
                        Math.Max(
                            0,
                            bounds.Height - 8));

                Color color =
                    section.WillSeparateOnNextStage
                        ? Warning
                        : context.DimPhosphorColor;

                using (Pen border =
                    new Pen(
                        color,
                        section.WillSeparateOnNextStage
                            ? 1.8f
                            : 1.0f))
                {
                    context.Graphics.DrawRectangle(
                        border,
                        node);
                }

                string title =
                    section.IsRetainedSection
                        ? "RETAINED"
                        : "SEP STG " +
                          section.SeparationStage.ToString();

                string text =
                    title +
                    "\r\nCAP " +
                    section.CapacityEc.ToString("0.0") +
                    " EC\r\n" +
                    section.StoragePartCount.ToString() +
                    " STORAGE PART" +
                    (section.StoragePartCount == 1
                        ? string.Empty
                        : "S");

                if (section.WillSeparateOnNextStage)
                {
                    text +=
                        "\r\nNEXT STAGE LOSS";
                }

                DrawText(
                    context.Graphics,
                    new Rectangle(
                        node.Left + 6,
                        node.Top + 6,
                        Math.Max(
                            0,
                            node.Width - 12),
                        Math.Max(
                            0,
                            node.Height - 12)),
                    text,
                    context,
                    color,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak |
                    TextFormatFlags.NoPadding);
            }

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left,
                    bounds.Bottom -
                    RowHeight(context),
                    bounds.Width,
                    RowHeight(context)),
                "TOPOLOGY BOXES SHOW CAPACITY / STAGING ONLY - NOT LIVE EC AMOUNT",
                context,
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawCurrentActionPanel(
            MissionRenderContext context,
            Rectangle panel,
            SyntheticElectricalDistributionModel distribution,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalFlowModel flow)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "CURRENT ELECTRICAL ACTION");

            SyntheticElectricalBus mainA =
                distribution != null
                    ? distribution.FindBus("BUS_MAIN_A")
                    : null;

            SyntheticElectricalBus mainB =
                distribution != null
                    ? distribution.FindBus("BUS_MAIN_B")
                    : null;

            SyntheticElectricalBus ess =
                distribution != null
                    ? distribution.FindBus("BUS_ESS")
                    : null;

            bool blackout =
                IsDead(mainA) &&
                IsDead(mainB) &&
                IsDead(ess);

            bool degraded =
                IsDegraded(mainA) ||
                IsDegraded(mainB) ||
                IsDegraded(ess);

            bool batteryOperation =
                IsBatteryFed(mainA) ||
                IsBatteryFed(mainB);

            bool discharging =
                flow != null &&
                flow.HasMeasuredNetStorageRate &&
                flow.NetStorageRateEcPerSecond <
                    -0.000001;

            string state;
            string action;
            string objective;
            Color color;

            if (blackout)
            {
                state =
                    "BLACKOUT";
                action =
                    "RESTORE GENERATION / SOURCE PATH; SHED NONESSENTIAL LOAD.";
                objective =
                    "RE-ENERGIZE ESSENTIAL BUS AND RECOVER ELECTRICCHARGE.";
                color =
                    Critical;
            }
            else if (degraded)
            {
                state =
                    "DISTRIBUTION DEGRADED";
                action =
                    "REDUCE NONESSENTIAL LOAD; RESTORE GENERATION IF AVAILABLE.";
                objective =
                    "RELIEVE A/B BUS LOADING AND KEEP ESSENTIAL AVIONICS ENERGIZED.";
                color =
                    Warning;
            }
            else if (batteryOperation &&
                     discharging)
            {
                state =
                    "BATTERY OPERATION";
                action =
                    "CONSERVE POWER; SHED NONESSENTIAL LOAD AS REQUIRED.";
                objective =
                    "MAXIMIZE ENDURANCE WHILE MAINTAINING ESSENTIAL LOADS.";
                color =
                    Advisory;
            }
            else
            {
                state =
                    "DISTRIBUTION STABLE";
                action =
                    "NO IMMEDIATE SWITCHING ACTION.";
                objective =
                    "MONITOR BUS STATE, RESERVE, AND NET EC TREND.";
                color =
                    Healthy;
            }

            int y =
                body.Top;

            y = DrawPair(
                context,
                body,
                y,
                "STATE",
                state,
                "ACTION",
                blackout || degraded || (batteryOperation && discharging)
                    ? "REQUIRED"
                    : "NONE",
                color);

            DrawTextSection(
                context,
                new Rectangle(
                    body.Left,
                    y + 4,
                    body.Width,
                    Math.Max(
                        0,
                        (body.Bottom - y) / 2 - 4)),
                "PRIMARY ACTION",
                action,
                color);

            int secondTop =
                y +
                Math.Max(
                    0,
                    (body.Bottom - y) / 2);

            DrawTextSection(
                context,
                new Rectangle(
                    body.Left,
                    secondTop + 4,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        secondTop -
                        4)),
                "OBJECTIVE",
                objective,
                context.DimPhosphorColor);
        }

        private static void DrawCurrentStatePanel(
            MissionRenderContext context,
            Rectangle panel,
            SyntheticElectricalDistributionModel distribution,
            ElectricalFlowModel flow)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "CURRENT DISTRIBUTION / EC TREND");

            SyntheticElectricalBus mainA =
                distribution != null
                    ? distribution.FindBus("BUS_MAIN_A")
                    : null;

            SyntheticElectricalBus mainB =
                distribution != null
                    ? distribution.FindBus("BUS_MAIN_B")
                    : null;

            SyntheticElectricalBus ess =
                distribution != null
                    ? distribution.FindBus("BUS_ESS")
                    : null;

            int y =
                body.Top;

            y = DrawPair(
                context,
                body,
                y,
                "MAIN A",
                BusSummary(mainA),
                "MAIN B",
                BusSummary(mainB),
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "ESS",
                BusSummary(ess),
                "NET FLOW",
                flow != null &&
                flow.HasMeasuredNetStorageRate
                    ? SignedRate(
                        flow.NetStorageRateEcPerSecond)
                    : "--",
                BusColor(
                    ess,
                    context));

            double automatic =
                distribution != null
                    ? distribution.Buses
                        .Where(bus => bus != null)
                        .Sum(bus => bus.ShedDemandAmps)
                    : 0.0;

            double manual =
                distribution != null
                    ? distribution.Buses
                        .Where(bus => bus != null)
                        .Sum(bus => bus.ManualShedDemandAmps)
                    : 0.0;

            y = DrawPair(
                context,
                body,
                y,
                "AUTO SHED",
                automatic > 0.01
                    ? automatic.ToString("0.0") + " A"
                    : "--",
                "MAN SHED",
                manual > 0.01
                    ? manual.ToString("0.0") + " A"
                    : "--",
                Advisory);

            y = DrawPair(
                context,
                body,
                y,
                "KMC LOAD CMD",
                distribution != null
                    ? distribution.KmcOwnedActiveLoadEcPerSecond
                        .ToString("0.000") + " EC/S"
                    : "--",
                "EVIDENCE",
                "LIVE SNAPSHOT",
                Healthy);

            DrawText(
                context.Graphics,
                new Rectangle(
                    body.Left,
                    y + 2,
                    body.Width,
                    Math.Max(
                        RowHeight(context),
                        body.Bottom -
                        y -
                        2)),
                "BUS HISTORY IS NOT CLAIMED HERE UNTIL A/B/ESS TRANSITION EVENTS ARE ENGINE-OWNED.",
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawStageRiskPanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalStorageModel topology)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "NEXT STAGE STORAGE CAPACITY RISK");

            bool hasLive =
                diagnostic != null &&
                diagnostic.TelemetryAvailable;

            bool losesStorage =
                topology != null &&
                topology.HasStorageLossOnNextStage;

            bool losesAll =
                topology != null &&
                topology.LosesAllStorageOnNextStage;

            double lostCapacity =
                topology != null
                    ? topology.NextStageLostCapacityEc
                    : 0.0;

            double liveCapacity =
                hasLive
                    ? diagnostic.CapacityEc
                    : topology != null
                        ? topology.CapacityEc
                        : 0.0;

            double remainingCapacity =
                Math.Max(
                    0.0,
                    liveCapacity -
                    lostCapacity);

            string headline =
                losesAll
                    ? "CRITICAL - ALL STORAGE CAPACITY LOST"
                    : losesStorage
                        ? "CAUTION - PARTIAL STORAGE CAPACITY LOSS"
                        : "NO STORAGE CAPACITY HAZARD";

            Color color =
                losesAll
                    ? Critical
                    : losesStorage
                        ? Warning
                        : Healthy;

            int y =
                body.Top;

            DrawCentered(
                context,
                new Rectangle(
                    body.Left,
                    y,
                    body.Width,
                    RowHeight(context)),
                headline,
                color);

            y +=
                RowHeight(context);

            string lostStored;
            string remainingStored;
            string reserveAfter;

            if (!losesStorage)
            {
                lostStored =
                    hasLive
                        ? "0.0 EC"
                        : "--";

                remainingStored =
                    hasLive
                        ? diagnostic.StoredEc.ToString("0.0") +
                          "/" +
                          diagnostic.CapacityEc.ToString("0.0") +
                          " EC"
                        : "--";

                reserveAfter =
                    hasLive
                        ? diagnostic.ReservePercent.ToString("0.0") +
                          "%"
                        : "--";
            }
            else if (losesAll)
            {
                lostStored =
                    hasLive
                        ? diagnostic.StoredEc.ToString("0.0") +
                          " EC"
                        : "--";

                remainingStored =
                    "0.0/0.0 EC";

                reserveAfter =
                    "--";
            }
            else
            {
                /*
                 * Topology tells us which storage CAPACITY separates, but the
                 * topology snapshot is not high-frequency live EC allocation.
                 * Do not fabricate how today's live EC is distributed across
                 * the detachable and retained batteries.
                 */
                lostStored =
                    "--";

                remainingStored =
                    "--/" +
                    remainingCapacity.ToString("0.0") +
                    " EC";

                reserveAfter =
                    "--";
            }

            y = DrawPair(
                context,
                body,
                y,
                "LOST STORED",
                lostStored,
                "LOST CAPACITY",
                lostCapacity.ToString("0.0") +
                " EC",
                color);

            y = DrawPair(
                context,
                body,
                y,
                "REMAINING",
                remainingStored,
                "RESERVE AFTER",
                reserveAfter,
                color);

            DrawText(
                context.Graphics,
                new Rectangle(
                    body.Left,
                    y + 2,
                    body.Width,
                    Math.Max(
                        RowHeight(context),
                        body.Bottom -
                        y -
                        2)),
                losesStorage && !losesAll
                    ? "LIVE EC ALLOCATION BY DETACHABLE STORAGE SECTION IS UNKNOWN; CAPACITY RISK ONLY."
                    : "LIVE TOTAL EC IS USED WHEN NO PARTIAL STAGE-ALLOCATION ASSUMPTION IS REQUIRED.",
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static Rectangle BeginPanel(
            MissionRenderContext context,
            Rectangle panel,
            string title)
        {
            Graphics g =
                context.Graphics;

            int small =
                SmallHeight(
                    context);

            int titleHeight =
                small + 12;

            using (SolidBrush fill =
                new SolidBrush(
                    PanelFill))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        150,
                        context.DimPhosphorColor),
                    1.4f))
            {
                g.FillRectangle(
                    fill,
                    panel);

                g.DrawRectangle(
                    border,
                    panel);

                DrawText(
                    g,
                    new Rectangle(
                        panel.Left + 12,
                        panel.Top + 5,
                        panel.Width - 24,
                        small + 4),
                    title,
                    context,
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                g.DrawLine(
                    border,
                    panel.Left + 10,
                    panel.Top + titleHeight,
                    panel.Right - 10,
                    panel.Top + titleHeight);
            }

            return
                new Rectangle(
                    panel.Left + 14,
                    panel.Top +
                    titleHeight +
                    8,
                    Math.Max(
                        0,
                        panel.Width - 28),
                    Math.Max(
                        0,
                        panel.Height -
                        titleHeight -
                        20));
        }

        private static int DrawPair(
            MissionRenderContext context,
            Rectangle body,
            int y,
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            Color valueColor)
        {
            int row =
                RowHeight(
                    context);

            if (y + row >
                body.Bottom)
            {
                return y;
            }

            int gap =
                18;

            int half =
                (body.Width - gap) / 2;

            DrawField(
                context,
                new Rectangle(
                    body.Left,
                    y,
                    half,
                    row),
                leftLabel,
                leftValue,
                valueColor);

            DrawField(
                context,
                new Rectangle(
                    body.Left + half + gap,
                    y,
                    half,
                    row),
                rightLabel,
                rightValue,
                valueColor);

            return
                y + row;
        }

        private static void DrawField(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            Color valueColor)
        {
            int labelWidth =
                Math.Max(
                    84,
                    bounds.Width * 43 / 100);

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    Math.Max(
                        0,
                        labelWidth - 6),
                    bounds.Height),
                label,
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left +
                    labelWidth,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width -
                        labelWidth),
                    bounds.Height),
                value,
                context,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawTextSection(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string text,
            Color color)
        {
            int small =
                SmallHeight(
                    context);

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    small + 4),
                label,
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left,
                    bounds.Top +
                    small +
                    6,
                    bounds.Width,
                    Math.Max(
                        0,
                        bounds.Height -
                        small -
                        6)),
                text,
                context,
                color,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static bool IsDead(
            SyntheticElectricalBus bus)
        {
            return
                bus == null ||
                bus.State ==
                    SyntheticElectricalBusState.Unpowered ||
                bus.State ==
                    SyntheticElectricalBusState.Failed;
        }

        private static bool IsDegraded(
            SyntheticElectricalBus bus)
        {
            if (bus == null)
            {
                return true;
            }

            return
                bus.State ==
                    SyntheticElectricalBusState.HighLoad ||
                bus.State ==
                    SyntheticElectricalBusState.Overloaded ||
                bus.State ==
                    SyntheticElectricalBusState.Undervoltage ||
                bus.State ==
                    SyntheticElectricalBusState.Failed ||
                bus.State ==
                    SyntheticElectricalBusState.Unpowered;
        }

        private static bool IsBatteryFed(
            SyntheticElectricalBus bus)
        {
            return
                bus != null &&
                !string.IsNullOrWhiteSpace(
                    bus.ActiveSourceId) &&
                bus.ActiveSourceId.IndexOf(
                    "BAT_",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BusSummary(
            SyntheticElectricalBus bus)
        {
            if (bus == null)
            {
                return "--";
            }

            return
                SplitWords(
                    bus.State.ToString()) +
                " " +
                bus.Voltage.ToString("0.0") +
                " V";
        }

        private static Color BusColor(
            SyntheticElectricalBus bus,
            MissionRenderContext context)
        {
            if (bus == null)
            {
                return
                    context.DimPhosphorColor;
            }

            switch (bus.State)
            {
                case SyntheticElectricalBusState.Nominal:
                    return Healthy;

                case SyntheticElectricalBusState.HighLoad:
                    return Advisory;

                case SyntheticElectricalBusState.Overloaded:
                    return Warning;

                case SyntheticElectricalBusState.Undervoltage:
                case SyntheticElectricalBusState.Failed:
                    return Critical;

                case SyntheticElectricalBusState.Unpowered:
                    return Dead;

                default:
                    return context.DimPhosphorColor;
            }
        }

        private static int SmallHeight(
            MissionRenderContext context)
        {
            return
                Math.Max(
                    1,
                    TextRenderer.MeasureText(
                        context.Graphics,
                        "Ag",
                        context.SmallFont,
                        new Size(
                            int.MaxValue,
                            int.MaxValue),
                        TextFormatFlags.NoPadding)
                    .Height);
        }

        private static int RowHeight(
            MissionRenderContext context)
        {
            return
                SmallHeight(context) +
                12;
        }

        private static string SignedRate(
            double value)
        {
            return
                (value > 0.000001
                    ? "+"
                    : string.Empty) +
                value.ToString("0.###") +
                " EC/S";
        }

        private static string Duration(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds) ||
                seconds < 0.0)
            {
                return "--";
            }

            TimeSpan time =
                TimeSpan.FromSeconds(
                    seconds);

            if (time.TotalHours >= 1.0)
            {
                return
                    ((int)time.TotalHours).ToString("00") +
                    ":" +
                    time.Minutes.ToString("00") +
                    ":" +
                    time.Seconds.ToString("00");
            }

            return
                time.Minutes.ToString("00") +
                ":" +
                time.Seconds.ToString("00");
        }

        private static string SplitWords(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "---";
            }

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char current =
                    value[index];

                if (index > 0 &&
                    char.IsUpper(current) &&
                    !char.IsUpper(
                        value[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(
                    current);
            }

            return
                builder.ToString()
                    .ToUpperInvariant();
        }

        private static void DrawCentered(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color)
        {
            DrawText(
                context.Graphics,
                bounds,
                text,
                context,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawText(
            Graphics graphics,
            Rectangle bounds,
            string text,
            MissionRenderContext context,
            Color color,
            TextFormatFlags flags)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            TextRenderer.DrawText(
                graphics,
                string.IsNullOrWhiteSpace(
                    text)
                    ? "---"
                    : text
                        .Trim()
                        .ToUpperInvariant(),
                context.SmallFont,
                bounds,
                color,
                flags);
        }
    }
}
