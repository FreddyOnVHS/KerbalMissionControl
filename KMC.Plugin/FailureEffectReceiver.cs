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
                    FailureEffectType.ElectricChargeDrain)
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
                        "EXACT PART ID NOT FOUND ON ACTIVE VESSEL");

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

            Complete(
                packet,
                "APPLIED",
                after ? 1.0 : 0.0,
                "ENGINE SHUTDOWN COMMAND EXECUTED");
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
            uint persistentId)
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

                if (part != null &&
                    GetPersistentId(part) ==
                        persistentId)
                {
                    return part;
                }
            }

            return null;
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

        private sealed class EffectRestoreState
        {
            public double? Primary;
            public double? Secondary;
            public double? Tertiary;
            public bool? Flag;
        }
    }
}
