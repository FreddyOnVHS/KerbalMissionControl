using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.18.2 loopback-only IVA annunciator test receiver.
    /// Test state is separate from telemetry and failure truth.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KmcIvaAnnunciatorTestReceiver : MonoBehaviour
    {
        private sealed class TestState
        {
            public bool Active;
            public DateTime UpdatedUtc;
        }

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Dictionary<IvaAnnunciatorTestId, TestState>> States =
            new Dictionary<string, Dictionary<IvaAnnunciatorTestId, TestState>>(StringComparer.Ordinal);
        private static readonly TimeSpan SafetyLease = TimeSpan.FromMinutes(30.0);

        private UdpClient _udp;
        private Thread _thread;
        private volatile bool _running;

        public void Awake()
        {
            lock (SyncRoot)
                States.Clear();

            try
            {
                _udp =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            IvaAnnunciatorTestPacket.CommandPort));

                _udp.Client.ReceiveTimeout = 500;
                _running = true;
                _thread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KMC IVA Annunciator Test"
                };
                _thread.Start();

                Debug.Log(
                    "[KMC] IVA annunciator test receiver started on loopback UDP " +
                    IvaAnnunciatorTestPacket.CommandPort.ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                _running = false;
                Debug.LogWarning(
                    "[KMC] IVA annunciator test receiver unavailable: " +
                    ex.GetType().Name +
                    " / " +
                    ex.Message);
            }
        }

        public void OnDestroy()
        {
            _running = false;
            try { if (_udp != null) _udp.Close(); } catch { }
            try { if (_thread != null && _thread.IsAlive) _thread.Join(750); } catch { }

            lock (SyncRoot)
                States.Clear();

            _udp = null;
            _thread = null;
        }

        public static bool IsActive(
            string vesselId,
            IvaAnnunciatorTestId testId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return false;

            KmcMfdStatusPacket status;
            if (!KmcMfdStatusReceiver.TryGetStatus(vesselId, out status))
            {
                lock (SyncRoot)
                    States.Remove(vesselId);
                return false;
            }

            lock (SyncRoot)
            {
                Dictionary<IvaAnnunciatorTestId, TestState> vesselStates;
                if (!States.TryGetValue(vesselId, out vesselStates) ||
                    vesselStates == null)
                    return false;

                TestState state;
                if (!vesselStates.TryGetValue(testId, out state) ||
                    state == null)
                    return false;

                if ((DateTime.UtcNow - state.UpdatedUtc) > SafetyLease)
                {
                    vesselStates.Remove(testId);
                    if (vesselStates.Count == 0)
                        States.Remove(vesselId);
                    return false;
                }

                return state.Active;
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint remote = new IPEndPoint(IPAddress.Loopback, 0);

            while (_running)
            {
                try
                {
                    byte[] bytes = _udp.Receive(ref remote);
                    if (bytes == null ||
                        bytes.Length == 0 ||
                        remote == null ||
                        !IPAddress.IsLoopback(remote.Address))
                        continue;

                    IvaAnnunciatorTestPacket packet;
                    if (!IvaAnnunciatorTestPacket.TryParse(
                            Encoding.UTF8.GetString(bytes),
                            out packet))
                        continue;

                    Apply(packet);
                }
                catch (SocketException ex)
                {
                    if (!_running)
                        return;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        continue;
                    Debug.LogWarning(
                        "[KMC] IVA annunciator test receive error: " +
                        ex.SocketErrorCode.ToString());
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogWarning(
                            "[KMC] IVA annunciator test receive exception: " +
                            ex.GetType().Name);
                }
            }
        }

        private static void Apply(
            IvaAnnunciatorTestPacket packet)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(packet.VesselId))
                return;

            lock (SyncRoot)
            {
                if (packet.Operation == IvaAnnunciatorTestOperation.ClearAll)
                {
                    States.Remove(packet.VesselId);
                    return;
                }

                Dictionary<IvaAnnunciatorTestId, TestState> vesselStates;
                if (!States.TryGetValue(packet.VesselId, out vesselStates) ||
                    vesselStates == null)
                {
                    vesselStates =
                        new Dictionary<IvaAnnunciatorTestId, TestState>();
                    States[packet.VesselId] = vesselStates;
                }

                vesselStates[packet.TestId] =
                    new TestState
                    {
                        Active =
                            packet.Operation == IvaAnnunciatorTestOperation.On,
                        UpdatedUtc = DateTime.UtcNow
                    };
            }
        }
    }

    // ---------------------------------------------------------------------
    // Build 14.18.7 vessel-wide RCS authority executor.
    // Lives in this existing compiled file: no plugin project delta.
    // ---------------------------------------------------------------------
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class KmcRcsAuthorityReceiver : MonoBehaviour
    {
        private const string LockId =
            "KMC_RCS_AUTHORITY";

        private const float LeaseSeconds =
            2.50f;

        private readonly object _rcsSyncRoot =
            new object();

        private readonly Queue<RcsAuthorityPacket> _pendingRcs =
            new Queue<RcsAuthorityPacket>();

        private readonly Dictionary<string, RcsLeaseState> _rcsLeases =
            new Dictionary<string, RcsLeaseState>(
                StringComparer.Ordinal);

        private UdpClient _rcsUdp;
        private Thread _rcsThread;
        private volatile bool _rcsRunning;

        private bool _rcsControlTypeResolved;
        private ControlTypes _rcsControlType;

        public void Start()
        {
            ResolveRcsControlType();

            try
            {
                _rcsUdp =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            RcsAuthorityPacket.CommandPort));

                _rcsUdp.Client.ReceiveTimeout = 500;
                _rcsRunning = true;

                _rcsThread = new Thread(RcsReceiveLoop)
                {
                    IsBackground = true,
                    Name = "KMC RCS Authority"
                };

                _rcsThread.Start();

                Debug.Log(
                    "[KMC] RCS authority receiver started on loopback UDP " +
                    RcsAuthorityPacket.CommandPort.ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                _rcsRunning = false;
                Debug.LogWarning(
                    "[KMC] RCS authority receiver unavailable: " +
                    ex.GetType().Name +
                    " / " +
                    ex.Message);
            }
        }

        public void Update()
        {
            ProcessRcsPending();
            MaintainRcsLeases();
        }

        private void ProcessRcsPending()
        {
            while (true)
            {
                RcsAuthorityPacket packet = null;

                lock (_rcsSyncRoot)
                {
                    if (_pendingRcs.Count > 0)
                        packet = _pendingRcs.Dequeue();
                }

                if (packet == null)
                    break;

                if (packet.Operation == RcsAuthorityOperation.Inhibit)
                    ApplyRcsInhibit(packet);
                else
                    ApplyRcsRestore(packet.VesselId, "RESTORE COMMAND");
            }
        }

        private void ApplyRcsInhibit(
            RcsAuthorityPacket packet)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(packet.VesselId))
                return;

            Vessel vessel = FlightGlobals.ActiveVessel;

            if (vessel == null ||
                !string.Equals(
                    vessel.id.ToString(),
                    packet.VesselId,
                    StringComparison.Ordinal))
                return;

            RcsLeaseState state;

            if (!_rcsLeases.TryGetValue(packet.VesselId, out state) ||
                state == null)
            {
                state = new RcsLeaseState
                {
                    VesselId = packet.VesselId
                };

                _rcsLeases[packet.VesselId] = state;
            }

            state.LastRefreshRealtime =
                Time.realtimeSinceStartup;

            DiscoverAndDisableRcs(vessel, state);
            UpdateRcsInputLock();
        }

        private void MaintainRcsLeases()
        {
            float now = Time.realtimeSinceStartup;
            List<string> expired = null;
            Vessel vessel = FlightGlobals.ActiveVessel;

            foreach (
                KeyValuePair<string, RcsLeaseState> pair
                in _rcsLeases)
            {
                RcsLeaseState state = pair.Value;
                if (state == null)
                    continue;

                if (now - state.LastRefreshRealtime > LeaseSeconds)
                {
                    RestoreRcsState(state);

                    if (expired == null)
                        expired = new List<string>();

                    expired.Add(pair.Key);
                    continue;
                }

                if (vessel != null &&
                    string.Equals(
                        vessel.id.ToString(),
                        state.VesselId,
                        StringComparison.Ordinal))
                {
                    DiscoverAndDisableRcs(vessel, state);
                }
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                    _rcsLeases.Remove(expired[i]);
            }

            UpdateRcsInputLock();
        }

        private static void DiscoverAndDisableRcs(
            Vessel vessel,
            RcsLeaseState state)
        {
            if (vessel == null ||
                state == null ||
                vessel.parts == null)
                return;

            for (int p = 0; p < vessel.parts.Count; p++)
            {
                Part part = vessel.parts[p];

                if (part == null ||
                    part.Modules == null)
                    continue;

                for (int m = 0; m < part.Modules.Count; m++)
                {
                    PartModule module = part.Modules[m];

                    if (!IsRcsModule(module))
                        continue;

                    if (!state.PriorEnabled.ContainsKey(module))
                        state.PriorEnabled[module] = module.enabled;

                    if (module.enabled)
                        module.enabled = false;
                }
            }
        }

        private static bool IsRcsModule(
            PartModule module)
        {
            if (module == null)
                return false;

            string moduleName =
                module.moduleName ?? string.Empty;

            string typeName =
                module.GetType().Name ?? string.Empty;

            return
                string.Equals(
                    moduleName,
                    "ModuleRCS",
                    StringComparison.Ordinal) ||
                string.Equals(
                    moduleName,
                    "ModuleRCSFX",
                    StringComparison.Ordinal) ||
                string.Equals(
                    typeName,
                    "ModuleRCS",
                    StringComparison.Ordinal) ||
                string.Equals(
                    typeName,
                    "ModuleRCSFX",
                    StringComparison.Ordinal);
        }

        private void ApplyRcsRestore(
            string vesselId,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            RcsLeaseState state;

            if (_rcsLeases.TryGetValue(vesselId, out state) &&
                state != null)
            {
                RestoreRcsState(state);
                _rcsLeases.Remove(vesselId);
            }

            UpdateRcsInputLock();

            Debug.Log(
                "[KMC] RCS authority restore | VesselId=" +
                vesselId +
                " | Reason=" +
                reason);
        }

        private static void RestoreRcsState(
            RcsLeaseState state)
        {
            if (state == null)
                return;

            foreach (
                KeyValuePair<PartModule, bool> pair
                in state.PriorEnabled)
            {
                PartModule module = pair.Key;

                if (module == null)
                    continue;

                try
                {
                    module.enabled = pair.Value;
                }
                catch
                {
                }
            }

            state.PriorEnabled.Clear();
        }

        private void UpdateRcsInputLock()
        {
            if (!_rcsControlTypeResolved)
                return;

            bool shouldLock = false;
            Vessel vessel = FlightGlobals.ActiveVessel;

            if (vessel != null)
            {
                RcsLeaseState active;

                shouldLock =
                    _rcsLeases.TryGetValue(
                        vessel.id.ToString(),
                        out active) &&
                    active != null &&
                    Time.realtimeSinceStartup -
                        active.LastRefreshRealtime <=
                    LeaseSeconds;
            }

            try
            {
                if (shouldLock)
                    InputLockManager.SetControlLock(
                        _rcsControlType,
                        LockId);
                else
                    InputLockManager.RemoveControlLock(
                        LockId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] RCS input-lock update failed: " +
                    ex.GetType().Name);
            }
        }

        private void ResolveRcsControlType()
        {
            try
            {
                object parsed =
                    Enum.Parse(
                        typeof(ControlTypes),
                        "RCS",
                        true);

                _rcsControlType =
                    (ControlTypes)parsed;

                _rcsControlTypeResolved = true;
            }
            catch (Exception ex)
            {
                _rcsControlTypeResolved = false;

                Debug.LogError(
                    "[KMC] Could not resolve ControlTypes.RCS: " +
                    ex);
            }
        }

        private void RcsReceiveLoop()
        {
            IPEndPoint remote =
                new IPEndPoint(
                    IPAddress.Loopback,
                    0);

            while (_rcsRunning)
            {
                try
                {
                    byte[] bytes =
                        _rcsUdp.Receive(ref remote);

                    if (bytes == null ||
                        bytes.Length == 0 ||
                        remote == null ||
                        !IPAddress.IsLoopback(remote.Address))
                        continue;

                    RcsAuthorityPacket packet;

                    if (!RcsAuthorityPacket.TryParse(
                            Encoding.UTF8.GetString(bytes),
                            out packet))
                        continue;

                    lock (_rcsSyncRoot)
                    {
                        _pendingRcs.Enqueue(packet);
                    }
                }
                catch (SocketException ex)
                {
                    if (!_rcsRunning)
                        return;

                    if (ex.SocketErrorCode == SocketError.TimedOut)
                        continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (_rcsRunning)
                    {
                        Debug.LogWarning(
                            "[KMC] RCS authority receive exception: " +
                            ex.GetType().Name);
                    }
                }
            }
        }

        public void OnDestroy()
        {
            _rcsRunning = false;

            foreach (
                KeyValuePair<string, RcsLeaseState> pair
                in _rcsLeases)
            {
                RestoreRcsState(pair.Value);
            }

            _rcsLeases.Clear();

            try
            {
                InputLockManager.RemoveControlLock(LockId);
            }
            catch
            {
            }

            try
            {
                if (_rcsUdp != null)
                    _rcsUdp.Close();
            }
            catch
            {
            }

            try
            {
                if (_rcsThread != null &&
                    _rcsThread.IsAlive)
                    _rcsThread.Join(750);
            }
            catch
            {
            }

            _rcsUdp = null;
            _rcsThread = null;
        }

        private sealed class RcsLeaseState
        {
            public string VesselId = string.Empty;
            public float LastRefreshRealtime;

            public readonly Dictionary<PartModule, bool>
                PriorEnabled =
                    new Dictionary<PartModule, bool>();
        }
    }
}
