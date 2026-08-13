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

                amount += Math.Max(0.0, resource.amount);
                capacity += Math.Max(0.0, resource.maxAmount);
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

            return Math.Max(0.0, maximum);
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
                        node.state ?? string.Empty;

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
            UdpClient client = _udpClient;
            _udpClient = null;

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

    /// <summary>
    /// Build 14.2 crew-side synthetic electrical control panel.
    ///
    /// The panel sends vessel-qualified KMC electrical commands only. It does
    /// not directly mutate stock KSP generators, batteries, resources or
    /// PartModules. Real KSP failure effects arrive in a later Build 14 step.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class ElectricalControlPanel :
        MonoBehaviour
    {
        private const int CommandPort = 5102;
        private const string ProtocolId = "KMC-ELEC1";
        private const float HeartbeatSeconds = 1.0f;

        private readonly Dictionary<string, Dictionary<string, bool>>
            _statesByVessel =
                new Dictionary<string, Dictionary<string, bool>>(
                    StringComparer.Ordinal);

        private UdpClient _client;
        private IPEndPoint _endpoint;
        private Rect _windowRect =
            new Rect(20f, 120f, 330f, 520f);
        private bool _visible = true;
        private float _nextHeartbeat;

        public void Start()
        {
            try
            {
                _client = new UdpClient();
                _endpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        CommandPort);

                Debug.Log(
                    "[KMC] Electrical control panel started. UDP " +
                    CommandPort +
                    ". Press F8 to show/hide.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Electrical control panel failed to start: " +
                    ex);
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _visible = !_visible;
            }

            if (_client == null ||
                Time.realtimeSinceStartup < _nextHeartbeat)
            {
                return;
            }

            _nextHeartbeat =
                Time.realtimeSinceStartup +
                HeartbeatSeconds;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                return;
            }

            Dictionary<string, bool> states =
                GetStates(vessel.id.ToString());

            foreach (KeyValuePair<string, bool> entry in states)
            {
                Send(
                    vessel.id.ToString(),
                    entry.Key,
                    entry.Value);
            }
        }

        public void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            _windowRect =
                GUI.Window(
                    GetInstanceID(),
                    _windowRect,
                    DrawWindow,
                    "KMC ELECTRICAL CONTROL");
        }

        private void DrawWindow(
            int windowId)
        {
            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                GUILayout.Label("NO ACTIVE VESSEL");
                GUI.DragWindow();
                return;
            }

            string vesselId =
                vessel.id.ToString();

            Dictionary<string, bool> states =
                GetStates(vesselId);

            GUILayout.Label(
                vessel.vesselName ?? "VESSEL");

            GUILayout.Space(6f);
            GUILayout.Label("MAIN BUS A SOURCES");
            DrawToggle(states, vesselId, "SRC_GEN_A", "GEN A");
            DrawToggle(states, vesselId, "SRC_BAT_A", "BAT A");

            GUILayout.Space(4f);
            GUILayout.Label("MAIN BUS B SOURCES");
            DrawToggle(states, vesselId, "SRC_GEN_B", "GEN B");
            DrawToggle(states, vesselId, "SRC_BAT_B", "BAT B");

            GUILayout.Space(4f);
            GUILayout.Label("ESSENTIAL BUS FEEDS");
            DrawToggle(states, vesselId, "FEED_ESS_A", "ESS FEED A");
            DrawToggle(states, vesselId, "FEED_ESS_B", "ESS FEED B");

            GUILayout.Space(4f);
            GUILayout.Label("SHED / RESTORE LOADS");
            DrawToggle(states, vesselId, "COMM_A", "COMM A");
            DrawToggle(states, vesselId, "COMM_B", "COMM B");
            DrawToggle(states, vesselId, "PUMP_A", "PUMP A");
            DrawToggle(states, vesselId, "PUMP_B", "PUMP B");

            GUILayout.Space(8f);

            if (GUILayout.Button("RESET ALL - NOMINAL"))
            {
                ResetStates(states);
                Send(vesselId, "RESET", true);
                SendAll(vesselId, states);
            }

            GUILayout.Label("F8: SHOW / HIDE");

            GUI.DragWindow();
        }

        private void DrawToggle(
            Dictionary<string, bool> states,
            string vesselId,
            string controlId,
            string label)
        {
            bool value = states[controlId];

            string text =
                label +
                "   " +
                (value ? "ON / CLOSED" : "OFF / OPEN");

            if (GUILayout.Button(text))
            {
                value = !value;
                states[controlId] = value;
                Send(vesselId, controlId, value);
            }
        }

        private Dictionary<string, bool> GetStates(
            string vesselId)
        {
            Dictionary<string, bool> states;

            if (!_statesByVessel.TryGetValue(
                    vesselId,
                    out states))
            {
                states =
                    new Dictionary<string, bool>(
                        StringComparer.Ordinal);

                ResetStates(states);
                _statesByVessel[vesselId] = states;
            }

            return states;
        }

        private static void ResetStates(
            Dictionary<string, bool> states)
        {
            states["SRC_GEN_A"] = true;
            states["SRC_BAT_A"] = true;
            states["SRC_GEN_B"] = true;
            states["SRC_BAT_B"] = true;
            states["FEED_ESS_A"] = true;
            states["FEED_ESS_B"] = true;
            states["COMM_A"] = true;
            states["COMM_B"] = true;
            states["PUMP_A"] = true;
            states["PUMP_B"] = true;
        }

        private void SendAll(
            string vesselId,
            Dictionary<string, bool> states)
        {
            foreach (KeyValuePair<string, bool> entry in states)
            {
                Send(vesselId, entry.Key, entry.Value);
            }
        }

        private void Send(
            string vesselId,
            string controlId,
            bool commandedOn)
        {
            if (_client == null ||
                _endpoint == null ||
                string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            try
            {
                string message =
                    string.Join(
                        "|",
                        new[]
                        {
                            ProtocolId,
                            Uri.EscapeDataString(vesselId),
                            Uri.EscapeDataString(controlId ?? string.Empty),
                            commandedOn ? "1" : "0"
                        });

                byte[] payload =
                    Encoding.UTF8.GetBytes(message);

                _client.Send(
                    payload,
                    payload.Length,
                    _endpoint);

                Debug.Log(
                    "[KMC] Electrical control sent" +
                    " | VesselId=" + vesselId +
                    " | Control=" + controlId +
                    " | Command=" +
                    (commandedOn ? "ON/CLOSED" : "OFF/OPEN"));
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Electrical control send failed: " +
                    ex.Message);
            }
        }

        public void OnDestroy()
        {
            UdpClient client = _client;
            _client = null;

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
