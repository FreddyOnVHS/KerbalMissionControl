using System;
using System.Diagnostics;
using KMC.Engine.Models;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Build 14.0 foundation for KMC-owned synthetic spacecraft systems.
    ///
    /// This creates a deterministic nominal A/B spacecraft template for the
    /// active vessel. It intentionally does not mutate KSP and does not use
    /// the existing stock ElectricCharge model as a synthetic bus model.
    /// Build 14.1 will add electrical source/bus/load behavior on top of this
    /// graph.
    /// </summary>
    public sealed class SpacecraftSystemsFoundationSystem
    {
        private const string FoundationTemplateId =
            "KMC-14.0-AB-FOUNDATION";

        private readonly object _syncRoot;
        private SpacecraftSystemsModel _latest;
        private string _lastDiagnosticKey;

        public SpacecraftSystemsFoundationSystem()
        {
            _syncRoot = new object();
            _latest = new SpacecraftSystemsModel();
            _lastDiagnosticKey = string.Empty;
        }

        public void Update(
            VesselModel vessel,
            DateTime generatedUtc)
        {
            if (vessel == null)
            {
                lock (_syncRoot)
                {
                    _latest =
                        new SpacecraftSystemsModel();
                }

                return;
            }

            SpacecraftSystemsModel model =
                BuildNominalFoundation(
                    vessel,
                    generatedUtc);

            lock (_syncRoot)
            {
                _latest =
                    model;
            }

            WriteDiagnosticIfChanged(
                model);
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

        private static SpacecraftSystemsModel
            BuildNominalFoundation(
                VesselModel vessel,
                DateTime generatedUtc)
        {
            SpacecraftSystemsModel model =
                new SpacecraftSystemsModel
                {
                    VesselId =
                        vessel.VesselId ?? string.Empty,

                    VesselName =
                        vessel.VesselName ?? string.Empty,

                    TopologyRevision =
                        vessel.TopologyRevision,

                    TemplateId =
                        FoundationTemplateId,

                    GeneratedUtc =
                        generatedUtc
                };

            AddComponent(
                model,
                "BUS_MAIN_A",
                "MAIN BUS A",
                SpacecraftSystemCategory.Electrical);

            AddComponent(
                model,
                "BUS_MAIN_B",
                "MAIN BUS B",
                SpacecraftSystemCategory.Electrical);

            AddComponent(
                model,
                "BUS_ESS",
                "ESSENTIAL BUS",
                SpacecraftSystemCategory.Electrical);

            AddComponent(
                model,
                "GUID_A",
                "GUID COMPUTER A",
                SpacecraftSystemCategory.Guidance);

            AddComponent(
                model,
                "GUID_B",
                "GUID COMPUTER B",
                SpacecraftSystemCategory.Guidance);

            AddComponent(
                model,
                "COMM_A",
                "COMM TRANSCEIVER A",
                SpacecraftSystemCategory.Communications);

            AddComponent(
                model,
                "COMM_B",
                "COMM TRANSCEIVER B",
                SpacecraftSystemCategory.Communications);

            AddComponent(
                model,
                "PUMP_A",
                "PROP FEED PUMP A",
                SpacecraftSystemCategory.Propulsion);

            AddComponent(
                model,
                "PUMP_B",
                "PROP FEED PUMP B",
                SpacecraftSystemCategory.Propulsion);

            AddComponent(
                model,
                "FLIGHT_COMPUTER",
                "PRIMARY FLIGHT COMPUTER",
                SpacecraftSystemCategory.Guidance);
            AddComponent(
                model,
                "FLIGHT_CONTROL",
                "SAS / FLIGHT CONTROL ELECTRONICS",
                SpacecraftSystemCategory.Guidance);
            AddComponent(
                model,
                "REACTION_WHEEL",
                "REACTION WHEEL POWER",
                SpacecraftSystemCategory.Guidance);
            AddComponent(
                model,
                "ENGINE_CONTROL",
                "ENGINE CONTROL / IGNITION",
                SpacecraftSystemCategory.Propulsion);
            AddComponent(
                model,
                "STAGING_CONTROL",
                "STAGING / SEPARATION",
                SpacecraftSystemCategory.Propulsion);
            AddComponent(
                model,
                "BRAKE_CONTROL",
                "BRAKE CONTROL",
                SpacecraftSystemCategory.Guidance);
            AddComponent(
                model,
                "GEAR_CONTROL",
                "GEAR CONTROL / ACTUATION",
                SpacecraftSystemCategory.Guidance);
            AddComponent(
                model,
                "LIGHTING_ESS",
                "EXTERNAL / EMERGENCY LIGHTING",
                SpacecraftSystemCategory.Electrical);

            AddPowerDependency(
                model,
                "BUS_MAIN_A",
                "GUID_A");

            AddPowerDependency(
                model,
                "BUS_MAIN_B",
                "GUID_B");

            AddPowerDependency(
                model,
                "BUS_MAIN_A",
                "COMM_A");

            AddPowerDependency(
                model,
                "BUS_MAIN_B",
                "COMM_B");

            AddPowerDependency(
                model,
                "BUS_MAIN_A",
                "PUMP_A");

            AddPowerDependency(
                model,
                "BUS_MAIN_B",
                "PUMP_B");

            AddPowerDependency(
                model,
                "BUS_ESS",
                "FLIGHT_COMPUTER");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "FLIGHT_CONTROL");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "REACTION_WHEEL");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "ENGINE_CONTROL");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "STAGING_CONTROL");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "BRAKE_CONTROL");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "GEAR_CONTROL");
            AddPowerDependency(
                model,
                "BUS_ESS",
                "LIGHTING_ESS");

            model.Recalculate();

            return model;
        }

        private static void AddComponent(
            SpacecraftSystemsModel model,
            string id,
            string displayName,
            SpacecraftSystemCategory category)
        {
            model.Components.Add(
                new SpacecraftSystemComponent
                {
                    Id = id,
                    DisplayName = displayName,
                    Category = category,
                    CommandedOn = true,
                    Health =
                        SpacecraftSystemHealth.Nominal
                });
        }

        private static void AddPowerDependency(
            SpacecraftSystemsModel model,
            string sourceId,
            string targetId)
        {
            model.Dependencies.Add(
                new SpacecraftSystemDependency
                {
                    SourceId = sourceId,
                    TargetId = targetId,
                    Kind =
                        SpacecraftDependencyKind.Power,
                    Required = true
                });
        }

        private void WriteDiagnosticIfChanged(
            SpacecraftSystemsModel model)
        {
            if (model == null)
            {
                return;
            }

            string key =
                (model.VesselId ?? string.Empty) +
                "|" +
                model.TopologyRevision.ToString() +
                "|" +
                model.TemplateId;

            if (string.Equals(
                    key,
                    _lastDiagnosticKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastDiagnosticKey =
                key;

            int online = 0;

            for (int index = 0;
                 index < model.Components.Count;
                 index++)
            {
                SpacecraftSystemComponent component =
                    model.Components[index];

                if (component != null &&
                    component.State ==
                        SpacecraftSystemState.Online)
                {
                    online++;
                }
            }

            Debug.WriteLine(
                "KMC.Engine SYSTEMS FOUNDATION" +
                " | VesselId=" +
                (model.VesselId ?? string.Empty) +
                " | Vessel=" +
                (model.VesselName ?? string.Empty) +
                " | Revision=" +
                model.TopologyRevision.ToString() +
                " | Template=" +
                (model.TemplateId ?? string.Empty) +
                " | Components=" +
                model.Components.Count.ToString() +
                " | Dependencies=" +
                model.Dependencies.Count.ToString() +
                " | Online=" +
                online.ToString());
        }
    }
}
