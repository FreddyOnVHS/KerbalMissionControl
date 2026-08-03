namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Prepared values for the full-width ascent telemetry footer.
    /// </summary>
    public sealed class FooterRenderModel
    {
        public double MissionTimeSeconds { get; set; }

        public int CurrentStage { get; set; }

        public double AltitudeMeters { get; set; }

        public double DownrangeMeters { get; set; }

        public double VerticalSpeedMetersPerSecond { get; set; }

        public double HorizontalSpeedMetersPerSecond { get; set; }

        public double ThrustToWeightRatio { get; set; }

        public double GForce { get; set; }

        public double ApoapsisMeters { get; set; }

        public double FuelPercent { get; set; }

        public string Status { get; set; }
    }
}
