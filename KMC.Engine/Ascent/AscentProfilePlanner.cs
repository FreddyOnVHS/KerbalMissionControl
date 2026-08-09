using System;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned reference-ascent profile planner.
    ///
    /// Build 9.1 intentionally preserves the existing MissionControl
    /// AscentProfilePlanner equations so ownership can be migrated without
    /// changing established guidance behavior.
    /// </summary>
    internal sealed class AscentProfilePlanner
    {
        private const double DefaultFallbackTwr =
            1.5;

        private double _planningThrustToWeightRatio =
            double.NaN;

        private double _planningProfileScaleMeters =
            double.NaN;

        public bool LaunchPlanCaptured
        {
            get
            {
                return
                    IsFinite(
                        _planningProfileScaleMeters);
            }
        }

        public double PlanningThrustToWeightRatio
        {
            get
            {
                return
                    _planningThrustToWeightRatio;
            }
        }

        public bool CaptureLaunchPlan(
            double liveThrustToWeightRatio)
        {
            if (LaunchPlanCaptured)
            {
                return false;
            }

            if (!IsFinite(
                    liveThrustToWeightRatio) ||
                liveThrustToWeightRatio < 1.0)
            {
                return false;
            }

            _planningThrustToWeightRatio =
                Clamp(
                    liveThrustToWeightRatio,
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

        public AscentProfileModel CreateModel(
            double downrangeMeters,
            double actualAltitudeMeters,
            double actualPitchDegrees,
            double liveThrustToWeightRatio,
            double targetApoapsisMeters,
            int initialStage,
            bool captureOccurredThisUpdate)
        {
            AscentProfileScaleSource source;

            double profileScale =
                GetProfileScale(
                    liveThrustToWeightRatio,
                    out source);

            double targetAltitude =
                CalculateTargetAltitude(
                    downrangeMeters,
                    targetApoapsisMeters,
                    profileScale);

            double targetPitch =
                CalculateTargetPitch(
                    downrangeMeters,
                    targetApoapsisMeters,
                    profileScale);

            return
                new AscentProfileModel
                {
                    Available =
                        true,

                    TargetApoapsisMeters =
                        targetApoapsisMeters,

                    LaunchPlanCaptured =
                        LaunchPlanCaptured,

                    CaptureOccurredThisUpdate =
                        captureOccurredThisUpdate,

                    InitialStage =
                        initialStage,

                    PlanningThrustToWeightRatioKnown =
                        IsFinite(
                            _planningThrustToWeightRatio),

                    PlanningThrustToWeightRatio =
                        IsFinite(
                            _planningThrustToWeightRatio)
                                ? _planningThrustToWeightRatio
                                : 0.0,

                    LiveThrustToWeightRatio =
                        liveThrustToWeightRatio,

                    ScaleSource =
                        source,

                    ProfileScaleMeters =
                        profileScale,

                    DownrangeMeters =
                        Math.Max(
                            0.0,
                            downrangeMeters),

                    TargetAltitudeMeters =
                        targetAltitude,

                    ActualAltitudeMeters =
                        actualAltitudeMeters,

                    AltitudeErrorMeters =
                        actualAltitudeMeters -
                        targetAltitude,

                    TargetPitchDegrees =
                        targetPitch,

                    ActualPitchDegrees =
                        actualPitchDegrees,

                    PitchErrorDegrees =
                        actualPitchDegrees -
                        targetPitch
                };
        }

        private double GetProfileScale(
            double liveThrustToWeightRatio,
            out AscentProfileScaleSource source)
        {
            if (IsFinite(
                    _planningProfileScaleMeters))
            {
                source =
                    AscentProfileScaleSource.CapturedLaunchTwr;

                return
                    _planningProfileScaleMeters;
            }

            double fallbackTwr;

            if (IsFinite(
                    liveThrustToWeightRatio))
            {
                fallbackTwr =
                    liveThrustToWeightRatio;

                source =
                    AscentProfileScaleSource.LiveTwrFallback;
            }
            else
            {
                fallbackTwr =
                    DefaultFallbackTwr;

                source =
                    AscentProfileScaleSource.DefaultTwrFallback;
            }

            return
                CalculateProfileScaleFromTwr(
                    fallbackTwr);
        }

        private static double CalculateTargetAltitude(
            double downrangeMeters,
            double targetApoapsisMeters,
            double profileScaleMeters)
        {
            double normalized =
                Math.Max(
                    0.0,
                    downrangeMeters) /
                profileScaleMeters;

            double altitude =
                targetApoapsisMeters *
                (1.0 -
                 Math.Exp(
                     -normalized));

            return
                Math.Min(
                    targetApoapsisMeters,
                    Math.Max(
                        0.0,
                        altitude));
        }

        private static double CalculateTargetPitch(
            double downrangeMeters,
            double targetApoapsisMeters,
            double profileScaleMeters)
        {
            double slope =
                targetApoapsisMeters /
                profileScaleMeters *
                Math.Exp(
                    -Math.Max(
                        0.0,
                        downrangeMeters) /
                    profileScaleMeters);

            double flightPathAngle =
                Math.Atan(
                    slope) *
                180.0 /
                Math.PI;

            return
                Clamp(
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

            return
                Clamp(
                    scale,
                    26000.0,
                    72000.0);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return
                Math.Max(
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
