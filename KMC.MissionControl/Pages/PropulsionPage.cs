using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using System;

namespace KMC.MissionControl.Pages
{
    public sealed class PropulsionPage : IMissionPage
    {
        public string Name
        {
            get { return "PROPULSION DATA"; }
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
                "CH 04");

            layout.Row(
                "STAGE",
                telemetry.CurrentStage.ToString("00"),
                "THROTTLE",
                FormatPercent(
                    telemetry.Throttle));

            layout.Row(
                "MASS",
                FormatMass(
                    telemetry.VesselMass),
                "TWR",
                FormatRatio(
                    telemetry.ThrustToWeightRatio));

            layout.Space();

            layout.Row(
                "CUR THRUST",
                FormatThrust(
                    telemetry.CurrentThrust),
                "MAX THRUST",
                FormatThrust(
                    telemetry.MaximumThrust));

            layout.Row(
                "THRUST LOAD",
                FormatThrustLoad(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust),
                "THR MARGIN",
                FormatThrustMargin(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust));

            layout.Space();

            layout.Row(
                "ENGINE COUNT",
                telemetry.EngineCount.ToString("00"),
                "ENG STATUS",
                GetEngineStatus(
                    telemetry));

            layout.Row(
                "IGNITED",
                telemetry.IgnitedEngineCount.ToString("00"),
                "PRODUCING",
                telemetry
                    .ProducingThrustEngineCount
                    .ToString("00"));

            layout.Row(
                "FLAMEOUTS",
                telemetry.FlameoutEngineCount.ToString("00"),
                "AVG ISP",
                FormatSpecificImpulse(
                    telemetry.AverageSpecificImpulse));

            layout.Space();

            layout.Row(
                "STAGE LF",
                FormatResource(
                    telemetry.StageLiquidFuelAmount,
                    telemetry.StageLiquidFuelCapacity),
                "TOTAL LF",
                FormatResource(
                    telemetry.TotalLiquidFuelAmount,
                    telemetry.TotalLiquidFuelCapacity));

            layout.Row(
                "STAGE OX",
                FormatResource(
                    telemetry.StageOxidizerAmount,
                    telemetry.StageOxidizerCapacity),
                "TOTAL OX",
                FormatResource(
                    telemetry.TotalOxidizerAmount,
                    telemetry.TotalOxidizerCapacity));

            layout.Row(
                "STAGE MP",
                FormatResource(
                    telemetry.StageMonopropellantAmount,
                    telemetry.StageMonopropellantCapacity),
                "TOTAL MP",
                FormatResource(
                    telemetry.TotalMonopropellantAmount,
                    telemetry.TotalMonopropellantCapacity));

            layout.Space();

            layout.Row(
                "STAGE DELTA-V",
                "---",
                "TOTAL DELTA-V",
                "---");

            layout.Row(
                "REQ DELTA-V",
                "---",
                "BURN TIME",
                "---");
        }

        private static string GetEngineStatus(
            MissionTelemetry telemetry)
        {
            if (telemetry.EngineCount <= 0)
            {
                return "NO ENGINES";
            }

            if (telemetry.FlameoutEngineCount > 0)
            {
                return "FLAMEOUT";
            }

            if (telemetry.ProducingThrustEngineCount > 0)
            {
                if (telemetry.ProducingThrustEngineCount ==
                    telemetry.IgnitedEngineCount)
                {
                    return "GO";
                }

                return "PARTIAL";
            }

            if (telemetry.IgnitedEngineCount > 0)
            {
                return "ARMED";
            }

            return "STANDBY";
        }

        private static string FormatSpecificImpulse(
            double seconds)
        {
            if (!IsFinite(seconds) ||
                seconds <= 0.0)
            {
                return "---";
            }

            return
                seconds.ToString("0.0")
                + " S";
        }

        private static string FormatResource(
            double amount,
            double capacity)
        {
            if (!IsFinite(amount) ||
                !IsFinite(capacity) ||
                capacity <= 0.0)
            {
                return "---";
            }

            amount =
                Math.Max(
                    0.0,
                    Math.Min(
                        amount,
                        capacity));

            double percent =
                amount /
                capacity *
                100.0;

            return
                percent.ToString("0")
                + "%";
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
                .ToString("0.0")
                + " T";
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
                .ToString("0.0")
                + " KN";
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
                percent.ToString("0")
                + "%";
        }

        private static string FormatThrustLoad(
            double currentThrust,
            double maximumThrust)
        {
            if (!IsFinite(currentThrust) ||
                !IsFinite(maximumThrust) ||
                maximumThrust <= 0.0)
            {
                return "---";
            }

            double percent =
                currentThrust /
                maximumThrust *
                100.0;

            percent =
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        percent));

            return
                percent.ToString("0.0")
                + "%";
        }

        private static string FormatThrustMargin(
            double currentThrust,
            double maximumThrust)
        {
            if (!IsFinite(currentThrust) ||
                !IsFinite(maximumThrust))
            {
                return "---";
            }

            double margin =
                Math.Max(
                    0.0,
                    maximumThrust -
                    currentThrust);

            return
                margin.ToString("0.0")
                + " KN";
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