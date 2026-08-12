using System;
using System.Diagnostics;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Build 14.1 synthetic DC electrical distribution.
    ///
    /// This layer intentionally does not mutate KSP and does not replace the
    /// existing stock ElectricCharge analysis. It provides the systems/failure
    /// simulation with explicit sources, buses, loads and redundancy.
    /// </summary>
    public sealed class SyntheticElectricalDistributionSystem
    {
        private const string DistributionTemplateId =
            "KMC-14.1-28V-DC-DISTRIBUTION";

        private const double NominalVoltage = 28.0;
        private const double HighLoadThreshold = 0.80;
        private const double UndervoltageThreshold = 24.0;

        private string _lastDiagnosticKey;

        public SyntheticElectricalDistributionSystem()
        {
            _lastDiagnosticKey =
                string.Empty;
        }

        public SyntheticElectricalDistributionModel BuildAndApply(
            SpacecraftSystemsModel systems,
            DateTime generatedUtc,
            ElectricalControlSnapshot controls,
            FailureSimulationSnapshot failures)
        {
            SyntheticElectricalDistributionModel distribution =
                BuildNominalDistribution(
                    generatedUtc);

            ApplyCrewControls(
                distribution,
                systems,
                controls);

            SyntheticFailureEngine.ApplyElectricalSourceFailures(
                distribution,
                failures);

            Recalculate(
                distribution);

            ApplyBusStatesToSystems(
                systems,
                distribution);

            WriteDiagnosticIfChanged(
                systems,
                distribution);

#if DEBUG
            RunDependencySelfTestOnce();
#endif

            return distribution;
        }

        internal static void Recalculate(
            SyntheticElectricalDistributionModel distribution)
        {
            if (distribution == null)
            {
                return;
            }

            /*
             * Loads are deterministic assignments in 14.1.
             */
            for (int index = 0;
                 index < distribution.Buses.Count;
                 index++)
            {
                SyntheticElectricalBus bus =
                    distribution.Buses[index];

                if (bus == null)
                {
                    continue;
                }

                bus.DemandAmps =
                    SumDemand(
                        distribution,
                        bus.Id);

                bus.AvailableCurrentAmps =
                    0.0;

                bus.ActiveSourceCount =
                    0;

                bus.Voltage =
                    0.0;

                bus.State =
                    SyntheticElectricalBusState.Unpowered;
            }

            /*
             * Resolve direct-source buses first. Bus-feed sources are handled
             * in bounded passes so Essential Bus redundancy can depend on the
             * resolved state of Main A or Main B.
             */
            int maximumPasses =
                Math.Max(
                    1,
                    distribution.Buses.Count + 1);

            for (int pass = 0;
                 pass < maximumPasses;
                 pass++)
            {
                bool changed = false;

                for (int index = 0;
                     index < distribution.Buses.Count;
                     index++)
                {
                    SyntheticElectricalBus bus =
                        distribution.Buses[index];

                    if (bus == null)
                    {
                        continue;
                    }

                    double available = 0.0;
                    double sourceVoltage = 0.0;
                    int sourceCount = 0;

                    for (int sourceIndex = 0;
                         sourceIndex < distribution.Sources.Count;
                         sourceIndex++)
                    {
                        SyntheticElectricalSource source =
                            distribution.Sources[sourceIndex];

                        if (source == null ||
                            !string.Equals(
                                source.BusId,
                                bus.Id,
                                StringComparison.Ordinal) ||
                            !IsSourceUsable(
                                distribution,
                                source))
                        {
                            continue;
                        }

                        double current =
                            source.AvailableCurrentAmps;

                        if (current <= 0.000001)
                        {
                            continue;
                        }

                        available +=
                            current;

                        sourceVoltage =
                            Math.Max(
                                sourceVoltage,
                                source.NominalVoltage);

                        sourceCount++;
                    }

                    SyntheticElectricalBusState nextState;
                    double nextVoltage;

                    CalculateBusState(
                        bus.NominalVoltage,
                        bus.DemandAmps,
                        available,
                        sourceVoltage,
                        out nextState,
                        out nextVoltage);

                    if (Math.Abs(
                            bus.AvailableCurrentAmps -
                            available) >
                            0.000001 ||
                        Math.Abs(
                            bus.Voltage -
                            nextVoltage) >
                            0.000001 ||
                        bus.ActiveSourceCount !=
                            sourceCount ||
                        bus.State !=
                            nextState)
                    {
                        bus.AvailableCurrentAmps =
                            available;

                        bus.ActiveSourceCount =
                            sourceCount;

                        bus.Voltage =
                            nextVoltage;

                        bus.State =
                            nextState;

                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private static SyntheticElectricalDistributionModel
            BuildNominalDistribution(
                DateTime generatedUtc)
        {
            SyntheticElectricalDistributionModel distribution =
                new SyntheticElectricalDistributionModel
                {
                    TemplateId =
                        DistributionTemplateId,

                    GeneratedUtc =
                        generatedUtc
                };

            AddBus(
                distribution,
                "BUS_MAIN_A",
                "MAIN BUS A");

            AddBus(
                distribution,
                "BUS_MAIN_B",
                "MAIN BUS B");

            AddBus(
                distribution,
                "BUS_ESS",
                "ESSENTIAL BUS");

            /*
             * Nominal KMC spacecraft design ratings.
             *
             * A/B main buses each have a primary generator and battery reserve.
             * Essential Bus is redundantly fed from either main bus.
             */
            AddSource(
                distribution,
                "SRC_GEN_A",
                "GENERATOR A",
                "BUS_MAIN_A",
                string.Empty,
                SyntheticElectricalSourceKind.Generator,
                12.0);

            AddSource(
                distribution,
                "SRC_BAT_A",
                "BATTERY A",
                "BUS_MAIN_A",
                string.Empty,
                SyntheticElectricalSourceKind.Battery,
                6.0);

            AddSource(
                distribution,
                "SRC_GEN_B",
                "GENERATOR B",
                "BUS_MAIN_B",
                string.Empty,
                SyntheticElectricalSourceKind.Generator,
                12.0);

            AddSource(
                distribution,
                "SRC_BAT_B",
                "BATTERY B",
                "BUS_MAIN_B",
                string.Empty,
                SyntheticElectricalSourceKind.Battery,
                6.0);

            AddSource(
                distribution,
                "FEED_ESS_A",
                "ESS FEED A",
                "BUS_ESS",
                "BUS_MAIN_A",
                SyntheticElectricalSourceKind.BusFeed,
                6.0);

            AddSource(
                distribution,
                "FEED_ESS_B",
                "ESS FEED B",
                "BUS_ESS",
                "BUS_MAIN_B",
                SyntheticElectricalSourceKind.BusFeed,
                6.0);

            AddLoad(
                distribution,
                "GUID_A",
                "GUID COMPUTER A",
                "BUS_MAIN_A",
                2.0,
                1);

            AddLoad(
                distribution,
                "COMM_A",
                "COMM TRANSCEIVER A",
                "BUS_MAIN_A",
                1.5,
                2);

            AddLoad(
                distribution,
                "PUMP_A",
                "PROP FEED PUMP A",
                "BUS_MAIN_A",
                4.0,
                2);

            AddLoad(
                distribution,
                "GUID_B",
                "GUID COMPUTER B",
                "BUS_MAIN_B",
                2.0,
                1);

            AddLoad(
                distribution,
                "COMM_B",
                "COMM TRANSCEIVER B",
                "BUS_MAIN_B",
                1.5,
                2);

            AddLoad(
                distribution,
                "PUMP_B",
                "PROP FEED PUMP B",
                "BUS_MAIN_B",
                4.0,
                2);

            AddLoad(
                distribution,
                "FLIGHT_COMPUTER",
                "PRIMARY FLIGHT COMPUTER",
                "BUS_ESS",
                3.0,
                1);

            return distribution;
        }

        private static void ApplyCrewControls(
            SyntheticElectricalDistributionModel distribution,
            SpacecraftSystemsModel systems,
            ElectricalControlSnapshot controls)
        {
            if (distribution == null ||
                controls == null)
            {
                return;
            }

            for (int index = 0;
                 index < distribution.Sources.Count;
                 index++)
            {
                SyntheticElectricalSource source =
                    distribution.Sources[index];

                if (source == null)
                {
                    continue;
                }

                bool commanded;

                if (controls.TryGet(
                        source.Id,
                        out commanded))
                {
                    source.CommandedAvailable =
                        commanded;
                }
            }

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load == null)
                {
                    continue;
                }

                bool commanded;

                if (!controls.TryGet(
                        load.EquipmentId,
                        out commanded))
                {
                    continue;
                }

                load.CommandedOn =
                    commanded;

                if (systems != null)
                {
                    SpacecraftSystemComponent component =
                        systems.FindComponent(
                            load.EquipmentId);

                    if (component != null)
                    {
                        component.CommandedOn =
                            commanded;
                    }
                }
            }
        }

        private static void ApplyBusStatesToSystems(
            SpacecraftSystemsModel systems,
            SyntheticElectricalDistributionModel distribution)
        {
            if (systems == null ||
                distribution == null)
            {
                return;
            }

            /*
             * Clear any prior electrical provider override before applying the
             * new distribution state.
             */
            for (int index = 0;
                 index < systems.Components.Count;
                 index++)
            {
                SpacecraftSystemComponent component =
                    systems.Components[index];

                if (component != null)
                {
                    component.ProviderStateOverride =
                        null;
                }
            }

            for (int index = 0;
                 index < distribution.Buses.Count;
                 index++)
            {
                SyntheticElectricalBus bus =
                    distribution.Buses[index];

                if (bus == null)
                {
                    continue;
                }

                SpacecraftSystemComponent busComponent =
                    systems.FindComponent(
                        bus.Id);

                if (busComponent == null)
                {
                    continue;
                }

                busComponent.ProviderStateOverride =
                    ConvertBusState(
                        bus.State);
            }

            /*
             * Re-running the generic 14.0 graph now propagates bus state through
             * the existing POWER dependencies to GUID/COMM/PUMP equipment.
             */
            systems.Recalculate();
        }

        private static SpacecraftSystemState ConvertBusState(
            SyntheticElectricalBusState state)
        {
            switch (state)
            {
                case SyntheticElectricalBusState.Unpowered:
                    return
                        SpacecraftSystemState.Unpowered;

                case SyntheticElectricalBusState.Overloaded:
                case SyntheticElectricalBusState.Undervoltage:
                    return
                        SpacecraftSystemState.Degraded;

                default:
                    return
                        SpacecraftSystemState.Online;
            }
        }

        private static bool IsSourceUsable(
            SyntheticElectricalDistributionModel distribution,
            SyntheticElectricalSource source)
        {
            if (source == null ||
                !source.CommandedAvailable ||
                source.State ==
                    SyntheticElectricalSourceState.Offline)
            {
                return false;
            }

            if (source.Kind !=
                    SyntheticElectricalSourceKind.BusFeed)
            {
                return true;
            }

            SyntheticElectricalBus parent =
                distribution.FindBus(
                    source.ParentBusId);

            if (parent == null)
            {
                return false;
            }

            return
                parent.State ==
                    SyntheticElectricalBusState.Nominal ||
                parent.State ==
                    SyntheticElectricalBusState.HighLoad;
        }

        private static double SumDemand(
            SyntheticElectricalDistributionModel distribution,
            string busId)
        {
            double demand = 0.0;

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load != null &&
                    load.CommandedOn &&
                    string.Equals(
                        load.BusId,
                        busId,
                        StringComparison.Ordinal))
                {
                    demand +=
                        Math.Max(
                            0.0,
                            load.DemandAmps);
                }
            }

            return demand;
        }

        private static void CalculateBusState(
            double nominalVoltage,
            double demandAmps,
            double availableAmps,
            double sourceVoltage,
            out SyntheticElectricalBusState state,
            out double voltage)
        {
            if (availableAmps <= 0.000001)
            {
                state =
                    SyntheticElectricalBusState.Unpowered;

                voltage =
                    0.0;

                return;
            }

            double effectiveSourceVoltage =
                sourceVoltage > 0.000001
                    ? sourceVoltage
                    : nominalVoltage;

            double fraction =
                demandAmps /
                availableAmps;

            if (fraction > 1.0)
            {
                /*
                 * Synthetic droop model:
                 * overloaded supply loses voltage in proportion to available
                 * current, with a lower clamp to keep the state observable.
                 */
                voltage =
                    effectiveSourceVoltage *
                    Math.Max(
                        0.70,
                        availableAmps /
                        Math.Max(
                            demandAmps,
                            0.000001));

                state =
                    voltage <
                        UndervoltageThreshold
                        ? SyntheticElectricalBusState.Undervoltage
                        : SyntheticElectricalBusState.Overloaded;

                return;
            }

            voltage =
                effectiveSourceVoltage;

            state =
                fraction >= HighLoadThreshold
                    ? SyntheticElectricalBusState.HighLoad
                    : SyntheticElectricalBusState.Nominal;
        }

        private static void AddBus(
            SyntheticElectricalDistributionModel distribution,
            string id,
            string displayName)
        {
            distribution.Buses.Add(
                new SyntheticElectricalBus
                {
                    Id = id,
                    DisplayName = displayName,
                    NominalVoltage = NominalVoltage
                });
        }

        private static void AddSource(
            SyntheticElectricalDistributionModel distribution,
            string id,
            string displayName,
            string busId,
            string parentBusId,
            SyntheticElectricalSourceKind kind,
            double capacityAmps)
        {
            distribution.Sources.Add(
                new SyntheticElectricalSource
                {
                    Id = id,
                    DisplayName = displayName,
                    BusId = busId,
                    ParentBusId = parentBusId,
                    Kind = kind,
                    CommandedAvailable = true,
                    State =
                        SyntheticElectricalSourceState.Online,
                    NominalVoltage = NominalVoltage,
                    CapacityAmps = capacityAmps
                });
        }

        private static void AddLoad(
            SyntheticElectricalDistributionModel distribution,
            string equipmentId,
            string displayName,
            string busId,
            double demandAmps,
            int priority)
        {
            distribution.Loads.Add(
                new SyntheticElectricalLoad
                {
                    EquipmentId = equipmentId,
                    DisplayName = displayName,
                    BusId = busId,
                    DemandAmps = demandAmps,
                    Priority = priority,
                    CommandedOn = true
                });
        }

        private void WriteDiagnosticIfChanged(
            SpacecraftSystemsModel systems,
            SyntheticElectricalDistributionModel distribution)
        {
            if (systems == null ||
                distribution == null)
            {
                return;
            }

            string key =
                (systems.VesselId ?? string.Empty) +
                "|" +
                systems.TopologyRevision.ToString() +
                "|" +
                (distribution.TemplateId ?? string.Empty) +
                FormatBusDiagnostic(
                    distribution.FindBus("BUS_MAIN_A"),
                    "MAIN_A") +
                FormatBusDiagnostic(
                    distribution.FindBus("BUS_MAIN_B"),
                    "MAIN_B") +
                FormatBusDiagnostic(
                    distribution.FindBus("BUS_ESS"),
                    "ESS") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "GUID_A") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "GUID_B") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "COMM_A") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "COMM_B") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "PUMP_A") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "PUMP_B") +
                "|" +
                DescribeLiveSystemState(
                    systems,
                    "FLIGHT_COMPUTER");

            if (string.Equals(
                    key,
                    _lastDiagnosticKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastDiagnosticKey =
                key;

            Debug.WriteLine(
                "KMC.Engine ELECTRICAL DISTRIBUTION" +
                " | Vessel=" +
                (systems.VesselName ?? string.Empty) +
                " | Revision=" +
                systems.TopologyRevision.ToString() +
                " | Template=" +
                (distribution.TemplateId ?? string.Empty) +
                " | Sources=" +
                distribution.Sources.Count.ToString() +
                " | Buses=" +
                distribution.Buses.Count.ToString() +
                " | Loads=" +
                distribution.Loads.Count.ToString() +
                FormatBusDiagnostic(
                    distribution.FindBus("BUS_MAIN_A"),
                    "MAIN_A") +
                FormatBusDiagnostic(
                    distribution.FindBus("BUS_MAIN_B"),
                    "MAIN_B") +
                FormatBusDiagnostic(
                    distribution.FindBus("BUS_ESS"),
                    "ESS") +
                " | GUID_A=" +
                DescribeLiveSystemState(
                    systems,
                    "GUID_A") +
                " | GUID_B=" +
                DescribeLiveSystemState(
                    systems,
                    "GUID_B") +
                " | COMM_A=" +
                DescribeLiveSystemState(
                    systems,
                    "COMM_A") +
                " | COMM_B=" +
                DescribeLiveSystemState(
                    systems,
                    "COMM_B") +
                " | PUMP_A=" +
                DescribeLiveSystemState(
                    systems,
                    "PUMP_A") +
                " | PUMP_B=" +
                DescribeLiveSystemState(
                    systems,
                    "PUMP_B") +
                " | FLIGHT_COMPUTER=" +
                DescribeLiveSystemState(
                    systems,
                    "FLIGHT_COMPUTER"));
        }

        private static string DescribeLiveSystemState(
            SpacecraftSystemsModel systems,
            string componentId)
        {
            SpacecraftSystemComponent component =
                systems != null
                    ? systems.FindComponent(
                        componentId)
                    : null;

            return
                component != null
                    ? component.State.ToString()
                    : "MISSING";
        }

        private static string FormatBusDiagnostic(
            SyntheticElectricalBus bus,
            string label)
        {
            if (bus == null)
            {
                return
                    " | " +
                    label +
                    "=MISSING";
            }

            return
                " | " +
                label +
                "=" +
                bus.State.ToString() +
                "," +
                bus.Voltage.ToString("0.0") +
                "V," +
                bus.DemandAmps.ToString("0.0") +
                "/" +
                bus.AvailableCurrentAmps.ToString("0.0") +
                "A," +
                bus.LoadPercent.ToString("0.0") +
                "%," +
                bus.ActiveSourceCount.ToString() +
                "SRC";
        }

#if DEBUG
        private static bool _selfTestCompleted;

        private static void RunDependencySelfTestOnce()
        {
            if (_selfTestCompleted)
            {
                return;
            }

            _selfTestCompleted =
                true;

            SyntheticElectricalDistributionModel distribution =
                BuildNominalDistribution(
                    DateTime.UtcNow);

            SyntheticElectricalSource genB =
                distribution.FindSource(
                    "SRC_GEN_B");

            SyntheticElectricalSource batB =
                distribution.FindSource(
                    "SRC_BAT_B");

            if (genB != null)
            {
                genB.State =
                    SyntheticElectricalSourceState.Offline;
            }

            if (batB != null)
            {
                batB.State =
                    SyntheticElectricalSourceState.Offline;
            }

            Recalculate(
                distribution);

            SpacecraftSystemsModel systems =
                BuildSelfTestSystemsModel();

            ApplyBusStatesToSystems(
                systems,
                distribution);

            SyntheticElectricalBus mainA =
                distribution.FindBus(
                    "BUS_MAIN_A");

            SyntheticElectricalBus mainB =
                distribution.FindBus(
                    "BUS_MAIN_B");

            SyntheticElectricalBus essential =
                distribution.FindBus(
                    "BUS_ESS");

            SpacecraftSystemComponent guidA =
                systems.FindComponent(
                    "GUID_A");

            SpacecraftSystemComponent guidB =
                systems.FindComponent(
                    "GUID_B");

            SpacecraftSystemComponent commB =
                systems.FindComponent(
                    "COMM_B");

            SpacecraftSystemComponent pumpB =
                systems.FindComponent(
                    "PUMP_B");

            SpacecraftSystemComponent flightComputer =
                systems.FindComponent(
                    "FLIGHT_COMPUTER");

            bool pass =
                mainA != null &&
                mainA.State ==
                    SyntheticElectricalBusState.Nominal &&
                mainB != null &&
                mainB.State ==
                    SyntheticElectricalBusState.Unpowered &&
                essential != null &&
                essential.State ==
                    SyntheticElectricalBusState.Nominal &&
                guidA != null &&
                guidA.State ==
                    SpacecraftSystemState.Online &&
                guidB != null &&
                guidB.State ==
                    SpacecraftSystemState.Unpowered &&
                commB != null &&
                commB.State ==
                    SpacecraftSystemState.Unpowered &&
                pumpB != null &&
                pumpB.State ==
                    SpacecraftSystemState.Unpowered &&
                flightComputer != null &&
                flightComputer.State ==
                    SpacecraftSystemState.Online;

            Debug.WriteLine(
                "KMC.Engine ELECTRICAL DISTRIBUTION SELFTEST" +
                " | " +
                (pass ? "PASS" : "FAIL") +
                " | MAIN_A=" +
                DescribeBusState(mainA) +
                " | MAIN_B=" +
                DescribeBusState(mainB) +
                " | ESS=" +
                DescribeBusState(essential) +
                " | GUID_A=" +
                DescribeSystemState(guidA) +
                " | GUID_B=" +
                DescribeSystemState(guidB) +
                " | COMM_B=" +
                DescribeSystemState(commB) +
                " | PUMP_B=" +
                DescribeSystemState(pumpB) +
                " | FLIGHT_COMPUTER=" +
                DescribeSystemState(flightComputer) +
                " | TEST=GEN_B+BAT_B OFFLINE");

            Debug.Assert(
                pass,
                "Build 14.1 electrical distribution self-test failed.");
        }

        private static SpacecraftSystemsModel
            BuildSelfTestSystemsModel()
        {
            SpacecraftSystemsModel systems =
                new SpacecraftSystemsModel();

            AddSelfTestComponent(
                systems,
                "BUS_MAIN_A");

            AddSelfTestComponent(
                systems,
                "BUS_MAIN_B");

            AddSelfTestComponent(
                systems,
                "BUS_ESS");

            AddSelfTestComponent(
                systems,
                "GUID_A");

            AddSelfTestComponent(
                systems,
                "GUID_B");

            AddSelfTestComponent(
                systems,
                "COMM_B");

            AddSelfTestComponent(
                systems,
                "PUMP_B");

            AddSelfTestComponent(
                systems,
                "FLIGHT_COMPUTER");

            AddSelfTestPowerDependency(
                systems,
                "BUS_MAIN_A",
                "GUID_A");

            AddSelfTestPowerDependency(
                systems,
                "BUS_MAIN_B",
                "GUID_B");

            AddSelfTestPowerDependency(
                systems,
                "BUS_MAIN_B",
                "COMM_B");

            AddSelfTestPowerDependency(
                systems,
                "BUS_MAIN_B",
                "PUMP_B");

            AddSelfTestPowerDependency(
                systems,
                "BUS_ESS",
                "FLIGHT_COMPUTER");

            systems.Recalculate();

            return systems;
        }

        private static void AddSelfTestComponent(
            SpacecraftSystemsModel systems,
            string id)
        {
            systems.Components.Add(
                new SpacecraftSystemComponent
                {
                    Id = id,
                    DisplayName = id,
                    CommandedOn = true,
                    Health =
                        SpacecraftSystemHealth.Nominal
                });
        }

        private static void AddSelfTestPowerDependency(
            SpacecraftSystemsModel systems,
            string sourceId,
            string targetId)
        {
            systems.Dependencies.Add(
                new SpacecraftSystemDependency
                {
                    SourceId = sourceId,
                    TargetId = targetId,
                    Kind =
                        SpacecraftDependencyKind.Power,
                    Required = true
                });
        }

        private static string DescribeBusState(
            SyntheticElectricalBus bus)
        {
            return
                bus != null
                    ? bus.State.ToString()
                    : "MISSING";
        }

        private static string DescribeSystemState(
            SpacecraftSystemComponent component)
        {
            return
                component != null
                    ? component.State.ToString()
                    : "MISSING";
        }

#endif
    }
}
