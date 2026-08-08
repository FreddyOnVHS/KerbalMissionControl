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
    /// Electrical engineering display.
    ///
    /// Build 8.10.2 uses the MissionDisplay responsive virtual-canvas path
    /// instead of forcing a fixed 1600 x 900 logical canvas. This lets POWER
    /// consume the available CRT viewport at large/full-screen window sizes.
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
                 * Size.Empty intentionally selects MissionDisplay's existing
                 * responsive canvas behavior. No shared renderer change is
                 * required, so other mission pages keep their current scaling.
                 */
                return Size.Empty;
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile.DenseEngineering;
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            AnalysisPipelineResult engineering;

            EngineeringSnapshotStore.TryGetLatest(
                out engineering);

            PowerPageRenderer.Draw(
                context,
                telemetry,
                engineering);
        }
    }
}
