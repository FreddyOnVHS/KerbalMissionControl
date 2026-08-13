using System;
using System.Collections.Generic;
using System.Diagnostics;
using KMC.Engine;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.Maneuver;
using KMC.Engine.Orbit;
using KMC.Engine.Propulsion;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Debugging;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Rendering.Propulsion;
using KMC.MissionControl.Telemetry;
using KMC.MissionControl.Transport;
using KMC.MissionControl.Training;
using KMC.Shared;
using KMC.Shared.Topology;

namespace KMC.MissionControl
{
    /// <summary>
    /// Coordinates typed telemetry with KMC.Engine.
    /// Socket ownership lives in MissionControl transport classes.
    /// </summary>
    public sealed class MissionControlReceiver :
        IDisposable
    {
        private readonly EngineeringEngine _engineeringEngine;
        private readonly TelemetryTransport _transport;
        private readonly ManeuverLinkTransport _maneuverLink;
        private readonly PowerFailureIntegrationController _powerFailureIntegration;
        private readonly PropulsionFailureIntegrationController _propulsionFailureIntegration;
        private readonly GncFailureIntegrationController _gncFailureIntegration;
        private readonly TelemetryCache _cache;
        private readonly object _engineeringSyncRoot;

        private bool _running;
        private long _engineeringSequence;
        private string _powerTrainingFailureId;
        private string _powerTrainingVesselId;
        private string _propDerateTrainingFailureId;
        private string _propDerateTrainingVesselId;
        private string _propShutdownTrainingFailureId;
        private string _propShutdownTrainingVesselId;
        private string _gncTrainingFailureId;
        private string _gncTrainingVesselId;
        private string _commATrainingFailureId;
        private string _commATrainingVesselId;

        public MissionControlReceiver()
        {
            _engineeringEngine =
                new EngineeringEngine();

            _transport =
                new TelemetryTransport();

            _maneuverLink =
                new ManeuverLinkTransport();

            _powerFailureIntegration =
                new PowerFailureIntegrationController();

            _propulsionFailureIntegration =
                new PropulsionFailureIntegrationController();

            _gncFailureIntegration =
                new GncFailureIntegrationController();

            _cache =
                new TelemetryCache();

            _engineeringSyncRoot =
                new object();

            _transport.FlightTelemetryReceived +=
                OnFlightTelemetryReceived;

            _transport.TopologyReceived +=
                OnTopologyReceived;

            _transport.SystemsTelemetryReceived +=
                OnSystemsTelemetryReceived;

            _transport.EngineStateTelemetryReceived +=
                OnEngineStateTelemetryReceived;

            _transport.VelocityVectorTelemetryReceived +=
                OnVelocityVectorTelemetryReceived;

            _maneuverLink.EpochReceived +=
                OnManeuverEpochReceived;

            _maneuverLink.AcknowledgmentReceived +=
                OnManeuverAcknowledgmentReceived;

            _maneuverLink.NodeStateReceived +=
                OnManeuverNodeStateReceived;
        }

        public event Action<TelemetryPacket> TelemetryReceived;
        public event Action<VesselTopology> TopologyReceived;
        public event Action<ManeuverUplinkAck> ManeuverAcknowledgmentReceived;

        public void Start()
        {
            if (_running)
            {
                return;
            }

            EngineeringSnapshotStore.Clear();
            ManeuverUplinkStatusStore.Clear();

            _engineeringEngine.ClearElectricalTelemetry();
            _engineeringEngine.ClearPropulsionTelemetry();
            _engineeringEngine.ClearVelocityVectorTelemetry();
            _engineeringEngine.ClearManeuverEpochTelemetry();

            EngineStateTelemetryStore.Clear();
            _cache.Clear();

            lock (_engineeringSyncRoot)
            {
                _engineeringSequence = 0;
                _powerTrainingFailureId = string.Empty;
                _powerTrainingVesselId = string.Empty;
                _propDerateTrainingFailureId = string.Empty;
                _propDerateTrainingVesselId = string.Empty;
                _propShutdownTrainingFailureId = string.Empty;
                _propShutdownTrainingVesselId = string.Empty;
                _gncTrainingFailureId = string.Empty;
                _gncTrainingVesselId = string.Empty;
                _commATrainingFailureId = string.Empty;
                _commATrainingVesselId = string.Empty;
            }

            _maneuverLink.Start();
            _transport.Start();

            _running = true;
        }

        /// <summary>
        /// Build 14.5 explicit developer/training toggle used to prove the
        /// complete 14.3 -> 14.4 POWER integration path. No automatic failure
        /// generation is enabled by this method.
        /// </summary>
        public bool TogglePowerFailureTrainingLeak(
            out string resultText)
        {
            resultText = string.Empty;

            if (!string.IsNullOrWhiteSpace(
                    _powerTrainingFailureId))
            {
                string clearResult;

                bool cleared =
                    _engineeringEngine.ClearSyntheticFailure(
                        _powerTrainingVesselId,
                        _powerTrainingFailureId,
                        out clearResult);

                if (cleared)
                {
                    resultText =
                        "CLEARED " +
                        _powerTrainingFailureId +
                        " / " +
                        SyntheticFailureTargets.ElectricChargeLeak;

                    _powerTrainingFailureId =
                        string.Empty;
                    _powerTrainingVesselId =
                        string.Empty;
                }
                else
                {
                    resultText = clearResult;
                }

                return cleared;
            }

            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(
                    latest.Snapshot.Vessel.VesselId))
            {
                resultText =
                    "NO ACTIVE ENGINEERING VESSEL";

                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems.FailureSimulation
                    : null;

            if (current == null ||
                current.Mode ==
                    FailureSimulationMode.Nominal)
            {
                string modeResult;

                if (!_engineeringEngine.SetFailureSimulationMode(
                        vesselId,
                        FailureSimulationMode.Training,
                        out modeResult))
                {
                    resultText = modeResult;
                    return false;
                }
            }

            string failureId;
            string injectResult;

            bool injected =
                _engineeringEngine.InjectSyntheticFailure(
                    new SyntheticFailureRequest
                    {
                        VesselId = vesselId,
                        TargetId =
                            SyntheticFailureTargets.ElectricChargeLeak,
                        TargetKind =
                            SyntheticFailureTargetKind.PowerEffect,
                        Kind =
                            SyntheticFailureKind.Sudden,
                        Severity =
                            SyntheticFailureSeverity.Caution,
                        ComponentHealth =
                            SpacecraftSystemHealth.Degraded,
                        EffectMagnitude = 8.0,
                        ActivateUtc = DateTime.UtcNow,
                        Detail =
                            "BUILD 14.5 EXPLICIT POWER INTEGRATION TEST"
                    },
                    out failureId,
                    out injectResult);

            if (!injected)
            {
                resultText = injectResult;
                return false;
            }

            _powerTrainingFailureId =
                failureId;
            _powerTrainingVesselId =
                vesselId;

            resultText =
                "INJECTED " +
                failureId +
                " / " +
                SyntheticFailureTargets.ElectricChargeLeak +
                " / 8.00 EC/S";

            return true;
        }

        /// <summary>
        /// Build 14.6 explicit training toggle for a 50% derate on one exact
        /// currently observed non-SRB engine. The chosen PartId is embedded
        /// into failure truth and remains immutable for that failure.
        /// </summary>
        public bool TogglePropulsionDerateTrainingFailure(
            out string resultText)
        {
            if (!string.IsNullOrWhiteSpace(
                    _propDerateTrainingFailureId))
            {
                return
                    ClearPropulsionTrainingFailure(
                        ref _propDerateTrainingFailureId,
                        ref _propDerateTrainingVesselId,
                        out resultText);
            }

            uint partId;

            if (!TrySelectPropulsionTrainingEngine(
                    out partId,
                    out resultText))
            {
                return false;
            }

            return
                InjectPropulsionTrainingFailure(
                    partId,
                    false,
                    0.50,
                    ref _propDerateTrainingFailureId,
                    ref _propDerateTrainingVesselId,
                    out resultText);
        }

        /// <summary>
        /// Build 14.6 explicit training toggle for shutdown of one exact
        /// currently observed non-SRB engine. Use only during a controlled
        /// ground/flight test.
        /// </summary>
        public bool TogglePropulsionShutdownTrainingFailure(
            out string resultText)
        {
            if (!string.IsNullOrWhiteSpace(
                    _propShutdownTrainingFailureId))
            {
                return
                    ClearPropulsionTrainingFailure(
                        ref _propShutdownTrainingFailureId,
                        ref _propShutdownTrainingVesselId,
                        out resultText);
            }

            uint partId;

            if (!TrySelectPropulsionTrainingEngine(
                    out partId,
                    out resultText))
            {
                return false;
            }

            return
                InjectPropulsionTrainingFailure(
                    partId,
                    true,
                    1.0,
                    ref _propShutdownTrainingFailureId,
                    ref _propShutdownTrainingVesselId,
                    out resultText);
        }

        private bool InjectPropulsionTrainingFailure(
            uint partId,
            bool shutdown,
            double magnitude,
            ref string failureIdStore,
            ref string vesselIdStore,
            out string resultText)
        {
            resultText = string.Empty;

            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(
                    latest.Snapshot.Vessel.VesselId))
            {
                resultText =
                    "NO ACTIVE ENGINEERING VESSEL";

                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems.FailureSimulation
                    : null;

            if (current == null ||
                current.Mode ==
                    FailureSimulationMode.Nominal)
            {
                string modeResult;

                if (!_engineeringEngine.SetFailureSimulationMode(
                        vesselId,
                        FailureSimulationMode.Training,
                        out modeResult))
                {
                    resultText = modeResult;
                    return false;
                }
            }

            string targetId =
                shutdown
                    ? SyntheticFailureTargets.CreateEngineShutdownTarget(
                        partId)
                    : SyntheticFailureTargets.CreateEngineDerateTarget(
                        partId);

            string failureId;
            string injectResult;

            bool injected =
                _engineeringEngine.InjectSyntheticFailure(
                    new SyntheticFailureRequest
                    {
                        VesselId = vesselId,
                        TargetId = targetId,
                        TargetKind =
                            SyntheticFailureTargetKind.PropulsionEffect,
                        Kind =
                            SyntheticFailureKind.Sudden,
                        Severity =
                            shutdown
                                ? SyntheticFailureSeverity.Critical
                                : SyntheticFailureSeverity.Caution,
                        ComponentHealth =
                            shutdown
                                ? SpacecraftSystemHealth.Failed
                                : SpacecraftSystemHealth.Degraded,
                        EffectMagnitude = magnitude,
                        ActivateUtc = DateTime.UtcNow,
                        Detail =
                            shutdown
                                ? "BUILD 14.6 EXPLICIT ENGINE SHUTDOWN TEST"
                                : "BUILD 14.6 EXPLICIT ENGINE DERATE TEST"
                    },
                    out failureId,
                    out injectResult);

            if (!injected)
            {
                resultText = injectResult;
                return false;
            }

            failureIdStore =
                failureId;
            vesselIdStore =
                vesselId;

            resultText =
                "INJECTED " +
                failureId +
                " / " +
                targetId +
                (shutdown
                    ? " / SHUTDOWN"
                    : " / 50% DERATE");

            return true;
        }

        private bool ClearPropulsionTrainingFailure(
            ref string failureIdStore,
            ref string vesselIdStore,
            out string resultText)
        {
            string failureId =
                failureIdStore;
            string vesselId =
                vesselIdStore;

            string clearResult;

            bool cleared =
                _engineeringEngine.ClearSyntheticFailure(
                    vesselId,
                    failureId,
                    out clearResult);

            if (cleared)
            {
                resultText =
                    "CLEARED " +
                    failureId;

                failureIdStore =
                    string.Empty;
                vesselIdStore =
                    string.Empty;
            }
            else
            {
                resultText =
                    clearResult;
            }

            return cleared;
        }

        private static bool TrySelectPropulsionTrainingEngine(
            out uint partId,
            out string resultText)
        {
            partId = 0;
            resultText = string.Empty;

            Dictionary<uint, EngineStateTelemetry> engines =
                EngineStateTelemetryStore.GetSnapshot();

            double bestMaximumThrust =
                double.NegativeInfinity;

            foreach (
                KeyValuePair<uint, EngineStateTelemetry> pair
                in engines)
            {
                EngineStateTelemetry engine =
                    pair.Value;

                if (engine == null ||
                    engine.PartId == 0 ||
                    engine.IsSolidBooster)
                {
                    continue;
                }

                double maximumThrust =
                    engine.MaximumThrust;

                if (partId == 0 ||
                    maximumThrust >
                        bestMaximumThrust ||
                    (Math.Abs(
                         maximumThrust -
                         bestMaximumThrust) <
                     0.0001 &&
                     engine.PartId <
                         partId))
                {
                    partId =
                        engine.PartId;
                    bestMaximumThrust =
                        maximumThrust;
                }
            }

            if (partId == 0)
            {
                resultText =
                    "NO LIVE NON-SRB ENGINE TELEMETRY";

                return false;
            }

            resultText =
                "SELECTED ENGINE PART " +
                partId.ToString();

            return true;
        }

        /// <summary>
        /// Build 14.7 explicit training toggle for 25% authority on one exact
        /// reaction-wheel part discovered from the live topology.
        /// </summary>
        public bool ToggleGncReactionWheelTrainingFailure(
            out string resultText)
        {
            if (!string.IsNullOrWhiteSpace(_gncTrainingFailureId))
            {
                return ClearTrainingFailure(
                    ref _gncTrainingFailureId,
                    ref _gncTrainingVesselId,
                    out resultText);
            }

            uint partId;
            if (!TrySelectReactionWheelPart(out partId, out resultText))
                return false;

            AnalysisPipelineResult latest;
            if (!TryGetActiveEngineeringVessel(out latest, out resultText))
                return false;

            string vesselId = latest.Snapshot.Vessel.VesselId;
            if (!EnsureTrainingMode(vesselId, latest, out resultText))
                return false;

            string targetId =
                SyntheticFailureTargets.CreateReactionWheelAuthorityTarget(partId);
            string failureId;
            string injectResult;
            bool injected = _engineeringEngine.InjectSyntheticFailure(
                new SyntheticFailureRequest
                {
                    VesselId = vesselId,
                    TargetId = targetId,
                    TargetKind = SyntheticFailureTargetKind.GuidanceEffect,
                    Kind = SyntheticFailureKind.Sudden,
                    Severity = SyntheticFailureSeverity.Caution,
                    ComponentHealth = SpacecraftSystemHealth.Degraded,
                    EffectMagnitude = 0.25,
                    ActivateUtc = DateTime.UtcNow,
                    Detail = "BUILD 14.7 EXPLICIT GNC REACTION WHEEL TEST"
                },
                out failureId,
                out injectResult);

            if (!injected)
            {
                resultText = injectResult;
                return false;
            }

            _gncTrainingFailureId = failureId;
            _gncTrainingVesselId = vesselId;
            resultText = "INJECTED " + failureId + " / " +
                targetId + " / 25% AUTHORITY";
            return true;
        }

        /// <summary>
        /// Build 14.7 synthetic COMM-A failure. This changes KMC spacecraft
        /// system truth only; it does not claim or mutate stock-KSP RF state.
        /// </summary>
        public bool ToggleCommATrainingFailure(
            out string resultText)
        {
            if (!string.IsNullOrWhiteSpace(_commATrainingFailureId))
            {
                return ClearTrainingFailure(
                    ref _commATrainingFailureId,
                    ref _commATrainingVesselId,
                    out resultText);
            }

            AnalysisPipelineResult latest;
            if (!TryGetActiveEngineeringVessel(out latest, out resultText))
                return false;

            string vesselId = latest.Snapshot.Vessel.VesselId;
            if (!EnsureTrainingMode(vesselId, latest, out resultText))
                return false;

            string failureId;
            string injectResult;
            bool injected = _engineeringEngine.InjectSyntheticFailure(
                new SyntheticFailureRequest
                {
                    VesselId = vesselId,
                    TargetId = "COMM_A",
                    TargetKind = SyntheticFailureTargetKind.Component,
                    Kind = SyntheticFailureKind.Sudden,
                    Severity = SyntheticFailureSeverity.Caution,
                    ComponentHealth = SpacecraftSystemHealth.Failed,
                    ActivateUtc = DateTime.UtcNow,
                    Detail = "BUILD 14.7 EXPLICIT COMM-A SYNTHETIC FAILURE TEST"
                },
                out failureId,
                out injectResult);

            if (!injected)
            {
                resultText = injectResult;
                return false;
            }

            _commATrainingFailureId = failureId;
            _commATrainingVesselId = vesselId;
            resultText = "INJECTED " + failureId + " / COMM_A / FAILED";
            return true;
        }

        private bool TrySelectReactionWheelPart(
            out uint partId,
            out string resultText)
        {
            partId = 0;
            resultText = string.Empty;
            VesselTopology topology = _cache.GetTopology();
            if (topology == null || topology.Nodes == null)
            {
                resultText = "NO LIVE TOPOLOGY";
                return false;
            }

            for (int i = 0; i < topology.Nodes.Count; i++)
            {
                VesselTopologyNode node = topology.Nodes[i];
                if (node == null || node.PartId == 0 || node.Modules == null)
                    continue;

                for (int m = 0; m < node.Modules.Count; m++)
                {
                    VesselModuleDescriptor module = node.Modules[m];
                    if (module == null) continue;
                    if (string.Equals(module.ModuleName, "ModuleReactionWheel",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(module.ModuleTypeName, "ModuleReactionWheel",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (partId == 0 || node.PartId < partId)
                            partId = node.PartId;
                    }
                }
            }

            if (partId == 0)
            {
                resultText = "NO REACTION WHEEL IN LIVE TOPOLOGY";
                return false;
            }

            resultText = "SELECTED REACTION WHEEL PART " + partId.ToString();
            return true;
        }

        private static bool TryGetActiveEngineeringVessel(
            out AnalysisPipelineResult latest,
            out string resultText)
        {
            resultText = string.Empty;
            if (!EngineeringSnapshotStore.TryGetLatest(out latest) ||
                latest == null || latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(latest.Snapshot.Vessel.VesselId))
            {
                resultText = "NO ACTIVE ENGINEERING VESSEL";
                return false;
            }
            return true;
        }

        private bool EnsureTrainingMode(
            string vesselId,
            AnalysisPipelineResult latest,
            out string resultText)
        {
            resultText = string.Empty;
            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems.FailureSimulation
                    : null;
            if (current != null && current.Mode != FailureSimulationMode.Nominal)
                return true;

            return _engineeringEngine.SetFailureSimulationMode(
                vesselId,
                FailureSimulationMode.Training,
                out resultText);
        }

        private bool ClearTrainingFailure(
            ref string failureIdStore,
            ref string vesselIdStore,
            out string resultText)
        {
            string failureId = failureIdStore;
            string vesselId = vesselIdStore;
            string clearResult;
            bool cleared = _engineeringEngine.ClearSyntheticFailure(
                vesselId, failureId, out clearResult);
            if (cleared)
            {
                resultText = "CLEARED " + failureId;
                failureIdStore = string.Empty;
                vesselIdStore = string.Empty;
            }
            else resultText = clearResult;
            return cleared;
        }


        /// <summary>
        /// Build 14.9 instructor console snapshot. This exposes only the
        /// Engine-owned failure snapshot for the current engineering vessel.
        /// </summary>
        public bool TryGetInstructorFailureSnapshot(
            out string vesselId,
            out string vesselName,
            out FailureSimulationSnapshot snapshot,
            out string resultText)
        {
            vesselId = string.Empty;
            vesselName = string.Empty;
            snapshot = null;
            resultText = string.Empty;

            AnalysisPipelineResult latest;

            if (!TryGetActiveEngineeringVessel(
                    out latest,
                    out resultText))
            {
                return false;
            }

            vesselId =
                latest.Snapshot.Vessel.VesselId;

            vesselName =
                latest.Snapshot.Vessel.VesselName ??
                string.Empty;

            snapshot =
                _engineeringEngine.GetFailureSimulationSnapshot(
                    vesselId);

            return snapshot != null;
        }

        /// <summary>
        /// Build 14.9 explicit instructor mode selection.
        /// NOMINAL uses the reset path so dormant uncleared failures cannot
        /// unexpectedly reactivate if Training/Scenario is selected later.
        /// </summary>
        public bool SetInstructorFailureMode(
            FailureSimulationMode mode,
            out string resultText)
        {
            if (mode ==
                FailureSimulationMode.Nominal)
            {
                return
                    ResetInstructorNominal(
                        out resultText);
            }

            AnalysisPipelineResult latest;

            if (!TryGetActiveEngineeringVessel(
                    out latest,
                    out resultText))
            {
                return false;
            }

            return
                _engineeringEngine.SetFailureSimulationMode(
                    latest.Snapshot.Vessel.VesselId,
                    mode,
                    out resultText);
        }

        /// <summary>
        /// Build 14.9 explicit instructor failure injection. Optional delay is
        /// represented by ActivateUtc in Engine truth, not by a UI timer.
        /// </summary>
        public bool InjectInstructorFailure(
            InstructorFailurePreset preset,
            double delaySeconds,
            out string failureId,
            out string resultText)
        {
            failureId = string.Empty;
            resultText = string.Empty;

            AnalysisPipelineResult latest;

            if (!TryGetActiveEngineeringVessel(
                    out latest,
                    out resultText))
            {
                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems.FailureSimulation
                    : null;

            if (current == null ||
                current.Mode ==
                    FailureSimulationMode.Nominal)
            {
                string modeResult;

                if (!_engineeringEngine.SetFailureSimulationMode(
                        vesselId,
                        FailureSimulationMode.Training,
                        out modeResult))
                {
                    resultText = modeResult;
                    return false;
                }
            }

            SyntheticFailureRequest request;

            if (!TryBuildInstructorFailureRequest(
                    preset,
                    vesselId,
                    Math.Max(
                        0.0,
                        Math.Min(
                            300.0,
                            delaySeconds)),
                    out request,
                    out resultText))
            {
                return false;
            }

            string injectResult;

            bool injected =
                _engineeringEngine.InjectSyntheticFailure(
                    request,
                    out failureId,
                    out injectResult);

            resultText =
                injected
                    ? "INJECTED " +
                      failureId +
                      " / " +
                      request.TargetId +
                      (request.ActivateUtc >
                           DateTime.UtcNow.AddSeconds(0.25)
                          ? " / SCHEDULED"
                          : " / IMMEDIATE")
                    : injectResult;

            Debug.WriteLine(
                "KMC.MissionControl INSTRUCTOR FAILURE" +
                " | Success=" +
                injected +
                " | Preset=" +
                preset.ToString() +
                " | Result=" +
                resultText);

            return injected;
        }

        public bool ClearInstructorFailure(
            string failureId,
            out string resultText)
        {
            resultText = string.Empty;

            if (string.IsNullOrWhiteSpace(
                    failureId))
            {
                resultText =
                    "FAILURE ID REQUIRED";

                return false;
            }

            AnalysisPipelineResult latest;

            if (!TryGetActiveEngineeringVessel(
                    out latest,
                    out resultText))
            {
                return false;
            }

            return
                _engineeringEngine.ClearSyntheticFailure(
                    latest.Snapshot.Vessel.VesselId,
                    failureId,
                    out resultText);
        }

        public bool ClearAllInstructorFailures(
            out string resultText)
        {
            resultText = string.Empty;

            string vesselId;
            string vesselName;
            FailureSimulationSnapshot snapshot;

            if (!TryGetInstructorFailureSnapshot(
                    out vesselId,
                    out vesselName,
                    out snapshot,
                    out resultText))
            {
                return false;
            }

            int cleared = 0;
            int rejected = 0;

            for (int index = 0;
                 index < snapshot.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[index];

                if (failure == null ||
                    failure.Condition ==
                        SyntheticFailureCondition.Cleared)
                {
                    continue;
                }

                string clearResult;

                if (_engineeringEngine.ClearSyntheticFailure(
                        vesselId,
                        failure.FailureId,
                        out clearResult))
                {
                    cleared++;
                }
                else
                {
                    rejected++;
                }
            }

            resultText =
                "CLEAR ALL / CLEARED " +
                cleared.ToString() +
                " / REJECTED " +
                rejected.ToString();

            return rejected == 0;
        }

        public bool ResetInstructorNominal(
            out string resultText)
        {
            resultText = string.Empty;

            AnalysisPipelineResult latest;

            if (!TryGetActiveEngineeringVessel(
                    out latest,
                    out resultText))
            {
                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot snapshot =
                _engineeringEngine.GetFailureSimulationSnapshot(
                    vesselId);

            int cleared = 0;

            if (snapshot != null)
            {
                for (int index = 0;
                     index < snapshot.Failures.Count;
                     index++)
                {
                    SyntheticFailureRecord failure =
                        snapshot.Failures[index];

                    if (failure == null ||
                        failure.Condition ==
                            SyntheticFailureCondition.Cleared)
                    {
                        continue;
                    }

                    string clearResult;

                    if (_engineeringEngine.ClearSyntheticFailure(
                            vesselId,
                            failure.FailureId,
                            out clearResult))
                    {
                        cleared++;
                    }
                }
            }

            string modeResult;

            bool modeSet =
                _engineeringEngine.SetFailureSimulationMode(
                    vesselId,
                    FailureSimulationMode.Nominal,
                    out modeResult);

            resultText =
                modeSet
                    ? "RESET NOMINAL / CLEARED " +
                      cleared.ToString() +
                      " / " +
                      modeResult
                    : modeResult;

            return modeSet;
        }

        /// <summary>
        /// Build 14.9 first predefined scenario. Scenario scheduling lives in
        /// Engine failure records: COMM A immediate, GUID A after 10 seconds,
        /// then PUMP A after 20 seconds as a chained cascade.
        /// </summary>
        public bool StartInstructorScenario(
            InstructorScenarioPreset scenario,
            out string resultText)
        {
            resultText = string.Empty;

            AnalysisPipelineResult latest;

            if (!TryGetActiveEngineeringVessel(
                    out latest,
                    out resultText))
            {
                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            string modeResult;

            if (!_engineeringEngine.SetFailureSimulationMode(
                    vesselId,
                    FailureSimulationMode.Scenario,
                    out modeResult))
            {
                resultText = modeResult;
                return false;
            }

            switch (scenario)
            {
                case InstructorScenarioPreset.ASideSystemsCascade:
                    return
                        StartASideSystemsScenario(
                            vesselId,
                            out resultText);

                default:
                    resultText =
                        "UNSUPPORTED SCENARIO";

                    return false;
            }
        }

        private bool StartASideSystemsScenario(
            string vesselId,
            out string resultText)
        {
            resultText = string.Empty;

            DateTime now =
                DateTime.UtcNow;

            string commFailureId;
            string injectResult;

            bool commInjected =
                _engineeringEngine.InjectSyntheticFailure(
                    new SyntheticFailureRequest
                    {
                        VesselId = vesselId,
                        TargetId = "COMM_A",
                        TargetKind =
                            SyntheticFailureTargetKind.Component,
                        Kind =
                            SyntheticFailureKind.Sudden,
                        Severity =
                            SyntheticFailureSeverity.Caution,
                        ComponentHealth =
                            SpacecraftSystemHealth.Failed,
                        ActivateUtc = now,
                        Detail =
                            "BUILD 14.9 A-SIDE SCENARIO / STEP 1 COMM A"
                    },
                    out commFailureId,
                    out injectResult);

            if (!commInjected)
            {
                resultText =
                    "SCENARIO STEP 1 REJECTED / " +
                    injectResult;

                return false;
            }

            string guidFailureId;

            bool guidInjected =
                _engineeringEngine.InjectSyntheticFailure(
                    new SyntheticFailureRequest
                    {
                        VesselId = vesselId,
                        TargetId = "GUID_A",
                        TargetKind =
                            SyntheticFailureTargetKind.Component,
                        Kind =
                            SyntheticFailureKind.Cascade,
                        Severity =
                            SyntheticFailureSeverity.Caution,
                        ComponentHealth =
                            SpacecraftSystemHealth.Failed,
                        ActivateUtc =
                            now.AddSeconds(10.0),
                        ParentFailureId =
                            commFailureId,
                        Detail =
                            "BUILD 14.9 A-SIDE SCENARIO / STEP 2 GUID A"
                    },
                    out guidFailureId,
                    out injectResult);

            if (!guidInjected)
            {
                string ignored;
                _engineeringEngine.ClearSyntheticFailure(
                    vesselId,
                    commFailureId,
                    out ignored);

                resultText =
                    "SCENARIO STEP 2 REJECTED / " +
                    injectResult;

                return false;
            }

            string pumpFailureId;

            bool pumpInjected =
                _engineeringEngine.InjectSyntheticFailure(
                    new SyntheticFailureRequest
                    {
                        VesselId = vesselId,
                        TargetId = "PUMP_A",
                        TargetKind =
                            SyntheticFailureTargetKind.Component,
                        Kind =
                            SyntheticFailureKind.Cascade,
                        Severity =
                            SyntheticFailureSeverity.Caution,
                        ComponentHealth =
                            SpacecraftSystemHealth.Failed,
                        ActivateUtc =
                            now.AddSeconds(20.0),
                        ParentFailureId =
                            guidFailureId,
                        Detail =
                            "BUILD 14.9 A-SIDE SCENARIO / STEP 3 PUMP A"
                    },
                    out pumpFailureId,
                    out injectResult);

            if (!pumpInjected)
            {
                string ignored;
                _engineeringEngine.ClearSyntheticFailure(
                    vesselId,
                    commFailureId,
                    out ignored);
                _engineeringEngine.ClearSyntheticFailure(
                    vesselId,
                    guidFailureId,
                    out ignored);

                resultText =
                    "SCENARIO STEP 3 REJECTED / " +
                    injectResult;

                return false;
            }

            resultText =
                "A-SIDE SYSTEMS CASCADE STARTED" +
                " / COMM_A T+0 " +
                commFailureId +
                " / GUID_A T+10 " +
                guidFailureId +
                " / PUMP_A T+20 " +
                pumpFailureId;

            Debug.WriteLine(
                "KMC.MissionControl INSTRUCTOR SCENARIO" +
                " | VesselId=" +
                vesselId +
                " | Result=" +
                resultText);

            return true;
        }

        private bool TryBuildInstructorFailureRequest(
            InstructorFailurePreset preset,
            string vesselId,
            double delaySeconds,
            out SyntheticFailureRequest request,
            out string resultText)
        {
            request = null;
            resultText = string.Empty;

            DateTime activateUtc =
                DateTime.UtcNow.AddSeconds(
                    delaySeconds);

            switch (preset)
            {
                case InstructorFailurePreset.PowerEcLeak:
                    request =
                        new SyntheticFailureRequest
                        {
                            VesselId = vesselId,
                            TargetId =
                                SyntheticFailureTargets.ElectricChargeLeak,
                            TargetKind =
                                SyntheticFailureTargetKind.PowerEffect,
                            Kind =
                                SyntheticFailureKind.Sudden,
                            Severity =
                                SyntheticFailureSeverity.Caution,
                            ComponentHealth =
                                SpacecraftSystemHealth.Degraded,
                            EffectMagnitude = 8.0,
                            ActivateUtc = activateUtc,
                            Detail =
                                "BUILD 14.9 INSTRUCTOR / POWER EC LEAK"
                        };
                    return true;

                case InstructorFailurePreset.CommA:
                    request =
                        BuildComponentInstructorFailure(
                            vesselId,
                            "COMM_A",
                            activateUtc,
                            "BUILD 14.9 INSTRUCTOR / COMM A");
                    return true;

                case InstructorFailurePreset.CommB:
                    request =
                        BuildComponentInstructorFailure(
                            vesselId,
                            "COMM_B",
                            activateUtc,
                            "BUILD 14.9 INSTRUCTOR / COMM B");
                    return true;

                case InstructorFailurePreset.GuidA:
                    request =
                        BuildComponentInstructorFailure(
                            vesselId,
                            "GUID_A",
                            activateUtc,
                            "BUILD 14.9 INSTRUCTOR / GUID A");
                    return true;

                case InstructorFailurePreset.GuidB:
                    request =
                        BuildComponentInstructorFailure(
                            vesselId,
                            "GUID_B",
                            activateUtc,
                            "BUILD 14.9 INSTRUCTOR / GUID B");
                    return true;

                case InstructorFailurePreset.PumpA:
                    request =
                        BuildComponentInstructorFailure(
                            vesselId,
                            "PUMP_A",
                            activateUtc,
                            "BUILD 14.9 INSTRUCTOR / PUMP A");
                    return true;

                case InstructorFailurePreset.PumpB:
                    request =
                        BuildComponentInstructorFailure(
                            vesselId,
                            "PUMP_B",
                            activateUtc,
                            "BUILD 14.9 INSTRUCTOR / PUMP B");
                    return true;

                case InstructorFailurePreset.EngineDerate50:
                {
                    uint partId;

                    if (!TrySelectPropulsionTrainingEngine(
                            out partId,
                            out resultText))
                    {
                        return false;
                    }

                    request =
                        new SyntheticFailureRequest
                        {
                            VesselId = vesselId,
                            TargetId =
                                SyntheticFailureTargets.CreateEngineDerateTarget(
                                    partId),
                            TargetKind =
                                SyntheticFailureTargetKind.PropulsionEffect,
                            Kind =
                                SyntheticFailureKind.Sudden,
                            Severity =
                                SyntheticFailureSeverity.Caution,
                            ComponentHealth =
                                SpacecraftSystemHealth.Degraded,
                            EffectMagnitude = 0.50,
                            ActivateUtc = activateUtc,
                            Detail =
                                "BUILD 14.9 INSTRUCTOR / ENGINE DERATE"
                        };
                    return true;
                }

                case InstructorFailurePreset.EngineShutdown:
                {
                    uint partId;

                    if (!TrySelectPropulsionTrainingEngine(
                            out partId,
                            out resultText))
                    {
                        return false;
                    }

                    request =
                        new SyntheticFailureRequest
                        {
                            VesselId = vesselId,
                            TargetId =
                                SyntheticFailureTargets.CreateEngineShutdownTarget(
                                    partId),
                            TargetKind =
                                SyntheticFailureTargetKind.PropulsionEffect,
                            Kind =
                                SyntheticFailureKind.Sudden,
                            Severity =
                                SyntheticFailureSeverity.Critical,
                            ComponentHealth =
                                SpacecraftSystemHealth.Failed,
                            EffectMagnitude = 1.0,
                            ActivateUtc = activateUtc,
                            Detail =
                                "BUILD 14.9 INSTRUCTOR / ENGINE SHUTDOWN"
                        };
                    return true;
                }

                case InstructorFailurePreset.ReactionWheel25:
                {
                    uint partId;

                    if (!TrySelectReactionWheelPart(
                            out partId,
                            out resultText))
                    {
                        return false;
                    }

                    request =
                        new SyntheticFailureRequest
                        {
                            VesselId = vesselId,
                            TargetId =
                                SyntheticFailureTargets.CreateReactionWheelAuthorityTarget(
                                    partId),
                            TargetKind =
                                SyntheticFailureTargetKind.GuidanceEffect,
                            Kind =
                                SyntheticFailureKind.Sudden,
                            Severity =
                                SyntheticFailureSeverity.Caution,
                            ComponentHealth =
                                SpacecraftSystemHealth.Degraded,
                            EffectMagnitude = 0.25,
                            ActivateUtc = activateUtc,
                            Detail =
                                "BUILD 14.9 INSTRUCTOR / REACTION WHEEL"
                        };
                    return true;
                }
            }

            resultText =
                "UNSUPPORTED FAILURE PRESET";

            return false;
        }

        private static SyntheticFailureRequest
            BuildComponentInstructorFailure(
                string vesselId,
                string targetId,
                DateTime activateUtc,
                string detail)
        {
            return
                new SyntheticFailureRequest
                {
                    VesselId = vesselId,
                    TargetId = targetId,
                    TargetKind =
                        SyntheticFailureTargetKind.Component,
                    Kind =
                        SyntheticFailureKind.Sudden,
                    Severity =
                        SyntheticFailureSeverity.Caution,
                    ComponentHealth =
                        SpacecraftSystemHealth.Failed,
                    ActivateUtc = activateUtc,
                    Detail = detail
                };
        }

        public bool UploadLatestManeuver(
            out string resultText)
        {
            resultText = string.Empty;

            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.ManeuverPlan == null)
            {
                resultText = "NO ENGINE MANEUVER PLAN";
                ManeuverUplinkStatusStore.PublishRejected(
                    string.Empty,
                    resultText);
                return false;
            }

            ManeuverPlanModel plan =
                result.Snapshot.ManeuverPlan;

            if (!plan.Available)
            {
                resultText = "MANEUVER PLAN IS NOT AVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);
                return false;
            }

            if (!plan.NodeUniversalTimeAvailable ||
                double.IsNaN(plan.NodeUniversalTimeSeconds) ||
                double.IsInfinity(plan.NodeUniversalTimeSeconds))
            {
                resultText = "KSP UNIVERSAL TIME IS NOT AVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);
                return false;
            }

            if (string.IsNullOrWhiteSpace(plan.VesselId))
            {
                resultText = "VESSEL ID IS NOT AVAILABLE";
                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);
                return false;
            }

            ManeuverUplinkPacket packet =
                new ManeuverUplinkPacket
                {
                    VesselId = plan.VesselId,
                    PlanId = plan.PlanId,
                    NodeUniversalTimeSeconds =
                        plan.NodeUniversalTimeSeconds,
                    ProgradeDeltaVMetersPerSecond =
                        plan.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        plan.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        plan.RadialDeltaVMetersPerSecond
                };

            try
            {
                _maneuverLink.Send(packet);

                ManeuverUplinkStatusStore.PublishPending(
                    plan.PlanId,
                    plan.NodeUniversalTimeSeconds);

                resultText = "UPLINK SENT - AWAITING PLUGIN ACK";
                return true;
            }
            catch (Exception ex)
            {
                resultText =
                    "UPLINK FAILED: " +
                    ex.Message;

                ManeuverUplinkStatusStore.PublishRejected(
                    plan.PlanId,
                    resultText);

                return false;
            }
        }

        private void OnManeuverEpochReceived(
            ManeuverEpochPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            _engineeringEngine.PublishManeuverEpochTelemetry(
                new ManeuverEpochTelemetryModel
                {
                    Available = true,
                    SourceTimestampUtc = packet.TimestampUtc,
                    ReceivedUtc = DateTime.UtcNow,
                    VesselId = packet.VesselId ?? string.Empty,
                    VesselName = packet.VesselName ?? string.Empty,
                    UniversalTimeSeconds =
                        packet.UniversalTimeSeconds,
                    MissionTimeSeconds =
                        packet.MissionTimeSeconds
                });
        }

        private void OnManeuverAcknowledgmentReceived(
            ManeuverUplinkAck ack)
        {
            ManeuverUplinkStatusStore.PublishAck(
                ack);

            Action<ManeuverUplinkAck> handler =
                ManeuverAcknowledgmentReceived;

            if (handler != null)
            {
                handler(ack);
            }
        }


        private void OnManeuverNodeStateReceived(
            ManeuverNodeStatePacket packet)
        {
            ManeuverUplinkStatusStore.PublishNodeState(
                packet);
        }

        private void OnVelocityVectorTelemetryReceived(
            VelocityVectorTelemetrySample sample)
        {
            if (sample == null)
            {
                return;
            }

            _engineeringEngine.PublishVelocityVectorTelemetry(
                new VelocityVectorTelemetryModel
                {
                    TelemetryAvailable = true,
                    SourceTimestampUtc = sample.SourceTimestampUtc,
                    ReceivedUtc = sample.ReceivedUtc,
                    VesselName = sample.VesselName ?? string.Empty,
                    SurfaceRightMetersPerSecond =
                        sample.SurfaceRightMetersPerSecond,
                    SurfaceNoseMetersPerSecond =
                        sample.SurfaceNoseMetersPerSecond,
                    SurfaceReferenceForwardMetersPerSecond =
                        sample.SurfaceReferenceForwardMetersPerSecond,
                    OrbitalRightMetersPerSecond =
                        sample.OrbitalRightMetersPerSecond,
                    OrbitalNoseMetersPerSecond =
                        sample.OrbitalNoseMetersPerSecond,
                    OrbitalReferenceForwardMetersPerSecond =
                        sample.OrbitalReferenceForwardMetersPerSecond
                });
        }

        private void OnEngineStateTelemetryReceived(
            DateTime sourceTimestampUtc,
            Dictionary<uint, EngineStateTelemetry> states)
        {
            EngineStateTelemetryStore.Publish(states);

            PropulsionTelemetryModel telemetry =
                new PropulsionTelemetryModel
                {
                    TelemetryAvailable = true,
                    SourceTimestampUtc = sourceTimestampUtc,
                    ReceivedUtc = DateTime.UtcNow
                };

            if (states != null)
            {
                foreach (
                    KeyValuePair<uint, EngineStateTelemetry> pair
                    in states)
                {
                    EngineStateTelemetry source =
                        pair.Value;

                    if (source == null)
                    {
                        continue;
                    }

                    telemetry.Entries.Add(
                        new PropulsionEngineTelemetryEntry
                        {
                            PartId = source.PartId,
                            OperatingState =
                                ConvertOperatingState(
                                    source.OperatingState),
                            IsSolidBooster = source.IsSolidBooster,
                            CurrentThrust = source.CurrentThrust,
                            MaximumThrust = source.MaximumThrust
                        });
                }
            }

            _engineeringEngine.PublishPropulsionTelemetry(
                telemetry);
        }

        private static PropulsionEngineOperatingState
            ConvertOperatingState(
                EngineOperatingState state)
        {
            switch (state)
            {
                case EngineOperatingState.Armed:
                    return PropulsionEngineOperatingState.Armed;

                case EngineOperatingState.Ignited:
                    return PropulsionEngineOperatingState.Ignited;

                case EngineOperatingState.Producing:
                    return PropulsionEngineOperatingState.Producing;

                case EngineOperatingState.Shutdown:
                    return PropulsionEngineOperatingState.Shutdown;

                case EngineOperatingState.Flameout:
                    return PropulsionEngineOperatingState.Flameout;

                default:
                    return PropulsionEngineOperatingState.Unknown;
            }
        }

        private void OnSystemsTelemetryReceived(
            SystemsTelemetrySample systems)
        {
            _cache.PublishSystems(systems);

            _engineeringEngine.PublishElectricalTelemetry(
                systems.ElectricChargeAmount,
                systems.ElectricChargeCapacity,
                systems.ReceivedUtc);

            ElectricalAttributionModel attribution =
                new ElectricalAttributionModel();

            attribution.TelemetryAvailable =
                systems.AttributionTelemetryAvailable;

            for (int index = 0;
                 index < systems.AttributionEntries.Count;
                 index++)
            {
                SystemsAttributionEntry source =
                    systems.AttributionEntries[index];

                attribution.Entries.Add(
                    new ElectricalAttributionEntry
                    {
                        Kind =
                            source.IsProducer
                                ? ElectricalAttributionKind.Producer
                                : ElectricalAttributionKind.Consumer,
                        PartId = source.PartId,
                        PartTitle = source.PartTitle,
                        Category = source.Category,
                        Evidence =
                            ParseEvidence(
                                source.Evidence),
                        CurrentRateKnown = source.CurrentKnown,
                        CurrentRateEcPerSecond =
                            source.CurrentRateEcPerSecond,
                        MaximumRateKnown = source.MaximumKnown,
                        MaximumRateEcPerSecond =
                            source.MaximumRateEcPerSecond,
                        Enabled = source.Enabled,
                        ActiveStateKnown = source.ActiveKnown,
                        Active = source.Active
                    });
            }

            attribution.Recalculate();

            _engineeringEngine.PublishElectricalAttribution(
                attribution);
        }

        private static ElectricalRateEvidence ParseEvidence(
            string value)
        {
            ElectricalRateEvidence parsed;

            if (Enum.TryParse(
                    value,
                    true,
                    out parsed))
            {
                return parsed;
            }

            return ElectricalRateEvidence.Unknown;
        }

        private void OnFlightTelemetryReceived(
            TelemetryPacket packet)
        {
            PropulsionDebugSnapshotStore
                .PublishTelemetry(packet);

            AnalyzeEngineering(packet);

            Action<TelemetryPacket> handler =
                TelemetryReceived;

            if (handler != null)
            {
                handler(packet);
            }
        }

        private void AnalyzeEngineering(
            TelemetryPacket packet)
        {
            VesselTopology topology =
                _cache.GetTopology();

            if (topology == null)
            {
                return;
            }

            long sequence;

            lock (_engineeringSyncRoot)
            {
                _engineeringSequence++;
                sequence = _engineeringSequence;
            }

            try
            {
                AnalysisPipelineResult result =
                    _engineeringEngine.Analyze(
                        sequence,
                        DateTime.UtcNow,
                        packet,
                        topology);

                EngineeringSnapshotStore.Publish(
                    result);

                _powerFailureIntegration.Evaluate(
                    result);

                _propulsionFailureIntegration.Evaluate(
                    result);

                _gncFailureIntegration.Evaluate(
                    result);
            }
            catch (Exception ex)
            {
                EngineeringSnapshotStore.ReportError(
                    ex);
            }
        }

        private void OnTopologyReceived(
            VesselTopology topology)
        {
            _cache.PublishTopology(topology);

            PropulsionDebugSnapshotStore
                .PublishTopology(topology);

            try
            {
                PropulsionRenderGraphBuilder builder =
                    new PropulsionRenderGraphBuilder();

                PropulsionRenderGraph graph =
                    builder.Build(topology);

                PropulsionGraphStore.Publish(graph);
                PropulsionGraphFileLogger.Write(graph);
            }
            catch (Exception ex)
            {
                PropulsionGraphFileLogger
                    .WriteError(ex);
            }

            Action<VesselTopology> handler =
                TopologyReceived;

            if (handler != null)
            {
                handler(topology);
            }
        }

        public void Stop()
        {
            _powerFailureIntegration.RestoreAll();
            _propulsionFailureIntegration.RestoreAll();
            _gncFailureIntegration.RestoreAll();

            if (!_running)
            {
                _transport.Stop();
                _maneuverLink.Stop();
                return;
            }

            _running = false;

            _transport.Stop();
            _maneuverLink.Stop();

            PropulsionGraphStore.Clear();
            PropulsionDebugSnapshotStore.Clear();
            EngineeringSnapshotStore.Clear();
            ManeuverUplinkStatusStore.Clear();
            EngineStateTelemetryStore.Clear();

            _engineeringEngine.ClearElectricalTelemetry();
            _engineeringEngine.ClearPropulsionTelemetry();
            _engineeringEngine.ClearVelocityVectorTelemetry();
            _engineeringEngine.ClearManeuverEpochTelemetry();

            _cache.Clear();
        }

        public void Dispose()
        {
            Stop();

            _transport.FlightTelemetryReceived -=
                OnFlightTelemetryReceived;

            _transport.TopologyReceived -=
                OnTopologyReceived;

            _transport.SystemsTelemetryReceived -=
                OnSystemsTelemetryReceived;

            _transport.EngineStateTelemetryReceived -=
                OnEngineStateTelemetryReceived;

            _transport.VelocityVectorTelemetryReceived -=
                OnVelocityVectorTelemetryReceived;

            _maneuverLink.EpochReceived -=
                OnManeuverEpochReceived;

            _maneuverLink.AcknowledgmentReceived -=
                OnManeuverAcknowledgmentReceived;

            _maneuverLink.NodeStateReceived -=
                OnManeuverNodeStateReceived;

            _transport.Dispose();
            _maneuverLink.Dispose();
            _powerFailureIntegration.Dispose();
            _propulsionFailureIntegration.Dispose();
            _gncFailureIntegration.Dispose();
        }
    }
}
