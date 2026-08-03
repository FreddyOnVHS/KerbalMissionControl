using KMC.MissionControl.Guidance;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Immutable input prepared by AscentPage for the Flight Director.
    /// The renderer does not calculate guidance or read page-private state.
    /// </summary>
    public sealed class FlightDirectorRenderModel
    {
        public double TargetApoapsisMeters { get; set; }

        public double DownrangeMeters { get; set; }

        public double TargetAltitudeMeters { get; set; }

        public double ActualAltitudeMeters { get; set; }

        public double ActualPitchDegrees { get; set; }

        public double DynamicPressureKpa { get; set; }

        public double MissionTimeSeconds { get; set; }

        public MissionPlannerResult Plan { get; set; }
    }
}
