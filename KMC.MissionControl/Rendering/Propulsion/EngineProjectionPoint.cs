namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// One physical engine projected into a normalized engine-bell view.
    /// NormalizedX and NormalizedY are normally within -1..1.
    /// </summary>
    public sealed class EngineProjectionPoint
    {
        public uint PartId { get; set; }

        public string DisplayName { get; set; } =
            string.Empty;

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }

        public double NormalizedX { get; set; }

        public double NormalizedY { get; set; }

        public int DisplayNumber { get; set; }

        /// <summary>
        /// Stable mission-lifetime identifier such as SK01 or T03.
        /// </summary>
        public string Identifier { get; set; } =
            string.Empty;
    }
}
