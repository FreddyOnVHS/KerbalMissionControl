using System;
using System.Reflection;
using KMC.Engine;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;

namespace KMC.MissionControl.Training
{
    /// <summary>
    /// Build 14.11.3B narrow instructor bridge for the new generator-source
    /// failure tests. The request is injected into the same EngineeringEngine
    /// instance owned by MissionControlReceiver, so it becomes normal Engine
    /// failure truth and is cleared through the normal F10 paths.
    /// </summary>
    internal static class InstructorElectricalSourceFailureBridge
    {
        public static bool InjectGeneratorFailure(
            MissionControlReceiver receiver,
            string sourceId,
            SpacecraftSystemHealth sourceHealth,
            double delaySeconds,
            out string failureId,
            out string resultText)
        {
            failureId = string.Empty;
            resultText = string.Empty;

            if (receiver == null)
            {
                resultText = "NO MISSION CONTROL RECEIVER";
                return false;
            }

            if (!string.Equals(sourceId, "SRC_GEN_A", StringComparison.Ordinal) &&
                !string.Equals(sourceId, "SRC_GEN_B", StringComparison.Ordinal))
            {
                resultText = "UNSUPPORTED GENERATOR SOURCE";
                return false;
            }

            if (sourceHealth !=
                    SpacecraftSystemHealth.Degraded &&
                sourceHealth !=
                    SpacecraftSystemHealth.Failed)
            {
                resultText = "UNSUPPORTED GENERATOR HEALTH";
                return false;
            }

            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore.TryGetLatest(out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(latest.Snapshot.Vessel.VesselId))
            {
                resultText = "NO ACTIVE ENGINEERING VESSEL";
                return false;
            }

            FieldInfo field =
                typeof(MissionControlReceiver).GetField(
                    "_engineeringEngine",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            EngineeringEngine engine =
                field != null
                    ? field.GetValue(receiver) as EngineeringEngine
                    : null;

            if (engine == null)
            {
                resultText = "ENGINE ACCESS UNAVAILABLE";
                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems.FailureSimulation
                    : null;

            if (current == null ||
                current.Mode == FailureSimulationMode.Nominal)
            {
                string modeResult;

                if (!engine.SetFailureSimulationMode(
                        vesselId,
                        FailureSimulationMode.Training,
                        out modeResult))
                {
                    resultText = modeResult;
                    return false;
                }
            }

            double delay =
                Math.Max(
                    0.0,
                    Math.Min(
                        300.0,
                        delaySeconds));

            SyntheticFailureRequest request =
                new SyntheticFailureRequest
                {
                    VesselId = vesselId,
                    TargetId = sourceId,
                    TargetKind =
                        SyntheticFailureTargetKind.ElectricalSource,
                    Kind =
                        SyntheticFailureKind.Sudden,
                    Severity =
                        SyntheticFailureSeverity.Caution,
                    ComponentHealth =
                        sourceHealth,
                    ActivateUtc =
                        DateTime.UtcNow.AddSeconds(delay),
                    Detail =
                        "BUILD 14.11.5 INSTRUCTOR / " +
                        sourceId +
                        " / " +
                        sourceHealth.ToString().ToUpperInvariant()
                };

            string injectResult;

            bool injected =
                engine.InjectSyntheticFailure(
                    request,
                    out failureId,
                    out injectResult);

            if (!injected)
            {
                resultText = injectResult;
                return false;
            }

            resultText =
                "INJECTED " +
                failureId +
                " / " +
                sourceId +
                " / " +
                sourceHealth.ToString().ToUpperInvariant() +
                (delay > 0.25
                    ? " / SCHEDULED"
                    : " / IMMEDIATE");

            return true;
        }

        public static bool InjectSwitchFailure(
            MissionControlReceiver receiver,
            string switchId,
            SyntheticElectricalSwitchFailureMode mode,
            double delaySeconds,
            out string failureId,
            out string resultText)
        {
            failureId = string.Empty;
            resultText = string.Empty;

            if (receiver == null)
            {
                resultText = "NO MISSION CONTROL RECEIVER";
                return false;
            }

            string targetId =
                SyntheticElectricalSwitchFailureTargets.Create(
                    switchId,
                    mode);

            if (string.IsNullOrWhiteSpace(targetId))
            {
                resultText = "INVALID ELECTRICAL SWITCH FAILURE";
                return false;
            }

            AnalysisPipelineResult latest;

            if (!EngineeringSnapshotStore.TryGetLatest(out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(latest.Snapshot.Vessel.VesselId))
            {
                resultText = "NO ACTIVE ENGINEERING VESSEL";
                return false;
            }

            FieldInfo field =
                typeof(MissionControlReceiver).GetField(
                    "_engineeringEngine",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            EngineeringEngine engine =
                field != null
                    ? field.GetValue(receiver) as EngineeringEngine
                    : null;

            if (engine == null)
            {
                resultText = "ENGINE ACCESS UNAVAILABLE";
                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems.FailureSimulation
                    : null;

            if (current == null ||
                current.Mode == FailureSimulationMode.Nominal)
            {
                string modeResult;

                if (!engine.SetFailureSimulationMode(
                        vesselId,
                        FailureSimulationMode.Training,
                        out modeResult))
                {
                    resultText = modeResult;
                    return false;
                }
            }

            double delay =
                Math.Max(
                    0.0,
                    Math.Min(
                        300.0,
                        delaySeconds));

            bool indicationOnly =
                mode ==
                    SyntheticElectricalSwitchFailureMode.FalseClosedIndication ||
                mode ==
                    SyntheticElectricalSwitchFailureMode.FalseOpenIndication;

            SyntheticFailureRequest request =
                new SyntheticFailureRequest
                {
                    VesselId = vesselId,
                    TargetId = targetId,
                    TargetKind =
                        indicationOnly
                            ? SyntheticFailureTargetKind.Instrumentation
                            : SyntheticFailureTargetKind.Component,
                    Kind =
                        SyntheticFailureKind.Sudden,
                    Severity =
                        SyntheticFailureSeverity.Caution,
                    ComponentHealth =
                        SpacecraftSystemHealth.Failed,
                    ActivateUtc =
                        DateTime.UtcNow.AddSeconds(delay),
                    Detail =
                        "BUILD 14.11.4 INSTRUCTOR / " +
                        switchId +
                        " / " +
                        mode.ToString()
                };

            string injectResult;

            bool injected =
                engine.InjectSyntheticFailure(
                    request,
                    out failureId,
                    out injectResult);

            if (!injected)
            {
                resultText = injectResult;
                return false;
            }

            resultText =
                "INJECTED " +
                failureId +
                " / " +
                switchId +
                " / " +
                mode.ToString().ToUpperInvariant();

            return true;
        }
    }
}
