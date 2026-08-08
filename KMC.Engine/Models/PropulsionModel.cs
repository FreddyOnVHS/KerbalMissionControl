using KMC.Engine.Propulsion;

namespace KMC.Engine.Models
{
    public sealed class PropulsionModel
    {
        public PropulsionModel()
        {
            Topology =
                new PropulsionTopologyModel();
        }

        /// <summary>
        /// True when the Engine has a vessel topology snapshot from which
        /// propulsion structure can be analyzed.
        /// </summary>
        public bool IsAvailable { get; internal set; }

        /// <summary>
        /// True when at least one propulsion engine or solid booster exists
        /// in the Engine-owned topology model.
        /// </summary>
        public bool HasPropulsion { get; internal set; }

        /// <summary>
        /// Engine-owned structural propulsion model.
        /// </summary>
        public PropulsionTopologyModel Topology
        {
            get;
            internal set;
        }

        /*
         * Compatibility summary fields.
         *
         * Build 8.12 makes EngineCount topology-owned. Operability and thrust
         * remain intentionally unavailable until live engine telemetry is
         * migrated into KMC.Engine in the next propulsion milestone.
         */
        public int EngineCount { get; internal set; }

        public int OperableEngineCount { get; internal set; }

        public double AvailableThrust { get; internal set; }

        public bool LiveEngineStateAvailable { get; internal set; }
    }
}
