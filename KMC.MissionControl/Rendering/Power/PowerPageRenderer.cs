using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.Models;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Apollo/MOCR-inspired electrical schematic display.
    ///
    /// Build 8.10.1 uses measured text rows rather than fixed undersized
    /// label/value rectangles. The layout is intentionally conservative:
    /// readability takes priority over packing maximum information density.
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
            public int CompactRowHeight;
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
                    context.ContentBounds.Left + 20,
                    context.ContentBounds.Top + 68,
                    context.ContentBounds.Width - 40,
                    context.ContentBounds.Height - 88);

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

            /*
             * 8.10.1 layout:
             *
             * TOP    : Sources | Main Bus | Loads
             * MIDDLE : Procedure           | Storage
             * BOTTOM : Recovery            | Stage Risk
             *
             * The original 8.10 stage-risk panel had excess unused height
             * while other panels were cramped. This distribution gives the
             * information-heavy panels enough vertical room.
             */
            int gap =
                14;

            int topHeight =
                Math.Max(
                    286,
                    area.Height * 37 / 100);

            int middleHeight =
                Math.Max(
                    218,
                    area.Height * 29 / 100);

            int bottomHeight =
                area.Height -
                topHeight -
                middleHeight -
                gap * 2;

            int sourceWidth =
                area.Width * 28 / 100;

            int loadWidth =
                area.Width * 30 / 100;

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

            int middleLeftWidth =
                area.Width * 54 / 100;

            Rectangle procedureBox =
                new Rectangle(
                    area.Left,
                    sources.Bottom + gap,
                    middleLeftWidth,
                    middleHeight);

            Rectangle storageBox =
                new Rectangle(
                    procedureBox.Right + gap,
                    sources.Bottom + gap,
                    area.Right -
                    procedureBox.Right -
                    gap,
                    middleHeight);

            int bottomLeftWidth =
                area.Width * 52 / 100;

            Rectangle recoveryBox =
                new Rectangle(
                    area.Left,
                    procedureBox.Bottom + gap,
                    bottomLeftWidth,
                    bottomHeight);

            Rectangle stageBox =
                new Rectangle(
                    recoveryBox.Right + gap,
                    procedureBox.Bottom + gap,
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
                "LOAD / SHEDDING",
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
                storageBox,
                "EC STORAGE",
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

            DrawSchematicConnectors(
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

            DrawLoads(
                graphics,
                loads,
                load,
                attribution,
                shedding,
                context,
                metrics);

            DrawProcedure(
                graphics,
                procedureBox,
                procedure,
                context,
                metrics);

            DrawStorage(
                graphics,
                storageBox,
                storage,
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

            /*
             * No field may be shorter than the actual font it contains.
             * Additional spacing keeps scanline/bicubic presentation from
             * visually merging adjacent rows.
             */
            metrics.RowHeight =
                metrics.SmallHeight +
                12;

            metrics.CompactRowHeight =
                metrics.SmallHeight +
                8;

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

            string evidence =
                attribution != null &&
                attribution.TelemetryAvailable
                    ? attribution.KnownCurrentProducerCount +
                      "/" +
                      producers +
                      " CURRENT"
                    : "WAITING";

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

            DrawRow(
                graphics,
                body,
                y,
                "EVIDENCE",
                evidence,
                context,
                metrics,
                context.DimPhosphorColor);
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

            int statusHeight =
                metrics.LargeHeight +
                metrics.SmallHeight +
                12;

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    metrics.LargeHeight + 4),
                severity,
                context.LargeFont,
                stateColor);

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top +
                    metrics.LargeHeight +
                    2,
                    body.Width,
                    metrics.SmallHeight + 4),
                condition,
                context.SmallFont,
                stateColor);

            int y =
                body.Top +
                statusHeight;

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

            string flowRate =
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

            string margin =
                status != null &&
                status.HasPowerMargin
                    ? FormatSignedRate(
                        status.PowerMarginEcPerSecond)
                    : "--";

            y = DrawPairRow(
                graphics,
                body,
                y,
                "EC",
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
                flowRate,
                "ENDURANCE",
                endurance,
                context,
                metrics);

            DrawPairRow(
                graphics,
                body,
                y,
                "DEMAND",
                demand,
                "MARGIN",
                margin,
                context,
                metrics);
        }

        private static void DrawLoads(
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

            int consumers =
                attribution != null
                    ? attribution.ConsumerCount
                    : 0;

            int known =
                attribution != null
                    ? attribution.KnownCurrentConsumerCount
                    : 0;

            string coverage =
                load != null &&
                load.HasInferredTotalLoad
                    ? load.AttributionCoveragePercent.ToString("0.0") +
                      "%"
                    : "UNKNOWN";

            int y =
                body.Top;

            y = DrawPairRow(
                graphics,
                body,
                y,
                "CONSUMERS",
                consumers.ToString(),
                "CURRENT KNOWN",
                known.ToString(),
                context,
                metrics);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "ATTRIBUTED",
                load != null
                    ? FormatRate(
                        load.AttributedCurrentLoadEcPerSecond)
                    : "--",
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

            string recommendation =
                shedding != null
                    ? SplitWords(
                        shedding.State.ToString())
                    : "UNAVAILABLE";

            Color recommendationColor =
                shedding != null &&
                shedding.SheddingRecommended
                    ? Warning
                    : context.PhosphorColor;

            DrawRow(
                graphics,
                body,
                y,
                "SHEDDING",
                recommendation,
                context,
                metrics,
                recommendationColor);
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

            Color stateColor =
                procedure.ActionRequired
                    ? Warning
                    : context.PhosphorColor;

            int y =
                body.Top;

            y = DrawPairRow(
                graphics,
                body,
                y,
                "STATE",
                SplitWords(
                    procedure.State.ToString()),
                "ACTION",
                procedure.ActionRequired
                    ? "REQUIRED"
                    : "NONE",
                context,
                metrics,
                stateColor);

            y = DrawRow(
                graphics,
                body,
                y,
                "CONFIDENCE",
                procedure.RecoveryConfidence.ToString(),
                context,
                metrics,
                context.PhosphorColor);

            y += 4;

            int available =
                body.Bottom -
                y;

            int actionHeight =
                Math.Max(
                    metrics.SmallHeight * 2 + 8,
                    available * 46 / 100);

            DrawTextSection(
                graphics,
                new Rectangle(
                    body.Left,
                    y,
                    body.Width,
                    actionHeight),
                "PRIMARY ACTION",
                procedure.PrimaryAction,
                context,
                metrics,
                stateColor);

            DrawTextSection(
                graphics,
                new Rectangle(
                    body.Left,
                    y + actionHeight + 6,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        y -
                        actionHeight -
                        6)),
                "OBJECTIVE",
                procedure.Objective,
                context,
                metrics,
                context.DimPhosphorColor);
        }

        private static void DrawStorage(
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

            int y =
                body.Top;

            y = DrawRow(
                graphics,
                body,
                y,
                "TOTAL EC",
                storage.StoredEc.ToString("0.0") +
                "/" +
                storage.CapacityEc.ToString("0.0") +
                " EC",
                context,
                metrics,
                context.PhosphorColor);

            y = DrawRow(
                graphics,
                body,
                y,
                "CHARGE",
                storage.ChargePercent.ToString("0.0") +
                "%",
                context,
                metrics,
                ReserveColor(
                    storage.ChargePercent,
                    context));

            y = DrawPairRow(
                graphics,
                body,
                y,
                "STORAGE PARTS",
                storage.Parts.Count.ToString(),
                "SECTIONS",
                storage.StageSections.Count.ToString(),
                context,
                metrics);

            y = DrawPairRow(
                graphics,
                body,
                y,
                "BRANCHES",
                storage.BranchSections.Count.ToString(),
                "NEXT STAGE",
                FormatStage(
                    storage.NextStage),
                context,
                metrics);

            Rectangle bar =
                new Rectangle(
                    body.Left,
                    Math.Min(
                        body.Bottom -
                        18,
                        y + 8),
                    body.Width,
                    16);

            DrawChargeBar(
                graphics,
                bar,
                storage.ChargePercent,
                context);
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

            y = DrawRow(
                graphics,
                body,
                y,
                "STATE",
                SplitWords(
                    procedure.RecoveryState.ToString()),
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

            y = DrawPairRow(
                graphics,
                body,
                y,
                "IMPROVEMENT",
                improvement,
                "DEFICIT CLEAR",
                procedure.DeficitCleared
                    ? "YES"
                    : "NO",
                context,
                metrics);

            y += 4;

            DrawTextSection(
                graphics,
                new Rectangle(
                    body.Left,
                    y,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        y)),
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
                metrics);

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
                metrics);
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
                    115,
                    body.Width * 44 / 100);

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
                18;

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
                metrics,
                valueColor);

            DrawInlineField(
                graphics,
                right,
                rightLabel,
                rightValue,
                context,
                metrics,
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
            TextMetrics metrics,
            Color valueColor)
        {
            int labelWidth =
                Math.Max(
                    82,
                    bounds.Width * 48 / 100);

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
                    labelBounds.Bottom + 3,
                    bounds.Width,
                    Math.Max(
                        0,
                        bounds.Bottom -
                        labelBounds.Bottom -
                        3));

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

        private static void DrawChargeBar(
            Graphics graphics,
            Rectangle bounds,
            double percent,
            MissionRenderContext context)
        {
            double clamped =
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        percent));

            using (Pen outline =
                new Pen(
                    context.DimPhosphorColor,
                    1.0f))
            using (SolidBrush fill =
                new SolidBrush(
                    ReserveColor(
                        clamped,
                        context)))
            {
                graphics.DrawRectangle(
                    outline,
                    bounds);

                Rectangle level =
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Top + 2,
                        (int)Math.Round(
                            Math.Max(
                                0,
                                bounds.Width - 3) *
                            clamped /
                            100.0),
                        Math.Max(
                            0,
                            bounds.Height - 3));

                if (level.Width > 0 &&
                    level.Height > 0)
                {
                    graphics.FillRectangle(
                        fill,
                        level);
                }
            }
        }

        private static void DrawSchematicConnectors(
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
                        150,
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
                    7;

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

        private static Color ReserveColor(
            double percent,
            MissionRenderContext context)
        {
            if (percent <= 5.0)
            {
                return Critical;
            }

            if (percent <= 15.0)
            {
                return Warning;
            }

            if (percent <= 30.0)
            {
                return Amber;
            }

            return
                context.PhosphorColor;
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

        private static string FormatStage(
            int stage)
        {
            return
                stage >= 0
                    ? stage.ToString()
                    : "--";
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
