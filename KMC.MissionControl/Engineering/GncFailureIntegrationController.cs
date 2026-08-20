using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Engine.SpacecraftSystems;
using KMC.Shared;

namespace KMC.MissionControl.Engineering
{
    /// <summary>
    /// Build 14.18.8
    ///
    /// Existing GNC reaction-wheel bridge plus vessel-wide RCS authority
    /// integration.
    ///
    /// Reaction-wheel behavior from Build 14.7 is preserved.
    /// RCS authority is leased independently over KMC-RCSAUTH1.
    ///
    /// Build 14.18.8 derives RCS electrical authority from the actual
    /// RCS_CONTROL branch on BUS_ESS after real KSP source evidence has been
    /// applied. Missing electrical evidence fails open rather than inventing
    /// a power loss.
    /// </summary>
    internal sealed class GncFailureIntegrationController : IDisposable
    {
        private static readonly TimeSpan RefreshInterval =
            TimeSpan.FromSeconds(1.0);

        private readonly object _syncRoot = new object();

        private readonly UdpClient _client =
            new UdpClient();

        private readonly UdpClient _rcsClient =
            new UdpClient();

        private readonly IPEndPoint _endpoint;
        private readonly IPEndPoint _rcsEndpoint;

        private readonly Dictionary<string, BridgeState> _states =
            new Dictionary<string, BridgeState>(
                StringComparer.Ordinal);

        private readonly Dictionary<string, RcsBridgeState> _rcsStates =
            new Dictionary<string, RcsBridgeState>(
                StringComparer.Ordinal);

        private readonly string _commandSessionId;
        private long _commandSequence;
        private long _rcsCommandSequence;

        public GncFailureIntegrationController()
        {
            _endpoint =
                new IPEndPoint(
                    IPAddress.Loopback,
                    FailureEffectPacket.CommandPort);

            _rcsEndpoint =
                new IPEndPoint(
                    IPAddress.Loopback,
                    RcsAuthorityPacket.CommandPort);

            _commandSessionId =
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 8)
                    .ToUpperInvariant();
        }

        public void Evaluate(
            AnalysisPipelineResult result)
        {
            if (result == null ||
                result.Snapshot == null ||
                result.Snapshot.Vessel == null ||
                result.Snapshot.SpacecraftSystems == null)
            {
                return;
            }

            string vesselId =
                result.Snapshot.Vessel.VesselId ??
                string.Empty;

            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            Dictionary<string, DesiredEffect> desired =
                ResolveDesiredEffects(
                    result.Snapshot
                        .SpacecraftSystems
                        .FailureSimulation);

            lock (_syncRoot)
            {
                DateTime nowUtc =
                    DateTime.UtcNow;

                EvaluateReactionWheels(
                    vesselId,
                    desired,
                    nowUtc);

                EvaluateRcsAuthority(
                    result,
                    vesselId,
                    nowUtc);
            }
        }

        private void EvaluateReactionWheels(
            string vesselId,
            Dictionary<string, DesiredEffect> desired,
            DateTime nowUtc)
        {
            foreach (
                KeyValuePair<string, DesiredEffect> pair
                in desired)
            {
                string stateKey =
                    vesselId + "|" + pair.Key;

                BridgeState state;

                if (!_states.TryGetValue(
                        stateKey,
                        out state))
                {
                    state =
                        new BridgeState
                        {
                            VesselId = vesselId,
                            TargetId = pair.Key
                        };

                    _states[stateKey] =
                        state;
                }

                DesiredEffect effect =
                    pair.Value;

                bool changed =
                    !state.Active ||
                    state.PartId != effect.PartId ||
                    Math.Abs(
                        state.Magnitude -
                        effect.Magnitude) > 0.0001;

                bool refreshDue =
                    state.LastApplyUtc ==
                        DateTime.MinValue ||
                    nowUtc -
                        state.LastApplyUtc >=
                    RefreshInterval;

                if (changed || refreshDue)
                {
                    Send(
                        vesselId,
                        effect.PartId,
                        FailureEffectOperation.Apply,
                        effect.Magnitude,
                        effect.TargetId,
                        changed
                            ? "ACTIVE/CHANGED"
                            : "LEASE REFRESH");

                    state.Active = true;
                    state.PartId = effect.PartId;
                    state.Magnitude =
                        effect.Magnitude;
                    state.LastApplyUtc =
                        nowUtc;
                }
            }

            List<string> remove =
                null;

            foreach (
                KeyValuePair<string, BridgeState> pair
                in _states)
            {
                BridgeState state =
                    pair.Value;

                if (state == null ||
                    !state.Active ||
                    !string.Equals(
                        state.VesselId,
                        vesselId,
                        StringComparison.Ordinal) ||
                    desired.ContainsKey(
                        state.TargetId))
                {
                    continue;
                }

                Send(
                    state.VesselId,
                    state.PartId,
                    FailureEffectOperation.Restore,
                    1.0,
                    state.TargetId,
                    "FAILURE CLEARED/INACTIVE");

                state.Active = false;

                if (remove == null)
                {
                    remove =
                        new List<string>();
                }

                remove.Add(
                    pair.Key);
            }

            if (remove != null)
            {
                for (int i = 0;
                     i < remove.Count;
                     i++)
                {
                    _states.Remove(
                        remove[i]);
                }
            }
        }

        private void EvaluateRcsAuthority(
            AnalysisPipelineResult result,
            string vesselId,
            DateTime nowUtc)
        {
            int rcsPartCount = 0;

            if (result.Snapshot.Capabilities != null)
            {
                rcsPartCount =
                    result.Snapshot.Capabilities
                        .GetPartCount(
                            VesselCapabilityType
                                .ReactionControl);
            }

            RcsAuthorityStore.PublishHardware(
                vesselId,
                rcsPartCount);

            PublishRcsElectricalPower(
                result,
                vesselId,
                rcsPartCount);

            RcsAuthoritySnapshot authority =
                RcsAuthorityStore.GetSnapshot(
                    vesselId);

            bool inhibitDesired =
                rcsPartCount > 0 &&
                authority.Known &&
                !authority.AuthorityAvailable;

            RcsBridgeState state;

            if (!_rcsStates.TryGetValue(
                    vesselId,
                    out state))
            {
                state =
                    new RcsBridgeState
                    {
                        VesselId = vesselId
                    };

                _rcsStates[vesselId] =
                    state;
            }

            if (inhibitDesired)
            {
                bool refreshDue =
                    !state.Active ||
                    state.LastApplyUtc ==
                        DateTime.MinValue ||
                    nowUtc -
                        state.LastApplyUtc >=
                    RefreshInterval;

                if (refreshDue)
                {
                    SendRcs(
                        vesselId,
                        RcsAuthorityOperation.Inhibit,
                        state.Active
                            ? "LEASE REFRESH"
                            : authority.Detail);

                    state.Active = true;
                    state.LastApplyUtc =
                        nowUtc;
                }

                return;
            }

            if (state.Active)
            {
                SendRcs(
                    vesselId,
                    RcsAuthorityOperation.Restore,
                    authority.HardwareDetected
                        ? "AUTHORITY AVAILABLE"
                        : "RCS HARDWARE NOT PRESENT");

                state.Active = false;
            }

            _rcsStates.Remove(
                vesselId);
        }

        private static void PublishRcsElectricalPower(
            AnalysisPipelineResult result,
            string vesselId,
            int rcsPartCount)
        {
            /*
             * No RCS hardware means there is no RCS electrical consequence to
             * enforce. Keep the electrical input non-authoritative rather than
             * manufacturing a failed system on an RCS-less vehicle.
             */
            if (rcsPartCount <= 0)
            {
                RcsAuthorityStore.PublishElectricalPower(
                    vesselId,
                    false,
                    true,
                    "BUS_ESS",
                    0.0);

                return;
            }

            SyntheticElectricalDistributionModel distribution =
                result != null &&
                result.Snapshot != null &&
                result.Snapshot.SpacecraftSystems != null
                    ? result.Snapshot.SpacecraftSystems
                        .ElectricalDistribution
                    : null;

            if (distribution == null)
            {
                RcsAuthorityStore.PublishElectricalPower(
                    vesselId,
                    false,
                    true,
                    "BUS_ESS",
                    0.0);

                return;
            }

            SyntheticElectricalBus ess =
                distribution.FindBus(
                    "BUS_ESS");

            SyntheticElectricalLoad rcsLoad =
                null;

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad candidate =
                    distribution.Loads[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.EquipmentId,
                        "RCS_CONTROL",
                        StringComparison.Ordinal))
                {
                    rcsLoad =
                        candidate;

                    break;
                }
            }

            SyntheticElectricalSwitch breaker =
                rcsLoad != null
                    ? distribution.FindSwitch(
                        rcsLoad.BreakerId)
                    : null;

            bool known =
                ess != null &&
                rcsLoad != null &&
                breaker != null;

            if (!known)
            {
                RcsAuthorityStore.PublishElectricalPower(
                    vesselId,
                    false,
                    true,
                    "BUS_ESS",
                    0.0);

                return;
            }

            bool busEnergized =
                ess.State !=
                    SyntheticElectricalBusState.Unpowered &&
                ess.State !=
                    SyntheticElectricalBusState.Failed &&
                ess.Voltage >
                    0.000001;

            bool powered =
                rcsLoad.CommandedOn &&
                !rcsLoad.AutomaticallyShed &&
                breaker.Conducting &&
                busEnergized;

            RcsAuthorityStore.PublishElectricalPower(
                vesselId,
                true,
                powered,
                "BUS_ESS",
                ess.Voltage);
        }

        public void RestoreAll()
        {
            lock (_syncRoot)
            {
                foreach (
                    KeyValuePair<string, BridgeState> pair
                    in _states)
                {
                    BridgeState state =
                        pair.Value;

                    if (state == null ||
                        !state.Active)
                    {
                        continue;
                    }

                    Send(
                        state.VesselId,
                        state.PartId,
                        FailureEffectOperation.Restore,
                        1.0,
                        state.TargetId,
                        "MISSION CONTROL STOP");
                }

                _states.Clear();

                foreach (
                    KeyValuePair<string, RcsBridgeState> pair
                    in _rcsStates)
                {
                    RcsBridgeState state =
                        pair.Value;

                    if (state == null ||
                        !state.Active)
                    {
                        continue;
                    }

                    SendRcs(
                        state.VesselId,
                        RcsAuthorityOperation.Restore,
                        "MISSION CONTROL STOP");
                }

                _rcsStates.Clear();

                /*
                 * No KMC authority command survives Mission Control shutdown.
                 * The KSP lease also independently fails open.
                 */
                RcsAuthorityStore.ClearAll();
            }
        }

        private static Dictionary<string, DesiredEffect>
            ResolveDesiredEffects(
                FailureSimulationSnapshot snapshot)
        {
            Dictionary<string, DesiredEffect> desired =
                new Dictionary<string, DesiredEffect>(
                    StringComparer.Ordinal);

            if (snapshot == null ||
                snapshot.Mode ==
                    FailureSimulationMode.Nominal)
            {
                return desired;
            }

            for (int i = 0;
                 i < snapshot.Failures.Count;
                 i++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[i];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind
                            .GuidanceEffect)
                {
                    continue;
                }

                uint partId;

                if (!SyntheticFailureTargets
                        .TryParseGuidanceTarget(
                            failure.TargetId,
                            out partId))
                {
                    continue;
                }

                double magnitude =
                    Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            failure.EffectMagnitude));

                DesiredEffect existing;

                if (desired.TryGetValue(
                        failure.TargetId,
                        out existing) &&
                    magnitude >=
                        existing.Magnitude)
                {
                    continue;
                }

                desired[failure.TargetId] =
                    new DesiredEffect
                    {
                        TargetId =
                            failure.TargetId,
                        PartId = partId,
                        Magnitude = magnitude
                    };
            }

            return desired;
        }

        private void Send(
            string vesselId,
            uint partId,
            FailureEffectOperation operation,
            double magnitude,
            string targetId,
            string reason)
        {
            _commandSequence++;

            FailureEffectPacket packet =
                new FailureEffectPacket
                {
                    VesselId = vesselId,
                    CommandId =
                        "GNC14.7-" +
                        _commandSessionId +
                        "-" +
                        _commandSequence
                            .ToString("000000"),
                    PartPersistentId =
                        partId,
                    EffectType =
                        FailureEffectType
                            .ReactionWheelAuthority,
                    Operation = operation,
                    Magnitude = magnitude
                };

            try
            {
                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                _client.Send(
                    data,
                    data.Length,
                    _endpoint);

                Debug.WriteLine(
                    "KMC.MissionControl GNC FAILURE INTEGRATION" +
                    " | CommandId=" +
                    packet.CommandId +
                    " | VesselId=" +
                    vesselId +
                    " | Target=" +
                    targetId +
                    " | Part=" +
                    partId.ToString() +
                    " | Effect=ReactionWheelAuthority" +
                    " | Command=" +
                    operation
                        .ToString()
                        .ToUpperInvariant() +
                    " | Magnitude=" +
                    magnitude.ToString("0.00") +
                    " | Reason=" +
                    reason);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "KMC.MissionControl GNC FAILURE INTEGRATION ERROR" +
                    " | VesselId=" +
                    vesselId +
                    " | Target=" +
                    targetId +
                    " | Error=" +
                    ex.GetType().Name +
                    " | Detail=" +
                    ex.Message);
            }
        }

        private void SendRcs(
            string vesselId,
            RcsAuthorityOperation operation,
            string reason)
        {
            _rcsCommandSequence++;

            RcsAuthorityPacket packet =
                new RcsAuthorityPacket
                {
                    VesselId =
                        vesselId ?? string.Empty,
                    CommandId =
                        "RCS14.18.8-" +
                        _commandSessionId +
                        "-" +
                        _rcsCommandSequence
                            .ToString("000000"),
                    Operation = operation
                };

            try
            {
                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                _rcsClient.Send(
                    data,
                    data.Length,
                    _rcsEndpoint);

                Debug.WriteLine(
                    "KMC.MissionControl RCS AUTHORITY" +
                    " | CommandId=" +
                    packet.CommandId +
                    " | VesselId=" +
                    packet.VesselId +
                    " | Command=" +
                    operation
                        .ToString()
                        .ToUpperInvariant() +
                    " | Reason=" +
                    (reason ?? string.Empty));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "KMC.MissionControl RCS AUTHORITY ERROR" +
                    " | VesselId=" +
                    vesselId +
                    " | Error=" +
                    ex.GetType().Name +
                    " | Detail=" +
                    ex.Message);
            }
        }

        public void Dispose()
        {
            RestoreAll();
            _client.Close();
            _rcsClient.Close();
        }

        private sealed class BridgeState
        {
            public string VesselId =
                string.Empty;

            public string TargetId =
                string.Empty;

            public uint PartId;
            public double Magnitude;

            public DateTime LastApplyUtc =
                DateTime.MinValue;

            public bool Active;
        }

        private sealed class DesiredEffect
        {
            public string TargetId =
                string.Empty;

            public uint PartId;
            public double Magnitude;
        }

        private sealed class RcsBridgeState
        {
            public string VesselId =
                string.Empty;

            public DateTime LastApplyUtc =
                DateTime.MinValue;

            public bool Active;
        }
    }
}
