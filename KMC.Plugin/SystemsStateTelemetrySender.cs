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
    public sealed class SystemsStateTelemetrySender :
        MonoBehaviour
    {
        private const float SendIntervalSeconds = 0.1f;
        private const int SystemsTelemetryPort = 5091;
        private const string ProtocolId = "KMCSYS1";

        private UdpClient _udpClient;
        private IPEndPoint _endpoint;
        private float _nextSendTime;

        public void Start()
        {
            try
            {
                _udpClient =
                    new UdpClient();

                _endpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        SystemsTelemetryPort);

                Debug.Log(
                    "[KMC] Systems-state telemetry sender started on UDP " +
                    SystemsTelemetryPort +
                    ".");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Systems-state sender failed to start: " +
                    ex);
            }
        }

        public void Update()
        {
            if (_udpClient == null ||
                Time.realtimeSinceStartup < _nextSendTime)
            {
                return;
            }

            _nextSendTime =
                Time.realtimeSinceStartup +
                SendIntervalSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                return;
            }

            try
            {
                double amount;
                double capacity;

                ReadElectricCharge(
                    vessel,
                    out amount,
                    out capacity);

                string message =
                    string.Join(
                        "|",
                        new[]
                        {
                            ProtocolId,
                            amount.ToString(
                                "R",
                                CultureInfo.InvariantCulture),
                            capacity.ToString(
                                "R",
                                CultureInfo.InvariantCulture),
                            GetMaximumThermalRatio(vessel)
                                .ToString(
                                    "R",
                                    CultureInfo.InvariantCulture),
                            IsDocked(vessel)
                                ? "1"
                                : "0",
                            ElectricalAttributionTelemetry
                                .BuildEncodedPayload(
                                    vessel)
                        });

                byte[] payload =
                    Encoding.UTF8.GetBytes(
                        message);

                _udpClient.Send(
                    payload,
                    payload.Length,
                    _endpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Systems-state send failed: " +
                    ex);
            }
        }

        private static void ReadElectricCharge(
            Vessel vessel,
            out double amount,
            out double capacity)
        {
            amount = 0.0;
            capacity = 0.0;

            if (vessel.parts == null)
            {
                return;
            }

            foreach (Part part in vessel.parts)
            {
                if (part == null ||
                    part.Resources == null)
                {
                    continue;
                }

                PartResource resource =
                    part.Resources["ElectricCharge"];

                if (resource == null)
                {
                    continue;
                }

                amount +=
                    Math.Max(
                        0.0,
                        resource.amount);

                capacity +=
                    Math.Max(
                        0.0,
                        resource.maxAmount);
            }
        }

        private static double GetMaximumThermalRatio(
            Vessel vessel)
        {
            double maximum = 0.0;

            if (vessel.parts == null)
            {
                return maximum;
            }

            foreach (Part part in vessel.parts)
            {
                if (part == null)
                {
                    continue;
                }

                if (part.maxTemp > 0.0)
                {
                    maximum =
                        Math.Max(
                            maximum,
                            part.temperature /
                            part.maxTemp);
                }

                if (part.skinMaxTemp > 0.0)
                {
                    maximum =
                        Math.Max(
                            maximum,
                            part.skinTemperature /
                            part.skinMaxTemp);
                }
            }

            return
                Math.Max(
                    0.0,
                    maximum);
        }

        private static bool IsDocked(
            Vessel vessel)
        {
            if (vessel.parts == null)
            {
                return false;
            }

            foreach (Part part in vessel.parts)
            {
                if (part == null ||
                    part.Modules == null)
                {
                    continue;
                }

                foreach (PartModule module in part.Modules)
                {
                    ModuleDockingNode node =
                        module as ModuleDockingNode;

                    if (node == null)
                    {
                        continue;
                    }

                    string state =
                        node.state ??
                        string.Empty;

                    if (state.StartsWith(
                            "Docked",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void OnDestroy()
        {
            UdpClient client =
                _udpClient;

            _udpClient =
                null;

            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
        }
    }
}
