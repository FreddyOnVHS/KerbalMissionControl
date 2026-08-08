using KMC.Engine.Propulsion;

namespace KMC.Engine.Models
{
    public sealed class PropulsionModel
    {
        public PropulsionModel()
        {
            Topology =
                new PropulsionTopologyModel();

            Live =
                new PropulsionLiveStateModel();
        }

        public bool IsAvailable { get; internal set; }

        public bool HasPropulsion { get; internal set; }

        public PropulsionTopologyModel Topology
        {
            get;
            internal set;
        }

        public PropulsionLiveStateModel Live
        {
            get;
            internal set;
        }

        public int EngineCount { get; internal set; }

        /// <summary>
        /// Fresh matched engines currently in Armed, Ignited, or Producing
        /// state. Shutdown, Flameout, Unknown, stale, and unmatched engines
        /// are not assumed operable.
        /// </summary>
        public int OperableEngineCount { get; internal set; }

        /// <summary>
        /// Conservative immediately-ready maximum thrust. It is known only
        /// when fresh per-engine telemetry fully covers the topology.
        /// </summary>
        public double AvailableThrust { get; internal set; }

        public bool AvailableThrustKnown { get; internal set; }

        public bool LiveEngineStateAvailable { get; internal set; }
    }
}
