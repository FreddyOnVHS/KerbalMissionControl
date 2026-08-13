using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Power;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Build 14.11.2 EECOM POWER redesign foundation.
    ///
    /// POWER 1/2 is now a performance-first top-to-bottom one-line schematic.
    /// The legacy POWER renderer is intentionally not called. Page 2/2 is
    /// visually reserved for the later detail/analysis page.
    /// </summary>
    public sealed class PowerPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        public string Name
        {
            get { return "POWER"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                /*
                 * Keep a bounded high-resolution logical canvas. 2400 x 900 has
                 * nearly the same pixel count as 1920 x 1080, but matches
                 * KMC's wide CRT much better so POWER fills the display
                 * without returning to the oversized responsive bitmap.
                 */
                return new Size(
                    3000,
                    1100);
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return MissionPageContentProfile.DenseEngineering;
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            AnalysisPipelineResult engineering;

            EngineeringSnapshotStore.TryGetLatest(
                out engineering);

            PowerSchematicRenderer.Draw(
                context,
                telemetry,
                engineering);
        }
    }
}
