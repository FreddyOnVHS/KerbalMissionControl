using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.14.3 compact POWER 2/2 distribution-event annunciator.
    ///
    /// The complete Engine history is retained in the snapshot. This initial UI
    /// foundation intentionally displays only count + latest event in unused
    /// header space so the consolidated 2/2 panel layout is not disturbed.
    /// </summary>
    internal static class PowerDistributionEventHeaderRenderer
    {
        private static readonly Color Info =
            Color.FromArgb(
                112,
                202,
                154);

        private static readonly Color Advisory =
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

        public static void Draw(
            MissionRenderContext context,
            AnalysisPipelineResult engineering)
        {
            if (context == null)
            {
                return;
            }

            Rectangle content =
                context.ContentBounds;

            const int leftReserve = 720;
            const int rightReserve = 410;

            Rectangle box =
                new Rectangle(
                    content.Left +
                    leftReserve,
                    content.Top + 24,
                    Math.Max(
                        0,
                        content.Width -
                        leftReserve -
                        rightReserve),
                    32);

            if (box.Width <= 0)
            {
                return;
            }

            ElectricalDistributionEventHistoryModel history =
                engineering != null &&
                engineering.Snapshot != null &&
                engineering.Snapshot.Power != null
                    ? engineering.Snapshot.Power.DistributionEvents
                    : null;

            ElectricalDistributionEventRecord latest =
                history != null
                    ? history.Latest
                    : null;

            string text;
            Color color;

            if (latest == null)
            {
                text =
                    "DIST EVT 00 / BASELINE";

                color =
                    context.DimPhosphorColor;
            }
            else
            {
                text =
                    "DIST EVT " +
                    history.Count.ToString("00") +
                    "  " +
                    latest.TimestampUtc.ToString("HH:mm:ss") +
                    "Z  " +
                    (latest.Code ?? "---");

                if (!string.IsNullOrWhiteSpace(
                        latest.Message))
                {
                    text +=
                        "  [" +
                        latest.Message +
                        "]";
                }

                color =
                    SeverityColor(
                        latest.Severity,
                        context);
            }

            TextRenderer.DrawText(
                context.Graphics,
                text.ToUpperInvariant(),
                context.SmallFont,
                box,
                color,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static Color SeverityColor(
            ElectricalEventSeverity severity,
            MissionRenderContext context)
        {
            switch (severity)
            {
                case ElectricalEventSeverity.Info:
                    return
                        Info;

                case ElectricalEventSeverity.Advisory:
                    return
                        Advisory;

                case ElectricalEventSeverity.Warning:
                    return
                        Warning;

                case ElectricalEventSeverity.Critical:
                    return
                        Critical;

                default:
                    return
                        context.DimPhosphorColor;
            }
        }
    }
}
