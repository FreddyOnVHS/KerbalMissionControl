using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace KMC.Plugin
{
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class SolidFuelTelemetrySender :
        MonoBehaviour
    {
        private const int Port = 5057;
        private const float SendIntervalSeconds = 0.1f;
        private const string ProtocolId = "KMC-SOLID2";

        private UdpClient _client;
        private IPEndPoint _endpoint;
        private float _nextSendTime;

        public void Start()
        {
            _client =
                new UdpClient();

            _endpoint =
                new IPEndPoint(
                    IPAddress.Loopback,
                    Port);
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
                vessel.parts == null ||
                _client == null)
            {
                return;
            }

            List<BoosterState> boosters =
                ReadBoosters(
                    vessel);

            double totalAmount = 0.0;
            double totalCapacity = 0.0;
            double activeAmount = 0.0;
            double activeCapacity = 0.0;
            int burningCount = 0;

            for (int index = 0;
                 index < boosters.Count;
                 index++)
            {
                BoosterState booster =
                    boosters[index];

                totalAmount +=
                    booster.Amount;

                totalCapacity +=
                    booster.Capacity;

                if (booster.Burning)
                {
                    burningCount++;

                    activeAmount +=
                        booster.Amount;

                    activeCapacity +=
                        booster.Capacity;
                }
            }

            BoosterState left =
                boosters.Count > 0
                    ? boosters[0]
                    : new BoosterState();

            BoosterState right =
                boosters.Count > 1
                    ? boosters[
                        boosters.Count - 1]
                    : new BoosterState();

            string message =
                string.Join(
                    "|",
                    new[]
                    {
                        ProtocolId,

                        DateTime.UtcNow.Ticks.ToString(
                            CultureInfo.InvariantCulture),

                        Format(totalAmount),
                        Format(totalCapacity),
                        Format(activeAmount),
                        Format(activeCapacity),

                        boosters.Count.ToString(
                            CultureInfo.InvariantCulture),

                        burningCount.ToString(
                            CultureInfo.InvariantCulture),

                        Format(left.Amount),
                        Format(left.Capacity),

                        left.Burning
                            ? "1"
                            : "0",

                        Format(right.Amount),
                        Format(right.Capacity),

                        right.Burning
                            ? "1"
                            : "0"
                    });

            byte[] data =
                Encoding.UTF8.GetBytes(
                    message);

            _client.Send(
                data,
                data.Length,
                _endpoint);
        }

        private static List<BoosterState>
            ReadBoosters(
                Vessel vessel)
        {
            List<BoosterState> result =
                new List<BoosterState>();

            for (int partIndex = 0;
                 partIndex < vessel.parts.Count;
                 partIndex++)
            {
                Part part =
                    vessel.parts[partIndex];

                if (part == null)
                {
                    continue;
                }

                PartResource solidFuel =
                    part.Resources != null
                        ? part.Resources[
                            "SolidFuel"]
                        : null;

                if (solidFuel == null)
                {
                    continue;
                }

                bool burning;

                if (!HasSolidEngine(
                        part,
                        out burning))
                {
                    continue;
                }

                double lateralPosition =
                    GetLateralPosition(
                        vessel,
                        part);

                result.Add(
                    new BoosterState
                    {
                        Amount =
                            Math.Max(
                                0.0,
                                solidFuel.amount),

                        Capacity =
                            Math.Max(
                                0.0,
                                solidFuel.maxAmount),

                        Burning =
                            burning,

                        LateralPosition =
                            lateralPosition
                    });
            }

            result.Sort(
                delegate(
                    BoosterState left,
                    BoosterState right)
                {
                    return left.LateralPosition
                        .CompareTo(
                            right.LateralPosition);
                });

            return result;
        }

        private static double GetLateralPosition(
            Vessel vessel,
            Part part)
        {
            if (vessel == null ||
                part == null ||
                part.transform == null ||
                vessel.ReferenceTransform == null)
            {
                return 0.0;
            }

            Vector3 offset =
                part.transform.position -
                vessel.ReferenceTransform.position;

            return Vector3.Dot(
                offset,
                vessel.ReferenceTransform.right);
        }

        private static bool HasSolidEngine(
            Part part,
            out bool burning)
        {
            burning =
                false;

            if (part.Modules == null)
            {
                return false;
            }

            bool found =
                false;

            for (int moduleIndex = 0;
                 moduleIndex < part.Modules.Count;
                 moduleIndex++)
            {
                ModuleEngines engine =
                    part.Modules[moduleIndex]
                    as ModuleEngines;

                if (engine == null)
                {
                    continue;
                }

                bool usesSolidFuel =
                    false;

                for (int propellantIndex = 0;
                     propellantIndex <
                        engine.propellants.Count;
                     propellantIndex++)
                {
                    Propellant propellant =
                        engine.propellants[
                            propellantIndex];

                    if (propellant != null &&
                        string.Equals(
                            propellant.name,
                            "SolidFuel",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        usesSolidFuel =
                            true;

                        break;
                    }
                }

                if (!usesSolidFuel)
                {
                    continue;
                }

                found =
                    true;

                if (engine.EngineIgnited &&
                    !engine.engineShutdown &&
                    engine.finalThrust > 0.01f)
                {
                    burning =
                        true;
                }
            }

            return found;
        }

        private static string Format(
            double value)
        {
            return value.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }

        public void OnDestroy()
        {
            if (_client != null)
            {
                _client.Close();

                _client =
                    null;
            }
        }

        private sealed class BoosterState
        {
            public double Amount;
            public double Capacity;
            public bool Burning;
            public double LateralPosition;
        }
    }
}
