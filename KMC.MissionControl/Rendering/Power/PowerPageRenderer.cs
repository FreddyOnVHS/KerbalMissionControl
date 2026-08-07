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
    /// This renderer deliberately shows an engineering block schematic rather
    /// than claiming to know physical spacecraft wire routing. The Engine is
    /// the source of truth for every displayed electrical value.
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

            int topHeight =
                258;

            int middleHeight =
                254;

            int gap =
                12;

            Rectangle sources =
                new Rectangle(
                    area.Left,
                    area.Top,
                    340,
                    topHeight);

            Rectangle bus =
                new Rectangle(
                    sources.Right + gap,
                    area.Top,
                    area.Width -
                    340 -
                    420 -
                    gap * 2,
                    topHeight);

            Rectangle loads =
                new Rectangle(
                    bus.Right + gap,
                    area.Top,
                    420,
                    topHeight);

            Rectangle procedureBox =
                new Rectangle(
                    area.Left,
                    sources.Bottom + gap,
                    470,
                    middleHeight);

            Rectangle storageBox =
                new Rectangle(
                    procedureBox.Right + gap,
                    sources.Bottom + gap,
                    area.Width -
                    470 -
                    470 -
                    gap * 2,
                    middleHeight);

            Rectangle recoveryBox =
                new Rectangle(
                    storageBox.Right + gap,
                    sources.Bottom + gap,
                    470,
                    middleHeight);

            Rectangle stageBox =
                new Rectangle(
                    area.Left,
                    procedureBox.Bottom + gap,
                    area.Width,
                    area.Bottom -
                    procedureBox.Bottom -
                    gap);

            DrawPanel(
                graphics,
                sources,
                "POWER SOURCES",
                context);

            DrawPanel(
                graphics,
                bus,
                "MAIN ELECTRICAL BUS",
                context);

            DrawPanel(
                graphics,
                loads,
                "LOAD / SHEDDING",
                context);

            DrawPanel(
                graphics,
                procedureBox,
                "ENGINEERING PROCEDURE",
                context);

            DrawPanel(
                graphics,
                storageBox,
                "EC STORAGE",
                context);

            DrawPanel(
                graphics,
                recoveryBox,
                "RECOVERY VERIFICATION",
                context);

            DrawPanel(
                graphics,
                stageBox,
                "NEXT STAGE ELECTRICAL RISK",
                context);

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
                context);

            DrawBus(
                graphics,
                bus,
                status,
                flow,
                load,
                context);

            DrawLoads(
                graphics,
                loads,
                load,
                attribution,
                shedding,
                context);

            DrawProcedure(
                graphics,
                procedureBox,
                procedure,
                context);

            DrawStorage(
                graphics,
                storageBox,
                storage,
                context);

            DrawRecovery(
                graphics,
                recoveryBox,
                procedure,
                context);

            DrawStageRisk(
                graphics,
                stageBox,
                status,
                storage,
                context);
        }

        private static void DrawSources(
            Graphics graphics,
            Rectangle panel,
            ElectricalNetwork network,
            ElectricalLoadModel load,
            ElectricalAttributionModel attribution,
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

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

            DrawMetric(
                graphics,
                body,
                0,
                "SOURCE NODES",
                sourceNodes.ToString(),
                context,
                context.PhosphorColor);

            DrawMetric(
                graphics,
                body,
                1,
                "PRODUCERS",
                producers.ToString(),
                context,
                context.PhosphorColor);

            DrawMetric(
                graphics,
                body,
                2,
                "KNOWN GENERATION",
                generation,
                context,
                Healthy);

            DrawMetric(
                graphics,
                body,
                3,
                "DECLARED MAX",
                maxGeneration,
                context,
                context.PhosphorColor);

            DrawMetric(
                graphics,
                body,
                4,
                "EVIDENCE",
                evidence,
                context,
                context.DimPhosphorColor);
        }

        private static void DrawBus(
            Graphics graphics,
            Rectangle panel,
            ElectricalPowerDiagnosticModel status,
            ElectricalFlowModel flow,
            ElectricalLoadModel load,
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

            Color stateColor =
                SeverityColor(
                    status,
                    context);

            string stored =
                status != null &&
                status.TelemetryAvailable
                    ? status.StoredEc.ToString("0.0") +
                      " / " +
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

            string severity =
                status != null
                    ? status.Severity.ToString()
                    : "UNKNOWN";

            string condition =
                status != null
                    ? SplitWords(
                        status.Condition.ToString())
                    : "UNKNOWN";

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    36),
                severity,
                context.LargeFont,
                stateColor);

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top + 34,
                    body.Width,
                    26),
                condition,
                context.SmallFont,
                stateColor);

            DrawTwoColumnMetric(
                graphics,
                body,
                74,
                "EC",
                stored,
                "RESERVE",
                reserve,
                context);

            DrawTwoColumnMetric(
                graphics,
                body,
                112,
                "NET FLOW",
                flowRate,
                "ENDURANCE",
                endurance,
                context);

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

            DrawTwoColumnMetric(
                graphics,
                body,
                150,
                "DEMAND",
                demand,
                "MARGIN",
                margin,
                context);
        }

        private static void DrawLoads(
            Graphics graphics,
            Rectangle panel,
            ElectricalLoadModel load,
            ElectricalAttributionModel attribution,
            ElectricalLoadSheddingModel shedding,
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

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

            DrawTwoColumnMetric(
                graphics,
                body,
                0,
                "CONSUMERS",
                consumers.ToString(),
                "CURRENT KNOWN",
                known.ToString(),
                context);

            DrawTwoColumnMetric(
                graphics,
                body,
                40,
                "ATTRIBUTED",
                load != null
                    ? FormatRate(
                        load.AttributedCurrentLoadEcPerSecond)
                    : "--",
                "COVERAGE",
                coverage,
                context);

            DrawTwoColumnMetric(
                graphics,
                body,
                80,
                "PROTECTED",
                shedding != null
                    ? shedding.ProtectedConsumerCount.ToString()
                    : "--",
                "CANDIDATES",
                shedding != null
                    ? shedding.CandidateCount.ToString()
                    : "--",
                context);

            DrawTwoColumnMetric(
                graphics,
                body,
                120,
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
                context);

            string recommendation =
                shedding != null
                    ? shedding.State.ToString()
                    : "UNAVAILABLE";

            Color color =
                shedding != null &&
                shedding.SheddingRecommended
                    ? Warning
                    : context.PhosphorColor;

            DrawMetric(
                graphics,
                body,
                4,
                "SHEDDING",
                SplitWords(
                    recommendation),
                context,
                color);
        }

        private static void DrawProcedure(
            Graphics graphics,
            Rectangle panel,
            ElectricalProcedureModel procedure,
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

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

            DrawMetric(
                graphics,
                body,
                0,
                "STATE",
                SplitWords(
                    procedure.State.ToString()),
                context,
                stateColor);

            DrawMetric(
                graphics,
                body,
                1,
                "ACTION REQUIRED",
                procedure.ActionRequired
                    ? "YES"
                    : "NO",
                context,
                stateColor);

            DrawMetric(
                graphics,
                body,
                2,
                "CONFIDENCE",
                procedure.RecoveryConfidence.ToString(),
                context,
                context.PhosphorColor);

            DrawWrappedText(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top + 116,
                    body.Width,
                    54),
                procedure.PrimaryAction,
                context.SmallFont,
                stateColor);

            DrawWrappedText(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top + 174,
                    body.Width,
                    body.Bottom -
                    body.Top -
                    174),
                procedure.Objective,
                context.SmallFont,
                context.DimPhosphorColor);
        }

        private static void DrawStorage(
            Graphics graphics,
            Rectangle panel,
            ElectricalStorageModel storage,
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

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

            string amount =
                storage.StoredEc.ToString("0.0") +
                " / " +
                storage.CapacityEc.ToString("0.0") +
                " EC";

            DrawMetric(
                graphics,
                body,
                0,
                "TOTAL EC",
                amount,
                context,
                context.PhosphorColor);

            DrawMetric(
                graphics,
                body,
                1,
                "CHARGE",
                storage.ChargePercent.ToString("0.0") +
                "%",
                context,
                ReserveColor(
                    storage.ChargePercent,
                    context));

            DrawTwoColumnMetric(
                graphics,
                body,
                82,
                "STORAGE PARTS",
                storage.Parts.Count.ToString(),
                "SECTIONS",
                storage.StageSections.Count.ToString(),
                context);

            DrawTwoColumnMetric(
                graphics,
                body,
                122,
                "BRANCHES",
                storage.BranchSections.Count.ToString(),
                "NEXT STAGE",
                FormatStage(
                    storage.NextStage),
                context);

            Rectangle bar =
                new Rectangle(
                    body.Left,
                    body.Bottom - 28,
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
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

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

            DrawMetric(
                graphics,
                body,
                0,
                "RECOVERY STATE",
                SplitWords(
                    procedure.RecoveryState.ToString()),
                context,
                recoveryColor);

            DrawTwoColumnMetric(
                graphics,
                body,
                44,
                "BASELINE",
                baseline,
                "CURRENT",
                current,
                context);

            DrawTwoColumnMetric(
                graphics,
                body,
                84,
                "IMPROVEMENT",
                improvement,
                "DEFICIT CLEAR",
                procedure.DeficitCleared
                    ? "YES"
                    : "NO",
                context);

            DrawWrappedText(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top + 142,
                    body.Width,
                    body.Height - 142),
                procedure.Verification,
                context.SmallFont,
                context.DimPhosphorColor);
        }

        private static void DrawStageRisk(
            Graphics graphics,
            Rectangle panel,
            ElectricalPowerDiagnosticModel status,
            ElectricalStorageModel storage,
            MissionRenderContext context)
        {
            Rectangle body =
                Body(
                    panel);

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
                    ? "CRITICAL — NEXT STAGE REMOVES ALL KNOWN EC STORAGE"
                    : losesStorage
                        ? "CAUTION — NEXT STAGE REMOVES EC STORAGE"
                        : "NO NEXT-STAGE STORAGE HAZARD";

            DrawCenteredValue(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    34),
                headline,
                context.LargeFont,
                riskColor);

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

            string lost =
                lostStored.ToString("0.0") +
                " / " +
                lostCapacity.ToString("0.0") +
                " EC";

            string remaining =
                remainStored.ToString("0.0") +
                " / " +
                remainCapacity.ToString("0.0") +
                " EC";

            DrawTwoColumnMetric(
                graphics,
                body,
                44,
                "LOST ON STAGE",
                lost,
                "REMAINING",
                remaining,
                context);

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

            DrawTwoColumnMetric(
                graphics,
                body,
                84,
                "LOSE ALL",
                losesAll
                    ? "YES"
                    : "NO",
                "RESERVE AFTER",
                reserveAfter,
                context);
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
                line.StartCap =
                    LineCap.Flat;

                line.EndCap =
                    LineCap.Flat;

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
                    6;

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
            MissionRenderContext context)
        {
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
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    border,
                    bounds);

                graphics.DrawString(
                    title,
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 12,
                    bounds.Top + 8);

                graphics.DrawLine(
                    border,
                    bounds.Left + 10,
                    bounds.Top + 32,
                    bounds.Right - 10,
                    bounds.Top + 32);
            }
        }

        private static Rectangle Body(
            Rectangle panel)
        {
            return new Rectangle(
                panel.Left + 14,
                panel.Top + 40,
                Math.Max(
                    0,
                    panel.Width - 28),
                Math.Max(
                    0,
                    panel.Height - 52));
        }

        private static void DrawMetric(
            Graphics graphics,
            Rectangle body,
            int row,
            string label,
            string value,
            MissionRenderContext context,
            Color valueColor)
        {
            int y =
                body.Top +
                row * 36;

            DrawLabel(
                graphics,
                new Rectangle(
                    body.Left,
                    y,
                    body.Width,
                    15),
                label,
                context);

            DrawValue(
                graphics,
                new Rectangle(
                    body.Left,
                    y + 14,
                    body.Width,
                    24),
                value,
                context,
                valueColor);
        }

        private static void DrawTwoColumnMetric(
            Graphics graphics,
            Rectangle body,
            int yOffset,
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            MissionRenderContext context)
        {
            int gap =
                14;

            int width =
                (body.Width -
                 gap) /
                2;

            Rectangle left =
                new Rectangle(
                    body.Left,
                    body.Top + yOffset,
                    width,
                    38);

            Rectangle right =
                new Rectangle(
                    left.Right + gap,
                    body.Top + yOffset,
                    width,
                    38);

            DrawLabel(
                graphics,
                new Rectangle(
                    left.Left,
                    left.Top,
                    left.Width,
                    14),
                leftLabel,
                context);

            DrawValue(
                graphics,
                new Rectangle(
                    left.Left,
                    left.Top + 14,
                    left.Width,
                    24),
                leftValue,
                context,
                context.PhosphorColor);

            DrawLabel(
                graphics,
                new Rectangle(
                    right.Left,
                    right.Top,
                    right.Width,
                    14),
                rightLabel,
                context);

            DrawValue(
                graphics,
                new Rectangle(
                    right.Left,
                    right.Top + 14,
                    right.Width,
                    24),
                rightValue,
                context,
                context.PhosphorColor);
        }

        private static void DrawLabel(
            Graphics graphics,
            Rectangle bounds,
            string text,
            MissionRenderContext context)
        {
            TextRenderer.DrawText(
                graphics,
                Safe(
                    text),
                context.SmallFont,
                bounds,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawValue(
            Graphics graphics,
            Rectangle bounds,
            string text,
            MissionRenderContext context,
            Color color)
        {
            TextRenderer.DrawText(
                graphics,
                Safe(
                    text),
                context.SmallFont,
                bounds,
                color,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawCenteredValue(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color)
        {
            TextRenderer.DrawText(
                graphics,
                Safe(
                    text),
                font,
                bounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawWrappedText(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color)
        {
            TextRenderer.DrawText(
                graphics,
                Safe(
                    text),
                font,
                bounds,
                color,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
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
                            (bounds.Width - 3) *
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
            DrawPanel(
                graphics,
                bounds,
                "ELECTRICAL SYSTEMS",
                context);

            DrawCenteredValue(
                graphics,
                Body(
                    bounds),
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
