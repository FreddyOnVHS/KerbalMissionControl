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
    /// KMC Build 14.19.1
    ///
    /// Executes KMC-owned vessel-level command-authority leases.
    ///
    /// SAS:
    ///   disables ModuleSAS hardware AND suppresses VesselAutopilot while the
    ///   lease is active. Manual reaction-wheel authority is not disabled.
    ///   The pre-inhibit SAS command is restored when authority returns.
    ///
    /// GEAR:
    ///   preserves the Gear action-group command but temporarily disables the
    ///   deployment module's KSP actions/events so deploy and retract commands
    ///   cannot reach the hardware. The final retained Gear command is replayed
    ///   after authority returns.
    ///
    /// BRAKES:
    ///   disables ModuleWheelBrakes modules.
    ///
    /// LIGHTS:
    ///   preserves the Light action-group command, disables stock light-module
    ///   actions/events, forces physical light output OFF while inhibited, and
    ///   reapplies the retained command on restore.
    ///
    /// Every inhibit is a 2.5 second lease. Mission Control disappearance
    /// restores exact prior module.enabled states and light output.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class KmcSystemAuthorityReceiver :
        MonoBehaviour
    {
        private const float LeaseSeconds =
            2.50f;

        private readonly object _syncRoot =
            new object();

        private readonly Queue<SystemAuthorityPacket> _pending =
            new Queue<SystemAuthorityPacket>();

        private readonly Dictionary<string, LeaseState> _leases =
            new Dictionary<string, LeaseState>(
                StringComparer.Ordinal);

        private UdpClient _receiveClient;
        private Thread _receiveThread;
        private volatile bool _running;

        public void Start()
        {
            try
            {
                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            SystemAuthorityPacket
                                .CommandPort));

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop)
                    {
                        IsBackground = true,
                        Name =
                            "KMC System Authority"
                    };

                _receiveThread.Start();

                Debug.Log(
                    "[KMC] System authority receiver started on UDP " +
                    SystemAuthorityPacket.CommandPort
                        .ToString() +
                    ".");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] System authority receiver start failed: " +
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
                SystemAuthorityPacket packet =
                    null;

                lock (_syncRoot)
                {
                    if (_pending.Count > 0)
                        packet = _pending.Dequeue();
                }

                if (packet == null)
                    break;

                if (packet.Operation ==
                    SystemAuthorityOperation.Inhibit)
                {
                    ApplyInhibit(packet);
                }
                else
                {
                    ApplyRestore(
                        BuildKey(
                            packet.VesselId,
                            packet.Authority),
                        "RESTORE COMMAND");
                }
            }
        }

        private void ApplyInhibit(
            SystemAuthorityPacket packet)
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
                return;
            }

            string key =
                BuildKey(
                    packet.VesselId,
                    packet.Authority);

            LeaseState state;

            if (!_leases.TryGetValue(
                    key,
                    out state) ||
                state == null)
            {
                state =
                    new LeaseState
                    {
                        VesselId =
                            packet.VesselId,
                        Authority =
                            packet.Authority
                    };

                if (packet.Authority ==
                        SystemAuthorityKind.Sas &&
                    vessel.ActionGroups != null)
                {
                    state.SasCommandKnown = true;
                    state.SasCommandedOn =
                        vessel.ActionGroups[
                            KSPActionGroup.SAS];
                }

                _leases[key] =
                    state;
            }

            state.LastRefreshRealtime =
                Time.realtimeSinceStartup;

            DiscoverAndInhibit(
                vessel,
                state);

            MaintainPhysicalOutput(
                vessel,
                state);
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
                    continue;

                if (now -
                    state.LastRefreshRealtime >
                    LeaseSeconds)
                {
                    RestoreState(state);

                    if (expired == null)
                        expired =
                            new List<string>();

                    expired.Add(pair.Key);

                    Debug.Log(
                        "[KMC] SYSTEM AUTHORITY FAILSAFE" +
                        " | VesselId=" +
                        state.VesselId +
                        " | Authority=" +
                        state.Authority.ToString() +
                        " | Action=LEASE EXPIRED / RESTORED");

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
                    DiscoverAndInhibit(
                        vessel,
                        state);

                    MaintainPhysicalOutput(
                        vessel,
                        state);
                }
            }

            if (expired != null)
            {
                for (int index = 0;
                     index < expired.Count;
                     index++)
                {
                    _leases.Remove(
                        expired[index]);
                }
            }
        }

        private static string BuildKey(
            string vesselId,
            SystemAuthorityKind authority)
        {
            return
                (vesselId ?? string.Empty) +
                "|" +
                authority.ToString();
        }

        private static void DiscoverAndInhibit(
            Vessel vessel,
            LeaseState state)
        {
            if (vessel == null ||
                state == null ||
                vessel.parts == null)
            {
                return;
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
                    if (!MatchesAuthority(
                            module,
                            state.Authority))
                    {
                        continue;
                    }

                    if (!state.PriorEnabled
                            .ContainsKey(module))
                    {
                        state.PriorEnabled[module] =
                            module.enabled;
                    }

                    /*
                     * Gear and lights are edge-commanded through KSP BaseAction
                     * callbacks. PartModule.enabled alone is not a reliable
                     * authority gate: an action can still reach a module even
                     * when the module's Update path is disabled.
                     *
                     * Preserve the crew's vessel action-group command but make
                     * every downstream action/event on the matched hardware
                     * temporarily unavailable. On restore their exact active
                     * states are reinstated and the retained command is replayed.
                     */
                    if (state.Authority ==
                            SystemAuthorityKind.Gear ||
                        state.Authority ==
                            SystemAuthorityKind.Lights)
                    {
                        GateModuleActionsAndEvents(
                            module,
                            state);

                        if (state.Authority ==
                            SystemAuthorityKind.Lights)
                        {
                            ForceLightPhysicalState(
                                module,
                                false);
                        }

                        continue;
                    }

                    /*
                     * SAS and wheel brakes retain the proven Rev-C module
                     * inhibition path. SAS additionally suppresses
                     * VesselAutopilot in MaintainPhysicalOutput().
                     */
                    if (module.enabled)
                        module.enabled = false;
                }
            }
        }

        private static void MaintainPhysicalOutput(
            Vessel vessel,
            LeaseState state)
        {
            if (vessel == null ||
                state == null)
            {
                return;
            }

            /*
             * ModuleSAS advertises SAS capability, but the active controller
             * that actually applies stabilization is VesselAutopilot. An
             * already-running autopilot can continue stabilizing after its
             * ModuleSAS is disabled, so the lease must suppress the vessel
             * controller itself every frame.
             *
             * Reaction wheels and other attitude actuators remain enabled,
             * preserving manual pitch/yaw/roll authority.
             */
            if (state.Authority ==
                SystemAuthorityKind.Sas)
            {
                try
                {
                    if (vessel.Autopilot != null &&
                        vessel.Autopilot.Enabled)
                    {
                        vessel.Autopilot.Disable();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[KMC] SAS autopilot inhibit failed: " +
                        ex.GetType().Name);
                }

                return;
            }

            if (state.Authority !=
                SystemAuthorityKind.Lights)
            {
                return;
            }

            foreach (
                KeyValuePair<PartModule, bool> pair
                in state.PriorEnabled)
            {
                PartModule module =
                    pair.Key;

                if (module != null)
                {
                    InvokeNoArg(
                        module,
                        "LightsOff",
                        "LightOff",
                        "TurnOff");
                }
            }
        }

        private static void GateModuleActionsAndEvents(
            PartModule module,
            LeaseState state)
        {
            if (module == null ||
                state == null)
            {
                return;
            }

            try
            {
                if (module.Actions != null)
                {
                    foreach (BaseAction action in
                        module.Actions)
                    {
                        if (action == null)
                            continue;

                        if (!state.PriorActionActive
                                .ContainsKey(action))
                        {
                            state.PriorActionActive[action] =
                                action.active;
                        }

                        action.active = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] System authority action gate failed: " +
                    ex.GetType().Name);
            }

            try
            {
                if (module.Events != null)
                {
                    foreach (BaseEvent evt in
                        module.Events)
                    {
                        if (evt == null)
                            continue;

                        if (!state.PriorEventActive
                                .ContainsKey(evt))
                        {
                            state.PriorEventActive[evt] =
                                evt.active;
                        }

                        evt.active = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] System authority event gate failed: " +
                    ex.GetType().Name);
            }
        }

        private static void RestoreModuleActionsAndEvents(
            LeaseState state)
        {
            if (state == null)
                return;

            foreach (
                KeyValuePair<BaseAction, bool> pair
                in state.PriorActionActive)
            {
                BaseAction action =
                    pair.Key;

                if (action != null)
                {
                    try
                    {
                        action.active =
                            pair.Value;
                    }
                    catch
                    {
                    }
                }
            }

            foreach (
                KeyValuePair<BaseEvent, bool> pair
                in state.PriorEventActive)
            {
                BaseEvent evt =
                    pair.Key;

                if (evt != null)
                {
                    try
                    {
                        evt.active =
                            pair.Value;
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static bool MatchesAuthority(
            PartModule module,
            SystemAuthorityKind authority)
        {
            if (module == null)
                return false;

            string moduleName =
                module.moduleName ??
                string.Empty;

            string typeName =
                module.GetType().Name ??
                string.Empty;

            switch (authority)
            {
                case SystemAuthorityKind.Sas:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleSAS");

                case SystemAuthorityKind.Gear:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleWheelDeployment") ||
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleLandingGear");

                case SystemAuthorityKind.Brakes:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleWheelBrakes");

                case SystemAuthorityKind.Lights:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleLight") ||
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleColoredLensLight") ||
                        (IsName(
                             moduleName,
                             typeName,
                             "ModuleColorChanger") &&
                         ModuleUsesActionGroup(
                             module,
                             KSPActionGroup.Light));

                default:
                    return false;
            }
        }

        private static bool ModuleUsesActionGroup(
            PartModule module,
            KSPActionGroup expectedGroup)
        {
            if (module == null)
                return false;

            /*
             * Prefer the live BaseAction assignment because that reflects
             * stock/mod configuration after KSP has loaded the part.
             */
            try
            {
                if (module.Actions != null)
                {
                    foreach (BaseAction action in
                        module.Actions)
                    {
                        if (action == null)
                            continue;

                        object groupValue =
                            GetMemberValue(
                                action,
                                "actionGroup");

                        if (groupValue is KSPActionGroup &&
                            (KSPActionGroup)groupValue ==
                                expectedGroup)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            /*
             * ModuleColorChanger also exposes defaultActionGroup in stock
             * configs. Use it as a fallback so this remains robust if the
             * BaseAction member layout differs between KSP builds.
             */
            try
            {
                object configured =
                    GetMemberValue(
                        module,
                        "defaultActionGroup");

                if (configured is KSPActionGroup &&
                    (KSPActionGroup)configured ==
                        expectedGroup)
                {
                    return true;
                }

                string text =
                    Convert.ToString(
                        configured);

                if (string.Equals(
                        text,
                        expectedGroup.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static object GetMemberValue(
            object target,
            string memberName)
        {
            if (target == null ||
                string.IsNullOrWhiteSpace(
                    memberName))
            {
                return null;
            }

            Type type =
                target.GetType();

            try
            {
                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    return
                        field.GetValue(
                            target);
                }
            }
            catch
            {
            }

            try
            {
                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (property != null &&
                    property.CanRead)
                {
                    return
                        property.GetValue(
                            target,
                            null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static void ForceLightPhysicalState(
            PartModule module,
            bool commandedOn)
        {
            if (module == null)
                return;

            string moduleName =
                module.moduleName ??
                string.Empty;

            string typeName =
                module.GetType().Name ??
                string.Empty;

            /*
             * Stock command-pod window lighting is commonly implemented with
             * ModuleColorChanger. Its emissive animation implements
             * IScalarModule, so SetScalar(0/1) directly controls the effective
             * rendered state without mutating the vessel Light command.
             */
            if (IsName(
                    moduleName,
                    typeName,
                    "ModuleColorChanger"))
            {
                if (TrySetScalar(
                        module,
                        commandedOn
                            ? 1.0f
                            : 0.0f))
                {
                    return;
                }
            }

            if (commandedOn)
            {
                InvokeNoArg(
                    module,
                    "LightsOn",
                    "LightOn",
                    "TurnOn");
            }
            else
            {
                InvokeNoArg(
                    module,
                    "LightsOff",
                    "LightOff",
                    "TurnOff");
            }
        }

        private static bool TrySetScalar(
            object target,
            float value)
        {
            if (target == null)
                return false;

            try
            {
                MethodInfo method =
                    target.GetType()
                        .GetMethod(
                            "SetScalar",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic,
                            null,
                            new[]
                            {
                                typeof(float)
                            },
                            null);

                if (method == null)
                    return false;

                method.Invoke(
                    target,
                    new object[]
                    {
                        value
                    });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsName(
            string moduleName,
            string typeName,
            string expected)
        {
            return
                string.Equals(
                    moduleName,
                    expected,
                    StringComparison.Ordinal) ||
                string.Equals(
                    typeName,
                    expected,
                    StringComparison.Ordinal);
        }

        private void ApplyRestore(
            string key,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            LeaseState state;

            if (_leases.TryGetValue(
                    key,
                    out state) &&
                state != null)
            {
                RestoreState(state);
                _leases.Remove(key);

                Debug.Log(
                    "[KMC] SYSTEM AUTHORITY RESTORE" +
                    " | VesselId=" +
                    state.VesselId +
                    " | Authority=" +
                    state.Authority.ToString() +
                    " | Reason=" +
                    reason);
            }
        }

        private static void RestoreState(
            LeaseState state)
        {
            if (state == null)
                return;

            RestoreModuleActionsAndEvents(
                state);

            foreach (
                KeyValuePair<PartModule, bool> pair
                in state.PriorEnabled)
            {
                PartModule module =
                    pair.Key;

                if (module == null)
                    continue;

                module.enabled =
                    pair.Value;
            }

            Vessel vessel =
                FindVessel(
                    state.VesselId);

            if (vessel == null)
                return;

            /*
             * SAS is a command/effective-state split. The inhibit suppresses
             * VesselAutopilot itself. On restore, reapply the command that was
             * present when KMC authority was lost. This prevents a transient
             * authority failure from silently changing the crew's selected
             * SAS state.
             */
            if (state.Authority ==
                SystemAuthorityKind.Sas)
            {
                try
                {
                    if (state.SasCommandKnown &&
                        vessel.ActionGroups != null)
                    {
                        SetActionGroup(
                            vessel,
                            KSPActionGroup.SAS,
                            state.SasCommandedOn);
                    }

                    if (vessel.Autopilot != null)
                    {
                        if (state.SasCommandKnown &&
                            state.SasCommandedOn)
                        {
                            vessel.Autopilot.Enable();
                        }
                        else
                        {
                            vessel.Autopilot.Disable();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[KMC] SAS autopilot restore failed: " +
                        ex.GetType().Name);
                }
            }

            /*
             * Restore effective light output from the retained crew command.
             * KMC never clears the Light action-group state.
             */
            if (state.Authority ==
                SystemAuthorityKind.Lights)
            {
                bool commandedOn =
                    vessel.ActionGroups != null &&
                    vessel.ActionGroups[
                        KSPActionGroup.Light];

                foreach (
                    KeyValuePair<PartModule, bool> pair
                    in state.PriorEnabled)
                {
                    PartModule module =
                        pair.Key;

                    if (module == null ||
                        !pair.Value)
                    {
                        continue;
                    }

                    ForceLightPhysicalState(
                        module,
                        commandedOn);
                }
            }

            /*
             * Gear is edge-commanded in stock KSP. If the crew changed the
             * Gear action-group command while its deployment modules were
             * inhibited, replay the retained final command after restoration.
             *
             * Brakes are normally continuous, but the same replay is harmless
             * and ensures restored wheel modules see the current command.
             */
            if (state.Authority ==
                    SystemAuthorityKind.Gear ||
                state.Authority ==
                    SystemAuthorityKind.Brakes)
            {
                KSPActionGroup group =
                    state.Authority ==
                        SystemAuthorityKind.Gear
                        ? KSPActionGroup.Gear
                        : KSPActionGroup.Brakes;

                ReplayActionGroup(
                    vessel,
                    group);
            }
        }

        private static void SetActionGroup(
            Vessel vessel,
            KSPActionGroup group,
            bool desired)
        {
            if (vessel == null ||
                vessel.ActionGroups == null)
            {
                return;
            }

            try
            {
                Type type =
                    vessel.ActionGroups
                        .GetType();

                MethodInfo setGroup =
                    type.GetMethod(
                        "SetGroup",
                        BindingFlags.Instance |
                        BindingFlags.Public,
                        null,
                        new[]
                        {
                            typeof(KSPActionGroup),
                            typeof(bool)
                        },
                        null);

                if (setGroup != null)
                {
                    setGroup.Invoke(
                        vessel.ActionGroups,
                        new object[]
                        {
                            group,
                            desired
                        });
                }
            }
            catch
            {
            }
        }

        private static void ReplayActionGroup(
            Vessel vessel,
            KSPActionGroup group)
        {
            if (vessel == null ||
                vessel.ActionGroups == null)
            {
                return;
            }

            bool desired =
                vessel.ActionGroups[group];

            try
            {
                Type type =
                    vessel.ActionGroups
                        .GetType();

                MethodInfo setGroup =
                    type.GetMethod(
                        "SetGroup",
                        BindingFlags.Instance |
                        BindingFlags.Public,
                        null,
                        new[]
                        {
                            typeof(KSPActionGroup),
                            typeof(bool)
                        },
                        null);

                if (setGroup == null)
                    return;

                setGroup.Invoke(
                    vessel.ActionGroups,
                    new object[]
                    {
                        group,
                        !desired
                    });

                setGroup.Invoke(
                    vessel.ActionGroups,
                    new object[]
                    {
                        group,
                        desired
                    });
            }
            catch
            {
            }
        }

        private static Vessel FindVessel(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId) ||
                FlightGlobals.Vessels == null)
            {
                return null;
            }

            foreach (Vessel vessel in
                FlightGlobals.Vessels)
            {
                if (vessel != null &&
                    string.Equals(
                        vessel.id.ToString(),
                        vesselId,
                        StringComparison.Ordinal))
                {
                    return vessel;
                }
            }

            return null;
        }

        private static void InvokeNoArg(
            object target,
            params string[] methodNames)
        {
            if (target == null ||
                methodNames == null)
            {
                return;
            }

            Type type =
                target.GetType();

            for (int index = 0;
                 index < methodNames.Length;
                 index++)
            {
                try
                {
                    MethodInfo method =
                        type.GetMethod(
                            methodNames[index],
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic,
                            null,
                            Type.EmptyTypes,
                            null);

                    if (method != null)
                    {
                        method.Invoke(
                            target,
                            null);

                        return;
                    }
                }
                catch
                {
                }
            }
        }

        private void ReceiveLoop()
        {
            IPEndPoint source =
                new IPEndPoint(
                    IPAddress.Loopback,
                    0);

            while (_running)
            {
                try
                {
                    byte[] data =
                        _receiveClient.Receive(
                            ref source);

                    string message =
                        Encoding.UTF8.GetString(
                            data);

                    SystemAuthorityPacket packet;

                    if (!SystemAuthorityPacket
                            .TryParse(
                                message,
                                out packet))
                    {
                        continue;
                    }

                    lock (_syncRoot)
                        _pending.Enqueue(packet);
                }
                catch (SocketException)
                {
                    if (!_running)
                        return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[KMC] System authority receive failed: " +
                        ex);
                }
            }
        }

        public void OnDestroy()
        {
            _running = false;

            UdpClient client =
                _receiveClient;

            _receiveClient = null;

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

            if (_receiveThread != null &&
                _receiveThread.IsAlive)
            {
                try
                {
                    _receiveThread.Join(250);
                }
                catch
                {
                }
            }

            foreach (
                KeyValuePair<string, LeaseState> pair
                in _leases)
            {
                RestoreState(pair.Value);
            }

            _leases.Clear();
        }

        private sealed class LeaseState
        {
            public string VesselId =
                string.Empty;

            public SystemAuthorityKind Authority;

            public float LastRefreshRealtime;

            public bool SasCommandKnown;
            public bool SasCommandedOn;

            public readonly Dictionary<PartModule, bool>
                PriorEnabled =
                    new Dictionary<PartModule, bool>();

            public readonly Dictionary<BaseAction, bool>
                PriorActionActive =
                    new Dictionary<BaseAction, bool>();

            public readonly Dictionary<BaseEvent, bool>
                PriorEventActive =
                    new Dictionary<BaseEvent, bool>();
        }
    }
}
