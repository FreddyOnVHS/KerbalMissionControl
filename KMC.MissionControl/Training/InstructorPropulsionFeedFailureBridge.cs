using System;
using System.Collections.Generic;
using System.Reflection;
using KMC.Engine;
using KMC.Engine.Analysis;
using KMC.Engine.Propulsion;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Telemetry;

namespace KMC.MissionControl.Training
{
    /// <summary>
    /// Build 14.12.3 narrow instructor bridge for an exact-engine synthetic
    /// feed-path failure.
    ///
    /// The instructor preset describes the training cause as a failed-closed
    /// feed valve. Engine/operator truth records only the observable result:
    /// one exact engine feed path is unavailable.
    ///
    /// This bridge injects into the same EngineeringEngine instance owned by
    /// MissionControlReceiver. It does not send any command to KSP.
    /// </summary>
    internal static class InstructorPropulsionFeedFailureBridge
    {
        public static bool InjectExactEngineFeedPathFailure(
            MissionControlReceiver receiver,
            double delaySeconds,
            out string failureId,
            out string resultText)
        {
            return
                InjectExactEngineFeedFailure(
                    receiver,
                    delaySeconds,
                    SpacecraftSystemHealth.Failed,
                    SyntheticFailureKind.Sudden,
                    4.0,
                    0.50,
                    "EXACT ENGINE FEED PATH LOSS",
                    out failureId,
                    out resultText);
        }

        public static bool InjectExactEngineFeedRestriction(
            MissionControlReceiver receiver,
            double delaySeconds,
            out string failureId,
            out string resultText)
        {
            return
                InjectExactEngineFeedFailure(
                    receiver,
                    delaySeconds,
                    SpacecraftSystemHealth.Degraded,
                    SyntheticFailureKind.Sudden,
                    4.0,
                    0.50,
                    "EXACT ENGINE FEED RESTRICTION",
                    out failureId,
                    out resultText);
        }

        public static bool InjectExactEngineIntermittentFeedFailure(
            MissionControlReceiver receiver,
            double delaySeconds,
            out string failureId,
            out string resultText)
        {
            return
                InjectExactEngineFeedFailure(
                    receiver,
                    delaySeconds,
                    SpacecraftSystemHealth.Failed,
                    SyntheticFailureKind.Intermittent,
                    6.0,
                    0.50,
                    "EXACT ENGINE INTERMITTENT FEED LOSS",
                    out failureId,
                    out resultText);
        }

        public static bool InjectExactEngineStartInhibit(
            MissionControlReceiver receiver,
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

            uint partId;

            if (!TrySelectEngine(
                    out partId,
                    out resultText))
            {
                return false;
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
                resultText = "NO ACTIVE ENGINEERING VESSEL";
                return false;
            }

            FieldInfo field =
                typeof(MissionControlReceiver).GetField(
                    "_engineeringEngine",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

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

            string targetId =
                PropulsionEngineFailureTargets
                    .CreateExactEngineStartInhibitTarget(
                        partId);

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
                    TargetId = targetId,
                    TargetKind =
                        SyntheticFailureTargetKind.Component,
                    Kind =
                        SyntheticFailureKind.Sudden,
                    Severity =
                        SyntheticFailureSeverity.Caution,
                    ComponentHealth =
                        SpacecraftSystemHealth.Failed,
                    EffectMagnitude = 1.0,
                    ActivateUtc =
                        DateTime.UtcNow.AddSeconds(delay),
                    Detail =
                        "BUILD 14.12.5 INSTRUCTOR / " +
                        "EXACT ENGINE START INHIBIT / PART " +
                        partId.ToString()
                };

            string injectResult;

            bool injected =
                engine.InjectSyntheticFailure(
                    request,
                    out failureId,
                    out injectResult);

            resultText =
                injected
                    ? "INJECTED " +
                      failureId +
                      " / " +
                      targetId +
                      (delay > 0.25
                          ? " / SCHEDULED"
                          : " / IMMEDIATE")
                    : injectResult;

            return injected;
        }

        private static bool InjectExactEngineFeedFailure(
            MissionControlReceiver receiver,
            double delaySeconds,
            SpacecraftSystemHealth health,
            SyntheticFailureKind kind,
            double intermittentPeriodSeconds,
            double intermittentDutyCycle,
            string instructorDetail,
            out string failureId,
            out string resultText)
        {
            failureId = string.Empty;
            resultText = string.Empty;

            if (receiver == null)
            {
                resultText =
                    "NO MISSION CONTROL RECEIVER";

                return false;
            }

            uint partId;

            if (!TrySelectEngine(
                    out partId,
                    out resultText))
            {
                return false;
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

            FieldInfo field =
                typeof(MissionControlReceiver).GetField(
                    "_engineeringEngine",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            EngineeringEngine engine =
                field != null
                    ? field.GetValue(
                        receiver) as
                        EngineeringEngine
                    : null;

            if (engine == null)
            {
                resultText =
                    "ENGINE ACCESS UNAVAILABLE";

                return false;
            }

            string vesselId =
                latest.Snapshot.Vessel.VesselId;

            FailureSimulationSnapshot current =
                latest.Snapshot.SpacecraftSystems != null
                    ? latest.Snapshot.SpacecraftSystems
                        .FailureSimulation
                    : null;

            if (current == null ||
                current.Mode ==
                    FailureSimulationMode.Nominal)
            {
                string modeResult;

                if (!engine.SetFailureSimulationMode(
                        vesselId,
                        FailureSimulationMode.Training,
                        out modeResult))
                {
                    resultText =
                        modeResult;

                    return false;
                }
            }

            string targetId =
                PropulsionFeedFailureTargets
                    .CreateExactEngineFeedPathTarget(
                        partId);

            if (string.IsNullOrWhiteSpace(
                    targetId))
            {
                resultText =
                    "INVALID ENGINE FEED TARGET";

                return false;
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
                    VesselId =
                        vesselId,

                    TargetId =
                        targetId,

                    TargetKind =
                        SyntheticFailureTargetKind.Component,

                    Kind =
                        kind,

                    Severity =
                        SyntheticFailureSeverity.Caution,

                    ComponentHealth =
                        health,

                    EffectMagnitude =
                        1.0,

                    ActivateUtc =
                        DateTime.UtcNow.AddSeconds(
                            delay),

                    IntermittentPeriodSeconds =
                        intermittentPeriodSeconds,

                    IntermittentDutyCycle =
                        intermittentDutyCycle,

                    Detail =
                        "BUILD 14.12.4 INSTRUCTOR / " +
                        instructorDetail +
                        " / PART " +
                        partId.ToString()
                };

            string injectResult;

            bool injected =
                engine.InjectSyntheticFailure(
                    request,
                    out failureId,
                    out injectResult);

            if (!injected)
            {
                resultText =
                    injectResult;

                return false;
            }

            resultText =
                "INJECTED " +
                failureId +
                " / " +
                targetId +
                " / " +
                kind.ToString().ToUpperInvariant() +
                (delay > 0.25
                    ? " / SCHEDULED"
                    : " / IMMEDIATE");

            return true;
        }

        private static bool TrySelectEngine(
            out uint partId,
            out string resultText)
        {
            partId =
                0;

            resultText =
                string.Empty;

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
    }
}
