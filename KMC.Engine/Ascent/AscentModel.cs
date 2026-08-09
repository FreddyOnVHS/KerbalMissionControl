using System;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Build 9.0 Engine-owned ascent foundation.
    ///
    /// This model intentionally contains current flight state and trajectory
    /// history only. Reference profile, prediction, flight phase, and guidance
    /// move into Engine in later milestones.
    /// </summary>
    public sealed class AscentModel
    {
        public AscentModel()
        {
            Current =
                new AscentTelemetryState();

            History =
                new AscentHistoryModel();
        }

        public bool Available { get; internal set; }

        public DateTime ReceivedUtc { get; internal set; }

        public AscentTelemetryState Current
        {
            get;
            internal set;
        }

        public AscentHistoryModel History
        {
            get;
            internal set;
        }

        /// <summary>
        /// Surface-relative two-dimensional flight-path angle derived from
        /// vertical and horizontal velocity components.
        ///
        /// This is useful groundwork for the future ASCENT FDAI/navball.
        /// </summary>
        public bool FlightPathAngleAvailable { get; internal set; }

        public double FlightPathAngleDegrees { get; internal set; }
    }
}
