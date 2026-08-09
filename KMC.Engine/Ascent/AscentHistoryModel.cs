using System.Collections.Generic;

namespace KMC.Engine.Ascent
{
    public sealed class AscentHistorySample
    {
        public double MissionTimeSeconds { get; internal set; }

        public int StageNumber { get; internal set; }

        public double DownrangeMeters { get; internal set; }

        public double AltitudeMeters { get; internal set; }

        public double ApoapsisMeters { get; internal set; }

        public double PitchDegrees { get; internal set; }

        public double DynamicPressureKpa { get; internal set; }

        public double VerticalSpeedMetersPerSecond { get; internal set; }

        public double HorizontalSpeedMetersPerSecond { get; internal set; }

        public double OrbitalSpeedMetersPerSecond { get; internal set; }

        public double VesselMassTonnes { get; internal set; }

        public double CurrentThrustKilonewtons { get; internal set; }

        public double AverageSpecificImpulseSeconds { get; internal set; }

        public double StageLiquidFuelAmount { get; internal set; }

        public double StageOxidizerAmount { get; internal set; }
    }

    public sealed class AscentHistoryModel
    {
        public AscentHistoryModel()
        {
            Samples =
                new List<AscentHistorySample>();
        }

        public bool Available { get; internal set; }

        public string TrackedVesselName { get; internal set; } =
            string.Empty;

        public double DownrangeMeters { get; internal set; }

        public int SampleCount
        {
            get { return Samples.Count; }
        }

        public bool MissionResetDetected { get; internal set; }

        public long MissionResetCount { get; internal set; }

        public List<AscentHistorySample> Samples
        {
            get;
            private set;
        }
    }
}
