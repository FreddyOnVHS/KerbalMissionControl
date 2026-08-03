namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// One altitude-versus-downrange point used by the ascent graph.
    /// </summary>
    public sealed class AscentGraphPoint
    {
        public double DownrangeMeters { get; set; }

        public double AltitudeMeters { get; set; }
    }

    /// <summary>
    /// Prepared trajectory data for the main Ascent Guidance graph.
    ///
    /// Profile generation and flight-history ownership remain in AscentPage.
    /// </summary>
    public sealed class AscentGraphRenderModel
    {
        public double MaximumDownrangeMeters { get; set; }

        public double MaximumAltitudeMeters { get; set; }

        public AscentGraphPoint[] TargetPoints { get; set; }

        public AscentGraphPoint[] ActualPoints { get; set; }
    }
}
