using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.4 real KSP failure-effect executor.
    ///
    /// Network receive occurs on a background thread, but every read/mutation
    /// of KSP flight state is performed from Update() on Unity's flight thread.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class FailureEffectReceiver :
        MonoBehaviour
    {
        private const double MinimumDerateFactor = 0.10;
        private const double MaximumDerateFactor = 1.00;
        private const double MinimumWheelFactor = 0.00;
        private const double MaximumWheelFactor = 1.00;
        private const double MinimumEcPulse = 0.10;
        private const double MaximumEcPulse = 25.00;
        private const double MinimumEcLeakRate = 0.10;
        private const double MaximumEcLeakRate = 10.00;
        private const float EcLeakApplyIntervalSeconds = 0.20f;
        private const float EcLeakLeaseSeconds = 2.50f;
        private const float PropulsionEffectLeaseSeconds = 2.50f;

        private readonly object _syncRoot =
            new object();

        private readonly Queue<FailureEffectPacket> _pending =
            new Queue<FailureEffectPacket>();

        private readonly Dictionary<string, EffectRestoreState> _restore =
            new Dictionary<string, EffectRestoreState>(
                StringComparer.Ordinal);

        private readonly Dictionary<string, FailureEffectAck> _completed =
            new Dictionary<string, FailureEffectAck>(
                StringComparer.Ordinal);

        private readonly Queue<string> _completedOrder =
            new Queue<string>();

        private readonly Dictionary<string, PropulsionEffectLeaseState>
            _propulsionEffectLeases =
                new Dictionary<string, PropulsionEffectLeaseState>(
                    StringComparer.Ordinal);

        private readonly Dictionary<string, ContinuousEcLeakState>
            _ecLeaks =
                new Dictionary<string, ContinuousEcLeakState>(
                    StringComparer.Ordinal);

        private UdpClient _receiveClient;
        private UdpClient _ackClient;
        private UdpClient _testSendClient;
        private IPEndPoint _ackEndpoint;
        private IPEndPoint _testEndpoint;
        private Thread _receiveThread;
        private volatile bool _running;

        private bool _showTestPanel;
        private Rect _windowRect =
            new Rect(500f, 120f, 390f, 430f);

        private string _lastStatus =
            "NO COMMAND SENT";

        private float _lastEcLeakApplyTime;

        public void Start()
        {
            try
            {
                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            FailureEffectPacket.CommandPort));

                _ackClient =
                    new UdpClient();

                _testSendClient =
                    new UdpClient();

                _ackEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        FailureEffectPacket.AckPort);

                _testEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        FailureEffectPacket.CommandPort);

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop)
                    {
                        IsBackground = true,
                        Name = "KMC Failure Effect"
                    };

                _receiveThread.Start();

                Debug.Log(
                    "[KMC] Failure effect receiver started on UDP " +
                    FailureEffectPacket.CommandPort.ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Failure effect receiver start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _showTestPanel =
                    !_showTestPanel;
            }

            while (true)
            {
                FailureEffectPacket packet = null;

                lock (_syncRoot)
                {
                    if (_pending.Count > 0)
                    {
                        packet =
                            _pending.Dequeue();
                    }
                }

                if (packet == null)
                {
                    break;
                }

                ApplyCommand(
                    packet);
            }

            ApplyContinuousEcLeaks();
            ExpirePropulsionEffectLeases();
        }

        public void OnGUI()
        {
            if (!_showTestPanel)
            {
                return;
            }

            _windowRect =
                GUI.Window(
                    1404,
                    _windowRect,
                    DrawWindow,
                    "KMC FAILURE EFFECT TEST");
        }

        private void DrawWindow(
            int windowId)
        {
            Vessel vessel =
                FlightGlobals.ActiveVessel;

            GUILayout.Label(
                "BUILD 14.4 / EXPLICIT TEST ONLY");

            if (vessel == null)
            {
                GUILayout.Label(
                    "NO ACTIVE VESSEL");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label(
                "VESSEL: " +
                vessel.vesselName);

            Part enginePart =
                FindFirstPartWithModule(
                    vessel,
                    "ModuleEngines",
                    "ModuleEnginesFX");

            Part wheelPart =
                FindFirstPartWithModule(
                    vessel,
                    "ModuleReactionWheel");

            GUILayout.Space(6f);
            GUILayout.Label("ENGINE EFFECT");

            if (enginePart == null)
            {
                GUILayout.Label(
                    "NO ENGINE PART FOUND");
            }
            else
            {
                GUILayout.Label(
                    DescribePart(
                        enginePart));

                if (GUILayout.Button(
                        "DERATE FIRST ENGINE TO 50%"))
                {
                    SendTestCommand(
                        vessel,
                        enginePart,
                        FailureEffectType.EngineDerate,
                        FailureEffectOperation.Apply,
                        0.50);
                }

                if (GUILayout.Button(
                        "RESTORE ENGINE LIMITER"))
                {
                    SendTestCommand(
                        vessel,
                        enginePart,
                        FailureEffectType.EngineDerate,
                        FailureEffectOperation.Restore,
                        1.00);
                }

                if (GUILayout.Button(
                        "SHUTDOWN FIRST ENGINE"))
                {
                    SendTestCommand(
                        vessel,
                        enginePart,
                        FailureEffectType.EngineShutdown,
                        FailureEffectOperation.Apply,
                        1.00);
                }

                if (GUILayout.Button(
                        "RESTORE ENGINE IGNITION STATE"))
                {
                    SendTestCommand(
                        vessel,
                        enginePart,
                        FailureEffectType.EngineShutdown,
                        FailureEffectOperation.Restore,
                        1.00);
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("REACTION WHEEL EFFECT");

            if (wheelPart == null)
            {
                GUILayout.Label(
                    "NO REACTION WHEEL PART FOUND");
            }
            else
            {
                GUILayout.Label(
                    DescribePart(
                        wheelPart));

                if (GUILayout.Button(
                        "REACTION WHEEL AUTHORITY 25%"))
                {
                    SendTestCommand(
                        vessel,
                        wheelPart,
                        FailureEffectType.ReactionWheelAuthority,
                        FailureEffectOperation.Apply,
                        0.25);
                }

                if (GUILayout.Button(
                        "RESTORE REACTION WHEEL"))
                {
                    SendTestCommand(
                        vessel,
                        wheelPart,
                        FailureEffectType.ReactionWheelAuthority,
                        FailureEffectOperation.Restore,
                        1.00);
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("RESOURCE EFFECT");

            if (GUILayout.Button(
                    "DRAIN 5.0 EC PULSE"))
            {
                SendTestCommand(
                    vessel,
                    null,
                    FailureEffectType.ElectricChargeDrain,
                    FailureEffectOperation.Pulse,
                    5.0);
            }

            GUILayout.Space(8f);
            GUILayout.Label(
                "STATUS: " +
                _lastStatus);

            GUILayout.Label(
                "F9 = HIDE PANEL");

            GUI.DragWindow();
        }

        private void SendTestCommand(
            Vessel vessel,
            Part part,
            FailureEffectType effectType,
            FailureEffectOperation operation,
            double magnitude)
        {
            if (vessel == null ||
                _testSendClient == null ||
                _testEndpoint == null)
            {
                return;
            }

            FailureEffectPacket packet =
                new FailureEffectPacket
                {
                    VesselId =
                        vessel.id.ToString(),
                    CommandId =
                        "TEST-" +
                        Guid.NewGuid().ToString("N"),
                    PartPersistentId =
                        part != null
                            ? GetPersistentId(part)
                            : 0,
                    EffectType =
                        effectType,
                    Operation =
                        operation,
                    Magnitude =
                        magnitude
                };

            try
            {
                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                _testSendClient.Send(
                    data,
                    data.Length,
                    _testEndpoint);

                _lastStatus =
                    "SENT " +
                    effectType.ToString().ToUpperInvariant() +
                    " " +
                    operation.ToString().ToUpperInvariant();
            }
            catch (Exception ex)
            {
                _lastStatus =
                    "SEND FAILED " +
                    ex.GetType().Name;
            }
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender =
                        new IPEndPoint(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        _receiveClient.Receive(
                            ref sender);

                    string text =
                        Encoding.UTF8.GetString(
                            data);

                    FailureEffectPacket packet;

                    if (!FailureEffectPacket.TryParse(
                            text,
                            out packet))
                    {
                        continue;
                    }

                    lock (_syncRoot)
                    {
                        _pending.Enqueue(
                            packet);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[KMC] Failure effect receive failed: " +
                        ex);
                }
            }
        }

        private void ApplyCommand(
            FailureEffectPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            FailureEffectAck prior;

            if (_completed.TryGetValue(
                    packet.CommandId,
                    out prior))
            {
                SendAck(
                    prior);

                return;
            }

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "NO ACTIVE VESSEL");

                return;
            }

            string activeVesselId =
                vessel.id.ToString();

            if (!string.Equals(
                    activeVesselId,
                    packet.VesselId,
                    StringComparison.Ordinal))
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "ACTIVE VESSEL ID DOES NOT MATCH COMMAND");

                return;
            }

            Part part = null;

            if (packet.EffectType !=
                    FailureEffectType.ElectricChargeDrain &&
                packet.EffectType !=
                    FailureEffectType.ElectricChargeLeak)
            {
                if (packet.PartPersistentId == 0)
                {
                    Complete(
                        packet,
                        "REJECTED",
                        double.NaN,
                        "PART ID REQUIRED");

                    return;
                }

                part =
                    FindPartByPersistentId(
                        vessel,
                        packet.PartPersistentId);

                if (part == null)
                {
                    Complete(
                        packet,
                        "REJECTED",
                        double.NaN,
                        "EXACT PART ID NOT FOUND OR IDENTITY AMBIGUOUS ON ACTIVE VESSEL");

                    return;
                }
            }

            try
            {
                switch (packet.EffectType)
                {
                    case FailureEffectType.EngineDerate:
                        ApplyEngineDerate(
                            packet,
                            part);
                        return;

                    case FailureEffectType.EngineShutdown:
                        ApplyEngineShutdown(
                            packet,
                            part);
                        return;

                    case FailureEffectType.ReactionWheelAuthority:
                        ApplyReactionWheelAuthority(
                            packet,
                            part);
                        return;

                    case FailureEffectType.ElectricChargeDrain:
                        ApplyEcDrain(
                            packet,
                            vessel);
                        return;

                    case FailureEffectType.ElectricChargeLeak:
                        ApplyEcLeakCommand(
                            packet,
                            vessel);
                        return;

                    default:
                        Complete(
                            packet,
                            "REJECTED",
                            double.NaN,
                            "UNSUPPORTED EFFECT TYPE");
                        return;
                }
            }
            catch (Exception ex)
            {
                Complete(
                    packet,
                    "ERROR",
                    double.NaN,
                    "KSP EFFECT FAILED: " +
                    ex.GetType().Name);

                Debug.LogError(
                    "[KMC] Failure effect execution failed: " +
                    ex);
            }
        }

        private void ApplyEngineDerate(
            FailureEffectPacket packet,
            Part part)
        {
            PartModule engine =
                FindModule(
                    part,
                    "ModuleEngines",
                    "ModuleEnginesFX");

            if (engine == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "TARGET PART HAS NO ENGINE MODULE");

                return;
            }

            string key =
                RestoreKey(
                    packet,
                    FailureEffectType.EngineDerate);

            if (packet.Operation ==
                    FailureEffectOperation.Restore)
            {
                EffectRestoreState state;

                if (!_restore.TryGetValue(
                        key,
                        out state) ||
                    !state.Primary.HasValue)
                {
                    Complete(
                        packet,
                        "REJECTED",
                        double.NaN,
                        "NO CACHED ENGINE LIMITER STATE");

                    return;
                }

                SetNumericMember(
                    engine,
                    "thrustPercentage",
                    state.Primary.Value);

                double restored =
                    GetNumericMember(
                        engine,
                        "thrustPercentage");

                _restore.Remove(
                    key);

                _propulsionEffectLeases.Remove(
                    key);

                Complete(
                    packet,
                    "RESTORED",
                    restored,
                    "ENGINE THRUST LIMITER RESTORED");

                return;
            }

            if (packet.Operation !=
                    FailureEffectOperation.Apply ||
                packet.Magnitude <
                    MinimumDerateFactor ||
                packet.Magnitude >
                    MaximumDerateFactor)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "ENGINE DERATE FACTOR MUST BE 0.10..1.00");

                return;
            }

            double original =
                GetNumericMember(
                    engine,
                    "thrustPercentage");

            if (!_restore.ContainsKey(key))
            {
                _restore[key] =
                    new EffectRestoreState
                    {
                        Primary = original
                    };
            }
            else
            {
                original =
                    _restore[key].Primary.Value;
            }

            double requested =
                original *
                packet.Magnitude;

            SetNumericMember(
                engine,
                "thrustPercentage",
                requested);

            double observed =
                GetNumericMember(
                    engine,
                    "thrustPercentage");

            RefreshPropulsionEffectLease(
                packet,
                FailureEffectType.EngineDerate,
                key);

            Complete(
                packet,
                "APPLIED",
                observed,
                "ENGINE LIMITER DERATED FROM " +
                original.ToString("0.0") +
                "%");
        }

        private void ApplyEngineShutdown(
            FailureEffectPacket packet,
            Part part)
        {
            PartModule engine =
                FindModule(
                    part,
                    "ModuleEngines",
                    "ModuleEnginesFX");

            if (engine == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "TARGET PART HAS NO ENGINE MODULE");

                return;
            }

            string key =
                RestoreKey(
                    packet,
                    FailureEffectType.EngineShutdown);

            if (packet.Operation ==
                    FailureEffectOperation.Restore)
            {
                EffectRestoreState state;

                if (!_restore.TryGetValue(
                        key,
                        out state) ||
                    !state.Flag.HasValue)
                {
                    Complete(
                        packet,
                        "REJECTED",
                        double.NaN,
                        "NO CACHED ENGINE IGNITION STATE");

                    return;
                }

                if (state.Flag.Value)
                {
                    InvokeParameterless(
                        engine,
                        "Activate");
                }

                _restore.Remove(
                    key);

                _propulsionEffectLeases.Remove(
                    key);

                bool ignited =
                    GetBooleanMember(
                        engine,
                        "EngineIgnited",
                        false);

                Complete(
                    packet,
                    "RESTORED",
                    ignited ? 1.0 : 0.0,
                    state.Flag.Value
                        ? "ENGINE REACTIVATION REQUESTED"
                        : "ENGINE WAS NOT IGNITED BEFORE TEST");

                return;
            }

            if (packet.Operation !=
                    FailureEffectOperation.Apply)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "ENGINE SHUTDOWN REQUIRES APPLY OR RESTORE");

                return;
            }

            bool originalIgnited =
                GetBooleanMember(
                    engine,
                    "EngineIgnited",
                    false);

            if (!_restore.ContainsKey(key))
            {
                _restore[key] =
                    new EffectRestoreState
                    {
                        Flag =
                            originalIgnited
                    };
            }

            InvokeParameterless(
                engine,
                "Shutdown");

            bool after =
                GetBooleanMember(
                    engine,
                    "EngineIgnited",
                    false);

            RefreshPropulsionEffectLease(
                packet,
                FailureEffectType.EngineShutdown,
                key);

            Complete(
                packet,
                "APPLIED",
                after ? 1.0 : 0.0,
                "ENGINE SHUTDOWN COMMAND EXECUTED");
        }

        private void RefreshPropulsionEffectLease(
            FailureEffectPacket packet,
            FailureEffectType effectType,
            string restoreKey)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(restoreKey) ||
                string.IsNullOrWhiteSpace(packet.CommandId) ||
                (!packet.CommandId.StartsWith(
                    "PROP14.6-",
                    StringComparison.Ordinal) &&
                 !packet.CommandId.StartsWith(
                    "GNC14.7-",
                    StringComparison.Ordinal)))
            {
                return;
            }

            _propulsionEffectLeases[restoreKey] =
                new PropulsionEffectLeaseState
                {
                    VesselId =
                        packet.VesselId ?? string.Empty,
                    PartPersistentId =
                        packet.PartPersistentId,
                    EffectType =
                        effectType,
                    RestoreKey =
                        restoreKey,
                    LastRefreshRealtime =
                        Time.realtimeSinceStartup
                };
        }

        private void ExpirePropulsionEffectLeases()
        {
            if (_propulsionEffectLeases.Count == 0)
            {
                return;
            }

            float now =
                Time.realtimeSinceStartup;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            List<string> expired =
                null;

            foreach (
                KeyValuePair<string, PropulsionEffectLeaseState> pair
                in _propulsionEffectLeases)
            {
                PropulsionEffectLeaseState lease =
                    pair.Value;

                if (lease == null ||
                    now -
                        lease.LastRefreshRealtime <=
                    PropulsionEffectLeaseSeconds)
                {
                    continue;
                }

                /*
                 * KSP mutation remains tied to the exact active vessel. If the
                 * crew switched away, keep the expired lease pending and
                 * restore it the next time that exact vessel is active.
                 */
                if (vessel == null ||
                    !string.Equals(
                        vessel.id.ToString(),
                        lease.VesselId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Part part =
                    FindPartByPersistentId(
                        vessel,
                        lease.PartPersistentId);

                EffectRestoreState restore;

                if (part != null &&
                    _restore.TryGetValue(
                        lease.RestoreKey,
                        out restore))
                {
                    try
                    {
                        if (lease.EffectType ==
                                FailureEffectType.ReactionWheelAuthority)
                        {
                            PartModule wheel =
                                FindModule(
                                    part,
                                    "ModuleReactionWheel");

                            if (wheel != null &&
                                restore.Primary.HasValue &&
                                restore.Secondary.HasValue &&
                                restore.Tertiary.HasValue)
                            {
                                SetNumericMember(wheel, "PitchTorque", restore.Primary.Value);
                                SetNumericMember(wheel, "YawTorque", restore.Secondary.Value);
                                SetNumericMember(wheel, "RollTorque", restore.Tertiary.Value);
                            }
                        }
                        else
                        {
                            PartModule engine =
                                FindModule(
                                    part,
                                    "ModuleEngines",
                                    "ModuleEnginesFX");

                            if (engine != null)
                            {
                                if (lease.EffectType ==
                                        FailureEffectType.EngineDerate &&
                                    restore.Primary.HasValue)
                                {
                                    SetNumericMember(
                                        engine,
                                        "thrustPercentage",
                                        restore.Primary.Value);
                                }
                                else if (lease.EffectType ==
                                             FailureEffectType.EngineShutdown &&
                                         restore.Flag.HasValue &&
                                         restore.Flag.Value)
                                {
                                    InvokeParameterless(
                                        engine,
                                        "Activate");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            "[KMC] FAILURE EFFECT FAILSAFE RESTORE ERROR" +
                            " | VesselId=" +
                            lease.VesselId +
                            " | Part=" +
                            lease.PartPersistentId.ToString() +
                            " | Effect=" +
                            lease.EffectType +
                            " | Error=" +
                            ex.GetType().Name);
                    }
                }

                _restore.Remove(
                    lease.RestoreKey);

                if (expired == null)
                {
                    expired =
                        new List<string>();
                }

                expired.Add(
                    pair.Key);

                Debug.Log(
                    "[KMC] FAILURE EFFECT FAILSAFE" +
                    " | VesselId=" +
                    lease.VesselId +
                    " | Part=" +
                    lease.PartPersistentId.ToString() +
                    " | Effect=" +
                    lease.EffectType +
                    " | Action=LEASE EXPIRED / EFFECT RESTORED");
            }

            if (expired == null)
            {
                return;
            }

            for (int index = 0;
                 index < expired.Count;
                 index++)
            {
                _propulsionEffectLeases.Remove(
                    expired[index]);
            }
        }

        private void ApplyReactionWheelAuthority(
            FailureEffectPacket packet,
            Part part)
        {
            PartModule wheel =
                FindModule(
                    part,
                    "ModuleReactionWheel");

            if (wheel == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "TARGET PART HAS NO REACTION WHEEL MODULE");

                return;
            }

            string key =
                RestoreKey(
                    packet,
                    FailureEffectType.ReactionWheelAuthority);

            if (packet.Operation ==
                    FailureEffectOperation.Restore)
            {
                EffectRestoreState state;

                if (!_restore.TryGetValue(
                        key,
                        out state) ||
                    !state.Primary.HasValue ||
                    !state.Secondary.HasValue ||
                    !state.Tertiary.HasValue)
                {
                    Complete(
                        packet,
                        "REJECTED",
                        double.NaN,
                        "NO CACHED REACTION WHEEL STATE");

                    return;
                }

                SetNumericMember(
                    wheel,
                    "PitchTorque",
                    state.Primary.Value);

                SetNumericMember(
                    wheel,
                    "YawTorque",
                    state.Secondary.Value);

                SetNumericMember(
                    wheel,
                    "RollTorque",
                    state.Tertiary.Value);

                _restore.Remove(
                    key);

                _propulsionEffectLeases.Remove(
                    key);

                Complete(
                    packet,
                    "RESTORED",
                    state.Primary.Value,
                    "REACTION WHEEL TORQUE RESTORED");

                return;
            }

            if (packet.Operation !=
                    FailureEffectOperation.Apply ||
                packet.Magnitude <
                    MinimumWheelFactor ||
                packet.Magnitude >
                    MaximumWheelFactor)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "REACTION WHEEL FACTOR MUST BE 0.00..1.00");

                return;
            }

            double pitch =
                GetNumericMember(
                    wheel,
                    "PitchTorque");

            double yaw =
                GetNumericMember(
                    wheel,
                    "YawTorque");

            double roll =
                GetNumericMember(
                    wheel,
                    "RollTorque");

            if (!_restore.ContainsKey(key))
            {
                _restore[key] =
                    new EffectRestoreState
                    {
                        Primary = pitch,
                        Secondary = yaw,
                        Tertiary = roll
                    };
            }
            else
            {
                EffectRestoreState state =
                    _restore[key];

                pitch = state.Primary.Value;
                yaw = state.Secondary.Value;
                roll = state.Tertiary.Value;
            }

            SetNumericMember(
                wheel,
                "PitchTorque",
                pitch * packet.Magnitude);

            SetNumericMember(
                wheel,
                "YawTorque",
                yaw * packet.Magnitude);

            SetNumericMember(
                wheel,
                "RollTorque",
                roll * packet.Magnitude);

            double observed =
                GetNumericMember(
                    wheel,
                    "PitchTorque");

            RefreshPropulsionEffectLease(
                packet,
                FailureEffectType.ReactionWheelAuthority,
                key);

            Complete(
                packet,
                "APPLIED",
                observed,
                "REACTION WHEEL TORQUE FACTOR " +
                packet.Magnitude.ToString("0.00"));
        }

        private void ApplyEcDrain(
            FailureEffectPacket packet,
            Vessel vessel)
        {
            if (packet.Operation !=
                    FailureEffectOperation.Pulse ||
                packet.Magnitude <
                    MinimumEcPulse ||
                packet.Magnitude >
                    MaximumEcPulse)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "EC DRAIN PULSE MUST BE 0.10..25.00");

                return;
            }

            if (vessel == null ||
                vessel.rootPart == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "VESSEL ROOT PART UNAVAILABLE");

                return;
            }

            MethodInfo requestResource =
                FindRequestResourceMethod(
                    vessel.rootPart.GetType());

            if (requestResource == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "KSP REQUESTRESOURCE API NOT FOUND");

                return;
            }

            object result =
                requestResource.Invoke(
                    vessel.rootPart,
                    new object[]
                    {
                        "ElectricCharge",
                        packet.Magnitude
                    });

            double consumed =
                result != null
                    ? Convert.ToDouble(result)
                    : 0.0;

            Complete(
                packet,
                "APPLIED",
                consumed,
                "ELECTRICCHARGE DRAIN PULSE REQUESTED " +
                packet.Magnitude.ToString("0.00") +
                " EC");
        }

        private void ApplyEcLeakCommand(
            FailureEffectPacket packet,
            Vessel vessel)
        {
            if (packet.Operation ==
                    FailureEffectOperation.Restore)
            {
                _ecLeaks.Remove(
                    packet.VesselId ?? string.Empty);

                Complete(
                    packet,
                    "RESTORED",
                    0.0,
                    "CONTINUOUS ELECTRICCHARGE LOAD REMOVED");

                return;
            }

            if (packet.Operation !=
                    FailureEffectOperation.Apply ||
                packet.Magnitude <
                    MinimumEcLeakRate ||
                packet.Magnitude >
                    MaximumEcLeakRate)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "EC LEAK RATE MUST BE 0.10..10.00 EC/S");

                return;
            }

            if (vessel == null ||
                vessel.rootPart == null)
            {
                Complete(
                    packet,
                    "REJECTED",
                    double.NaN,
                    "VESSEL ROOT PART UNAVAILABLE");

                return;
            }

            string vesselId =
                packet.VesselId ?? string.Empty;

            _ecLeaks[vesselId] =
                new ContinuousEcLeakState
                {
                    VesselId = vesselId,
                    RateEcPerSecond = packet.Magnitude,
                    LastRefreshRealtime =
                        Time.realtimeSinceStartup
                };

            Complete(
                packet,
                "APPLIED",
                packet.Magnitude,
                "CONTINUOUS ELECTRICCHARGE LOAD LEASED AT " +
                packet.Magnitude.ToString("0.00") +
                " EC/S");
        }

        private void ApplyContinuousEcLeaks()
        {
            float now =
                Time.realtimeSinceStartup;

            float elapsed =
                now -
                _lastEcLeakApplyTime;

            if (_lastEcLeakApplyTime <= 0f)
            {
                _lastEcLeakApplyTime = now;
                return;
            }

            if (elapsed <
                EcLeakApplyIntervalSeconds)
            {
                return;
            }

            _lastEcLeakApplyTime = now;

            elapsed =
                Math.Min(
                    0.50f,
                    Math.Max(
                        0f,
                        elapsed));

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            List<string> expired =
                null;

            foreach (
                KeyValuePair<string, ContinuousEcLeakState> pair
                in _ecLeaks)
            {
                ContinuousEcLeakState state =
                    pair.Value;

                if (state == null)
                {
                    continue;
                }

                if (now -
                    state.LastRefreshRealtime >
                    EcLeakLeaseSeconds)
                {
                    if (expired == null)
                    {
                        expired =
                            new List<string>();
                    }

                    expired.Add(
                        pair.Key);

                    Debug.Log(
                        "[KMC] POWER FAILURE EFFECT FAILSAFE" +
                        " | VesselId=" +
                        state.VesselId +
                        " | Effect=ElectricChargeLeak" +
                        " | Action=LEASE EXPIRED / LOAD REMOVED");

                    continue;
                }

                if (vessel == null ||
                    vessel.rootPart == null ||
                    !string.Equals(
                        vessel.id.ToString(),
                        state.VesselId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MethodInfo requestResource =
                    FindRequestResourceMethod(
                        vessel.rootPart.GetType());

                if (requestResource == null)
                {
                    continue;
                }

                double request =
                    state.RateEcPerSecond *
                    elapsed;

                if (request <= 0.0)
                {
                    continue;
                }

                try
                {
                    requestResource.Invoke(
                        vessel.rootPart,
                        new object[]
                        {
                            "ElectricCharge",
                            request
                        });
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[KMC] Continuous EC failure load failed: " +
                        ex.GetType().Name);
                }
            }

            if (expired != null)
            {
                for (int index = 0;
                     index < expired.Count;
                     index++)
                {
                    _ecLeaks.Remove(
                        expired[index]);
                }
            }
        }

        private void Complete(
            FailureEffectPacket packet,
            string status,
            double observedValue,
            string detail)
        {
            FailureEffectAck ack =
                new FailureEffectAck
                {
                    VesselId =
                        packet.VesselId ?? string.Empty,
                    CommandId =
                        packet.CommandId ?? string.Empty,
                    Status =
                        status ?? string.Empty,
                    EffectType =
                        packet.EffectType,
                    PartPersistentId =
                        packet.PartPersistentId,
                    ObservedValue =
                        observedValue,
                    Detail =
                        detail ?? string.Empty
                };

            RememberAck(
                ack);

            SendAck(
                ack);

            _lastStatus =
                ack.Status +
                " " +
                ack.EffectType.ToString().ToUpperInvariant();

            Debug.Log(
                "[KMC] FAILURE EFFECT ACK" +
                " | VesselId=" +
                ack.VesselId +
                " | CommandId=" +
                ack.CommandId +
                " | Effect=" +
                ack.EffectType +
                " | Status=" +
                ack.Status +
                " | Part=" +
                ack.PartPersistentId.ToString() +
                " | Observed=" +
                FormatObserved(
                    ack.ObservedValue) +
                " | Detail=" +
                ack.Detail);
        }

        private void RememberAck(
            FailureEffectAck ack)
        {
            if (ack == null ||
                string.IsNullOrWhiteSpace(
                    ack.CommandId))
            {
                return;
            }

            _completed[ack.CommandId] =
                ack;

            _completedOrder.Enqueue(
                ack.CommandId);

            while (_completedOrder.Count > 64)
            {
                string remove =
                    _completedOrder.Dequeue();

                _completed.Remove(
                    remove);
            }
        }

        private void SendAck(
            FailureEffectAck ack)
        {
            if (_ackClient == null ||
                _ackEndpoint == null ||
                ack == null)
            {
                return;
            }

            try
            {
                byte[] data =
                    Encoding.UTF8.GetBytes(
                        ack.Serialize());

                _ackClient.Send(
                    data,
                    data.Length,
                    _ackEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Failure effect ACK send failed: " +
                    ex);
            }
        }

        private static Part FindFirstPartWithModule(
            Vessel vessel,
            params string[] moduleNames)
        {
            if (vessel == null ||
                vessel.parts == null)
            {
                return null;
            }

            for (int index = 0;
                 index < vessel.parts.Count;
                 index++)
            {
                Part part =
                    vessel.parts[index];

                if (FindModule(
                        part,
                        moduleNames) != null)
                {
                    return part;
                }
            }

            return null;
        }

        private static Part FindPartByPersistentId(
            Vessel vessel,
            uint commandPartId)
        {
            if (vessel == null ||
                vessel.parts == null ||
                commandPartId == 0)
            {
                return null;
            }

            Part matchedPart = null;

            for (int index = 0;
                 index < vessel.parts.Count;
                 index++)
            {
                Part part =
                    vessel.parts[index];

                if (part == null)
                {
                    continue;
                }

                /*
                 * KMC topology and propulsion telemetry use Part.flightID as
                 * their canonical live-vessel identity. The original 14.4
                 * F9 test panel sends Part.persistentId.
                 *
                 * Accept either exact identity so existing 14.4 tests remain
                 * compatible while 14.6 can target the same PartId seen by
                 * PROP/topology. If two different parts somehow collide across
                 * these identity domains, reject by returning null rather than
                 * mutating an ambiguous target.
                 */
                bool matches =
                    part.flightID ==
                        commandPartId ||
                    GetPersistentId(part) ==
                        commandPartId;

                if (!matches)
                {
                    continue;
                }

                if (matchedPart != null &&
                    !object.ReferenceEquals(
                        matchedPart,
                        part))
                {
                    return null;
                }

                matchedPart =
                    part;
            }

            return matchedPart;
        }

        private static PartModule FindModule(
            Part part,
            params string[] moduleNames)
        {
            if (part == null ||
                part.Modules == null ||
                moduleNames == null)
            {
                return null;
            }

            for (int index = 0;
                 index < part.Modules.Count;
                 index++)
            {
                PartModule module =
                    part.Modules[index];

                if (module == null)
                {
                    continue;
                }

                string moduleName =
                    module.moduleName ?? string.Empty;

                for (int nameIndex = 0;
                     nameIndex < moduleNames.Length;
                     nameIndex++)
                {
                    if (string.Equals(
                            moduleName,
                            moduleNames[nameIndex],
                            StringComparison.Ordinal))
                    {
                        return module;
                    }
                }
            }

            return null;
        }

        private static uint GetPersistentId(
            Part part)
        {
            if (part == null)
            {
                return 0;
            }

            object value =
                GetMemberValue(
                    part,
                    "persistentId");

            if (value == null)
            {
                return 0;
            }

            try
            {
                return
                    Convert.ToUInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static string DescribePart(
            Part part)
        {
            if (part == null)
            {
                return "---";
            }

            string title =
                part.partInfo != null &&
                !string.IsNullOrWhiteSpace(
                    part.partInfo.title)
                    ? part.partInfo.title
                    : part.name;

            return
                title +
                " / PART " +
                GetPersistentId(part).ToString();
        }

        private static string RestoreKey(
            FailureEffectPacket packet,
            FailureEffectType type)
        {
            return
                (packet.VesselId ?? string.Empty) +
                "|" +
                packet.PartPersistentId.ToString() +
                "|" +
                type.ToString();
        }

        private static double GetNumericMember(
            object target,
            string name)
        {
            object value =
                GetMemberValue(
                    target,
                    name);

            if (value == null)
            {
                throw new MissingMemberException(
                    target != null
                        ? target.GetType().FullName
                        : string.Empty,
                    name);
            }

            return
                Convert.ToDouble(value);
        }

        private static bool GetBooleanMember(
            object target,
            string name,
            bool fallback)
        {
            object value =
                GetMemberValue(
                    target,
                    name);

            if (value == null)
            {
                return fallback;
            }

            try
            {
                return
                    Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static object GetMemberValue(
            object target,
            string name)
        {
            if (target == null ||
                string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Type type =
                target.GetType();

            FieldInfo field =
                FindField(
                    type,
                    name);

            if (field != null)
            {
                return
                    field.GetValue(target);
            }

            PropertyInfo property =
                FindProperty(
                    type,
                    name);

            if (property != null &&
                property.CanRead)
            {
                return
                    property.GetValue(
                        target,
                        null);
            }

            return null;
        }

        private static void SetNumericMember(
            object target,
            string name,
            double value)
        {
            if (target == null)
            {
                throw new ArgumentNullException(
                    "target");
            }

            Type type =
                target.GetType();

            FieldInfo field =
                FindField(
                    type,
                    name);

            if (field != null)
            {
                field.SetValue(
                    target,
                    Convert.ChangeType(
                        value,
                        field.FieldType));

                return;
            }

            PropertyInfo property =
                FindProperty(
                    type,
                    name);

            if (property != null &&
                property.CanWrite)
            {
                property.SetValue(
                    target,
                    Convert.ChangeType(
                        value,
                        property.PropertyType),
                    null);

                return;
            }

            throw new MissingMemberException(
                type.FullName,
                name);
        }

        private static FieldInfo FindField(
            Type type,
            string name)
        {
            while (type != null)
            {
                FieldInfo field =
                    type.GetField(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    return field;
                }

                type =
                    type.BaseType;
            }

            return null;
        }

        private static PropertyInfo FindProperty(
            Type type,
            string name)
        {
            while (type != null)
            {
                PropertyInfo property =
                    type.GetProperty(
                        name,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (property != null)
                {
                    return property;
                }

                type =
                    type.BaseType;
            }

            return null;
        }

        private static void InvokeParameterless(
            object target,
            string methodName)
        {
            if (target == null)
            {
                throw new ArgumentNullException(
                    "target");
            }

            MethodInfo method =
                target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);

            if (method == null)
            {
                throw new MissingMethodException(
                    target.GetType().FullName,
                    methodName);
            }

            method.Invoke(
                target,
                null);
        }

        private static MethodInfo FindRequestResourceMethod(
            Type type)
        {
            while (type != null)
            {
                MethodInfo[] methods =
                    type.GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                for (int index = 0;
                     index < methods.Length;
                     index++)
                {
                    MethodInfo method =
                        methods[index];

                    if (!string.Equals(
                            method.Name,
                            "RequestResource",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters =
                        method.GetParameters();

                    if (parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                            typeof(string) &&
                        parameters[1].ParameterType ==
                            typeof(double))
                    {
                        return method;
                    }
                }

                type =
                    type.BaseType;
            }

            return null;
        }

        private static string FormatObserved(
            double value)
        {
            return
                double.IsNaN(value) ||
                double.IsInfinity(value)
                    ? "---"
                    : value.ToString("0.###");
        }

        public void OnDestroy()
        {
            _running = false;
            _ecLeaks.Clear();

            if (_receiveClient != null)
            {
                _receiveClient.Close();
                _receiveClient = null;
            }

            if (_ackClient != null)
            {
                _ackClient.Close();
                _ackClient = null;
            }

            if (_testSendClient != null)
            {
                _testSendClient.Close();
                _testSendClient = null;
            }

            if (_receiveThread != null &&
                _receiveThread.IsAlive)
            {
                _receiveThread.Join(250);
            }

            _receiveThread = null;
        }

        private sealed class ContinuousEcLeakState
        {
            public string VesselId;
            public double RateEcPerSecond;
            public float LastRefreshRealtime;
        }

        private sealed class PropulsionEffectLeaseState
        {
            public string VesselId = string.Empty;
            public uint PartPersistentId;
            public FailureEffectType EffectType;
            public string RestoreKey = string.Empty;
            public float LastRefreshRealtime;
        }

        private sealed class EffectRestoreState
        {
            public double? Primary;
            public double? Secondary;
            public double? Tertiary;
            public bool? Flag;
        }
    }
}
