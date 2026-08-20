using System;
using System.Collections.Generic;
using KMC.Engine.Models;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Owns the complete Engine-side synthetic spacecraft model.
    ///
    /// 14.0 supplies the generic component/dependency foundation.
    /// 14.1 overlays electrical distribution and reapplies dependency state.
    /// </summary>
    public sealed class SpacecraftSystemsSystem
    {
        private readonly object _syncRoot;
        private readonly SpacecraftSystemsFoundationSystem _foundation;
        private readonly SyntheticElectricalDistributionSystem _electrical;
        private readonly SyntheticFailureEngine _failureEngine;
        private SpacecraftSystemsModel _latest;

        public SpacecraftSystemsSystem()
        {
            _syncRoot =
                new object();

            _foundation =
                new SpacecraftSystemsFoundationSystem();

            _electrical =
                new SyntheticElectricalDistributionSystem();

            _failureEngine =
                new SyntheticFailureEngine();

            _latest =
                new SpacecraftSystemsModel();
        }

        public void Update(
            VesselModel vessel,
            DateTime generatedUtc)
        {
            _foundation.Update(
                vessel,
                generatedUtc);

            SpacecraftSystemsModel model =
                _foundation.GetLatest();

            if (vessel != null)
            {
                FailureSimulationSnapshot failures =
                    _failureEngine.GetSnapshot(
                        vessel.VesselId,
                        generatedUtc);

                model.FailureSimulation =
                    failures;

                SyntheticFailureEngine.ApplyComponentFailures(
                    model,
                    failures);

                ElectricalControlSnapshot controls =
                    ElectricalControlCommandStore.GetSnapshot(
                        vessel.VesselId);

                model.ElectricalDistribution =
                    _electrical.BuildAndApply(
                        model,
                        generatedUtc,
                        controls,
                        failures);

                /*
                 * Build 14.18.8:
                 * RCS controller / valve-solenoid power is an essential
                 * spacecraft load on BUS_ESS.
                 *
                 * This is an overlay on the existing 14.11.x distribution
                 * template so no project-file or base-template rewrite is
                 * required. The branch is priority 1 and therefore is not
                 * eligible for automatic priority-3 load shedding.
                 *
                 * The external EngineeringEngine pass will recalculate again
                 * after real KSP generation/storage evidence is applied.
                 */
                EnsureRcsElectricalControlLoad(
                    model.ElectricalDistribution,
                    controls);

                SyntheticElectricalDistributionSystem.Recalculate(
                    model.ElectricalDistribution);
            }

            lock (_syncRoot)
            {
                _latest =
                    model;
            }
        }

        private static void EnsureRcsElectricalControlLoad(
            SyntheticElectricalDistributionModel distribution,
            ElectricalControlSnapshot controls)
        {
            if (distribution == null)
            {
                return;
            }

            const string equipmentId =
                "RCS_CONTROL";

            const string breakerId =
                "BRK_RCS_CONTROL";

            SyntheticElectricalLoad load =
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
                        equipmentId,
                        StringComparison.Ordinal))
                {
                    load =
                        candidate;

                    break;
                }
            }

            if (load == null)
            {
                load =
                    new SyntheticElectricalLoad
                    {
                        EquipmentId =
                            equipmentId,

                        DisplayName =
                            "RCS CONTROL / VALVE POWER",

                        BusId =
                            "BUS_ESS",

                        BreakerId =
                            breakerId,

                        DemandAmps =
                            1.0,

                        Priority =
                            1,

                        CommandedOn =
                            true
                    };

                distribution.Loads.Add(
                    load);
            }

            SyntheticElectricalSwitch breaker =
                distribution.FindSwitch(
                    breakerId);

            if (breaker == null)
            {
                breaker =
                    new SyntheticElectricalSwitch
                    {
                        Id =
                            breakerId,

                        DisplayName =
                            "RCS CONTROL / VALVE POWER BREAKER",

                        UpstreamId =
                            "BUS_ESS",

                        DownstreamId =
                            equipmentId,

                        Kind =
                            SyntheticElectricalSwitchKind.LoadBreaker,

                        CommandedClosed =
                            true,

                        ActualClosed =
                            true,

                        IndicatedClosed =
                            true,

                        Conducting =
                            false,

                        Automatic =
                            false
                    };

                distribution.Switches.Add(
                    breaker);
            }

            bool commandedOn;

            if (controls != null &&
                controls.TryGet(
                    equipmentId,
                    out commandedOn))
            {
                load.CommandedOn =
                    commandedOn;

                breaker.CommandedClosed =
                    commandedOn;
            }
        }

        public bool SetFailureSimulationMode(
            string vesselId,
            FailureSimulationMode mode,
            out string resultText)
        {
            return
                _failureEngine.SetMode(
                    vesselId,
                    mode,
                    out resultText);
        }

        public bool InjectFailure(
            SyntheticFailureRequest request,
            out string failureId,
            out string resultText)
        {
            return
                _failureEngine.Inject(
                    request,
                    out failureId,
                    out resultText);
        }

        public bool ClearFailure(
            string vesselId,
            string failureId,
            out string resultText)
        {
            return
                _failureEngine.ClearFailure(
                    vesselId,
                    failureId,
                    out resultText);
        }

        public FailureSimulationSnapshot GetFailureSimulationSnapshot(
            string vesselId,
            DateTime generatedUtc)
        {
            return
                _failureEngine.GetSnapshot(
                    vesselId,
                    generatedUtc);
        }

        public SpacecraftSystemsModel GetLatest()
        {
            lock (_syncRoot)
            {
                return
                    _latest != null
                        ? _latest.Clone()
                        : new SpacecraftSystemsModel();
            }
        }
    }

    public sealed class ElectricalControlSnapshot
    {
        private readonly Dictionary<string, bool>
            _states;

        internal ElectricalControlSnapshot(
            Dictionary<string, bool> states)
        {
            _states =
                states ??
                new Dictionary<string, bool>(
                    StringComparer.Ordinal);
        }

        public bool TryGet(
            string controlId,
            out bool commandedOn)
        {
            commandedOn = true;

            if (string.IsNullOrWhiteSpace(controlId))
            {
                return false;
            }

            return _states.TryGetValue(
                controlId,
                out commandedOn);
        }
    }

    public static class ElectricalControlCommandStore
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly
            Dictionary<string, Dictionary<string, bool>>
            ByVessel =
                new Dictionary<string, Dictionary<string, bool>>(
                    StringComparer.Ordinal);

        public static void Publish(
            string vesselId,
            string controlId,
            bool commandedOn)
        {
            if (string.IsNullOrWhiteSpace(vesselId) ||
                string.IsNullOrWhiteSpace(controlId))
            {
                return;
            }

            lock (SyncRoot)
            {
                Dictionary<string, bool> states;

                if (!ByVessel.TryGetValue(vesselId, out states))
                {
                    states =
                        new Dictionary<string, bool>(
                            StringComparer.Ordinal);

                    ByVessel[vesselId] = states;
                }

                states[controlId] = commandedOn;
            }
        }

        public static void Reset(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return;
            }

            lock (SyncRoot)
            {
                ByVessel.Remove(vesselId);
            }
        }

        public static ElectricalControlSnapshot GetSnapshot(
            string vesselId)
        {
            lock (SyncRoot)
            {
                Dictionary<string, bool> copy =
                    new Dictionary<string, bool>(
                        StringComparer.Ordinal);

                Dictionary<string, bool> states;

                if (!string.IsNullOrWhiteSpace(vesselId) &&
                    ByVessel.TryGetValue(vesselId, out states))
                {
                    foreach (
                        KeyValuePair<string, bool> entry in states)
                    {
                        copy[entry.Key] = entry.Value;
                    }
                }

                return new ElectricalControlSnapshot(copy);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                ByVessel.Clear();
            }
        }
    }

    // ---------------------------------------------------------------------
    // Build 14.18.7 / 14.18.8 RCS authority + ESS control-power foundation.
    // Kept in this already-compiled file so no project-file change is needed.
    // ---------------------------------------------------------------------
    public sealed class RcsAuthoritySnapshot
    {
        public RcsAuthoritySnapshot()
        {
            VesselId = string.Empty;
            Known = false;
            RcsPartCount = 0;
            HardwareDetected = false;
            InstructorInhibited = false;
            ElectricalPowerKnown = false;
            ElectricalPowered = true;
            ElectricalUnpowered = false;
            ElectricalBusId = "BUS_ESS";
            ElectricalVoltage = 0.0;
            AuthorityAvailable = false;
            Detail = "UNKNOWN";
        }

        public string VesselId { get; internal set; }
        public bool Known { get; internal set; }
        public int RcsPartCount { get; internal set; }
        public bool HardwareDetected { get; internal set; }
        public bool InstructorInhibited { get; internal set; }
        public bool ElectricalPowerKnown { get; internal set; }
        public bool ElectricalPowered { get; internal set; }
        public bool ElectricalUnpowered { get; internal set; }
        public string ElectricalBusId { get; internal set; }
        public double ElectricalVoltage { get; internal set; }
        public bool AuthorityAvailable { get; internal set; }
        public string Detail { get; internal set; }
    }

    public static class RcsAuthorityStore
    {
        private static readonly object RcsSyncRoot =
            new object();

        private static readonly Dictionary<string, MutableRcsAuthorityState>
            RcsByVessel =
                new Dictionary<string, MutableRcsAuthorityState>(
                    StringComparer.Ordinal);

        public static void PublishHardware(
            string vesselId,
            int rcsPartCount)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (RcsSyncRoot)
            {
                MutableRcsAuthorityState state =
                    GetOrCreateRcs(vesselId);

                state.HardwareKnown = true;
                state.RcsPartCount =
                    Math.Max(0, rcsPartCount);
            }
        }

        public static void SetInstructorInhibit(
            string vesselId,
            bool inhibited)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (RcsSyncRoot)
            {
                GetOrCreateRcs(vesselId)
                    .InstructorInhibited = inhibited;
            }
        }

        // Build 14.18.8 authoritative RCS control-power evidence.
        public static void PublishElectricalPower(
            string vesselId,
            bool known,
            bool powered,
            string busId,
            double voltage)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (RcsSyncRoot)
            {
                MutableRcsAuthorityState state =
                    GetOrCreateRcs(vesselId);

                state.ElectricalPowerKnown =
                    known;

                state.ElectricalPowered =
                    !known || powered;

                state.ElectricalBusId =
                    string.IsNullOrWhiteSpace(busId)
                        ? "BUS_ESS"
                        : busId;

                state.ElectricalVoltage =
                    known
                        ? Math.Max(0.0, voltage)
                        : 0.0;
            }
        }

        // Compatibility hook retained for later integrations.
        public static void SetElectricalUnpowered(
            string vesselId,
            bool unpowered)
        {
            PublishElectricalPower(
                vesselId,
                true,
                !unpowered,
                "BUS_ESS",
                unpowered
                    ? 0.0
                    : 28.0);
        }

        public static RcsAuthoritySnapshot GetSnapshot(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return new RcsAuthoritySnapshot();

            lock (RcsSyncRoot)
            {
                MutableRcsAuthorityState state;

                if (!RcsByVessel.TryGetValue(vesselId, out state) ||
                    state == null)
                {
                    return new RcsAuthoritySnapshot
                    {
                        VesselId = vesselId
                    };
                }

                bool hardwareDetected =
                    state.HardwareKnown &&
                    state.RcsPartCount > 0;

                bool electricalUnpowered =
                    state.ElectricalPowerKnown &&
                    !state.ElectricalPowered;

                bool authorityAvailable =
                    hardwareDetected &&
                    !state.InstructorInhibited &&
                    !electricalUnpowered;

                string detail;

                if (!state.HardwareKnown)
                    detail = "HARDWARE UNKNOWN";
                else if (!hardwareDetected)
                    detail = "NO RCS HARDWARE";
                else if (state.InstructorInhibited)
                    detail = "INSTRUCTOR INHIBIT";
                else if (electricalUnpowered)
                    detail = "CONTROL POWER UNAVAILABLE";
                else if (!state.ElectricalPowerKnown)
                    detail = "CONTROL POWER UNKNOWN / FAIL OPEN";
                else
                    detail = "AVAILABLE";

                return new RcsAuthoritySnapshot
                {
                    VesselId = vesselId,
                    Known = state.HardwareKnown,
                    RcsPartCount = state.RcsPartCount,
                    HardwareDetected = hardwareDetected,
                    InstructorInhibited = state.InstructorInhibited,
                    ElectricalPowerKnown = state.ElectricalPowerKnown,
                    ElectricalPowered = state.ElectricalPowered,
                    ElectricalUnpowered = electricalUnpowered,
                    ElectricalBusId = state.ElectricalBusId,
                    ElectricalVoltage = state.ElectricalVoltage,
                    AuthorityAvailable = authorityAvailable,
                    Detail = detail
                };
            }
        }

        public static void Reset(
            string vesselId)
        {
            if (string.IsNullOrWhiteSpace(vesselId))
                return;

            lock (RcsSyncRoot)
            {
                RcsByVessel.Remove(vesselId);
            }
        }

        public static void ClearAll()
        {
            lock (RcsSyncRoot)
            {
                RcsByVessel.Clear();
            }
        }

        private static MutableRcsAuthorityState GetOrCreateRcs(
            string vesselId)
        {
            MutableRcsAuthorityState state;

            if (!RcsByVessel.TryGetValue(vesselId, out state) ||
                state == null)
            {
                state = new MutableRcsAuthorityState();
                RcsByVessel[vesselId] = state;
            }

            return state;
        }

        private sealed class MutableRcsAuthorityState
        {
            public bool HardwareKnown;
            public int RcsPartCount;
            public bool InstructorInhibited;
            public bool ElectricalPowerKnown;
            public bool ElectricalPowered = true;
            public string ElectricalBusId = "BUS_ESS";
            public double ElectricalVoltage;
        }
    }
}
