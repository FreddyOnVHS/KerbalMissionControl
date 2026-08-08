using System;
using System.Drawing;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class EngineClusterCard :
        MissionDisplayCard<PropulsionPageRenderModel>
    {
        public EngineClusterCard()
            : base("prop.engine-cluster", "CURRENT STAGE ENGINE GROUP")
        {
        }

        protected override void DrawContent(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionPageRenderModel model)
        {
            EngineClusterProjection cluster =
                model != null && model.Analysis != null
                    ? model.Analysis.EngineCluster
                    : null;

            PropulsionDisplayRenderer.DrawEngineCluster(
                context.Graphics,
                bounds,
                cluster,
                model != null ? model.Telemetry : null,
                context.SmallFont,
                context.SmallFont,
                context.PhosphorColor,
                context.DimPhosphorColor);

            DrawOperatorHeader(context, bounds, cluster, model);
        }

        private static void DrawOperatorHeader(
            MissionRenderContext context,
            Rectangle bounds,
            EngineClusterProjection cluster,
            PropulsionPageRenderModel model)
        {
            Rectangle header = new Rectangle(
                bounds.Left + 2,
                bounds.Top,
                Math.Max(1, bounds.Width - 4),
                38);

            using (SolidBrush mask = new SolidBrush(Color.FromArgb(255, 2, 14, 20)))
            {
                context.Graphics.FillRectangle(mask, header);
            }

            int groupCount = cluster != null ? cluster.Engines.Count : 0;
            int installed = model != null &&
                            model.Engineering != null &&
                            model.Engineering.Status != null
                                ? model.Engineering.Status.InstalledEngineCount
                                : groupCount;

            int stage = model != null &&
                        model.Engineering != null &&
                        model.Engineering.Status != null
                            ? model.Engineering.Status.LiveCurrentStage
                            : model != null && model.Telemetry != null
                                ? model.Telemetry.CurrentStage
                                : 0;

            int producing = model != null &&
                            model.Engineering != null &&
                            model.Engineering.Status != null
                                ? model.Engineering.Status.ProducingEngineCount
                                : 0;

            int flameout = model != null &&
                           model.Engineering != null &&
                           model.Engineering.Status != null
                                ? model.Engineering.Status.FlameoutEngineCount
                                : 0;

            string line1 =
                "STAGE " + stage.ToString("00") +
                "  •  GROUP " + groupCount.ToString("00") +
                " / INSTALLED " + installed.ToString("00");

            string line2 =
                flameout > 0
                    ? producing.ToString("00") + " PRODUCING  •  " +
                      flameout.ToString("00") + " FLAMEOUT"
                    : producing.ToString("00") +
                      " PRODUCING  •  ENGINE BELL VIEW";

            Color stateColor =
                flameout > 0
                    ? Color.FromArgb(255, 255, 196, 72)
                    : context.DimPhosphorColor;

            using (SolidBrush a = new SolidBrush(context.PhosphorColor))
            using (SolidBrush b = new SolidBrush(stateColor))
            using (StringFormat fmt = new StringFormat {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                context.Graphics.DrawString(
                    line1, context.SmallFont, a,
                    new Rectangle(header.Left, header.Top, header.Width, 19), fmt);

                context.Graphics.DrawString(
                    line2, context.SmallFont, b,
                    new Rectangle(header.Left, header.Top + 19, header.Width, 19), fmt);
            }
        }
    }
}
