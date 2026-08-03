namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// Describes state transitions produced by one history update.
    /// </summary>
    public sealed class AscentFlightHistoryUpdate
    {
        public bool MissionReset { get; set; }

        public bool SampleAdded { get; set; }
    }
}
