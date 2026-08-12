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
    /// Build 14.5 bridge from Engine-owned POWER failure truth to the
    /// Build 14.4 validated KSP failure-effect actuator.
    ///
    /// Only explicit PowerEffect targets are bridged. Synthetic Main A/B
    /// source failures remain KMC spacecraft-design simulation and are not
    /// bound to arbitrary stock-KSP producer parts.
    /// </summary>
    internal sealed class PowerFailureIntegrationController :
        IDisposable
    {
        private const double MinimumLeakRate = 0.10;
        private const double MaximumLeakRate = 10.00;
        private static readonly TimeSpan RefreshInterval =
            TimeSpan.FromSeconds(1.0);

        private readonly object _syncRoot;
        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private readonly Dictionary<string, VesselBridgeState> _states;
        private long _commandSequence;

        public PowerFailureIntegrationController()
        {
            _syncRoot = new object();
            _client = new UdpClient();
            _endpoint =
                new IPEndPoint(
                    IPAddress.Loopback,
                    FailureEffectPacket.CommandPort);
            _states =
                new Dictionary<string, VesselBridgeState>(
                    StringComparer.Ordinal);
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

            double desiredRate =
                ResolveDesiredLeakRate(
                    failures);

            lock (_syncRoot)
            {
                VesselBridgeState state;

                if (!_states.TryGetValue(
                        vesselId,
                        out state))
                {
                    state =
                        new VesselBridgeState();

                    _states[vesselId] =
                        state;
                }

                DateTime nowUtc =
                    DateTime.UtcNow;

                if (desiredRate > 0.0)
                {
                    bool changed =
                        Math.Abs(
                            desiredRate -
                            state.LastDesiredRate) >
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
                            FailureEffectOperation.Apply,
                            desiredRate,
                            changed
                                ? "ACTIVE/CHANGED"
                                : "LEASE REFRESH");

                        state.LastDesiredRate =
                            desiredRate;
                        state.LastApplyUtc =
                            nowUtc;
                        state.RestoreSent =
                            false;
                    }

                    return;
                }

                if (state.LastDesiredRate > 0.0 &&
                    !state.RestoreSent)
                {
                    Send(
                        vesselId,
                        FailureEffectOperation.Restore,
                        0.0,
                        "FAILURE CLEARED/INACTIVE");

                    state.RestoreSent =
                        true;
                    state.LastDesiredRate =
                        0.0;
                    state.LastApplyUtc =
                        DateTime.MinValue;
                }
            }
        }

        public void RestoreAll()
        {
            lock (_syncRoot)
            {
                foreach (
                    KeyValuePair<string, VesselBridgeState> pair
                    in _states)
                {
                    VesselBridgeState state =
                        pair.Value;

                    if (state == null ||
                        state.LastDesiredRate <= 0.0 ||
                        state.RestoreSent)
                    {
                        continue;
                    }

                    Send(
                        pair.Key,
                        FailureEffectOperation.Restore,
                        0.0,
                        "MISSION CONTROL STOP");

                    state.RestoreSent = true;
                    state.LastDesiredRate = 0.0;
                }

                _states.Clear();
            }
        }

        private static double ResolveDesiredLeakRate(
            FailureSimulationSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Mode ==
                    FailureSimulationMode.Nominal)
            {
                return 0.0;
            }

            double desired = 0.0;

            for (int index = 0;
                 index < snapshot.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.PowerEffect ||
                    !string.Equals(
                        failure.TargetId,
                        SyntheticFailureTargets.ElectricChargeLeak,
                        StringComparison.Ordinal) ||
                    double.IsNaN(
                        failure.EffectMagnitude) ||
                    double.IsInfinity(
                        failure.EffectMagnitude))
                {
                    continue;
                }

                desired =
                    Math.Max(
                        desired,
                        Math.Max(
                            MinimumLeakRate,
                            Math.Min(
                                MaximumLeakRate,
                                failure.EffectMagnitude)));
            }

            /*
             * Multiple active leak records do not stack in 14.5. The bridge
             * chooses the greatest requested vehicle-level load. This keeps
             * the real effect bounded and deterministic.
             */
            return desired;
        }

        private void Send(
            string vesselId,
            FailureEffectOperation operation,
            double magnitude,
            string reason)
        {
            _commandSequence++;

            FailureEffectPacket packet =
                new FailureEffectPacket
                {
                    VesselId = vesselId,
                    CommandId =
                        "PWR14.5-" +
                        _commandSequence.ToString("000000"),
                    PartPersistentId = 0,
                    EffectType =
                        FailureEffectType.ElectricChargeLeak,
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
                    "KMC.MissionControl POWER FAILURE INTEGRATION" +
                    " | VesselId=" + vesselId +
                    " | Target=" +
                    SyntheticFailureTargets.ElectricChargeLeak +
                    " | Command=" +
                    operation.ToString().ToUpperInvariant() +
                    " | Rate=" +
                    magnitude.ToString("0.00") +
                    " EC/s" +
                    " | Reason=" + reason);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "KMC.MissionControl POWER FAILURE INTEGRATION ERROR" +
                    " | VesselId=" + vesselId +
                    " | Command=" +
                    operation.ToString().ToUpperInvariant() +
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
        }

        private sealed class VesselBridgeState
        {
            public double LastDesiredRate;
            public DateTime LastApplyUtc = DateTime.MinValue;
            public bool RestoreSent;
        }
    }
}
