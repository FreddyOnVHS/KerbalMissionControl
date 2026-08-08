using System;
using System.Drawing;
using KMC.Engine.Propulsion;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class PropellantFlowCard :
        MissionDisplayCard<PropulsionPageRenderModel>
    {
        public PropellantFlowCard()
            : base(
                "prop.flow",
                "PROPELLANT FEED / STAGE SYSTEM")
        {
        }

        protected override void DrawContent(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionPageRenderModel model)
        {
            if (model == null ||
                model.Analysis == null)
            {
                return;
            }

            int stripHeight =
                Math.Max(
                    54,
                    Math.Min(
                        72,
                        bounds.Height / 7));

            Rectangle schematic =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    Math.Max(
                        1,
                        bounds.Height -
                        stripHeight -
                        8));

            Rectangle strip =
                new Rectangle(
                    bounds.Left,
                    schematic.Bottom + 8,
                    bounds.Width,
                    stripHeight);

            /*
             * Geometry is intentionally retained from the proven propulsion
             * renderer. Engineering meaning comes from Snapshot.Propulsion.
             */
            PropulsionDisplayRenderer.DrawSystemFlow(
                context.Graphics,
                schematic,
                model.Analysis.SystemModel,
                model.Telemetry,
                context.SmallFont,
                context.SmallFont,
                context.PhosphorColor,
                context.DimPhosphorColor);

            DrawEngineeringStrip(
                context,
                strip,
                model);
        }

        private static void DrawEngineeringStrip(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionPageRenderModel model)
        {
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        95,
                        context.DimPhosphorColor)))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);
            }

            if (model.Engineering == null ||
                model.Engineering.Feed == null ||
                !model.Engineering.Feed.Available)
            {
                DrawCentered(
                    context,
                    bounds,
                    "ENGINE FEED MODEL UNAVAILABLE",
                    context.DimPhosphorColor);
                return;
            }

            PropulsionFeedModel feed =
                model.Engineering.Feed;

            PropulsionStatusModel status =
                model.Engineering.Status;

            string[] labels =
            {
                "CURRENT FEED",
                "READY / FED",
                "NEXT RETAIN",
                "NEXT LOST",
                "NEXT FEED",
                "OBSERVABILITY"
            };

            string[] values =
            {
                feed.CurrentFeedAvailableEngineCount +
                "/" +
                feed.EngineCount,

                feed.ReadyEngineFeedAvailableCount +
                "/" +
                feed.ReadyEngineCount,

                feed.NextStageRetainedEngineCount
                    .ToString(),

                feed.NextStageLostEngineCount
                    .ToString(),

                feed.NextStageRetainedFeedAvailableCount +
                "/" +
                feed.NextStageRetainedEngineCount,

                status != null
                    ? status.FeedObservability
                        .ToString()
                        .ToUpperInvariant()
                    : "SNAPSHOT"
            };

            int cellWidth =
                Math.Max(
                    1,
                    bounds.Width /
                    labels.Length);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        bounds.Left +
                        index * cellWidth,
                        bounds.Top,
                        index ==
                            labels.Length - 1
                                ? bounds.Right -
                                  (bounds.Left +
                                   index * cellWidth)
                                : cellWidth,
                        bounds.Height);

                DrawCell(
                    context,
                    cell,
                    labels[index],
                    values[index],
                    index > 0,
                    status != null &&
                    status.NextStageHasFeedRisk &&
                    (index == 4));
            }
        }

        private static void DrawCell(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            bool divider,
            bool warning)
        {
            Color valueColor =
                warning
                    ? Color.FromArgb(
                        255,
                        255,
                        196,
                        72)
                    : context.PhosphorColor;

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    valueColor))
            using (Pen dividerPen =
                new Pen(
                    Color.FromArgb(
                        70,
                        context.DimPhosphorColor)))
            using (StringFormat center =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                if (divider)
                {
                    context.Graphics.DrawLine(
                        dividerPen,
                        bounds.Left,
                        bounds.Top + 7,
                        bounds.Left,
                        bounds.Bottom - 7);
                }

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 3,
                        bounds.Width,
                        bounds.Height / 2),
                    center);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top +
                        bounds.Height / 2,
                        bounds.Width,
                        bounds.Height / 2 - 3),
                    center);
            }
        }

        private static void DrawCentered(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    color))
            using (StringFormat center =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    brush,
                    bounds,
                    center);
            }
        }
    }
}
