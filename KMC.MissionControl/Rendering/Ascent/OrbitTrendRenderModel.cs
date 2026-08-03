namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Prepared values for the Orbit Trend panel.
    /// </summary>
    public sealed class OrbitTrendRenderModel
    {
        public double Eccentricity { get; set; }

        public double TrueAnomalyDegrees { get; set; }

        public double ApoapsisMeters { get; set; }

        public double PeriapsisMeters { get; set; }

        public double InclinationDegrees { get; set; }
    }
}
