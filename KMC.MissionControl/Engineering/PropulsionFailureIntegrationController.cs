using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Engine.Analysis;
using KMC.Engine.Propulsion;
using KMC.Engine.SpacecraftSystems;
using KMC.Shared;

namespace KMC.MissionControl.Engineering
{
    internal sealed class PropulsionFailureIntegrationController :
        IDisposable
    {
        private static readonly TimeSpan RefreshInterval =
            TimeSpan.FromSeconds(1.0);

        private readonly object _syncRoot;
        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private readonly Dictionary<string, TargetBridgeState> _states;
        private readonly string _commandSessionId;
        private long _commandSequence;

        public PropulsionFailureIntegrationController()
        {
            _syncRoot = new object();
            _client = new UdpClient();
            _endpoint =
                new IPEndPoint(
                    IPAddress.Loopback,
                    FailureEffectPacket.CommandPort);

            _states =
                new Dictionary<string, TargetBridgeState>(
                    StringComparer.Ordinal);

            _commandSessionId =
                Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
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

            FailureSimulationSnapshot failures =
                result.Snapshot.SpacecraftSystems.FailureSimulation;

            Dictionary<string, DesiredEffect> desired =
                ResolveDesiredEffects(
                    failures);

            lock (_syncRoot)
            {
                DateTime nowUtc =
                    DateTime.UtcNow;

                foreach (
                    KeyValuePair<string, DesiredEffect> pair
                    in desired)
                {
                    string key =
                        CreateStateKey(
                            vesselId,
                            pair.Key);

                    TargetBridgeState state;

                    if (!_states.TryGetValue(
                            key,
                            out state))
                    {
                        state =
                            new TargetBridgeState
                            {
                                VesselId = vesselId,
                                TargetId = pair.Key
                            };

                        _states[key] =
                            state;
                    }

                    DesiredEffect effect =
                        pair.Value;

                    bool changed =
                        !state.Active ||
                        state.EffectType != effect.EffectType ||
                        state.PartPersistentId != effect.PartPersistentId ||
                        Math.Abs(
                            state.Magnitude -
                            effect.Magnitude) >
                        0.0001;

                    bool refreshDue =
                        state.LastApplyUtc ==
                            DateTime.MinValue ||
                        nowUtc -
                            state.LastApplyUtc >=
                            RefreshInterval;

                    if (changed ||
                        refreshDue)
                    {
                        Send(
                            vesselId,
                            effect.PartPersistentId,
                            effect.EffectType,
                            FailureEffectOperation.Apply,
                            effect.Magnitude,
                            effect.TargetId,
                            changed
                                ? "ACTIVE/CHANGED"
                                : "LEASE REFRESH");

                        state.Active = true;
                        state.PartPersistentId =
                            effect.PartPersistentId;
                        state.EffectType =
                            effect.EffectType;
                        state.Magnitude =
                            effect.Magnitude;
                        state.LastApplyUtc =
                            nowUtc;
                    }
                }

                List<string> restoreKeys =
                    null;

                foreach (
                    KeyValuePair<string, TargetBridgeState> pair
                    in _states)
                {
                    TargetBridgeState state =
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
                        state.PartPersistentId,
                        state.EffectType,
                        FailureEffectOperation.Restore,
                        1.0,
                        state.TargetId,
                        "FAILURE CLEARED/INACTIVE");

                    state.Active = false;

                    if (restoreKeys == null)
                    {
                        restoreKeys =
                            new List<string>();
                    }

                    restoreKeys.Add(
                        pair.Key);
                }

                if (restoreKeys != null)
                {
                    for (int index = 0;
                         index < restoreKeys.Count;
                         index++)
                    {
                        _states.Remove(
                            restoreKeys[index]);
                    }
                }
            }
        }

        public void RestoreAll()
        {
            lock (_syncRoot)
            {
                foreach (
                    KeyValuePair<string, TargetBridgeState> pair
                    in _states)
                {
                    TargetBridgeState state =
                        pair.Value;

                    if (state == null ||
                        !state.Active)
                    {
                        continue;
                    }

                    Send(
                        state.VesselId,
                        state.PartPersistentId,
                        state.EffectType,
                        FailureEffectOperation.Restore,
                        1.0,
                        state.TargetId,
                        "MISSION CONTROL STOP");
                }

                _states.Clear();
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

            for (int index = 0;
                 index < snapshot.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow)
                {
                    continue;
                }

                /*
                 * Build 14.12.5:
                 * A synthetic exact-engine START INHIBIT is Component truth,
                 * but its truthful physical consequence is that the selected
                 * engine cannot run. Reuse the validated EngineShutdown
                 * actuator without reclassifying the hidden failure itself as
                 * a direct PropulsionEffect.
                 */
                if (failure.TargetKind ==
                        SyntheticFailureTargetKind.Component)
                {
                    uint startPartId;

                    if (PropulsionEngineFailureTargets
                            .TryParseExactEngineStartInhibitTarget(
                                failure.TargetId,
                                out startPartId))
                    {
                        desired[failure.TargetId] =
                            new DesiredEffect
                            {
                                TargetId =
                                    failure.TargetId,
                                PartPersistentId =
                                    startPartId,
                                EffectType =
                                    FailureEffectType.EngineShutdown,
                                Magnitude =
                                    1.0
                            };
                    }

                    continue;
                }

                if (failure.TargetKind !=
                    SyntheticFailureTargetKind.PropulsionEffect)
                {
                    continue;
                }

                uint partId;
                bool shutdown;

                if (!SyntheticFailureTargets.TryParsePropulsionTarget(
                        failure.TargetId,
                        out partId,
                        out shutdown))
                {
                    continue;
                }

                FailureEffectType effectType =
                    shutdown
                        ? FailureEffectType.EngineShutdown
                        : FailureEffectType.EngineDerate;

                double magnitude =
                    shutdown
                        ? 1.0
                        : ResolveDerateMagnitude(
                            failure);

                DesiredEffect existing;

                if (desired.TryGetValue(
                        failure.TargetId,
                        out existing))
                {
                    if (!shutdown &&
                        magnitude >=
                            existing.Magnitude)
                    {
                        continue;
                    }
                }

                desired[failure.TargetId] =
                    new DesiredEffect
                    {
                        TargetId =
                            failure.TargetId,
                        PartPersistentId =
                            partId,
                        EffectType =
                            effectType,
                        Magnitude =
                            magnitude
                    };
            }

            return desired;
        }

        private static double ResolveDerateMagnitude(
            SyntheticFailureRecord failure)
        {
            if (failure == null)
            {
                return 1.0;
            }

            double target =
                Math.Max(
                    0.10,
                    Math.Min(
                        1.00,
                        failure.EffectMagnitude));

            if (failure.Kind !=
                    SyntheticFailureKind.Degrading)
            {
                return target;
            }

            /*
             * Build 14.12.6 progressive thrust decay:
             * 100% -> requested target over 20 seconds from activation.
             * The failure engine owns timing/truth; this bridge converts that
             * truth into the continuously refreshed physical derate magnitude.
             */
            const double decaySeconds = 20.0;

            double elapsed =
                Math.Max(
                    0.0,
                    (DateTime.UtcNow -
                     failure.ActivateUtc)
                    .TotalSeconds);

            double fraction =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        elapsed /
                        decaySeconds));

            return
                1.0 -
                ((1.0 - target) *
                 fraction);
        }

        private void Send(
            string vesselId,
            uint partPersistentId,
            FailureEffectType effectType,
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
                        "PROP14.6-" +
                        _commandSessionId +
                        "-" +
                        _commandSequence.ToString("000000"),
                    PartPersistentId =
                        partPersistentId,
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

                _client.Send(
                    data,
                    data.Length,
                    _endpoint);

                Debug.WriteLine(
                    "KMC.MissionControl PROP FAILURE INTEGRATION" +
                    " | CommandId=" + packet.CommandId +
                    " | VesselId=" + vesselId +
                    " | Target=" + targetId +
                    " | Part=" +
                    partPersistentId.ToString() +
                    " | Effect=" +
                    effectType.ToString() +
                    " | Command=" +
                    operation.ToString().ToUpperInvariant() +
                    " | Magnitude=" +
                    magnitude.ToString("0.00") +
                    " | Reason=" + reason);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "KMC.MissionControl PROP FAILURE INTEGRATION ERROR" +
                    " | VesselId=" + vesselId +
                    " | Target=" + targetId +
                    " | Error=" +
                    ex.GetType().Name +
                    " | Detail=" +
                    ex.Message);
            }
        }

        private static string CreateStateKey(
            string vesselId,
            string targetId)
        {
            return
                (vesselId ?? string.Empty) +
                "|" +
                (targetId ?? string.Empty);
        }

        public void Dispose()
        {
            RestoreAll();
            _client.Close();
        }

        private sealed class TargetBridgeState
        {
            public string VesselId = string.Empty;
            public string TargetId = string.Empty;
            public uint PartPersistentId;
            public FailureEffectType EffectType;
            public double Magnitude;
            public DateTime LastApplyUtc = DateTime.MinValue;
            public bool Active;
        }

        private sealed class DesiredEffect
        {
            public string TargetId = string.Empty;
            public uint PartPersistentId;
            public FailureEffectType EffectType;
            public double Magnitude;
        }
    }
}
