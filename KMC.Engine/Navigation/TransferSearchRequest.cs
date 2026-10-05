using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Defines a bounded zero-revolution Lambert search in one shared parent-body
    /// inertial frame. The caller supplies the parent gravitational parameter
    /// explicitly; KMC does not hardcode or infer stock-body constants here.
    /// </summary>
    public sealed class TransferSearchRequest
    {
        public TransferSearchRequest()
        {
            SearchShortWay = true;
            SearchLongWay = true;
            DepartureSamples = 1;
            TimeOfFlightSamples = 1;
        }

        public CelestialBodyState OriginBody { get; set; }

        public CelestialBodyState DestinationBody { get; set; }

        public double ParentGravParameter { get; set; }

        public double EarliestDepartureUniversalTimeSeconds { get; set; }

        public double LatestDepartureUniversalTimeSeconds { get; set; }

        public double MinimumTimeOfFlightSeconds { get; set; }

        public double MaximumTimeOfFlightSeconds { get; set; }

        public int DepartureSamples { get; set; }

        public int TimeOfFlightSamples { get; set; }

        public bool SearchShortWay { get; set; }

        public bool SearchLongWay { get; set; }
    }
}
