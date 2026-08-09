namespace KMC.Engine.Ascent
{
    public enum AscentFlightPhase
    {
        Unknown = 0,
        Prelaunch,
        Ascent,
        TargetApproach,
        MecoCountdown,
        Meco,
        CoastHandoff
    }

    /// <summary>
    /// Engine-owned ASCENT phase state.
    ///
    /// CoastHandoff is the terminal ASCENT v1 state. It explicitly hands the
    /// mission to the future ORBIT system instead of absorbing circularization
    /// and periapsis-recovery responsibilities.
    /// </summary>
    public sealed class AscentPhaseModel
    {
        public AscentPhaseModel()
        {
            Cutoff =
                new AscentCutoffModel();
        }

        public bool Available { get; internal set; }

        public AscentFlightPhase Phase { get; internal set; }

        public string PhaseName { get; internal set; } =
            "UNKNOWN";

        public bool MissionStarted { get; internal set; }

        public bool MecoLatched { get; internal set; }

        public int MecoCountdownSeconds { get; internal set; }

        public bool FlashAlert { get; internal set; }

        public bool OrbitHandoffRequired { get; internal set; }

        public AscentCutoffModel Cutoff
        {
            get;
            internal set;
        }
    }
}
