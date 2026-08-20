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
    /// KMC Build 14.18.7
    ///
    /// Vessel-wide RCS authority executor.
    ///
    /// INHIBIT:
    /// - applies KMC's named KSP RCS input lock;
    /// - disables actual ModuleRCS / ModuleRCSFX PartModules;
    /// - does NOT force the RCS action-group state OFF;
    /// - does NOT patch IVA controls.
    ///
    /// The inhibit is a short lease. If Mission Control stops refreshing it,
    /// the receiver restores every tracked module to its exact prior enabled
    /// state and removes only KMC's named input lock.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class KmcRcsAuthorityReceiver :
        MonoBehaviour
    {
        private const string LockId =
            "KMC_RCS_AUTHORITY";

        private const float LeaseSeconds =
            2.50f;

        private readonly object _syncRoot =
            new object();

        private readonly Queue<RcsAuthorityPacket> _pending =
            new Queue<RcsAuthorityPacket>();

        private readonly Dictionary<string, LeaseState> _leases =
            new Dictionary<string, LeaseState>(
                StringComparer.Ordinal);

        private UdpClient _receiveClient;
        private Thread _receiveThread;
        private volatile bool _running;

        private bool _rcsControlTypeResolved;
        private ControlTypes _rcsControlType;

        public void Start()
        {
            ResolveRcsControlType();

            try
            {
                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            RcsAuthorityPacket
                                .CommandPort));

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop)
                    {
                        IsBackground = true,
                        Name =
                            "KMC RCS Authority"
                    };

                _receiveThread.Start();

                Debug.Log(
                    "[KMC] RCS authority receiver started on UDP " +
                    RcsAuthorityPacket.CommandPort
                        .ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] RCS authority receiver start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            ProcessPending();
            MaintainLeases();
        }

        private void ProcessPending()
        {
            while (true)
            {
                RcsAuthorityPacket packet =
                    null;

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

                if (packet.Operation ==
                    RcsAuthorityOperation.Inhibit)
                {
                    ApplyInhibit(
                        packet);
                }
                else
                {
                    ApplyRestore(
                        packet.VesselId,
                        "RESTORE COMMAND");
                }
            }
        }

        private void ApplyInhibit(
            RcsAuthorityPacket packet)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(
                    packet.VesselId))
            {
                return;
            }

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null ||
                !string.Equals(
                    vessel.id.ToString(),
                    packet.VesselId,
                    StringComparison.Ordinal))
            {
                /*
                 * Never apply a vehicle-wide effect to a different active
                 * vessel. Mission Control will refresh when the intended
                 * vessel is active.
                 */
                return;
            }

            LeaseState state;

            if (!_leases.TryGetValue(
                    packet.VesselId,
                    out state) ||
                state == null)
            {
                state =
                    new LeaseState
                    {
                        VesselId =
                            packet.VesselId
                    };

                _leases[packet.VesselId] =
                    state;
            }

            state.LastRefreshRealtime =
                Time.realtimeSinceStartup;

            DiscoverAndDisable(
                vessel,
                state);

            UpdateInputLock();

            Debug.Log(
                "[KMC] RCS AUTHORITY INHIBIT" +
                " | VesselId=" +
                state.VesselId +
                " | ModulesTracked=" +
                state.PriorEnabled.Count
                    .ToString());
        }

        private void MaintainLeases()
        {
            float now =
                Time.realtimeSinceStartup;

            List<string> expired =
                null;

            foreach (
                KeyValuePair<string, LeaseState> pair
                in _leases)
            {
                LeaseState state =
                    pair.Value;

                if (state == null)
                {
                    continue;
                }

                if (now -
                    state.LastRefreshRealtime >
                    LeaseSeconds)
                {
                    RestoreState(
                        state);

                    if (expired == null)
                    {
                        expired =
                            new List<string>();
                    }

                    expired.Add(
                        pair.Key);

                    Debug.Log(
                        "[KMC] RCS AUTHORITY FAILSAFE" +
                        " | VesselId=" +
                        state.VesselId +
                        " | Action=LEASE EXPIRED / AUTHORITY RESTORED");

                    continue;
                }

                Vessel vessel =
                    FlightGlobals.ActiveVessel;

                if (vessel != null &&
                    string.Equals(
                        vessel.id.ToString(),
                        state.VesselId,
                        StringComparison.Ordinal))
                {
                    /*
                     * Re-discover every frame while leased so staging/docking
                     * changes cannot create a newly-added active RCS module.
                     * Existing modules are never re-cached after inhibition.
                     */
                    DiscoverAndDisable(
                        vessel,
                        state);
                }
            }

            if (expired != null)
            {
                for (int i = 0;
                     i < expired.Count;
                     i++)
                {
                    _leases.Remove(
                        expired[i]);
                }
            }

            UpdateInputLock();
        }

        private static void DiscoverAndDisable(
            Vessel vessel,
            LeaseState state)
        {
            if (vessel == null ||
                state == null ||
                vessel.parts == null)
            {
                return;
            }

            for (int p = 0;
                 p < vessel.parts.Count;
                 p++)
            {
                Part part =
                    vessel.parts[p];

                if (part == null ||
                    part.Modules == null)
                {
                    continue;
                }

                for (int m = 0;
                     m < part.Modules.Count;
                     m++)
                {
                    PartModule module =
                        part.Modules[m];

                    if (!IsRcsModule(
                            module))
                    {
                        continue;
                    }

                    if (!state.PriorEnabled
                            .ContainsKey(module))
                    {
                        state.PriorEnabled[module] =
                            module.enabled;
                    }

                    if (module.enabled)
                    {
                        module.enabled = false;
                    }
                }
            }
        }

        private static bool IsRcsModule(
            PartModule module)
        {
            if (module == null)
            {
                return false;
            }

            string moduleName =
                module.moduleName ??
                string.Empty;

            string typeName =
                module.GetType().Name ??
                string.Empty;

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

        private void ApplyRestore(
            string vesselId,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(
                    vesselId))
            {
                return;
            }

            LeaseState state;

            if (_leases.TryGetValue(
                    vesselId,
                    out state) &&
                state != null)
            {
                RestoreState(
                    state);

                _leases.Remove(
                    vesselId);
            }

            UpdateInputLock();

            Debug.Log(
                "[KMC] RCS AUTHORITY RESTORE" +
                " | VesselId=" +
                vesselId +
                " | Reason=" +
                reason);
        }

        private static void RestoreState(
            LeaseState state)
        {
            if (state == null)
            {
                return;
            }

            foreach (
                KeyValuePair<PartModule, bool> pair
                in state.PriorEnabled)
            {
                PartModule module =
                    pair.Key;

                if (module == null)
                {
                    continue;
                }

                try
                {
                    module.enabled =
                        pair.Value;
                }
                catch
                {
                }
            }

            state.PriorEnabled.Clear();
        }

        private void UpdateInputLock()
        {
            if (!_rcsControlTypeResolved)
            {
                return;
            }

            bool shouldLock =
                false;

            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel != null)
            {
                LeaseState active;

                shouldLock =
                    _leases.TryGetValue(
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
                {
                    InputLockManager
                        .SetControlLock(
                            _rcsControlType,
                            LockId);
                }
                else
                {
                    InputLockManager
                        .RemoveControlLock(
                            LockId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] RCS authority input-lock update failed: " +
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

                _rcsControlTypeResolved =
                    true;
            }
            catch (Exception ex)
            {
                _rcsControlTypeResolved =
                    false;

                Debug.LogError(
                    "[KMC] Could not resolve ControlTypes.RCS: " +
                    ex);
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

                    RcsAuthorityPacket packet;

                    if (!RcsAuthorityPacket.TryParse(
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
                        "[KMC] RCS authority receive failed: " +
                        ex);
                }
            }
        }

        public void OnDestroy()
        {
            _running = false;

            foreach (
                KeyValuePair<string, LeaseState> pair
                in _leases)
            {
                RestoreState(
                    pair.Value);
            }

            _leases.Clear();

            try
            {
                InputLockManager.RemoveControlLock(
                    LockId);
            }
            catch
            {
            }

            if (_receiveClient != null)
            {
                _receiveClient.Close();
                _receiveClient = null;
            }

            if (_receiveThread != null &&
                _receiveThread.IsAlive)
            {
                _receiveThread.Join(250);
            }

            _receiveThread = null;
        }

        private sealed class LeaseState
        {
            public string VesselId =
                string.Empty;

            public float LastRefreshRealtime;

            public readonly Dictionary<PartModule, bool>
                PriorEnabled =
                    new Dictionary<PartModule, bool>();
        }
    }
}
