namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// A schematic-level engine cluster rather than an individual KSP part.
    /// </summary>
    public sealed class PropulsionEngineGroup
    {
        public PropulsionEngineGroup()
        {
            DisplayName = string.Empty;
        }

        public string DisplayName { get; set; }

        public int Count { get; set; }

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }
    }
}
