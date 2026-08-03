using System;

namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// Mutable mission target shared by ascent planning, prediction,
    /// guidance, rendering, and diagnostics.
    /// </summary>
    public sealed class MissionTarget
    {
        private double _targetApoapsisMeters;

        public MissionTarget(
            double targetApoapsisMeters)
        {
            TargetApoapsisMeters =
                targetApoapsisMeters;
        }

        public double TargetApoapsisMeters
        {
            get
            {
                return _targetApoapsisMeters;
            }

            set
            {
                if (double.IsNaN(value) ||
                    double.IsInfinity(value) ||
                    value <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Target apoapsis must be a positive finite value.");
                }

                _targetApoapsisMeters =
                    value;
            }
        }
    }
}
