using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.Models;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Expanded Apollo/MOCR-inspired electrical engineering schematic.
    ///
    /// Build 8.10.2 keeps the measured-text safeguards introduced in 8.10.1
    /// and spends additional responsive-canvas space on real engineering
    /// detail: source evidence, load categories, storage stage sections,
    /// procedure/recovery, and next-stage electrical risk.
    /// </summary>
    public static class PowerPageRenderer
    {
        private static readonly Color Amber =
            Color.FromArgb(
                232,
                188,
                84);

        private static readonly Color Warning =
            Color.FromArgb(
                236,
                142,
                66);

        private static readonly Color Critical =
            Color.FromArgb(
                236,
                92,
                76);

        private static readonly Color Dead =
            Color.FromArgb(
                196,
                72,
                72);

        private static readonly Color Healthy =
            Color.FromArgb(
                112,
                202,
                154);

        private sealed class TextMetrics
        {
            public int SmallHeight;
            public int LargeHeight;
            public int RowHeight;
        }

        private sealed class LoadGroup
        {
            public string Category;
            public ElectricalLoadSheddingPriority Priority;
            public int Count;
            public int CurrentKnownCount;
            public double CurrentRate;
            public double MaximumRate;
        }

        public static void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry,
            AnalysisPipelineResult engineering)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            Graphics graphics =
                context.Graphics;

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            MissionPageLayout page =
                new MissionPageLayout(
                    context);

            page.DrawHeader(
                "ELECTRICAL SYSTEMS",
                "CH 05");

            Rectangle area =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 80);

            if (engineering == null ||
                engineering.Snapshot == null ||
                engineering.Snapshot.Power == null)
            {
                DrawWaiting(
                    graphics,
                    area,
                    context);

                return;
            }

            TextMetrics metrics =
                MeasureText(
                    graphics,
                    context);

            PowerModel power =
                engineering.Snapshot.Power;

            ElectricalNetwork network =
                power.ElectricalNetwork;

            ElectricalStorageModel storage =
                network != null
                    ? network.Storage
                    : null;

            ElectricalFlowModel flow =
                power.Flow;

            ElectricalLoadModel load =
                power.Load;

            ElectricalAttributionModel attribution =
                power.Attribution;

            ElectricalPowerDiagnosticModel status =
                power.Diagnostic;

            ElectricalLoadSheddingModel shedding =
                power.LoadShedding;

            ElectricalProcedureModel procedure =
                power.Procedure;

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
                    area.Right -
                    loadWidth,
                    area.Top,
                    loadWidth,
                    topHeight);

            Rectangle bus =
                new Rectangle(
                    sources.Right + gap,
                    area.Top,
                    loads.Left -
                    sources.Right -
                    gap * 2,
                    topHeight);

            int storageWidth =
                area.Width * 61 / 100;

            Rectangle storageBox =
                new Rectangle(
                    area.Left,
                    sources.Bottom + gap,
                    storageWidth,
                    middleHeight);

            Rectangle procedureBox =
                new Rectangle(
                    storageBox.Right + gap,
                    sources.Bottom + gap,
                    area.Right -
                    storageBox.Right -
                    gap,
                    middleHeight);

            int recoveryWidth =
                area.Width * 52 / 100;

            Rectangle recoveryBox =
                new Rectangle(
                    area.Left,
                    storageBox.Bottom + gap,
                    recoveryWidth,
                    bottomHeight);

            Rectangle stageBox =
                new Rectangle(
                    recoveryBox.Right + gap,
                    storageBox.Bottom + gap,
                    area.Right -
                    recoveryBox.Right -
                    gap,
                    bottomHeight);

            DrawPanel(
                graphics,
                sources,
                "POWER SOURCES",
                context,
                metrics);

            DrawPanel(
                graphics,
                bus,
                "MAIN ELECTRICAL BUS",
                context,
                metrics);

            DrawPanel(
                graphics,
                loads,
                "LOAD GROUPS / SHEDDING",
                context,
                metrics);

            DrawPanel(
                graphics,
                storageBox,
                "EC STORAGE NETWORK",
                context,
                metrics);

            DrawPanel(
                graphics,
                procedureBox,
                "ENGINEERING PROCEDURE",
                context,
                metrics);

            DrawPanel(
                graphics,
                recoveryBox,
                "RECOVERY VERIFICATION",
                context,
                metrics);

            DrawPanel(
                graphics,
                stageBox,
                "NEXT STAGE ELECTRICAL RISK",
                context,
                metrics);

            DrawSchematicBackbone(
                graphics,
                sources,
                bus,
                loads,
                storageBox,
                context,
                status);

            DrawSources(
                graphics,
                sources,
                network,
                load,
                attribution,
                context,
                metrics);

            DrawBus(
                graphics,
                bus,
                status,
                flow,
                load,
                context,
                metrics);

            DrawLoadGroups(
                graphics,
                loads,
                load,
                attribution,
                shedding,
                context,
                metrics);

            DrawStorageNetwork(
                graphics,
                storageBox,
                storage,
                context,
                metrics);

            DrawProcedure(
                graphics,
                procedureBox,
                procedure,
                context,
                metrics);

            DrawRecovery(
                graphics,
                recoveryBox,
                procedure,
                context,
                metrics);

            DrawStageRisk(
                graphics,
                stageBox,
                status,
                storage,
                context,
                metrics);
        }

        private static TextMetrics MeasureText(
            Graphics graphics,
            MissionRenderContext context)
        {
            Size small =
                TextRenderer.MeasureText(
                    graphics,
                    "Ag",
                    context.SmallFont,
                    new Size(
                        int.MaxValue,
                        int.MaxValue),
                    TextFormatFlags.NoPadding);

            Size large =
                TextRenderer.MeasureText(
                    graphics,
                    "Ag",
                    context.LargeFont,
                    new Size(
                        int.MaxValue,
                        int.MaxValue),
                    TextFormatFlags.NoPadding);

            TextMetrics metrics =
                new TextMetrics();

            metrics.SmallHeight =
                Math.Max(
                    1,
                    small.Height);

            metrics.LargeHeight =
                Math.Max(
                    1,
                    large.Height);

            metrics.RowHeight =
                metrics.SmallHeight +
                12;

            return metrics;
        }

        private static void DrawSources(
            Graphics graphics,
            Rectangle panel,
            ElectricalNetwork network,
            ElectricalLoadModel load,
            ElectricalAttributionModel attribution,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            int producers =
                attribution != null
                    ? attribution.ProducerCount
                    : 0;

            int sourceNodes =
                network != null
                    ? network.SourceNodeCount
                    : 0;

            string generation =
                load != null &&
                load.GenerationRateComplete
                    ? FormatRate(
                        load.GenerationEcPerSecond)
                    : "UNKNOWN";

            string maxGeneration =
                attribution != null &&
                attribution.TelemetryAvailable
                    ? FormatRate(
                        attribution.DeclaredMaximumGenerationEcPerSecond)
                    : "UNKNOWN";

            int y =
                body.Top;

            y = DrawRow(
                graphics,
                body,
                y,
                "SOURCE NODES",
                sourceNodes.ToString(),
                context,
                metrics,
                context.PhosphorColor);

            y = DrawRow(
                graphics,
                body,
                y,
                "PRODUCERS",
                producers.ToString(),
                context,
                metrics,
                context.PhosphorColor);

            y = DrawRow(
                graphics,
                body,
                y,
                "GENERATION",
                generation,
                context,
                metrics,
                Healthy);

            y = DrawRow(
                graphics,
                body,
                y,
                "MAX AVAILABLE",
                maxGeneration,
                context,
                metrics,
                context.PhosphorColor);

            if (attribution == null ||
                !attribution.TelemetryAvailable)
            {
                DrawRow(
                    graphics,
                    body,
                    y,
                    "EVIDENCE",
                    "WAITING",
                    context,
                    metrics,
                    context.DimPhosphorColor);

                return;
            }

            List<ElectricalAttributionEntry> entries =
                attribution.Entries
                    .Where(
                        entry =>
                            entry != null &&
                            entry.Kind ==
                                ElectricalAttributionKind.Producer)
                    .Take(
                        4)
                    .ToList();

            for (int index = 0;
                 index < entries.Count;
                 index++)
            {
                ElectricalAttributionEntry entry =
                    entries[index];

                string label =
                    Shorten(
                        string.IsNullOrWhiteSpace(
                            entry.Category)
                            ? entry.PartTitle
                            : entry.Category,
                        16);

                string rate =
                    entry.CurrentRateKnown
                        ? FormatRate(
                            entry.CurrentRateEcPerSecond)
                        : entry.MaximumRateKnown
                            ? "MAX " +
                              FormatRate(
                                  entry.MaximumRateEcPerSecond)
                            : "RATE UNKNOWN";

                y = DrawRow(
                    graphics,
                    body,
                    y,
                    label,
                    rate,
                    context,
                    metrics,
                    entry.CurrentRateKnown
                        ? context.PhosphorColor
                        : context.DimPhosphorColor);
            }
        }

        private static void DrawBus(
            Graphics graphics,
            Rectangle panel,
            ElectricalPowerDiagnosticModel status,
            ElectricalFlowModel flow,
            ElectricalLoadModel load,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            Color stateColor =
                SeverityColor(
                    status,
                    context);

            string severity =
                status != null
                    ? status.Severity.ToString()
                    : "UNKNOWN";

            string condition =
                status != null
                    ? SplitWords(
                        status.Condition.ToString())
                    : "UNKNOWN";

            int y =
                body.Top;

            int headlineHeight =
                metrics.LargeHeight +
                metrics.SmallHeight +
                16;

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    y,
                    body.Width,
                    metrics.LargeHeight + 4),
                severity,
                context.LargeFont,
                stateColor);

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    y + metrics.LargeHeight + 2,
                    body.Width,
                    metrics.SmallHeight + 4),
                condition,
                context.SmallFont,
                stateColor);

            y +=
                headlineHeight;

            string stored =
                status != null &&
                status.TelemetryAvailable
                    ? status.StoredEc.ToString("0.0") +
                      "/" +
                      status.CapacityEc.ToString("0.0") +
                      " EC"
                    : "--";

            string reserve =
                status != null &&
                status.TelemetryAvailable
                    ? status.ReservePercent.ToString("0.0") +
                      "%"
                    : "--";

            string net =
                flow != null &&
                flow.HasMeasuredNetStorageRate
                    ? FormatSignedRate(
                        flow.NetStorageRateEcPerSecond)
                    : "UNOBSERVABLE";

            string endurance =
                status != null &&
                status.HasEndurance
                    ? FormatDuration(
                        status.EnduranceSeconds)
                    : "--";

            string demand =
                load != null &&
                load.HasInferredTotalLoad
                    ? FormatRate(
                        load.InferredTotalLoadEcPerSecond)
                    : "UNKNOWN";

            string generation =
                load != null &&
                load.GenerationRateComplete
                    ? FormatRate(
                        load.GenerationEcPerSecond)
                    : "UNKNOWN";

            y = DrawPairRow(
                graphics,
                body,
                y,
                "EC STORAGE",
                stored,
                "RESERVE",
                reserve,
                context,
                metrics);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "NET FLOW",
                net,
                "ENDURANCE",
                endurance,
                context,
                metrics);

            DrawPairRow(
                graphics,
                body,
                y,
                "GENERATION",
                generation,
                "DEMAND",
                demand,
                context,
                metrics);
        }

        private static void DrawLoadGroups(
            Graphics graphics,
            Rectangle panel,
            ElectricalLoadModel load,
            ElectricalAttributionModel attribution,
            ElectricalLoadSheddingModel shedding,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            int y =
                body.Top;

            int consumers =
                attribution != null
                    ? attribution.ConsumerCount
                    : 0;

            string coverage =
                load != null &&
                load.HasInferredTotalLoad
                    ? load.AttributionCoveragePercent.ToString("0.0") +
                      "%"
                    : "UNKNOWN";

            y = DrawPairRow(
                graphics,
                body,
                y,
                "CONSUMERS",
                consumers.ToString(),
                "COVERAGE",
                coverage,
                context,
                metrics);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "PROTECTED",
                shedding != null
                    ? shedding.ProtectedConsumerCount.ToString()
                    : "--",
                "CANDIDATES",
                shedding != null
                    ? shedding.CandidateCount.ToString()
                    : "--",
                context,
                metrics);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "QUANTIFIED",
                shedding != null
                    ? FormatRate(
                        shedding.QuantifiedRecoverableEcPerSecond)
                    : "--",
                "POTENTIAL MAX",
                shedding != null
                    ? FormatRate(
                        shedding.PotentialMaximumRecoverableEcPerSecond)
                    : "--",
                context,
                metrics);

            if (shedding == null ||
                shedding.Candidates == null ||
                shedding.Candidates.Count == 0)
            {
                DrawRow(
                    graphics,
                    body,
                    y,
                    "LOAD GROUPS",
                    "NO CANDIDATE DETAIL",
                    context,
                    metrics,
                    context.DimPhosphorColor);

                return;
            }

            List<LoadGroup> groups =
                shedding.Candidates
                    .Where(
                        candidate =>
                            candidate != null)
                    .GroupBy(
                        candidate =>
                            string.IsNullOrWhiteSpace(
                                candidate.Category)
                                ? "OTHER"
                                : candidate.Category)
                    .Select(
                        group =>
                        {
                            List<ElectricalLoadSheddingCandidate> items =
                                group.ToList();

                            LoadGroup item =
                                new LoadGroup();

                            item.Category =
                                group.Key;

                            item.Priority =
                                items
                                    .OrderByDescending(
                                        candidate =>
                                            (int)candidate.Priority)
                                    .First()
                                    .Priority;

                            item.Count =
                                items.Count;

                            item.CurrentKnownCount =
                                items.Count(
                                    candidate =>
                                        candidate.CurrentRateKnown);

                            item.CurrentRate =
                                items
                                    .Where(
                                        candidate =>
                                            candidate.CurrentRateKnown)
                                    .Sum(
                                        candidate =>
                                            candidate.CurrentRateEcPerSecond);

                            item.MaximumRate =
                                items
                                    .Where(
                                        candidate =>
                                            candidate.MaximumRateKnown)
                                    .Sum(
                                        candidate =>
                                            candidate.MaximumRateEcPerSecond);

                            return item;
                        })
                    .OrderByDescending(
                        group =>
                            (int)group.Priority)
                    .ThenBy(
                        group =>
                            group.Category)
                    .Take(
                        5)
                    .ToList();

            for (int index = 0;
                 index < groups.Count;
                 index++)
            {
                LoadGroup group =
                    groups[index];

                string label =
                    Shorten(
                        group.Category,
                        15);

                string value =
                    PriorityText(
                        group.Priority) +
                    "  " +
                    group.Count +
                    "X";

                if (group.CurrentKnownCount > 0)
                {
                    value +=
                        "  " +
                        group.CurrentRate.ToString("0.###") +
                        " EC/s";
                }
                else if (group.MaximumRate > 0.0)
                {
                    value +=
                        "  MAX " +
                        group.MaximumRate.ToString("0.###");
                }

                y = DrawRow(
                    graphics,
                    body,
                    y,
                    label,
                    value,
                    context,
                    metrics,
                    PriorityColor(
                        group.Priority,
                        context));
            }
        }

        private static void DrawStorageNetwork(
            Graphics graphics,
            Rectangle panel,
            ElectricalStorageModel storage,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            if (storage == null)
            {
                DrawCenteredValue(
                    graphics,
                    body,
                    "STORAGE MODEL UNAVAILABLE",
                    context.LargeFont,
                    context.DimPhosphorColor);

                return;
            }

            int summaryHeight =
                metrics.RowHeight * 2 +
                8;

            Rectangle summary =
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    summaryHeight);

            int y =
                summary.Top;

            y = DrawPairRow(
                graphics,
                summary,
                y,
                "TOTAL EC",
                storage.StoredEc.ToString("0.0") +
                "/" +
                storage.CapacityEc.ToString("0.0") +
                " EC",
                "CHARGE",
                storage.ChargePercent.ToString("0.0") +
                "%",
                context,
                metrics);

            DrawPairRow(
                graphics,
                summary,
                y,
                "STORAGE PARTS",
                storage.Parts.Count.ToString(),
                "BRANCHES",
                storage.BranchSections.Count.ToString(),
                context,
                metrics);

            Rectangle schematic =
                new Rectangle(
                    body.Left,
                    summary.Bottom + 8,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        summary.Bottom -
                        8));

            DrawStorageSections(
                graphics,
                schematic,
                storage,
                context,
                metrics);
        }

        private static void DrawStorageSections(
            Graphics graphics,
            Rectangle bounds,
            ElectricalStorageModel storage,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            List<ElectricalStorageStageSection> sections =
                storage.StageSections
                    .OrderByDescending(
                        section =>
                            section.IsRetainedSection)
                    .ThenByDescending(
                        section =>
                            section.SeparationStage)
                    .Take(
                        6)
                    .ToList();

            if (sections.Count == 0)
            {
                DrawCenteredValue(
                    graphics,
                    bounds,
                    "NO EC STORAGE SECTIONS",
                    context.SmallFont,
                    context.DimPhosphorColor);

                return;
            }

            int gap =
                12;

            int usableWidth =
                bounds.Width -
                gap *
                Math.Max(
                    0,
                    sections.Count - 1);

            int nodeWidth =
                Math.Max(
                    110,
                    usableWidth /
                    sections.Count);

            int totalWidth =
                nodeWidth *
                sections.Count +
                gap *
                Math.Max(
                    0,
                    sections.Count - 1);

            int startX =
                bounds.Left +
                Math.Max(
                    0,
                    (bounds.Width -
                     totalWidth) /
                    2);

            int nodeHeight =
                Math.Min(
                    bounds.Height - 28,
                    metrics.RowHeight * 3 +
                    38);

            int nodeY =
                bounds.Top +
                Math.Max(
                    10,
                    (bounds.Height -
                     nodeHeight) /
                    2);

            using (Pen busPen =
                new Pen(
                    context.DimPhosphorColor,
                    2.0f))
            {
                int busY =
                    nodeY -
                    14;

                graphics.DrawLine(
                    busPen,
                    startX +
                    nodeWidth /
                    2,
                    busY,
                    startX +
                    totalWidth -
                    nodeWidth /
                    2,
                    busY);

                for (int index = 0;
                     index < sections.Count;
                     index++)
                {
                    int x =
                        startX +
                        index *
                        (nodeWidth + gap) +
                        nodeWidth /
                        2;

                    graphics.DrawLine(
                        busPen,
                        x,
                        busY,
                        x,
                        nodeY);
                }
            }

            for (int index = 0;
                 index < sections.Count;
                 index++)
            {
                ElectricalStorageStageSection section =
                    sections[index];

                Rectangle node =
                    new Rectangle(
                        startX +
                        index *
                        (nodeWidth + gap),
                        nodeY,
                        nodeWidth,
                        nodeHeight);

                bool lost =
                    section.WillSeparateOnNextStage;

                Color outlineColor =
                    lost
                        ? Warning
                        : context.DimPhosphorColor;

                using (SolidBrush fill =
                    new SolidBrush(
                        Color.FromArgb(
                            lost ? 15 : 8,
                            outlineColor)))
                using (Pen outline =
                    new Pen(
                        outlineColor,
                        lost
                            ? 2.2f
                            : 1.4f))
                {
                    if (lost)
                    {
                        outline.DashStyle =
                            DashStyle.Dash;
                    }

                    graphics.FillRectangle(
                        fill,
                        node);

                    graphics.DrawRectangle(
                        outline,
                        node);
                }

                string stageName =
                    section.IsRetainedSection
                        ? "RETAINED"
                        : "SEP STG " +
                          section.SeparationStage;

                DrawCenteredValue(
                    graphics,
                    new Rectangle(
                        node.Left + 4,
                        node.Top + 6,
                        node.Width - 8,
                        metrics.SmallHeight + 4),
                    stageName,
                    context.SmallFont,
                    lost
                        ? Warning
                        : context.PhosphorColor);

                DrawCenteredValue(
                    graphics,
                    new Rectangle(
                        node.Left + 4,
                        node.Top +
                        metrics.SmallHeight +
                        16,
                        node.Width - 8,
                        metrics.SmallHeight + 4),
                    section.StoredEc.ToString("0.0") +
                    "/" +
                    section.CapacityEc.ToString("0.0") +
                    " EC",
                    context.SmallFont,
                    context.PhosphorColor);

                DrawCenteredValue(
                    graphics,
                    new Rectangle(
                        node.Left + 4,
                        node.Top +
                        metrics.SmallHeight * 2 +
                        26,
                        node.Width - 8,
                        metrics.SmallHeight + 4),
                    section.StoragePartCount +
                    " STORAGE PART" +
                    (section.StoragePartCount == 1
                        ? string.Empty
                        : "S"),
                    context.SmallFont,
                    context.DimPhosphorColor);

                if (lost)
                {
                    DrawCenteredValue(
                        graphics,
                        new Rectangle(
                            node.Left + 4,
                            node.Bottom -
                            metrics.SmallHeight -
                            8,
                            node.Width - 8,
                            metrics.SmallHeight + 4),
                        "NEXT STAGE LOSS",
                        context.SmallFont,
                        Warning);
                }
            }
        }

        private static void DrawProcedure(
            Graphics graphics,
            Rectangle panel,
            ElectricalProcedureModel procedure,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            if (procedure == null)
            {
                DrawCenteredValue(
                    graphics,
                    body,
                    "PROCEDURE UNAVAILABLE",
                    context.LargeFont,
                    context.DimPhosphorColor);

                return;
            }

            Color actionColor =
                procedure.ActionRequired
                    ? Warning
                    : context.PhosphorColor;

            int y =
                body.Top;

            y = DrawRow(
                graphics,
                body,
                y,
                "STATE",
                SplitWords(
                    procedure.State.ToString()),
                context,
                metrics,
                actionColor);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "ACTION",
                procedure.ActionRequired
                    ? "REQUIRED"
                    : "NONE",
                "CONFIDENCE",
                procedure.RecoveryConfidence.ToString(),
                context,
                metrics);

            int remaining =
                body.Bottom -
                y;

            int primaryHeight =
                Math.Max(
                    metrics.SmallHeight * 3,
                    remaining * 47 / 100);

            DrawTextSection(
                graphics,
                new Rectangle(
                    body.Left,
                    y + 4,
                    body.Width,
                    primaryHeight - 4),
                "PRIMARY ACTION",
                procedure.PrimaryAction,
                context,
                metrics,
                actionColor);

            DrawTextSection(
                graphics,
                new Rectangle(
                    body.Left,
                    y + primaryHeight + 4,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        y -
                        primaryHeight -
                        4)),
                "OBJECTIVE",
                procedure.Objective,
                context,
                metrics,
                context.DimPhosphorColor);
        }

        private static void DrawRecovery(
            Graphics graphics,
            Rectangle panel,
            ElectricalProcedureModel procedure,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            if (procedure == null)
            {
                DrawCenteredValue(
                    graphics,
                    body,
                    "RECOVERY DATA UNAVAILABLE",
                    context.LargeFont,
                    context.DimPhosphorColor);

                return;
            }

            string baseline =
                procedure.HasBaseline
                    ? FormatSignedRate(
                        procedure.BaselineStorageRateEcPerSecond)
                    : "--";

            string current =
                procedure.CurrentStorageRateObservable
                    ? FormatSignedRate(
                        procedure.CurrentStorageRateEcPerSecond)
                    : "UNOBSERVABLE";

            string improvement =
                procedure.HasImprovement
                    ? "+" +
                      procedure.ImprovementEcPerSecond.ToString("0.###") +
                      " EC/s"
                    : "--";

            Color recoveryColor =
                procedure.DeficitCleared
                    ? Healthy
                    : procedure.HasImprovement
                        ? context.PhosphorColor
                        : context.DimPhosphorColor;

            int y =
                body.Top;

            y = DrawPairRow(
                graphics,
                body,
                y,
                "STATE",
                SplitWords(
                    procedure.RecoveryState.ToString()),
                "DEFICIT CLEAR",
                procedure.DeficitCleared
                    ? "YES"
                    : "NO",
                context,
                metrics,
                recoveryColor);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "BASELINE",
                baseline,
                "CURRENT",
                current,
                context,
                metrics);

            y = DrawRow(
                graphics,
                body,
                y,
                "IMPROVEMENT",
                improvement,
                context,
                metrics,
                recoveryColor);

            DrawTextSection(
                graphics,
                new Rectangle(
                    body.Left,
                    y + 4,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        y -
                        4)),
                "VERIFICATION",
                procedure.Verification,
                context,
                metrics,
                context.DimPhosphorColor);
        }

        private static void DrawStageRisk(
            Graphics graphics,
            Rectangle panel,
            ElectricalPowerDiagnosticModel status,
            ElectricalStorageModel storage,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            Rectangle body =
                Body(
                    panel,
                    metrics);

            bool losesStorage =
                status != null &&
                status.NextStageLosesStorage;

            bool losesAll =
                status != null &&
                status.NextStageLosesAllStorage;

            Color riskColor =
                losesAll
                    ? Critical
                    : losesStorage
                        ? Warning
                        : Healthy;

            string headline =
                losesAll
                    ? "CRITICAL - ALL EC STORAGE LOST"
                    : losesStorage
                        ? "CAUTION - EC STORAGE LOSS"
                        : "NO STORAGE HAZARD";

            int y =
                body.Top;

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    y,
                    body.Width,
                    metrics.LargeHeight + 8),
                headline,
                context.LargeFont,
                riskColor);

            y +=
                metrics.LargeHeight +
                12;

            double lostStored =
                status != null
                    ? status.NextStageLostStoredEc
                    : storage != null
                        ? storage.NextStageLostStoredEc
                        : 0.0;

            double lostCapacity =
                status != null
                    ? status.NextStageLostCapacityEc
                    : storage != null
                        ? storage.NextStageLostCapacityEc
                        : 0.0;

            double remainStored =
                status != null
                    ? status.NextStageRemainingStoredEc
                    : storage != null
                        ? storage.NextStageRemainingStoredEc
                        : 0.0;

            double remainCapacity =
                status != null
                    ? status.NextStageRemainingCapacityEc
                    : storage != null
                        ? storage.NextStageRemainingCapacityEc
                        : 0.0;

            y = DrawPairRow(
                graphics,
                body,
                y,
                "LOST",
                lostStored.ToString("0.0") +
                "/" +
                lostCapacity.ToString("0.0") +
                " EC",
                "REMAINING",
                remainStored.ToString("0.0") +
                "/" +
                remainCapacity.ToString("0.0") +
                " EC",
                context,
                metrics,
                riskColor);

            string reserveAfter =
                status != null &&
                status.NextStageRemainingCapacityEc >
                    0.000001
                    ? status.NextStageRemainingReservePercent.ToString("0.0") +
                      "%"
                    : storage != null &&
                      storage.NextStageRemainingCapacityEc >
                        0.000001
                        ? storage.NextStageRemainingChargePercent.ToString("0.0") +
                          "%"
                        : "--";

            DrawPairRow(
                graphics,
                body,
                y,
                "LOSE ALL",
                losesAll
                    ? "YES"
                    : "NO",
                "RESERVE AFTER",
                reserveAfter,
                context,
                metrics,
                riskColor);
        }

        private static void DrawSchematicBackbone(
            Graphics graphics,
            Rectangle sources,
            Rectangle bus,
            Rectangle loads,
            Rectangle storage,
            MissionRenderContext context,
            ElectricalPowerDiagnosticModel status)
        {
            Color color =
                SeverityColor(
                    status,
                    context);

            using (Pen line =
                new Pen(
                    Color.FromArgb(
                        170,
                        color),
                    3.0f))
            {
                int y =
                    sources.Top +
                    sources.Height /
                    2;

                graphics.DrawLine(
                    line,
                    sources.Right,
                    y,
                    bus.Left,
                    y);

                graphics.DrawLine(
                    line,
                    bus.Right,
                    y,
                    loads.Left,
                    y);

                int busX =
                    bus.Left +
                    bus.Width /
                    2;

                int storageX =
                    storage.Left +
                    storage.Width /
                    2;

                int elbowY =
                    bus.Bottom +
                    Math.Max(
                        6,
                        (storage.Top -
                         bus.Bottom) /
                        2);

                graphics.DrawLine(
                    line,
                    busX,
                    bus.Bottom,
                    busX,
                    elbowY);

                graphics.DrawLine(
                    line,
                    busX,
                    elbowY,
                    storageX,
                    elbowY);

                graphics.DrawLine(
                    line,
                    storageX,
                    elbowY,
                    storageX,
                    storage.Top);
            }

            DrawJunction(
                graphics,
                new Point(
                    bus.Left,
                    sources.Top +
                    sources.Height /
                    2),
                color);

            DrawJunction(
                graphics,
                new Point(
                    bus.Right,
                    sources.Top +
                    sources.Height /
                    2),
                color);
        }

        private static void DrawPanel(
            Graphics graphics,
            Rectangle bounds,
            string title,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            int titleHeight =
                metrics.SmallHeight +
                12;

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        7,
                        context.PhosphorColor)))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        150,
                        context.DimPhosphorColor),
                    1.4f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    border,
                    bounds);

                DrawText(
                    graphics,
                    new Rectangle(
                        bounds.Left + 12,
                        bounds.Top + 5,
                        bounds.Width - 24,
                        metrics.SmallHeight + 4),
                    title,
                    context.SmallFont,
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                graphics.DrawLine(
                    border,
                    bounds.Left + 10,
                    bounds.Top + titleHeight,
                    bounds.Right - 10,
                    bounds.Top + titleHeight);
            }
        }

        private static Rectangle Body(
            Rectangle panel,
            TextMetrics metrics)
        {
            int topInset =
                metrics.SmallHeight +
                22;

            return new Rectangle(
                panel.Left + 14,
                panel.Top + topInset,
                Math.Max(
                    0,
                    panel.Width - 28),
                Math.Max(
                    0,
                    panel.Height -
                    topInset -
                    12));
        }

        private static int DrawRow(
            Graphics graphics,
            Rectangle body,
            int y,
            string label,
            string value,
            MissionRenderContext context,
            TextMetrics metrics,
            Color valueColor)
        {
            int rowHeight =
                metrics.RowHeight;

            if (y + rowHeight >
                body.Bottom)
            {
                return y;
            }

            int labelWidth =
                Math.Max(
                    110,
                    body.Width * 42 / 100);

            Rectangle labelBounds =
                new Rectangle(
                    body.Left,
                    y,
                    labelWidth - 8,
                    rowHeight);

            Rectangle valueBounds =
                new Rectangle(
                    body.Left + labelWidth,
                    y,
                    Math.Max(
                        0,
                        body.Width -
                        labelWidth),
                    rowHeight);

            DrawText(
                graphics,
                labelBounds,
                label,
                context.SmallFont,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                graphics,
                valueBounds,
                value,
                context.SmallFont,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            return
                y +
                rowHeight;
        }

        private static int DrawPairRow(
            Graphics graphics,
            Rectangle body,
            int y,
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            MissionRenderContext context,
            TextMetrics metrics)
        {
            return
                DrawPairRow(
                    graphics,
                    body,
                    y,
                    leftLabel,
                    leftValue,
                    rightLabel,
                    rightValue,
                    context,
                    metrics,
                    context.PhosphorColor);
        }

        private static int DrawPairRow(
            Graphics graphics,
            Rectangle body,
            int y,
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            MissionRenderContext context,
            TextMetrics metrics,
            Color valueColor)
        {
            int rowHeight =
                metrics.RowHeight;

            if (y + rowHeight >
                body.Bottom)
            {
                return y;
            }

            int gap =
                22;

            int half =
                (body.Width -
                 gap) /
                2;

            Rectangle left =
                new Rectangle(
                    body.Left,
                    y,
                    half,
                    rowHeight);

            Rectangle right =
                new Rectangle(
                    left.Right + gap,
                    y,
                    half,
                    rowHeight);

            DrawInlineField(
                graphics,
                left,
                leftLabel,
                leftValue,
                context,
                valueColor);

            DrawInlineField(
                graphics,
                right,
                rightLabel,
                rightValue,
                context,
                valueColor);

            return
                y +
                rowHeight;
        }

        private static void DrawInlineField(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string value,
            MissionRenderContext context,
            Color valueColor)
        {
            int labelWidth =
                Math.Max(
                    78,
                    bounds.Width * 46 / 100);

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    labelWidth - 6,
                    bounds.Height);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left + labelWidth,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width -
                        labelWidth),
                    bounds.Height);

            DrawText(
                graphics,
                labelBounds,
                label,
                context.SmallFont,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                graphics,
                valueBounds,
                value,
                context.SmallFont,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawTextSection(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string text,
            MissionRenderContext context,
            TextMetrics metrics,
            Color textColor)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <=
                    metrics.SmallHeight)
            {
                return;
            }

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    metrics.SmallHeight + 2);

            DrawText(
                graphics,
                labelBounds,
                label,
                context.SmallFont,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            Rectangle textBounds =
                new Rectangle(
                    bounds.Left,
                    labelBounds.Bottom + 4,
                    bounds.Width,
                    Math.Max(
                        0,
                        bounds.Bottom -
                        labelBounds.Bottom -
                        4));

            DrawText(
                graphics,
                textBounds,
                text,
                context.SmallFont,
                textColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawText(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
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
                Safe(
                    text),
                font,
                bounds,
                color,
                flags);
        }

        private static void DrawCenteredValue(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color)
        {
            DrawText(
                graphics,
                bounds,
                text,
                font,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawJunction(
            Graphics graphics,
            Point point,
            Color color)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            {
                graphics.FillEllipse(
                    brush,
                    point.X - 4,
                    point.Y - 4,
                    8,
                    8);
            }
        }

        private static void DrawWaiting(
            Graphics graphics,
            Rectangle bounds,
            MissionRenderContext context)
        {
            TextMetrics metrics =
                MeasureText(
                    graphics,
                    context);

            DrawPanel(
                graphics,
                bounds,
                "ELECTRICAL SYSTEMS",
                context,
                metrics);

            DrawCenteredValue(
                graphics,
                Body(
                    bounds,
                    metrics),
                "WAITING FOR ENGINEERING TELEMETRY",
                context.LargeFont,
                context.DimPhosphorColor);
        }

        private static Color SeverityColor(
            ElectricalPowerDiagnosticModel status,
            MissionRenderContext context)
        {
            if (status == null)
            {
                return
                    context.DimPhosphorColor;
            }

            switch (status.Severity)
            {
                case ElectricalPowerSeverity.Normal:
                    return Healthy;

                case ElectricalPowerSeverity.Advisory:
                    return Amber;

                case ElectricalPowerSeverity.Warning:
                    return Warning;

                case ElectricalPowerSeverity.Critical:
                    return Critical;

                case ElectricalPowerSeverity.Blackout:
                    return Dead;

                default:
                    return context.DimPhosphorColor;
            }
        }

        private static Color PriorityColor(
            ElectricalLoadSheddingPriority priority,
            MissionRenderContext context)
        {
            switch (priority)
            {
                case ElectricalLoadSheddingPriority.Protected:
                    return Healthy;

                case ElectricalLoadSheddingPriority.Essential:
                    return context.PhosphorColor;

                case ElectricalLoadSheddingPriority.Conditional:
                    return Amber;

                case ElectricalLoadSheddingPriority.Preferred:
                    return Warning;

                case ElectricalLoadSheddingPriority.First:
                    return Critical;

                default:
                    return context.DimPhosphorColor;
            }
        }

        private static string PriorityText(
            ElectricalLoadSheddingPriority priority)
        {
            switch (priority)
            {
                case ElectricalLoadSheddingPriority.Protected:
                    return "PROTECTED";

                case ElectricalLoadSheddingPriority.Essential:
                    return "ESSENTIAL";

                case ElectricalLoadSheddingPriority.Conditional:
                    return "CONDITIONAL";

                case ElectricalLoadSheddingPriority.Preferred:
                    return "PREFERRED";

                case ElectricalLoadSheddingPriority.First:
                    return "FIRST SHED";

                default:
                    return "UNKNOWN";
            }
        }

        private static string FormatRate(
            double value)
        {
            return
                value.ToString("0.###") +
                " EC/s";
        }

        private static string FormatSignedRate(
            double value)
        {
            string prefix =
                value > 0.000001
                    ? "+"
                    : string.Empty;

            return
                prefix +
                value.ToString("0.###") +
                " EC/s";
        }

        private static string FormatDuration(
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
            if (string.IsNullOrWhiteSpace(
                value))
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

        private static string Shorten(
            string value,
            int maxLength)
        {
            string safe =
                Safe(
                    value);

            if (safe.Length <=
                maxLength)
            {
                return safe;
            }

            if (maxLength <= 3)
            {
                return
                    safe.Substring(
                        0,
                        maxLength);
            }

            return
                safe.Substring(
                    0,
                    maxLength - 3) +
                "...";
        }

        private static string Safe(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "---";
            }

            return
                value.Trim()
                    .ToUpperInvariant();
        }
    }
}
