using System;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Human-in-the-loop, low-authority prograde recovery controller.
    ///
    /// Throttle commands are deliberately gentle and stable enough for a
    /// remote pilot to read and follow. Cutoff always bypasses command delay.
    /// </summary>
    public sealed class PeriapsisRecoveryController
    {
        private const double MinimumSafePeriapsisMeters =
            70000.0;

        private const double PredictedCutoffPeriapsisMeters =
            71000.0;

        private const double MinimumCommandHoldSeconds =
            1.50;

        private const double ThresholdHysteresisMeters =
            1500.0;

        private double _commandedThrottlePercent =
            double.NaN;

        private double _commandStartMissionTime =
            double.NaN;

        public void Reset()
        {
            _commandedThrottlePercent =
                double.NaN;

            _commandStartMissionTime =
                double.NaN;
        }

        public PeriapsisRecoverySolution Calculate(
            PeriapsisRecoveryInput input)
        {
            PeriapsisRecoverySolution result =
                new PeriapsisRecoverySolution
                {
                    Reason =
                        "RECOVERY WAITING"
                };

            if (input == null)
            {
                return result;
            }

            double actualPeriapsis =
                IsFinite(
                    input.ActualPeriapsisMeters)
                    ? input.ActualPeriapsisMeters
                    : double.NegativeInfinity;

            double predictedPeriapsis =
                input.GuidanceAvailable &&
                IsFinite(
                    input.PredictedPeriapsisMeters)
                    ? input.PredictedPeriapsisMeters
                    : actualPeriapsis;

            double error =
                Math.Max(
                    0.0,
                    MinimumSafePeriapsisMeters -
                    actualPeriapsis);

            bool actualSafe =
                actualPeriapsis >=
                    MinimumSafePeriapsisMeters;

            bool predictedSafe =
                predictedPeriapsis >=
                    PredictedCutoffPeriapsisMeters;

            bool producingThrust =
                input.ProducingThrust ||
                input.Throttle > 0.01;

            result.PeriapsisErrorMeters =
                error;

            result.ActualPeriapsisSafe =
                actualSafe;

            result.PredictedPeriapsisSafe =
                predictedSafe;

            result.ProducingThrust =
                producingThrust;

            /*
             * Safety cutoff is never delayed by command hold or hysteresis.
             */
            if (actualSafe ||
                predictedSafe)
            {
                SetCommand(
                    0.0,
                    input.MissionTimeSeconds);

                result.ThrottlePercent =
                    0.0;

                result.DesiredThrottlePercent =
                    0.0;

                result.CommandAgeSeconds =
                    GetCommandAge(
                        input.MissionTimeSeconds);

                result.CutoffRequired =
                    producingThrust;

                result.Reason =
                    actualSafe
                        ? "LIVE PERIAPSIS SAFE"
                        : "PREDICTED PERIAPSIS SAFE";

                return result;
            }

            double desiredThrottle =
                CalculateDesiredThrottlePercent(
                    error);

            result.DesiredThrottlePercent =
                desiredThrottle;

            bool held =
                ShouldHoldCurrentCommand(
                    desiredThrottle,
                    error,
                    input.MissionTimeSeconds);

            if (!held)
            {
                SetCommand(
                    desiredThrottle,
                    input.MissionTimeSeconds);
            }

            result.ThrottlePercent =
                IsFinite(
                    _commandedThrottlePercent)
                    ? _commandedThrottlePercent
                    : desiredThrottle;

            result.CommandAgeSeconds =
                GetCommandAge(
                    input.MissionTimeSeconds);

            result.CommandHeldByHysteresis =
                held;

            result.CutoffRequired =
                false;

            result.Reason =
                held
                    ? "HOLD THROTTLE COMMAND"
                    : "RAISE PERIAPSIS";

            return result;
        }

        private bool ShouldHoldCurrentCommand(
            double desiredThrottle,
            double errorMeters,
            double missionTimeSeconds)
        {
            if (!IsFinite(
                    _commandedThrottlePercent) ||
                !IsFinite(
                    _commandStartMissionTime))
            {
                return false;
            }

            if (Math.Abs(
                    desiredThrottle -
                    _commandedThrottlePercent) <
                0.1)
            {
                return true;
            }

            double age =
                GetCommandAge(
                    missionTimeSeconds);

            if (age <
                MinimumCommandHoldSeconds)
            {
                return true;
            }

            /*
             * Down-step hysteresis prevents a command change exactly at a
             * noisy threshold. Because periapsis should rise during recovery,
             * commands normally move only toward lower throttle.
             */
            if (desiredThrottle <
                _commandedThrottlePercent)
            {
                double boundary =
                    GetBoundaryForThrottle(
                        _commandedThrottlePercent);

                if (errorMeters >
                    boundary -
                    ThresholdHysteresisMeters)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetCommand(
            double throttlePercent,
            double missionTimeSeconds)
        {
            if (IsFinite(
                    _commandedThrottlePercent) &&
                Math.Abs(
                    throttlePercent -
                    _commandedThrottlePercent) <
                0.1)
            {
                return;
            }

            _commandedThrottlePercent =
                throttlePercent;

            _commandStartMissionTime =
                missionTimeSeconds;
        }

        private double GetCommandAge(
            double missionTimeSeconds)
        {
            if (!IsFinite(
                    missionTimeSeconds) ||
                !IsFinite(
                    _commandStartMissionTime))
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                missionTimeSeconds -
                _commandStartMissionTime);
        }

        private static double CalculateDesiredThrottlePercent(
            double errorMeters)
        {
            if (errorMeters >
                40000.0)
            {
                return 30.0;
            }

            if (errorMeters >
                20000.0)
            {
                return 18.0;
            }

            if (errorMeters >
                8000.0)
            {
                return 10.0;
            }

            if (errorMeters >
                2000.0)
            {
                return 5.0;
            }

            return 3.0;
        }

        private static double GetBoundaryForThrottle(
            double throttlePercent)
        {
            if (throttlePercent >=
                29.0)
            {
                return 40000.0;
            }

            if (throttlePercent >=
                17.0)
            {
                return 20000.0;
            }

            if (throttlePercent >=
                9.0)
            {
                return 8000.0;
            }

            if (throttlePercent >=
                4.0)
            {
                return 2000.0;
            }

            return 0.0;
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
