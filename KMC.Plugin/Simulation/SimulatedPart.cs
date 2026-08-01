using System.Collections.Generic;

namespace KMC.Plugin.Simulation
{
    internal sealed class SimulatedPart
    {
        public SimulatedPart()
        {
            LinkedPartIds =
                new List<uint>();

            CrossFeedPartIds =
                new List<uint>();

            Resources =
                new List<SimulatedResource>();

            Engines =
                new List<SimulatedEngine>();
        }

        public uint PersistentId { get; set; }

        public string Name { get; set; }

        public int InverseStage { get; set; }

        public int DecoupledInStage { get; set; }

        public bool IsRoot { get; set; }

        public bool AllowsCrossFeed { get; set; }

        public double DryMassTonnes { get; set; }

        public IList<uint> LinkedPartIds
        {
            get;
            private set;
        }

        public IList<uint> CrossFeedPartIds
        {
            get;
            private set;
        }

        public IList<SimulatedResource> Resources
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
