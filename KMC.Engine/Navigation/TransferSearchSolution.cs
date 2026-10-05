using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Best sampled parent-frame Lambert transfer found by TransferSearch.
    ///
    /// Departure/arrival excess velocities are relative to the propagated body
    /// velocities at the boundary epochs. Their magnitudes are useful search
    /// metrics, but they are not yet parking-orbit ejection/capture burn costs.
    /// </summary>
    public sealed class TransferSearchSolution
    {
        internal TransferSearchSolution(
            string originName,
            string destinationName,
            string parentName,
            double departureUniversalTimeSeconds,
            double arrivalUniversalTimeSeconds,
            StateVector originDepartureState,
            StateVector destinationArrivalState,
            LambertSolution lambertSolution,
            Vector3d departureExcessVelocity,
            Vector3d arrivalExcessVelocity)
        {
            OriginName = originName ?? string.Empty;
            DestinationName = destinationName ?? string.Empty;
            ParentName = parentName ?? string.Empty;
            DepartureUniversalTimeSeconds = departureUniversalTimeSeconds;
            ArrivalUniversalTimeSeconds = arrivalUniversalTimeSeconds;
            TimeOfFlightSeconds =
                arrivalUniversalTimeSeconds - departureUniversalTimeSeconds;
            OriginDepartureState = originDepartureState;
            DestinationArrivalState = destinationArrivalState;
            LambertSolution = lambertSolution;
            DepartureExcessVelocity = departureExcessVelocity;
            ArrivalExcessVelocity = arrivalExcessVelocity;
            DepartureExcessSpeedMetersPerSecond =
                departureExcessVelocity.Magnitude;
            ArrivalExcessSpeedMetersPerSecond =
                arrivalExcessVelocity.Magnitude;
            CombinedExcessSpeedMetersPerSecond =
                DepartureExcessSpeedMetersPerSecond +
                ArrivalExcessSpeedMetersPerSecond;
        }

        public string OriginName { get; private set; }

        public string DestinationName { get; private set; }

        public string ParentName { get; private set; }

        public double DepartureUniversalTimeSeconds { get; private set; }

        public double ArrivalUniversalTimeSeconds { get; private set; }

        public double TimeOfFlightSeconds { get; private set; }

        public LambertTransferPath Path
        {
            get { return LambertSolution.Path; }
        }

        public StateVector OriginDepartureState { get; private set; }

        public StateVector DestinationArrivalState { get; private set; }

        public LambertSolution LambertSolution { get; private set; }

        /// <summary>
        /// Transfer departure velocity minus origin-body velocity.
        /// </summary>
        public Vector3d DepartureExcessVelocity { get; private set; }

        /// <summary>
        /// Transfer arrival velocity minus destination-body velocity.
        /// </summary>
        public Vector3d ArrivalExcessVelocity { get; private set; }

        public double DepartureExcessSpeedMetersPerSecond { get; private set; }

        public double ArrivalExcessSpeedMetersPerSecond { get; private set; }

        /// <summary>
        /// Search score = departure excess speed + arrival excess speed.
        /// This is a parent-frame ranking metric, not yet total spacecraft burn DV.
        /// </summary>
        public double CombinedExcessSpeedMetersPerSecond { get; private set; }
    }
}
