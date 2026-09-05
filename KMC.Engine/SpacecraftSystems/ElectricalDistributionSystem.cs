using System;
using System.Diagnostics;
using KMC.Engine.Electrical;
using KMC.Engine.Models;

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
            "KMC-14.11.5-28V-DC-LOAD-MANAGEMENT";

        private const double NominalVoltage = 28.0;
        private const double HighLoadThreshold = 0.80;
        private const double UndervoltageThreshold = 24.0;

        /*
         * Build 14.13.2B:
         * Essential avionics remain powered through a degraded main-bus
         * condition while usable voltage still exists. This is a synthetic
         * spacecraft design threshold, not a KSP voltage value.
         */
        private const double EssentialFeedMinimumVoltage = 20.0;

        /*
         * Build 14.13.4:
         * KMC-owned spacecraft loads have a normalized full-load EC budget.
         *
         * This is deliberately NOT an amp-to-EC physical conversion.
         * The 0.100 EC/s budget is apportioned by each synthetic load's share
         * of total configured amp demand. Actual breaker conduction determines
         * whether that share is currently charged to KSP.
         */
        private const double KmcOwnedFullLoadEcPerSecond = 0.100;

        private string _lastDiagnosticKey;

        public SyntheticElectricalDistributionSystem()
        {
            _lastDiagnosticKey =
                string.Empty;
        }


        /// <summary>
        /// Build 14.13.2 integration boundary.
        ///
        /// KSP decides whether generation and stored ElectricCharge really
        /// exist. KMC continues to own the synthetic 28 V A/B distribution.
        ///
        /// Real producer parts are deliberately NOT assigned to A or B.
        /// Both synthetic generator channels are gated by the same vessel-level
        /// real generation evidence.
        /// </summary>
        public static void ApplyRealKspSourceEvidence(
            SpacecraftSystemsModel systems,
            SyntheticElectricalDistributionModel distribution,
            PowerModel power)
        {
            if (distribution == null)
            {
                return;
            }

            ElectricalAttributionModel attribution =
                power != null
                    ? power.Attribution
                    : null;

            ElectricalPowerDiagnosticModel diagnostic =
                power != null
                    ? power.Diagnostic
                    : null;

            ElectricalFlowModel flow =
                power != null
                    ? power.Flow
                    : null;

            bool generationKnown;
            bool generationActive;

            ResolveGenerationEvidence(
                attribution,
                out generationKnown,
                out generationActive);

            bool storageKnown =
                diagnostic != null &&
                diagnostic.TelemetryAvailable;

            bool batteryAvailable =
                storageKnown &&
                diagnostic.CapacityEc > 0.000001 &&
                diagnostic.StoredEc > 0.000001;

            bool storageDraining =
                flow != null &&
                flow.HasMeasuredNetStorageRate &&
                flow.NetStorageRateEcPerSecond < -0.01;

            bool supplement =
                generationKnown &&
                generationActive &&
                batteryAvailable &&
                storageDraining;

            ApplyRealGeneratorState(
                distribution.FindSource("SRC_GEN_A"),
                generationKnown,
                generationActive);

            ApplyRealGeneratorState(
                distribution.FindSource("SRC_GEN_B"),
                generationKnown,
                generationActive);

            ApplyRealBatteryState(
                distribution.FindSource("SRC_BAT_A"),
                storageKnown,
                batteryAvailable,
                supplement);

            ApplyRealBatteryState(
                distribution.FindSource("SRC_BAT_B"),
                storageKnown,
                batteryAvailable,
                supplement);

            /*
             * Reapply local synthetic source failures after real KSP evidence
             * gates normal availability. A real producer cannot resurrect an
             * injected failed KMC generator channel.
             */
            FailureSimulationSnapshot failures =
                systems != null
                    ? systems.FailureSimulation
                    : null;

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
        }

        private static void ResolveGenerationEvidence(
            ElectricalAttributionModel attribution,
            out bool known,
            out bool active)
        {
            known = false;
            active = false;

            if (attribution == null ||
                !attribution.TelemetryAvailable)
            {
                return;
            }

            if (attribution.ProducerCount <= 0)
            {
                known = true;
                return;
            }

            if (attribution.KnownCurrentGenerationEcPerSecond >
                0.000001)
            {
                known = true;
                active = true;
                return;
            }

            /*
             * Zero generation is only authoritative when every discovered
             * producer has current-rate evidence. If any producer is unknown,
             * keep the generator state UNKNOWN rather than inventing zero.
             */
            if (attribution.KnownCurrentProducerCount >=
                attribution.ProducerCount)
            {
                known = true;
                active = false;
            }
        }

        private static void ApplyRealGeneratorState(
            SyntheticElectricalSource source,
            bool generationKnown,
            bool generationActive)
        {
            if (source == null)
            {
                return;
            }

            source.Supplementing =
                false;

            source.State =
                !generationKnown
                    ? SyntheticElectricalSourceState.Unknown
                    : generationActive
                        ? SyntheticElectricalSourceState.Online
                        : SyntheticElectricalSourceState.Offline;
        }

        private static void ApplyRealBatteryState(
            SyntheticElectricalSource source,
            bool storageKnown,
            bool batteryAvailable,
            bool supplement)
        {
            if (source == null)
            {
                return;
            }

            source.State =
                !storageKnown
                    ? SyntheticElectricalSourceState.Unknown
                    : batteryAvailable
                        ? SyntheticElectricalSourceState.Online
                        : SyntheticElectricalSourceState.Offline;

            source.Supplementing =
                batteryAvailable &&
                supplement;
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

            ApplyElectricalSwitchFailures(
                distribution,
                failures);

            ApplyElectricalBusFailures(
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

            ResetAutomaticLoadShedding(
                distribution);

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
                    0.0;

                bus.ShedDemandAmps =
                    0.0;

                bus.ManualShedDemandAmps =
                    0.0;

                bus.AvailableCurrentAmps =
                    0.0;

                bus.ActiveSourceCount =
                    0;

                bus.ActiveSourceId =
                    string.Empty;

                bus.Voltage =
                    0.0;

                bus.State =
                    bus.HardwareFailed
                        ? SyntheticElectricalBusState.Failed
                        : SyntheticElectricalBusState.Unpowered;
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

                    if (bus.HardwareFailed)
                    {
                        bus.DemandAmps =
                            0.0;

                        bus.ShedDemandAmps =
                            0.0;

                        bus.ManualShedDemandAmps =
                            SumManualShedDemand(
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
                            SyntheticElectricalBusState.Failed;

                        continue;
                    }

                    bus.DemandAmps =
                        SumDemand(
                            distribution,
                            bus.Id);

                    bus.ShedDemandAmps =
                        SumShedDemand(
                            distribution,
                            bus.Id);

                    bus.ManualShedDemandAmps =
                        SumManualShedDemand(
                            distribution,
                            bus.Id);

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

                        double candidateVoltage =
                            source.Kind ==
                                SyntheticElectricalSourceKind.BusFeed
                                ? GetBusFeedVoltage(
                                    distribution,
                                    source)
                                : source.NominalVoltage;

                        sourceVoltage =
                            Math.Max(
                                sourceVoltage,
                                candidateVoltage);

                        sourceCount++;

                        if (string.IsNullOrWhiteSpace(
                                activeSourceId))
                        {
                            activeSourceId =
                                source.Id;
                        }
                    }

                    if (available > 0.000001 &&
                        bus.DemandAmps >
                            available + 0.000001)
                    {
                        ApplyAutomaticLoadShedding(
                            distribution,
                            bus.Id,
                            available);

                        bus.DemandAmps =
                            SumDemand(
                                distribution,
                                bus.Id);

                        bus.ShedDemandAmps =
                            SumShedDemand(
                                distribution,
                                bus.Id);

                        bus.ManualShedDemandAmps =
                            SumManualShedDemand(
                                distribution,
                                bus.Id);
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

            UpdateKmcOwnedEcLoad(
                distribution);
        }

        /// <summary>
        /// Build 14.13.4 KMC-owned real-EC load bridge calculation.
        ///
        /// Configured amp demand is used only as a weighting system. KSP never
        /// receives an amp value. A load contributes to the current EC command
        /// only when its resolved breaker is actually conducting. Therefore:
        ///
        /// - manual breaker open -> EC load falls
        /// - automatic shed -> EC load falls
        /// - upstream bus unpowered -> EC load falls
        /// - welded-closed breaker can continue consuming despite CMD OPEN
        /// </summary>
        private static void UpdateKmcOwnedEcLoad(
            SyntheticElectricalDistributionModel distribution)
        {
            if (distribution == null)
            {
                return;
            }

            double configuredAmps = 0.0;
            double energizedAmps = 0.0;

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

                double demand =
                    Math.Max(
                        0.0,
                        load.DemandAmps);

                configuredAmps +=
                    demand;

                SyntheticElectricalSwitch breaker =
                    distribution.FindSwitch(
                        load.BreakerId);

                if (breaker != null &&
                    breaker.Conducting)
                {
                    energizedAmps +=
                        demand;
                }
            }

            double fraction =
                configuredAmps > 0.000001
                    ? Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            energizedAmps /
                            configuredAmps))
                    : 0.0;

            distribution.KmcOwnedFullLoadEcPerSecond =
                configuredAmps > 0.000001
                    ? KmcOwnedFullLoadEcPerSecond
                    : 0.0;

            distribution.KmcOwnedActiveLoadEcPerSecond =
                KmcOwnedFullLoadEcPerSecond *
                fraction;

            distribution.KmcOwnedConfiguredDemandAmps =
                configuredAmps;

            distribution.KmcOwnedEnergizedDemandAmps =
                energizedAmps;
        }

        private static void ApplyElectricalBusFailures(
            SyntheticElectricalDistributionModel distribution,
            FailureSimulationSnapshot failures)
        {
            if (distribution == null ||
                failures == null ||
                failures.Mode ==
                    FailureSimulationMode.Nominal ||
                failures.Failures == null)
            {
                return;
            }

            for (int index = 0;
                 index < failures.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    failures.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.Component ||
                    failure.ComponentHealth !=
                        SpacecraftSystemHealth.Failed)
                {
                    continue;
                }

                if (!string.Equals(
                        failure.TargetId,
                        "BUS_MAIN_A",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        failure.TargetId,
                        "BUS_MAIN_B",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        failure.TargetId,
                        "BUS_ESS",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                SyntheticElectricalBus bus =
                    distribution.FindBus(
                        failure.TargetId);

                if (bus != null)
                {
                    bus.HardwareFailed =
                        true;
                }
            }
        }

        private static void ApplyElectricalSwitchFailures(
            SyntheticElectricalDistributionModel distribution,
            FailureSimulationSnapshot failures)
        {
            if (distribution == null ||
                failures == null ||
                failures.Mode ==
                    FailureSimulationMode.Nominal)
            {
                return;
            }

            for (int index = 0;
                 index < failures.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    failures.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow)
                {
                    continue;
                }

                string switchId;
                SyntheticElectricalSwitchFailureMode mode;

                if (!SyntheticElectricalSwitchFailureTargets.TryParse(
                        failure.TargetId,
                        out switchId,
                        out mode))
                {
                    continue;
                }

                SyntheticElectricalSwitch item =
                    distribution.FindSwitch(
                        switchId);

                if (item != null)
                {
                    item.FailureMode =
                        mode;
                }
            }
        }

        private static void ResolveSwitchHardwarePosition(
            SyntheticElectricalSwitch item)
        {
            if (item == null)
            {
                return;
            }

            switch (item.FailureMode)
            {
                case SyntheticElectricalSwitchFailureMode.FailedOpen:
                case SyntheticElectricalSwitchFailureMode.TrippedOpen:
                    item.ActualClosed =
                        false;
                    break;

                case SyntheticElectricalSwitchFailureMode.WeldedClosed:
                    item.ActualClosed =
                        true;
                    break;

                default:
                    item.ActualClosed =
                        item.CommandedClosed;
                    break;
            }

            switch (item.FailureMode)
            {
                case SyntheticElectricalSwitchFailureMode.FalseClosedIndication:
                    item.IndicatedClosed =
                        true;
                    break;

                case SyntheticElectricalSwitchFailureMode.FalseOpenIndication:
                    item.IndicatedClosed =
                        false;
                    break;

                default:
                    item.IndicatedClosed =
                        item.ActualClosed;
                    break;
            }
        }

        /// <summary>
        /// Resolves commanded switch state into hardware position, indication
        /// and actual conduction after any active hardware failure is applied.
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

                ResolveSwitchHardwarePosition(
                    item);

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

                ResolveSwitchHardwarePosition(
                    breaker);

                SyntheticElectricalBus parentBus =
                    distribution.FindBus(
                        load.BusId);

                bool parentBusEnergized =
                    parentBus != null &&
                    parentBus.State !=
                        SyntheticElectricalBusState.Unpowered &&
                    parentBus.State !=
                        SyntheticElectricalBusState.Failed &&
                    parentBus.Voltage >
                        0.000001;

                breaker.Conducting =
                    breaker.ActualClosed &&
                    !load.AutomaticallyShed &&
                    parentBusEnergized;
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

            SyntheticElectricalBus destinationBus =
                distribution.FindBus(
                    busId);

            bool busFailed =
                destinationBus != null &&
                destinationBus.HardwareFailed;

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

                ResolveSwitchHardwarePosition(
                    transfer);

                transfer.Conducting =
                    transfer.ActualClosed &&
                    selected != null &&
                    !busFailed;

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
                !busFailed;

            selected.Conducting =
                !busFailed &&
                (transfer == null ||
                 transfer.Conducting);

            SyntheticElectricalSwitch selectedContactor =
                distribution.FindSwitch(
                    selected.ContactorId);

            if (selectedContactor != null)
            {
                selectedContactor.Conducting =
                    selected.Conducting;
            }

            /*
             * When real generation is active but the observed EC store is
             * draining, KMC keeps the generator as the primary transfer
             * selection and allows the modeled battery channel to supplement.
             * No real KSP part is assigned to either A or B.
             */
            if (selected == generator &&
                batteryReady &&
                battery != null &&
                battery.Supplementing &&
                !busFailed)
            {
                battery.Conducting =
                    true;

                battery.SelectedForBus =
                    false;

                if (batteryContactor != null)
                {
                    batteryContactor.Conducting =
                        true;
                }
            }
        }

        private static bool SourceHardwareReady(
            SyntheticElectricalSource source,
            SyntheticElectricalSwitch contactor)
        {
            if (source == null ||
                !source.CommandedAvailable ||
                (source.State ==
                    SyntheticElectricalSourceState.Offline ||
                 source.State ==
                    SyntheticElectricalSourceState.Unknown) ||
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
                parent.State !=
                    SyntheticElectricalBusState.Unpowered &&
                parent.State !=
                    SyntheticElectricalBusState.Failed &&
                parent.Voltage >=
                    EssentialFeedMinimumVoltage;

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
                12.0,
                "CONT_ESS_A");

            AddSource(
                distribution,
                "FEED_ESS_B",
                "ESS FEED B",
                "BUS_ESS",
                "BUS_MAIN_B",
                SyntheticElectricalSourceKind.BusFeed,
                12.0,
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

            /*
             * Build 14.11.5 expanded synthetic spacecraft loads.
             *
             * Each main bus receives 2.0 A of shed-first utility/thermal load.
             * Normal main-bus demand becomes 9.5 A / 12.0 A (79%), just below
             * the HIGH LOAD threshold. A 6 A degraded source or battery
             * transfer automatically sheds these priority-3 loads, preserving
             * the existing 7.5 A core spacecraft load.
             */
            AddLoad(
                distribution,
                "CABIN_FAN_A",
                "CABIN FAN A",
                "BUS_MAIN_A",
                1.0,
                3);

            AddLoad(
                distribution,
                "THERMAL_HEATER_A",
                "THERMAL HEATER A",
                "BUS_MAIN_A",
                1.0,
                3);

            AddLoad(
                distribution,
                "CABIN_FAN_B",
                "CABIN FAN B",
                "BUS_MAIN_B",
                1.0,
                3);

            AddLoad(
                distribution,
                "THERMAL_HEATER_B",
                "THERMAL HEATER B",
                "BUS_MAIN_B",
                1.0,
                3);

            AddLoad(
                distribution,
                "INSTRUMENTATION_ESS",
                "ESS INSTRUMENTATION",
                "BUS_ESS",
                1.0,
                1);
            AddLoad(
                distribution,
                "FLIGHT_CONTROL",
                "SAS / FLIGHT CONTROL ELECTRONICS",
                "BUS_ESS",
                1.0,
                1);
            AddLoad(
                distribution,
                "REACTION_WHEEL",
                "REACTION WHEEL POWER",
                "BUS_ESS",
                1.0,
                1);
            AddLoad(
                distribution,
                "ENGINE_CONTROL",
                "ENGINE CONTROL / IGNITION",
                "BUS_ESS",
                0.75,
                1);
            AddLoad(
                distribution,
                "STAGING_CONTROL",
                "STAGING / SEPARATION",
                "BUS_ESS",
                0.25,
                1);
            AddLoad(
                distribution,
                "BRAKE_CONTROL",
                "BRAKE CONTROL",
                "BUS_ESS",
                0.5,
                1);
            AddLoad(
                distribution,
                "GEAR_CONTROL",
                "GEAR CONTROL / ACTUATION",
                "BUS_ESS",
                0.5,
                1);
            AddLoad(
                distribution,
                "LIGHTING_ESS",
                "EXTERNAL / EMERGENCY LIGHTING",
                "BUS_ESS",
                0.5,
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
             * A commanded-on load with a non-conducting branch breaker is
             * electrically unpowered even if its parent bus remains healthy.
             * This is provider evidence, not intrinsic component failure.
             */
            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load == null ||
                    !load.CommandedOn)
                {
                    continue;
                }

                SyntheticElectricalSwitch breaker =
                    distribution.FindSwitch(
                        load.BreakerId);

                if (breaker == null ||
                    breaker.Conducting)
                {
                    continue;
                }

                SpacecraftSystemComponent component =
                    systems.FindComponent(
                        load.EquipmentId);

                if (component != null)
                {
                    component.ProviderStateOverride =
                        SpacecraftSystemState.Unpowered;
                }
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
                case SyntheticElectricalBusState.Failed:
                    return
                        SpacecraftSystemState.Failed;

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
                parent.State !=
                    SyntheticElectricalBusState.Unpowered &&
                parent.State !=
                    SyntheticElectricalBusState.Failed &&
                parent.Voltage >=
                    EssentialFeedMinimumVoltage;
        }

        private static void ResetAutomaticLoadShedding(
            SyntheticElectricalDistributionModel distribution)
        {
            if (distribution == null)
            {
                return;
            }

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load != null)
                {
                    load.AutomaticallyShed =
                        false;
                }
            }
        }

        private static void ApplyAutomaticLoadShedding(
            SyntheticElectricalDistributionModel distribution,
            string busId,
            double availableAmps)
        {
            if (distribution == null ||
                string.IsNullOrWhiteSpace(busId) ||
                availableAmps <= 0.000001)
            {
                return;
            }

            double demand =
                SumDemand(
                    distribution,
                    busId);

            if (demand <=
                    availableAmps + 0.000001)
            {
                return;
            }

            /*
             * Build 14.11.5 protects priority 1/2 equipment and sheds only
             * explicit priority-3 utility loads. List order is deterministic.
             */
            for (int index = 0;
                 index < distribution.Loads.Count &&
                 demand >
                    availableAmps + 0.000001;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load == null ||
                    !load.CommandedOn ||
                    load.AutomaticallyShed ||
                    load.Priority < 3 ||
                    !string.Equals(
                        load.BusId,
                        busId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                SyntheticElectricalSwitch breaker =
                    distribution.FindSwitch(
                        load.BreakerId);

                if (breaker != null &&
                    !breaker.ActualClosed)
                {
                    continue;
                }

                load.AutomaticallyShed =
                    true;

                if (breaker != null)
                {
                    breaker.Conducting =
                        false;
                }

                demand -=
                    Math.Max(
                        0.0,
                        load.DemandAmps);
            }
        }

        /// <summary>
        /// Build 14.13.3 manual load-management accounting.
        ///
        /// Only explicit crew-commanded OFF loads count here. Automatic
        /// priority shedding remains in ShedDemandAmps so EECOM can distinguish
        /// operator action from protection-system action.
        /// </summary>
        private static double SumManualShedDemand(
            SyntheticElectricalDistributionModel distribution,
            string busId)
        {
            double shed = 0.0;

            if (distribution == null ||
                string.IsNullOrWhiteSpace(
                    busId))
            {
                return shed;
            }

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load != null &&
                    !load.CommandedOn &&
                    string.Equals(
                        load.BusId,
                        busId,
                        StringComparison.Ordinal))
                {
                    shed +=
                        Math.Max(
                            0.0,
                            load.DemandAmps);
                }
            }

            return shed;
        }

        private static double SumShedDemand(
            SyntheticElectricalDistributionModel distribution,
            string busId)
        {
            double shed = 0.0;

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load != null &&
                    load.CommandedOn &&
                    load.AutomaticallyShed &&
                    string.Equals(
                        load.BusId,
                        busId,
                        StringComparison.Ordinal))
                {
                    shed +=
                        Math.Max(
                            0.0,
                            load.DemandAmps);
                }
            }

            return shed;
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

                SyntheticElectricalSwitch breaker =
                    load != null
                        ? distribution.FindSwitch(
                            load.BreakerId)
                        : null;

                bool branchConducting =
                    load != null &&
                    load.CommandedOn &&
                    (breaker == null ||
                     breaker.Conducting);

                if (branchConducting &&
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

        private static double GetBusFeedVoltage(
            SyntheticElectricalDistributionModel distribution,
            SyntheticElectricalSource source)
        {
            if (distribution == null ||
                source == null ||
                string.IsNullOrWhiteSpace(
                    source.ParentBusId))
            {
                return
                    source != null
                        ? source.NominalVoltage
                        : 0.0;
            }

            SyntheticElectricalBus parent =
                distribution.FindBus(
                    source.ParentBusId);

            return
                parent != null
                    ? Math.Max(
                        0.0,
                        parent.Voltage)
                    : 0.0;
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

            if (voltage <
                UndervoltageThreshold)
            {
                state =
                    SyntheticElectricalBusState.Undervoltage;

                return;
            }

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
                "%,SHED=" +
                bus.ShedDemandAmps.ToString("0.0") +
                "A," +
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
