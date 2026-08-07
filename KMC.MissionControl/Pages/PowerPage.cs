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
    /// Build 8.10 replaces the former topology/inspector page with a
    /// read-only schematic presentation of the Engine-owned electrical model.
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
                return new Size(
                    1600,
                    900);
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile.Standard;
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
