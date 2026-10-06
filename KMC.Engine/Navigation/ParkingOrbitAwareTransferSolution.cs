namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Lambert candidate paired with the local parking-orbit ejection required
    /// to realize its departure excess-velocity vector.
    /// </summary>
    public sealed class ParkingOrbitAwareTransferSolution
    {
        internal ParkingOrbitAwareTransferSolution(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution ejection)
        {
            Transfer = transfer;
            Ejection = ejection;
        }

        public TransferSearchSolution Transfer { get; private set; }

        public LambertParkingOrbitEjectionSolution Ejection { get; private set; }

        public double EjectionScoreMetersPerSecond
        {
            get { return Ejection.TotalDeltaVMetersPerSecond; }
        }

        public double ArrivalExcessSpeedMetersPerSecond
        {
            get { return Transfer.ArrivalExcessSpeedMetersPerSecond; }
        }
    }
}
