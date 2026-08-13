using System;
using System.Diagnostics;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Build 14.11.3 synthetic DC electrical distribution.
    ///
    /// KSP ElectricCharge remains observed physical resource truth. This layer
    /// provides the KMC spacecraft-design source, switching, bus, load and
    /// redundancy simulation.
    ///
    /// Generator A/B are the normal primary sources. Battery A/B are reserve
    /// sources selected automatically if the associated generator cannot feed
    /// its main bus.
    /// </summary>
    public sealed class SyntheticElectricalDistributionSystem
    {
        private const string DistributionTemplateId =
            "KMC-14.11.3-28V-DC-SWITCHED";

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

            ResolveSwitching(
                distribution);

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
             * Loads remain deterministic bus assignments. Switch conduction is
             * resolved separately so source hardware truth and energy flow are
             * not conflated with source health.
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

                bus.ActiveSourceId =
                    string.Empty;

                bus.Voltage =
                    0.0;

                bus.State =
                    SyntheticElectricalBusState.Unpowered;
            }

            /*
             * Bus-feed sources depend on the resolved state of their parent
             * main bus, so bounded passes are retained from the 14.1 model.
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

                ResolveSwitching(
                    distribution);

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
                    string activeSourceId =
                        string.Empty;

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

                        if (string.IsNullOrWhiteSpace(
                                activeSourceId))
                        {
                            activeSourceId =
                                source.Id;
                        }
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
                            nextState ||
                        !string.Equals(
                            bus.ActiveSourceId,
                            activeSourceId,
                            StringComparison.Ordinal))
                    {
                        bus.AvailableCurrentAmps =
                            available;

                        bus.ActiveSourceCount =
                            sourceCount;

                        bus.ActiveSourceId =
                            activeSourceId;

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

        /// <summary>
        /// Resolves the current no-fault hardware position and actual
        /// conduction state. Later 14.11 builds will insert switch failure
        /// effects between commanded and actual state.
        /// </summary>
        private static void ResolveSwitching(
            SyntheticElectricalDistributionModel distribution)
        {
            if (distribution == null)
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

                source.SelectedForBus =
                    false;

                source.Conducting =
                    false;
            }

            for (int index = 0;
                 index < distribution.Switches.Count;
                 index++)
            {
                SyntheticElectricalSwitch item =
                    distribution.Switches[index];

                if (item == null)
                {
                    continue;
                }

                /*
                 * 14.11.3 establishes separate truth fields. In this foundation
                 * build no switch hardware failure exists yet, so actual and
                 * indicated position follow command.
                 */
                item.ActualClosed =
                    item.CommandedClosed;

                item.IndicatedClosed =
                    item.ActualClosed;

                item.Conducting =
                    false;
            }

            ResolveMainSourceTransfer(
                distribution,
                "BUS_MAIN_A",
                "SRC_GEN_A",
                "SRC_BAT_A",
                "XFER_MAIN_A");

            ResolveMainSourceTransfer(
                distribution,
                "BUS_MAIN_B",
                "SRC_GEN_B",
                "SRC_BAT_B",
                "XFER_MAIN_B");

            ResolveFeed(
                distribution,
                "FEED_ESS_A");

            ResolveFeed(
                distribution,
                "FEED_ESS_B");

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

                SyntheticElectricalSwitch breaker =
                    distribution.FindSwitch(
                        load.BreakerId);

                if (breaker == null)
                {
                    continue;
                }

                breaker.CommandedClosed =
                    load.CommandedOn;

                breaker.ActualClosed =
                    breaker.CommandedClosed;

                breaker.IndicatedClosed =
                    breaker.ActualClosed;

                breaker.Conducting =
                    breaker.ActualClosed;
            }
        }

        private static void ResolveMainSourceTransfer(
            SyntheticElectricalDistributionModel distribution,
            string busId,
            string generatorId,
            string batteryId,
            string transferId)
        {
            SyntheticElectricalSource generator =
                distribution.FindSource(
                    generatorId);

            SyntheticElectricalSource battery =
                distribution.FindSource(
                    batteryId);

            SyntheticElectricalSwitch generatorContactor =
                generator != null
                    ? distribution.FindSwitch(
                        generator.ContactorId)
                    : null;

            SyntheticElectricalSwitch batteryContactor =
                battery != null
                    ? distribution.FindSwitch(
                        battery.ContactorId)
                    : null;

            SyntheticElectricalSwitch transfer =
                distribution.FindSwitch(
                    transferId);

            bool generatorReady =
                SourceHardwareReady(
                    generator,
                    generatorContactor);

            bool batteryReady =
                SourceHardwareReady(
                    battery,
                    batteryContactor);

            SyntheticElectricalSource selected =
                generatorReady
                    ? generator
                    : batteryReady
                        ? battery
                        : null;

            if (transfer != null)
            {
                transfer.CommandedClosed =
                    selected != null;

                transfer.ActualClosed =
                    transfer.CommandedClosed;

                transfer.IndicatedClosed =
                    transfer.ActualClosed;

                transfer.Conducting =
                    transfer.ActualClosed &&
                    selected != null;

                transfer.UpstreamId =
                    selected != null
                        ? selected.Id
                        : string.Empty;

                transfer.DownstreamId =
                    busId;
            }

            if (selected == null)
            {
                return;
            }

            selected.SelectedForBus =
                true;

            selected.Conducting =
                transfer == null ||
                transfer.Conducting;

            SyntheticElectricalSwitch selectedContactor =
                distribution.FindSwitch(
                    selected.ContactorId);

            if (selectedContactor != null)
            {
                selectedContactor.Conducting =
                    selected.Conducting;
            }
        }

        private static bool SourceHardwareReady(
            SyntheticElectricalSource source,
            SyntheticElectricalSwitch contactor)
        {
            if (source == null ||
                !source.CommandedAvailable ||
                source.State ==
                    SyntheticElectricalSourceState.Offline ||
                source.RatedAvailableCurrentAmps <=
                    0.000001)
            {
                return false;
            }

            return
                contactor == null ||
                contactor.ActualClosed;
        }

        private static void ResolveFeed(
            SyntheticElectricalDistributionModel distribution,
            string sourceId)
        {
            SyntheticElectricalSource feed =
                distribution.FindSource(
                    sourceId);

            if (feed == null)
            {
                return;
            }

            SyntheticElectricalSwitch contactor =
                distribution.FindSwitch(
                    feed.ContactorId);

            bool closed =
                contactor == null ||
                contactor.ActualClosed;

            SyntheticElectricalBus parent =
                distribution.FindBus(
                    feed.ParentBusId);

            bool parentUsable =
                parent != null &&
                (parent.State ==
                    SyntheticElectricalBusState.Nominal ||
                 parent.State ==
                    SyntheticElectricalBusState.HighLoad);

            feed.SelectedForBus =
                closed &&
                feed.CommandedAvailable &&
                feed.State !=
                    SyntheticElectricalSourceState.Offline &&
                parentUsable;

            feed.Conducting =
                feed.SelectedForBus;

            if (contactor != null)
            {
                contactor.Conducting =
                    feed.Conducting;
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
                "MAIN BUS A",
                "XFER_MAIN_A");

            AddBus(
                distribution,
                "BUS_MAIN_B",
                "MAIN BUS B",
                "XFER_MAIN_B");

            AddBus(
                distribution,
                "BUS_ESS",
                "ESSENTIAL BUS",
                string.Empty);

            /*
             * Main generator is the normal source; battery is available reserve.
             * Source transfer selects exactly one conducting source per main bus.
             */
            AddSource(
                distribution,
                "SRC_GEN_A",
                "GENERATOR A",
                "BUS_MAIN_A",
                string.Empty,
                SyntheticElectricalSourceKind.Generator,
                12.0,
                "CONT_GEN_A");

            AddSource(
                distribution,
                "SRC_BAT_A",
                "BATTERY A",
                "BUS_MAIN_A",
                string.Empty,
                SyntheticElectricalSourceKind.Battery,
                6.0,
                "CONT_BAT_A");

            AddSource(
                distribution,
                "SRC_GEN_B",
                "GENERATOR B",
                "BUS_MAIN_B",
                string.Empty,
                SyntheticElectricalSourceKind.Generator,
                12.0,
                "CONT_GEN_B");

            AddSource(
                distribution,
                "SRC_BAT_B",
                "BATTERY B",
                "BUS_MAIN_B",
                string.Empty,
                SyntheticElectricalSourceKind.Battery,
                6.0,
                "CONT_BAT_B");

            AddSource(
                distribution,
                "FEED_ESS_A",
                "ESS FEED A",
                "BUS_ESS",
                "BUS_MAIN_A",
                SyntheticElectricalSourceKind.BusFeed,
                6.0,
                "CONT_ESS_A");

            AddSource(
                distribution,
                "FEED_ESS_B",
                "ESS FEED B",
                "BUS_ESS",
                "BUS_MAIN_B",
                SyntheticElectricalSourceKind.BusFeed,
                6.0,
                "CONT_ESS_B");

            AddSwitch(
                distribution,
                "CONT_GEN_A",
                "GEN A CONTACTOR",
                "SRC_GEN_A",
                "XFER_MAIN_A",
                SyntheticElectricalSwitchKind.SourceContactor,
                false);

            AddSwitch(
                distribution,
                "CONT_BAT_A",
                "BAT A CONTACTOR",
                "SRC_BAT_A",
                "XFER_MAIN_A",
                SyntheticElectricalSwitchKind.SourceContactor,
                false);

            AddSwitch(
                distribution,
                "XFER_MAIN_A",
                "MAIN A SOURCE TRANSFER",
                string.Empty,
                "BUS_MAIN_A",
                SyntheticElectricalSwitchKind.SourceTransfer,
                true);

            AddSwitch(
                distribution,
                "CONT_GEN_B",
                "GEN B CONTACTOR",
                "SRC_GEN_B",
                "XFER_MAIN_B",
                SyntheticElectricalSwitchKind.SourceContactor,
                false);

            AddSwitch(
                distribution,
                "CONT_BAT_B",
                "BAT B CONTACTOR",
                "SRC_BAT_B",
                "XFER_MAIN_B",
                SyntheticElectricalSwitchKind.SourceContactor,
                false);

            AddSwitch(
                distribution,
                "XFER_MAIN_B",
                "MAIN B SOURCE TRANSFER",
                string.Empty,
                "BUS_MAIN_B",
                SyntheticElectricalSwitchKind.SourceTransfer,
                true);

            AddSwitch(
                distribution,
                "CONT_ESS_A",
                "ESS FEED A CONTACTOR",
                "BUS_MAIN_A",
                "BUS_ESS",
                SyntheticElectricalSwitchKind.BusFeedContactor,
                false);

            AddSwitch(
                distribution,
                "CONT_ESS_B",
                "ESS FEED B CONTACTOR",
                "BUS_MAIN_B",
                "BUS_ESS",
                SyntheticElectricalSwitchKind.BusFeedContactor,
                false);

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

                    SyntheticElectricalSwitch contactor =
                        distribution.FindSwitch(
                            source.ContactorId);

                    if (contactor != null)
                    {
                        contactor.CommandedClosed =
                            commanded;
                    }
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

                SyntheticElectricalSwitch breaker =
                    distribution.FindSwitch(
                        load.BreakerId);

                if (breaker != null)
                {
                    breaker.CommandedClosed =
                        commanded;
                }

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
                !source.Conducting ||
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
            string displayName,
            string transferSwitchId)
        {
            distribution.Buses.Add(
                new SyntheticElectricalBus
                {
                    Id = id,
                    DisplayName = displayName,
                    TransferSwitchId =
                        transferSwitchId ?? string.Empty,
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
            double capacityAmps,
            string contactorId)
        {
            distribution.Sources.Add(
                new SyntheticElectricalSource
                {
                    Id = id,
                    DisplayName = displayName,
                    BusId = busId,
                    ParentBusId = parentBusId,
                    ContactorId =
                        contactorId ?? string.Empty,
                    Kind = kind,
                    CommandedAvailable = true,
                    State =
                        SyntheticElectricalSourceState.Online,
                    NominalVoltage = NominalVoltage,
                    CapacityAmps = capacityAmps
                });
        }

        private static void AddSwitch(
            SyntheticElectricalDistributionModel distribution,
            string id,
            string displayName,
            string upstreamId,
            string downstreamId,
            SyntheticElectricalSwitchKind kind,
            bool automatic)
        {
            distribution.Switches.Add(
                new SyntheticElectricalSwitch
                {
                    Id = id,
                    DisplayName = displayName,
                    UpstreamId =
                        upstreamId ?? string.Empty,
                    DownstreamId =
                        downstreamId ?? string.Empty,
                    Kind = kind,
                    CommandedClosed = true,
                    ActualClosed = true,
                    IndicatedClosed = true,
                    Conducting = false,
                    Automatic = automatic
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
            string breakerId =
                "BRK_" +
                equipmentId;

            distribution.Loads.Add(
                new SyntheticElectricalLoad
                {
                    EquipmentId = equipmentId,
                    DisplayName = displayName,
                    BusId = busId,
                    BreakerId = breakerId,
                    DemandAmps = demandAmps,
                    Priority = priority,
                    CommandedOn = true
                });

            AddSwitch(
                distribution,
                breakerId,
                displayName + " BREAKER",
                busId,
                equipmentId,
                SyntheticElectricalSwitchKind.LoadBreaker,
                false);
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
                " | Switches=" +
                distribution.Switches.Count.ToString() +
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
                "SRC," +
                (string.IsNullOrWhiteSpace(
                    bus.ActiveSourceId)
                    ? "NONE"
                    : bus.ActiveSourceId);
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
                string.Equals(
                    mainA.ActiveSourceId,
                    "SRC_GEN_A",
                    StringComparison.Ordinal) &&
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
                " | TEST=GEN_B+BAT_B OFFLINE / SWITCHED SOURCE");

            Debug.Assert(
                pass,
                "Build 14.11.3 electrical distribution self-test failed.");
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
