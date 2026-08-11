using System;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Engine-owned maneuver request.
    ///
    /// Build 13.0 introduces the request contract but keeps the live
    /// EngineeringEngine workflow on the default circularization request.
    /// MissionControl request controls are intentionally deferred to 13.1.
    /// </summary>
    public sealed class ManeuverRequestModel
    {
        public ManeuverRequestModel()
        {
            Type =
                ManeuverRequestType.CircularizeAtApoapsis;

            TargetAltitudeMeters =
                double.NaN;

            RequestedUtc =
                DateTime.MinValue;
        }

        public ManeuverRequestType Type { get; set; }

        /// <summary>
        /// Requested opposite-apsis altitude above the central body's surface.
        /// Not used by CircularizeAtApoapsis.
        /// </summary>
        public double TargetAltitudeMeters { get; set; }

        public DateTime RequestedUtc { get; set; }

        public static ManeuverRequestModel CreateDefault()
        {
            return
                new ManeuverRequestModel
                {
                    Type =
                        ManeuverRequestType.CircularizeAtApoapsis,

                    TargetAltitudeMeters =
                        double.NaN,

                    RequestedUtc =
                        DateTime.UtcNow
                };
        }

        public static ManeuverRequestModel Clone(
            ManeuverRequestModel source)
        {
            if (source == null)
            {
                return CreateDefault();
            }

            return
                new ManeuverRequestModel
                {
                    Type =
                        source.Type,

                    TargetAltitudeMeters =
                        source.TargetAltitudeMeters,

                    RequestedUtc =
                        source.RequestedUtc
                };
        }
    }


    /// <summary>
    /// Process-local request bridge used by MissionControl to select the
    /// Engine-owned maneuver objective. The planner always consumes a clone.
    /// </summary>
    public static class ManeuverRequestStore
    {
        private static readonly object SyncRoot =
            new object();

        private static ManeuverRequestModel _latest =
            ManeuverRequestModel.CreateDefault();

        public static void Set(
            ManeuverRequestModel request)
        {
            lock (SyncRoot)
            {
                _latest =
                    ManeuverRequestModel.Clone(
                        request);
            }
        }

        public static ManeuverRequestModel Get()
        {
            lock (SyncRoot)
            {
                return
                    ManeuverRequestModel.Clone(
                        _latest);
            }
        }

        public static void Reset()
        {
            lock (SyncRoot)
            {
                _latest =
                    ManeuverRequestModel.CreateDefault();
            }
        }
    }

    public enum ManeuverRequestType
    {
        CircularizeAtApoapsis = 0,
        SetPeriapsisAtApoapsis = 1,
        SetApoapsisAtPeriapsis = 2
    }
}
