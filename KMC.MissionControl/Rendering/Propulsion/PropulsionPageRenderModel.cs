using KMC.Engine.Models;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class PropulsionPageRenderModel
    {
        public PropulsionRenderGraph Graph { get; set; }

        public PropulsionAnalysis Analysis { get; set; }

        public MissionTelemetry Telemetry { get; set; }

        /// <summary>
        /// Engine-owned propulsion interpretation from the latest engineering
        /// snapshot. Geometry still comes from Analysis during Build 8.16.
        /// </summary>
        public PropulsionModel Engineering { get; set; }

        public bool EngineeringAvailable
        {
            get
            {
                return
                    Engineering != null &&
                    Engineering.IsAvailable;
            }
        }
    }
}
