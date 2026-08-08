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

            Feed =
                new PropulsionFeedModel();

            Status =
                new PropulsionStatusModel();
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

        public PropulsionFeedModel Feed
        {
            get;
            internal set;
        }

        public PropulsionStatusModel Status
        {
            get;
            internal set;
        }

        public int EngineCount { get; internal set; }

        public int OperableEngineCount { get; internal set; }

        public double AvailableThrust { get; internal set; }

        public bool AvailableThrustKnown { get; internal set; }

        public bool LiveEngineStateAvailable { get; internal set; }
    }
}
