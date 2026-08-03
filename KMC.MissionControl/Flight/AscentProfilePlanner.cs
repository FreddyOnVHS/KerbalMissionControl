using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// Owns the launch-captured ascent-profile scale and converts downrange
    /// into target altitude and pitch.
    ///
    /// This preserves the existing exponential profile while making the
    /// target apoapsis an explicit input.
    /// </summary>
    public sealed class AscentProfilePlanner
    {
        private double _planningThrustToWeightRatio =
            double.NaN;

        private double _planningProfileScaleMeters =
            double.NaN;

        public double PlanningThrustToWeightRatio
        {
            get
            {
                return _planningThrustToWeightRatio;
            }
        }

        public bool CaptureLaunchPlan(
            MissionTelemetry telemetry)
        {
            if (telemetry == null ||
                IsFinite(
                    _planningProfileScaleMeters))
            {
                return false;
            }

            double twr =
                telemetry.ThrustToWeightRatio;

            if (!IsFinite(twr) ||
                twr < 1.0)
            {
                return false;
            }

            _planningThrustToWeightRatio =
                Clamp(
                    twr,
                    0.8,
                    3.0);

            _planningProfileScaleMeters =
                CalculateProfileScaleFromTwr(
                    _planningThrustToWeightRatio);

            return true;
        }

        public void Reset()
        {
            _planningThrustToWeightRatio =
                double.NaN;

            _planningProfileScaleMeters =
                double.NaN;
        }

        public double GetProfileScale(
            MissionTelemetry telemetry)
        {
            if (IsFinite(
                    _planningProfileScaleMeters))
            {
                return _planningProfileScaleMeters;
            }

            double fallbackTwr =
                telemetry != null &&
                IsFinite(
                    telemetry.ThrustToWeightRatio)
                    ? telemetry
                        .ThrustToWeightRatio
                    : 1.5;

            return CalculateProfileScaleFromTwr(
                fallbackTwr);
        }

        public double CalculateTargetAltitude(
            double downrangeMeters,
            MissionTelemetry telemetry,
            double targetApoapsisMeters)
        {
            double profileScale =
                GetProfileScale(
                    telemetry);

            double normalized =
                Math.Max(
                    0.0,
                    downrangeMeters) /
                profileScale;

            double altitude =
                targetApoapsisMeters *
                (1.0 -
                 Math.Exp(
                     -normalized));

            return Math.Min(
                targetApoapsisMeters,
                Math.Max(
                    0.0,
                    altitude));
        }

        public double CalculateTargetPitch(
            double downrangeMeters,
            MissionTelemetry telemetry,
            double targetApoapsisMeters)
        {
            double scale =
                GetProfileScale(
                    telemetry);

            double slope =
                targetApoapsisMeters /
                scale *
                Math.Exp(
                    -Math.Max(
                        0.0,
                        downrangeMeters) /
                    scale);

            double flightPathAngle =
                Math.Atan(
                    slope) *
                180.0 /
                Math.PI;

            return Clamp(
                flightPathAngle,
                0.0,
                90.0);
        }

        private static double CalculateProfileScaleFromTwr(
            double twr)
        {
            twr =
                Clamp(
                    twr,
                    0.8,
                    3.0);

            double scale =
                52000.0 /
                Math.Sqrt(
                    twr);

            return Clamp(
                scale,
                26000.0,
                72000.0);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value));
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
