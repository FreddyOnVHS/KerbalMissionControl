namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// One physical engine projected into a normalized top-down cluster view.
    /// NormalizedX and NormalizedY are normally within -1..1.
    /// </summary>
    public sealed class EngineProjectionPoint
    {
        public uint PartId { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }

        public double NormalizedX { get; set; }

        public double NormalizedY { get; set; }

        public int DisplayNumber { get; set; }
    }
}
