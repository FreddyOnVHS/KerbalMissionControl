using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public sealed class OrbitPage : IMissionPage
    {
        public string Name
        {
            get { return "ORBIT DATA"; }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            MissionPageLayout layout =
                new MissionPageLayout(context);

            layout.DrawHeader(
                Name,
                "CH 01");

            layout.Row(
                "VESSEL",
                FormatText(telemetry.VesselName),
                "STAGE",
                telemetry.CurrentStage.ToString("00"));

            layout.Row(
                "BODY",
                FormatText(telemetry.BodyName),
                "G FORCE",
                telemetry.GForce.ToString("0.00"));

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
                "PITCH",
                FormatSignedAngle(
                    telemetry.Pitch));

            layout.Row(
                "APOAPSIS",
                FormatDistance(
                    telemetry.Apoapsis),
                "HEADING",
                FormatHeading(
                    telemetry.Heading));

            layout.Row(
                "PERIAPSIS",
                FormatDistance(
                    telemetry.Periapsis),
                "ROLL",
                FormatSignedAngle(
                    telemetry.Roll));

            layout.Space();

            layout.Row(
                "ORB VEL",
                FormatSpeed(
                    telemetry.OrbitalSpeed));

            layout.Row(
                "VERT VEL",
                FormatSignedSpeed(
                    telemetry.VerticalSpeed));

            layout.Row(
                "SURF VEL",
                FormatSpeed(
                    telemetry.SurfaceSpeed));
        }

        private static string FormatDistance(
            double value)
        {
            return value.ToString("N0") + " M";
        }

        private static string FormatSpeed(
            double value)
        {
            return value.ToString("N1") + " M/S";
        }

        private static string FormatSignedSpeed(
            double value)
        {
            return value.ToString(
                "+0.0;-0.0;0.0") + " M/S";
        }

        private static string FormatSignedAngle(
            double value)
        {
            return value.ToString(
                "+0.0;-0.0;0.0") + "°";
        }

        private static string FormatHeading(
            double value)
        {
            double normalized =
                value % 360.0;

            if (normalized < 0)
            {
                normalized += 360.0;
            }

            return normalized.ToString(
                "000.0") + "°";
        }

        private static string FormatPercent(
            double value)
        {
            double percent =
                value * 100.0;

            if (percent < 0)
            {
                percent = 0;
            }

            if (percent > 100)
            {
                percent = 100;
            }

            return percent.ToString("0") + "%";
        }

        private static string FormatMissionTime(
            double totalSeconds)
        {
            if (totalSeconds < 0)
            {
                totalSeconds = 0;
            }

            int hours =
                (int)(totalSeconds / 3600);

            int minutes =
                (int)(totalSeconds % 3600) / 60;

            int seconds =
                (int)(totalSeconds % 60);

            return string.Format(
                "{0:000}:{1:00}:{2:00}",
                hours,
                minutes,
                seconds);
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
    }
}