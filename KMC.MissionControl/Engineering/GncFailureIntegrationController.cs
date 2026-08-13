using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.Shared;

namespace KMC.MissionControl.Engineering
{
    /// <summary>
    /// Build 14.7 bridge from Engine-owned GNC failure truth to the validated
    /// KSP reaction-wheel authority actuator introduced in Build 14.4.
    /// Exact part identity is retained for the life of the failure.
    /// </summary>
    internal sealed class GncFailureIntegrationController : IDisposable
    {
        private static readonly TimeSpan RefreshInterval =
            TimeSpan.FromSeconds(1.0);

        private readonly object _syncRoot = new object();
        private readonly UdpClient _client = new UdpClient();
        private readonly IPEndPoint _endpoint;
        private readonly Dictionary<string, BridgeState> _states =
            new Dictionary<string, BridgeState>(StringComparer.Ordinal);
        private readonly string _commandSessionId;
        private long _commandSequence;

        public GncFailureIntegrationController()
        {
            _endpoint = new IPEndPoint(
                IPAddress.Loopback,
                FailureEffectPacket.CommandPort);
            _commandSessionId =
                Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
        }

        public void Evaluate(AnalysisPipelineResult result)
        {
            if (result == null || result.Snapshot == null ||
                result.Snapshot.Vessel == null ||
                result.Snapshot.SpacecraftSystems == null)
            {
                return;
            }

            string vesselId = result.Snapshot.Vessel.VesselId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vesselId)) return;

            Dictionary<string, DesiredEffect> desired =
                ResolveDesiredEffects(
                    result.Snapshot.SpacecraftSystems.FailureSimulation);

            lock (_syncRoot)
            {
                DateTime nowUtc = DateTime.UtcNow;

                foreach (KeyValuePair<string, DesiredEffect> pair in desired)
                {
                    string stateKey = vesselId + "|" + pair.Key;
                    BridgeState state;
                    if (!_states.TryGetValue(stateKey, out state))
                    {
                        state = new BridgeState
                        {
                            VesselId = vesselId,
                            TargetId = pair.Key
                        };
                        _states[stateKey] = state;
                    }

                    DesiredEffect effect = pair.Value;
                    bool changed = !state.Active ||
                        state.PartId != effect.PartId ||
                        Math.Abs(state.Magnitude - effect.Magnitude) > 0.0001;
                    bool refreshDue = state.LastApplyUtc == DateTime.MinValue ||
                        nowUtc - state.LastApplyUtc >= RefreshInterval;

                    if (changed || refreshDue)
                    {
                        Send(vesselId, effect.PartId,
                            FailureEffectOperation.Apply,
                            effect.Magnitude,
                            effect.TargetId,
                            changed ? "ACTIVE/CHANGED" : "LEASE REFRESH");
                        state.Active = true;
                        state.PartId = effect.PartId;
                        state.Magnitude = effect.Magnitude;
                        state.LastApplyUtc = nowUtc;
                    }
                }

                List<string> remove = null;
                foreach (KeyValuePair<string, BridgeState> pair in _states)
                {
                    BridgeState state = pair.Value;
                    if (state == null || !state.Active ||
                        !string.Equals(state.VesselId, vesselId, StringComparison.Ordinal) ||
                        desired.ContainsKey(state.TargetId))
                    {
                        continue;
                    }

                    Send(state.VesselId, state.PartId,
                        FailureEffectOperation.Restore,
                        1.0, state.TargetId,
                        "FAILURE CLEARED/INACTIVE");
                    state.Active = false;
                    if (remove == null) remove = new List<string>();
                    remove.Add(pair.Key);
                }

                if (remove != null)
                    for (int i = 0; i < remove.Count; i++) _states.Remove(remove[i]);
            }
        }

        public void RestoreAll()
        {
            lock (_syncRoot)
            {
                foreach (KeyValuePair<string, BridgeState> pair in _states)
                {
                    BridgeState state = pair.Value;
                    if (state == null || !state.Active) continue;
                    Send(state.VesselId, state.PartId,
                        FailureEffectOperation.Restore,
                        1.0, state.TargetId,
                        "MISSION CONTROL STOP");
                }
                _states.Clear();
            }
        }

        private static Dictionary<string, DesiredEffect> ResolveDesiredEffects(
            FailureSimulationSnapshot snapshot)
        {
            Dictionary<string, DesiredEffect> desired =
                new Dictionary<string, DesiredEffect>(StringComparer.Ordinal);

            if (snapshot == null || snapshot.Mode == FailureSimulationMode.Nominal)
                return desired;

            for (int i = 0; i < snapshot.Failures.Count; i++)
            {
                SyntheticFailureRecord failure = snapshot.Failures[i];
                if (failure == null || !failure.EffectiveNow ||
                    failure.TargetKind != SyntheticFailureTargetKind.GuidanceEffect)
                    continue;

                uint partId;
                if (!SyntheticFailureTargets.TryParseGuidanceTarget(
                        failure.TargetId, out partId))
                    continue;

                double magnitude = Math.Max(0.0,
                    Math.Min(1.0, failure.EffectMagnitude));

                DesiredEffect existing;
                if (desired.TryGetValue(failure.TargetId, out existing) &&
                    magnitude >= existing.Magnitude)
                    continue;

                desired[failure.TargetId] = new DesiredEffect
                {
                    TargetId = failure.TargetId,
                    PartId = partId,
                    Magnitude = magnitude
                };
            }
            return desired;
        }

        private void Send(string vesselId, uint partId,
            FailureEffectOperation operation, double magnitude,
            string targetId, string reason)
        {
            _commandSequence++;
            FailureEffectPacket packet = new FailureEffectPacket
            {
                VesselId = vesselId,
                CommandId = "GNC14.7-" + _commandSessionId + "-" +
                    _commandSequence.ToString("000000"),
                PartPersistentId = partId,
                EffectType = FailureEffectType.ReactionWheelAuthority,
                Operation = operation,
                Magnitude = magnitude
            };

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(packet.Serialize());
                _client.Send(data, data.Length, _endpoint);
                Debug.WriteLine(
                    "KMC.MissionControl GNC FAILURE INTEGRATION" +
                    " | CommandId=" + packet.CommandId +
                    " | VesselId=" + vesselId +
                    " | Target=" + targetId +
                    " | Part=" + partId.ToString() +
                    " | Effect=ReactionWheelAuthority" +
                    " | Command=" + operation.ToString().ToUpperInvariant() +
                    " | Magnitude=" + magnitude.ToString("0.00") +
                    " | Reason=" + reason);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "KMC.MissionControl GNC FAILURE INTEGRATION ERROR" +
                    " | VesselId=" + vesselId +
                    " | Target=" + targetId +
                    " | Error=" + ex.GetType().Name +
                    " | Detail=" + ex.Message);
            }
        }

        public void Dispose()
        {
            RestoreAll();
            _client.Close();
        }

        private sealed class BridgeState
        {
            public string VesselId = string.Empty;
            public string TargetId = string.Empty;
            public uint PartId;
            public double Magnitude;
            public DateTime LastApplyUtc = DateTime.MinValue;
            public bool Active;
        }

        private sealed class DesiredEffect
        {
            public string TargetId = string.Empty;
            public uint PartId;
            public double Magnitude;
        }
    }
}
