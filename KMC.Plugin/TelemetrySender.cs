using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class TelemetrySender : MonoBehaviour
    {
        private const float SendIntervalSeconds =
            0.1f;

        private UdpClient _udpClient;

        private IPEndPoint
            _missionControlEndpoint;

        private float _nextSendTime;

        public void Start()
        {
            try
            {
                _udpClient =
                    new UdpClient();

                _missionControlEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        TelemetryPacket.TelemetryPort);

                Debug.Log(
                    "[KMC] Telemetry sender started.");

                ScreenMessages.PostScreenMessage(
                    "KMC telemetry link started",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Failed to start telemetry sender: " +
                    ex);
            }
        }

        public void Update()
        {
            if (Time.realtimeSinceStartup <
                _nextSendTime)
            {
                return;
            }

            _nextSendTime =
                Time.realtimeSinceStartup +
                SendIntervalSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null ||
                _udpClient == null)
            {
                return;
            }

            SendTelemetry(vessel);
        }

        private void SendTelemetry(
            Vessel vessel)
        {
            try
            {
                TelemetryPacket packet =
                    CreatePacket(vessel);

                SendPacket(packet);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Telemetry send failed: " +
                    ex);
            }
        }

        private static TelemetryPacket CreatePacket(
            Vessel vessel)
        {
            EngineTelemetry engineTelemetry =
                GetEngineTelemetry(vessel);

            ResourceTelemetry resourceTelemetry =
                GetResourceTelemetry(vessel);

            double radarAltitude =
                GetRadarAltitude(vessel);

            double mach =
                GetMach(vessel);

            double timeToApoapsis =
                GetTimeToApoapsis(vessel);

            double thrustToWeightRatio =
                GetThrustToWeightRatio(
                    vessel,
                    engineTelemetry.CurrentThrust);

            return new TelemetryPacket
            {
                TimestampUtc =
                    DateTime.UtcNow,

                VesselName =
                    vessel.vesselName,

                BodyName =
                    vessel.mainBody != null
                        ? vessel.mainBody.bodyName
                        : string.Empty,

                MissionTime =
                    vessel.missionTime,

                Altitude =
                    vessel.altitude,

                SurfaceSpeed =
                    vessel.srfSpeed,

                HorizontalSpeed =
                    vessel.horizontalSrfSpeed,

                VerticalSpeed =
                    vessel.verticalSpeed,

                OrbitalSpeed =
                    vessel.obt_speed,

                Apoapsis =
                    vessel.orbit != null
                        ? vessel.orbit.ApA
                        : 0.0,

                Periapsis =
                    vessel.orbit != null
                        ? vessel.orbit.PeA
                        : 0.0,

                RadarAltitude =
                    radarAltitude,

                TimeToApoapsis =
                    timeToApoapsis,

                Eccentricity =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.eccentricity),

                SemiMajorAxis =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.semiMajorAxis),

                TrueAnomalyDegrees =
                GetTrueAnomalyDegrees(
                    vessel),

                ArgumentOfPeriapsisDegrees =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.argumentOfPeriapsis),

                InclinationDegrees =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.inclination),

                LongitudeOfAscendingNodeDegrees =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.LAN),

                OrbitalPeriod =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.period),

                TimeToPeriapsis =
                GetTimeToPeriapsis(
                    vessel),

                Throttle =
                    vessel.ctrlState != null
                        ? vessel.ctrlState.mainThrottle
                        : 0.0,

                CurrentStage =
                    vessel.currentStage,

                GForce =
                    vessel.geeForce,

                Pitch =
                    GetPitch(vessel),

                Heading =
                    FlightGlobals.ship_heading,

                Roll =
                    GetRoll(vessel),

                DynamicPressureKpa =
                    vessel.dynamicPressurekPa,

                StaticPressureKpa =
                    vessel.staticPressurekPa,

                Mach =
                    mach,

                VesselMass =
                    vessel.totalMass,

                CurrentThrust =
                    engineTelemetry.CurrentThrust,

                MaximumThrust =
                    engineTelemetry.MaximumThrust,

                ThrustToWeightRatio =
                    thrustToWeightRatio,

                EngineCount =
                    engineTelemetry.EngineCount,

                IgnitedEngineCount =
                    engineTelemetry.IgnitedEngineCount,

                ProducingThrustEngineCount =
                    engineTelemetry
                        .ProducingThrustEngineCount,

                FlameoutEngineCount =
                    engineTelemetry.FlameoutEngineCount,

                AverageSpecificImpulse =
                    engineTelemetry
                        .AverageSpecificImpulse,

                StageLiquidFuelAmount =
                    resourceTelemetry
                        .StageLiquidFuelAmount,

                StageLiquidFuelCapacity =
                    resourceTelemetry
                        .StageLiquidFuelCapacity,

                StageOxidizerAmount =
                    resourceTelemetry
                        .StageOxidizerAmount,

                StageOxidizerCapacity =
                    resourceTelemetry
                        .StageOxidizerCapacity,

                StageMonopropellantAmount =
                    resourceTelemetry
                        .StageMonopropellantAmount,

                StageMonopropellantCapacity =
                    resourceTelemetry
                        .StageMonopropellantCapacity,

                TotalLiquidFuelAmount =
                    resourceTelemetry
                        .TotalLiquidFuelAmount,

                TotalLiquidFuelCapacity =
                    resourceTelemetry
                        .TotalLiquidFuelCapacity,

                TotalOxidizerAmount =
                    resourceTelemetry
                        .TotalOxidizerAmount,

                TotalOxidizerCapacity =
                    resourceTelemetry
                        .TotalOxidizerCapacity,

                TotalMonopropellantAmount =
                    resourceTelemetry
                        .TotalMonopropellantAmount,

                TotalMonopropellantCapacity =
                    resourceTelemetry
                        .TotalMonopropellantCapacity
            };
        }

        private static double GetRadarAltitude(
            Vessel vessel)
        {
            if (vessel == null)
            {
                return 0.0;
            }

            /*
             * heightFromTerrain can return a negative value
             * when no useful terrain reading is available.
             */
            if (vessel.heightFromTerrain >= 0.0f)
            {
                return vessel.heightFromTerrain;
            }

            /*
             * Fall back to altitude above the terrain's
             * sea-level elevation.
             */
            double fallbackAltitude =
                vessel.altitude -
                vessel.terrainAltitude;

            return Math.Max(
                0.0,
                fallbackAltitude);
        }

        private static double GetMach(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.speedOfSound <= 0.0)
            {
                return 0.0;
            }

            return vessel.srfSpeed /
                vessel.speedOfSound;
        }

        private static double GetTimeToApoapsis(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.orbit == null)
            {
                return 0.0;
            }

            double timeToApoapsis =
                vessel.orbit.timeToAp;

            if (double.IsNaN(timeToApoapsis) ||
                double.IsInfinity(timeToApoapsis) ||
                timeToApoapsis < 0.0)
            {
                return 0.0;
            }

            return timeToApoapsis;
        }

        private static double GetOrbitValue(
    Vessel vessel,
    Func<Orbit, double> selector)
        {
            if (vessel == null ||
                vessel.orbit == null ||
                selector == null)
            {
                return 0.0;
            }

            double value;

            try
            {
                value =
                    selector(
                        vessel.orbit);
            }
            catch
            {
                return 0.0;
            }

            if (!IsFinite(value))
            {
                return 0.0;
            }

            return value;
        }

        private static double GetTrueAnomalyDegrees(
            Vessel vessel)
        {
            double trueAnomalyRadians =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.trueAnomaly);

            double trueAnomalyDegrees =
                trueAnomalyRadians *
                180.0 /
                Math.PI;

            return NormalizeDegrees(
                trueAnomalyDegrees);
        }

        private static double GetTimeToPeriapsis(
            Vessel vessel)
        {
            double value =
                GetOrbitValue(
                    vessel,
                    orbit => orbit.timeToPe);

            if (value < 0.0)
            {
                return 0.0;
            }

            return value;
        }

        private static double NormalizeDegrees(
            double value)
        {
            if (!IsFinite(value))
            {
                return 0.0;
            }

            double normalized =
                value %
                360.0;

            if (normalized < 0.0)
            {
                normalized +=
                    360.0;
            }

            return normalized;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private static EngineTelemetry
            GetEngineTelemetry(
                Vessel vessel)
        {
            EngineTelemetry result =
                new EngineTelemetry();

            if (vessel == null ||
                vessel.parts == null)
            {
                return result;
            }

            double specificImpulseTotal = 0.0;
            int specificImpulseSampleCount = 0;

            foreach (Part part in vessel.parts)
            {
                if (part == null ||
                    part.Modules == null)
                {
                    continue;
                }

                foreach (PartModule module in
                    part.Modules)
                {
                    ModuleEngines engine =
                        module as ModuleEngines;

                    if (engine == null)
                    {
                        continue;
                    }

                    result.EngineCount++;

                    bool ignited =
                        engine.EngineIgnited &&
                        !engine.engineShutdown;

                    if (ignited)
                    {
                        result.IgnitedEngineCount++;
                    }

                    if (engine.flameout)
                    {
                        result.FlameoutEngineCount++;
                    }

                    /*
                     * finalThrust is the actual thrust produced
                     * by this engine at the current instant.
                     */
                    if (engine.finalThrust > 0.01f)
                    {
                        result.CurrentThrust +=
                            engine.finalThrust;

                        result
                            .ProducingThrustEngineCount++;
                    }

                    /*
                     * MaximumThrust represents the available
                     * thrust from engines that are currently
                     * ignited and not shut down or flamed out.
                     */
                    if (ignited &&
                        !engine.flameout)
                    {
                        double thrustLimit =
                            engine.thrustPercentage /
                            100.0;

                        if (thrustLimit < 0.0)
                        {
                            thrustLimit = 0.0;
                        }
                        else if (thrustLimit > 1.0)
                        {
                            thrustLimit = 1.0;
                        }

                        result.MaximumThrust +=
                            engine.maxThrust *
                            thrustLimit;

                        double specificImpulse =
                            GetEngineSpecificImpulse(
                                vessel,
                                engine);

                        if (specificImpulse > 0.0)
                        {
                            specificImpulseTotal +=
                                specificImpulse;

                            specificImpulseSampleCount++;
                        }
                    }
                }
            }

            if (specificImpulseSampleCount > 0)
            {
                result.AverageSpecificImpulse =
                    specificImpulseTotal /
                    specificImpulseSampleCount;
            }

            return result;
        }

        private static double
            GetEngineSpecificImpulse(
                Vessel vessel,
                ModuleEngines engine)
        {
            if (vessel == null ||
                engine == null ||
                engine.atmosphereCurve == null)
            {
                return 0.0;
            }

            /*
             * KSP atmosphereCurve pressure is measured in
             * standard atmospheres. Vessel static pressure is
             * reported in kilopascals.
             */
            double pressureAtmospheres =
                vessel.staticPressurekPa /
                101.325;

            if (pressureAtmospheres < 0.0)
            {
                pressureAtmospheres = 0.0;
            }

            double specificImpulse =
                engine.atmosphereCurve.Evaluate(
                    (float)pressureAtmospheres);

            if (double.IsNaN(specificImpulse) ||
                double.IsInfinity(specificImpulse) ||
                specificImpulse < 0.0)
            {
                return 0.0;
            }

            return specificImpulse;
        }

        private static ResourceTelemetry
            GetResourceTelemetry(
                Vessel vessel)
        {
            ResourceTelemetry result =
                new ResourceTelemetry();

            if (vessel == null ||
                PartResourceLibrary.Instance == null)
            {
                return result;
            }

            ReadResourceTotals(
                vessel,
                "LiquidFuel",
                out result.TotalLiquidFuelAmount,
                out result.TotalLiquidFuelCapacity);

            ReadResourceTotals(
                vessel,
                "Oxidizer",
                out result.TotalOxidizerAmount,
                out result.TotalOxidizerCapacity);

            ReadResourceTotals(
                vessel,
                "MonoPropellant",
                out result.TotalMonopropellantAmount,
                out result.TotalMonopropellantCapacity);

            /*
             * KSP's Vessel resource API reports the resources
             * connected to the active vessel. For this first
             * PROP implementation, use those connected totals
             * for both the stage and vessel displays.
             *
             * A later telemetry revision can calculate true
             * post-decoupling stage resources from the vessel's
             * staging and fuel-flow graph.
             */
            result.StageLiquidFuelAmount =
                result.TotalLiquidFuelAmount;

            result.StageLiquidFuelCapacity =
                result.TotalLiquidFuelCapacity;

            result.StageOxidizerAmount =
                result.TotalOxidizerAmount;

            result.StageOxidizerCapacity =
                result.TotalOxidizerCapacity;

            result.StageMonopropellantAmount =
                result.TotalMonopropellantAmount;

            result.StageMonopropellantCapacity =
                result.TotalMonopropellantCapacity;

            return result;
        }

        private static void ReadResourceTotals(
            Vessel vessel,
            string resourceName,
            out double amount,
            out double capacity)
        {
            amount = 0.0;
            capacity = 0.0;

            if (vessel == null ||
                string.IsNullOrEmpty(resourceName) ||
                PartResourceLibrary.Instance == null)
            {
                return;
            }

            PartResourceDefinition definition =
                PartResourceLibrary.Instance
                    .GetDefinition(resourceName);

            if (definition == null)
            {
                return;
            }

            vessel.GetConnectedResourceTotals(
                definition.id,
                out amount,
                out capacity);
        }

        private static double
            GetThrustToWeightRatio(
                Vessel vessel,
                double currentThrust)
        {
            if (vessel == null ||
                vessel.totalMass <= 0.0 ||
                currentThrust <= 0.0)
            {
                return 0.0;
            }

            /*
             * getGeeForceAtPosition returns local
             * gravitational acceleration in m/s².
             */
            Vector3d gravityVector =
                FlightGlobals
                    .getGeeForceAtPosition(
                        vessel.CoM);

            double localGravity =
                gravityVector.magnitude;

            if (localGravity <= 0.0)
            {
                return 0.0;
            }

            /*
             * KSP vessel mass is in metric tonnes and
             * engine thrust is in kilonewtons.
             *
             * tonne × m/s² = kN, so the units work
             * directly here.
             */
            double weightKilonewtons =
                vessel.totalMass *
                localGravity;

            return currentThrust /
                weightKilonewtons;
        }

        private static double GetPitch(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.ReferenceTransform == null ||
                vessel.mainBody == null)
            {
                return 0.0;
            }

            Vector3 vesselForward =
                vessel.ReferenceTransform.up;

            Vector3 surfaceUp =
                vessel.upAxis;

            return 90.0 -
                Vector3.Angle(
                    vesselForward,
                    surfaceUp);
        }

        private static double GetRoll(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.ReferenceTransform == null)
            {
                return 0.0;
            }

            Vector3 vesselRight =
                vessel.ReferenceTransform.right;

            Vector3 surfaceUp =
                vessel.upAxis;

            Vector3 vesselForward =
                vessel.ReferenceTransform.up;

            Vector3 projectedUp =
                Vector3.ProjectOnPlane(
                    surfaceUp,
                    vesselForward);

            if (projectedUp.sqrMagnitude <
                0.0001f)
            {
                return 0.0;
            }

            projectedUp.Normalize();

            return Vector3.SignedAngle(
                projectedUp,
                vesselRight,
                vesselForward) - 90.0;
        }

        private void SendPacket(
            TelemetryPacket packet)
        {
            string serializedPacket =
                packet.Serialize();

            byte[] data =
                Encoding.UTF8.GetBytes(
                    serializedPacket);

            _udpClient.Send(
                data,
                data.Length,
                _missionControlEndpoint);
        }

        public void OnDestroy()
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }

            Debug.Log(
                "[KMC] Telemetry sender stopped.");
        }

        private sealed class ResourceTelemetry
        {
            public double StageLiquidFuelAmount;

            public double StageLiquidFuelCapacity;

            public double StageOxidizerAmount;

            public double StageOxidizerCapacity;

            public double StageMonopropellantAmount;

            public double StageMonopropellantCapacity;

            public double TotalLiquidFuelAmount;

            public double TotalLiquidFuelCapacity;

            public double TotalOxidizerAmount;

            public double TotalOxidizerCapacity;

            public double TotalMonopropellantAmount;

            public double TotalMonopropellantCapacity;
        }

        private sealed class EngineTelemetry
        {
            public double CurrentThrust
            {
                get;
                set;
            }

            public double MaximumThrust
            {
                get;
                set;
            }

            public int EngineCount
            {
                get;
                set;
            }

            public int IgnitedEngineCount
            {
                get;
                set;
            }

            public int ProducingThrustEngineCount
            {
                get;
                set;
            }

            public int FlameoutEngineCount
            {
                get;
                set;
            }

            public double AverageSpecificImpulse
            {
                get;
                set;
            }
        }
    }
}