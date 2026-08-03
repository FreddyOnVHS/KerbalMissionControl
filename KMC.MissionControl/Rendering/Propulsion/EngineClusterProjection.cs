using System.Collections.Generic;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class EngineClusterProjection
    {
        public EngineClusterProjection()
        {
            DisplayName = string.Empty;
            Engines = new List<EngineProjectionPoint>();
        }

        public string DisplayName { get; set; }

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }

        public bool UsedFallbackAxis { get; set; }

        public List<EngineProjectionPoint> Engines { get; private set; }
    }
}
