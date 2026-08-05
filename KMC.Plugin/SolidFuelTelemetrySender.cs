using System;
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
        private const string ProtocolId = "KMC-SOLID1";

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

            double totalAmount = 0.0;
            double totalCapacity = 0.0;
            double activeAmount = 0.0;
            double activeCapacity = 0.0;
            int boosterCount = 0;
            int burningCount = 0;

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

                bool isBooster =
                    HasSolidEngine(
                        part,
                        out bool burning);

                if (!isBooster)
                {
                    continue;
                }

                boosterCount++;

                totalAmount +=
                    Math.Max(
                        0.0,
                        solidFuel.amount);

                totalCapacity +=
                    Math.Max(
                        0.0,
                        solidFuel.maxAmount);

                if (burning)
                {
                    burningCount++;

                    activeAmount +=
                        Math.Max(
                            0.0,
                            solidFuel.amount);

                    activeCapacity +=
                        Math.Max(
                            0.0,
                            solidFuel.maxAmount);
                }
            }

            string message =
                string.Join(
                    "|",
                    new[]
                    {
                        ProtocolId,
                        DateTime.UtcNow.Ticks.ToString(
                            CultureInfo.InvariantCulture),
                        totalAmount.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        totalCapacity.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        activeAmount.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        activeCapacity.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                        boosterCount.ToString(
                            CultureInfo.InvariantCulture),
                        burningCount.ToString(
                            CultureInfo.InvariantCulture)
                    });

            byte[] data =
                Encoding.UTF8.GetBytes(
                    message);

            _client.Send(
                data,
                data.Length,
                _endpoint);
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

        public void OnDestroy()
        {
            if (_client != null)
            {
                _client.Close();
                _client =
                    null;
            }
        }
    }
}
