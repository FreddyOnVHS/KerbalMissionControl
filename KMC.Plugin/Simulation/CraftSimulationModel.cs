using System.Collections.Generic;

namespace KMC.Plugin.Simulation
{
    internal sealed class CraftSimulationModel
    {
        public CraftSimulationModel()
        {
            Parts =
                new List<SimulatedPart>();

            Engines =
                new List<SimulatedEngine>();
        }

        public string VesselName { get; set; }

        public int RootPartCount { get; set; }

        public int CrossFeedLinkCount { get; set; }

        public IList<SimulatedPart> Parts
        {
            get;
            private set;
        }

        public IList<SimulatedEngine> Engines
        {
            get;
            private set;
        }
    }
}
