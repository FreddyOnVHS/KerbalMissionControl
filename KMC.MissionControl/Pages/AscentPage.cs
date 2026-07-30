using System;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public sealed class AscentPage : IMissionPage
    {
        public string Name
        {
            get { return "ASCENT DATA"; }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (telemetry == null)
            {
                return;
            }

            MissionPageLayout layout =
                new MissionPageLayout(context);

            layout.DrawHeader(
                Name,
                "CH 02");

            layout.Row(
                "VESSEL",
                FormatText(telemetry.VesselName),
                "STAGE",
                telemetry.CurrentStage.ToString("00"));

            layout.Row(
                "BODY",
                FormatText(telemetry.BodyName),
                "G FORCE",
                FormatGForce(telemetry.GForce));

            layout.Row(
                "MET",
                FormatMissionTime(
                    telemetry.MissionTime),
                "THROTTLE",
                FormatPercent(
                    telemetry.Throttle));

            layout.Space();

            layout.Row(
                "ALTITUDE",
                FormatDistance(
                    telemetry.Altitude),
                "RADAR ALT",
                FormatDistance(
                    telemetry.RadarAltitude));

            layout.Row(
                "APOAPSIS",
                FormatDistance(
                    telemetry.Apoapsis),
                "TIME TO AP",
                FormatDuration(
                    telemetry.TimeToApoapsis));

            layout.Row(
                "PITCH",
                FormatSignedAngle(
                    telemetry.Pitch),
                "HEADING",
                FormatHeading(
                    telemetry.Heading));

            layout.Row(
                "ROLL",
                FormatSignedAngle(
                    telemetry.Roll),
                "MACH",
                FormatMach(
                    telemetry.Mach));

            layout.Space();

            layout.Row(
                "VERT VEL",
                FormatSignedSpeed(
                    telemetry.VerticalSpeed),
                "HORIZ VEL",
                FormatSpeed(
                    telemetry.HorizontalSpeed));

            layout.Row(
                "SURF VEL",
                FormatSpeed(
                    telemetry.SurfaceSpeed),
                "DYN Q",
                FormatPressure(
                    telemetry.DynamicPressureKpa));

            layout.Row(
                "ORB VEL",
                FormatSpeed(
                    telemetry.OrbitalSpeed),
                "ATM PRES",
                FormatPressure(
                    telemetry.StaticPressureKpa));

            layout.Row(
                "THRUST",
                FormatThrust(
                    telemetry.CurrentThrust),
                "MASS",
                FormatMass(
                    telemetry.VesselMass));

            layout.Row(
                "MAX THRUST",
                FormatThrust(
                    telemetry.MaximumThrust),
                "TWR",
                FormatRatio(
                    telemetry.ThrustToWeightRatio));
        }

        private static string FormatText(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "---";
            }

            return value
                .Trim()
                .ToUpperInvariant();
        }

        private static string FormatDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            double absoluteValue =
                Math.Abs(meters);

            if (absoluteValue >= 1000000.0)
            {
                return
                    (meters / 1000000.0)
                    .ToString("0.00") +
                    " MM";
            }

            if (absoluteValue >= 1000.0)
            {
                return
                    (meters / 1000.0)
                    .ToString("0.0") +
                    " KM";
            }

            return
                meters.ToString("0") +
                " M";
        }

        private static string FormatSpeed(
            double metersPerSecond)
        {
            if (!IsFinite(metersPerSecond))
            {
                return "---";
            }

            return
                metersPerSecond.ToString("0.0") +
                " M/S";
        }

        private static string FormatSignedSpeed(
            double metersPerSecond)
        {
            if (!IsFinite(metersPerSecond))
            {
                return "---";
            }

            return
                metersPerSecond.ToString(
                    "+0.0;-0.0;0.0") +
                " M/S";
        }

        private static string FormatPressure(
            double kilopascals)
        {
            if (!IsFinite(kilopascals))
            {
                return "---";
            }

            return
                Math.Max(0.0, kilopascals)
                .ToString("0.00") +
                " KPA";
        }

        private static string FormatMach(
            double mach)
        {
            if (!IsFinite(mach))
            {
                return "---";
            }

            return
                Math.Max(0.0, mach)
                .ToString("0.00");
        }

        private static string FormatMass(
            double tonnes)
        {
            if (!IsFinite(tonnes))
            {
                return "---";
            }

            return
                Math.Max(0.0, tonnes)
                .ToString("0.0") +
                " T";
        }

        private static string FormatThrust(
            double kilonewtons)
        {
            if (!IsFinite(kilonewtons))
            {
                return "---";
            }

            return
                Math.Max(0.0, kilonewtons)
                .ToString("0.0") +
                " KN";
        }

        private static string FormatRatio(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                Math.Max(0.0, value)
                .ToString("0.00");
        }

        private static string FormatGForce(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                Math.Max(0.0, value)
                .ToString("0.00") +
                " G";
        }

        private static string FormatPercent(
            double fraction)
        {
            if (!IsFinite(fraction))
            {
                return "---";
            }

            double percent =
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        fraction * 100.0));

            return
                percent.ToString("0") +
                "%";
        }

        private static string FormatSignedAngle(
            double degrees)
        {
            if (!IsFinite(degrees))
            {
                return "---";
            }

            return
                degrees.ToString(
                    "+0.0;-0.0;0.0") +
                "°";
        }

        private static string FormatHeading(
            double degrees)
        {
            if (!IsFinite(degrees))
            {
                return "---";
            }

            double normalized =
                degrees % 360.0;

            if (normalized < 0.0)
            {
                normalized += 360.0;
            }

            return
                normalized.ToString("000.0") +
                "°";
        }

        private static string FormatMissionTime(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds < 0.0)
            {
                totalSeconds = 0.0;
            }

            int hours =
                (int)(totalSeconds / 3600.0);

            int minutes =
                (int)(totalSeconds % 3600.0) /
                60;

            int seconds =
                (int)(totalSeconds % 60.0);

            return string.Format(
                "{0:000}:{1:00}:{2:00}",
                hours,
                minutes,
                seconds);
        }

        private static string FormatDuration(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds < 0.0)
            {
                totalSeconds = 0.0;
            }

            int hours =
                (int)(totalSeconds / 3600.0);

            int minutes =
                (int)(totalSeconds % 3600.0) /
                60;

            int seconds =
                (int)(totalSeconds % 60.0);

            if (hours > 0)
            {
                return string.Format(
                    "{0:00}:{1:00}:{2:00}",
                    hours,
                    minutes,
                    seconds);
            }

            return string.Format(
                "{0:00}:{1:00}",
                minutes,
                seconds);
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