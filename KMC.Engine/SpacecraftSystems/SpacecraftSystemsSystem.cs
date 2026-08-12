using System;
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
            }

            lock (_syncRoot)
            {
                _latest =
                    model;
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
        private readonly System.Collections.Generic.Dictionary<string, bool>
            _states;

        internal ElectricalControlSnapshot(
            System.Collections.Generic.Dictionary<string, bool> states)
        {
            _states =
                states ??
                new System.Collections.Generic.Dictionary<string, bool>(
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
            System.Collections.Generic.Dictionary<
                string,
                System.Collections.Generic.Dictionary<string, bool>>
            ByVessel =
                new System.Collections.Generic.Dictionary<
                    string,
                    System.Collections.Generic.Dictionary<string, bool>>(
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
                System.Collections.Generic.Dictionary<string, bool> states;

                if (!ByVessel.TryGetValue(vesselId, out states))
                {
                    states =
                        new System.Collections.Generic.Dictionary<string, bool>(
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
                System.Collections.Generic.Dictionary<string, bool> copy =
                    new System.Collections.Generic.Dictionary<string, bool>(
                        StringComparer.Ordinal);

                System.Collections.Generic.Dictionary<string, bool> states;

                if (!string.IsNullOrWhiteSpace(vesselId) &&
                    ByVessel.TryGetValue(vesselId, out states))
                {
                    foreach (
                        System.Collections.Generic.KeyValuePair<string, bool>
                            entry in states)
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

}
