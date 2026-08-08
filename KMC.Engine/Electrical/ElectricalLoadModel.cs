using System;

namespace KMC.Engine.Electrical
{
    public enum ElectricalLoadInferenceState
    {
        Unavailable = 0,
        WaitingForFlow,
        GenerationIncomplete,
        StorageDepleted,
        StorageSaturated,
        Available
    }

    public sealed class ElectricalLoadModel
    {
        public ElectricalLoadModel()
        {
            State =
                ElectricalLoadInferenceState.Unavailable;
        }

        public ElectricalLoadInferenceState State { get; internal set; }

        public bool GenerationRateComplete { get; internal set; }
        public bool GenerationDerivedFromNoSources { get; internal set; }
        public double GenerationEcPerSecond { get; internal set; }

        public bool HasMeasuredStorageRate { get; internal set; }
        public double StorageRateEcPerSecond { get; internal set; }

        public bool HasInferredTotalLoad { get; internal set; }
        public double InferredTotalLoadEcPerSecond { get; internal set; }

        public double AttributedCurrentLoadEcPerSecond { get; internal set; }
        public double UnattributedLoadEcPerSecond { get; internal set; }

        public double AttributionCoverageFraction
        {
            get
            {
                if (!HasInferredTotalLoad)
                {
                    return 0.0;
                }

                if (InferredTotalLoadEcPerSecond <= 0.000001)
                {
                    return
                        AttributedCurrentLoadEcPerSecond <= 0.000001
                            ? 1.0
                            : 0.0;
                }

                double value =
                    AttributedCurrentLoadEcPerSecond /
                    InferredTotalLoadEcPerSecond;

                if (value < 0.0)
                {
                    return 0.0;
                }

                if (value > 1.0)
                {
                    return 1.0;
                }

                return value;
            }
        }

        public double AttributionCoveragePercent
        {
            get
            {
                return
                    AttributionCoverageFraction *
                    100.0;
            }
        }

        public bool HasUnattributedLoad
        {
            get
            {
                return
                    UnattributedLoadEcPerSecond >
                    0.005;
            }
        }

        public bool AttributionExceedsInferredLoad { get; internal set; }
    }

    internal static class ElectricalLoadAnalyzer
    {
        /*
         * Empirical upper-bound guard from the Build 8.10.2 generator test.
         *
         * KSP can stop accepting generator output before the aggregate EC value
         * reports exact CapacityEc. The observed test entered a flat/limited
         * storage response at approximately 99.4 percent reserve while the
         * producer continued reporting about 7.24 EC/s.
         *
         * Above this reserve threshold, complete positive generation combined
         * with a non-discharging storage buffer cannot prove vessel demand from
         * Generation - dEC/dt. Surplus generation may simply be rejected by
         * the storage boundary.
         */
        private const double SaturationReservePercent = 99.0;
        private const double PositiveGenerationThresholdEcPerSecond = 0.005;
        private const double DischargeProofThresholdEcPerSecond = -0.005;

        public static ElectricalLoadModel Analyze(
            ElectricalNetwork network,
            ElectricalFlowModel flow,
            ElectricalAttributionModel attribution)
        {
            ElectricalLoadModel model =
                new ElectricalLoadModel();

            if (network == null)
            {
                return model;
            }

            /*
             * At zero stored EC, storage delta is clamped by the physical
             * boundary and can no longer reveal vessel demand. Do not convert
             * a flat 0 EC buffer into a false zero-load measurement.
             */
            if (flow != null &&
                flow.TelemetryAvailable &&
                flow.State ==
                    ElectricalStorageFlowState.Depleted)
            {
                model.State =
                    ElectricalLoadInferenceState.StorageDepleted;

                model.AttributedCurrentLoadEcPerSecond =
                    attribution != null
                        ? Math.Max(
                            0.0,
                            attribution.KnownCurrentConsumptionEcPerSecond)
                        : 0.0;

                return model;
            }

            if (flow == null ||
                !flow.TelemetryAvailable ||
                !flow.HasMeasuredNetStorageRate)
            {
                model.State =
                    ElectricalLoadInferenceState.WaitingForFlow;

                return model;
            }

            model.HasMeasuredStorageRate =
                true;

            model.StorageRateEcPerSecond =
                flow.NetStorageRateEcPerSecond;

            /*
             * Establish generation evidence before deciding whether the upper
             * storage boundary makes demand inference unobservable. Generation
             * remains useful and should still be exposed even when total demand
             * cannot be inferred.
             */
            if (network.SourceNodeCount == 0)
            {
                model.GenerationRateComplete =
                    true;

                model.GenerationDerivedFromNoSources =
                    true;

                model.GenerationEcPerSecond =
                    0.0;
            }
            else if (attribution != null &&
                     attribution.TelemetryAvailable &&
                     attribution.ProducerCount ==
                         network.SourceNodeCount &&
                     attribution.KnownCurrentProducerCount ==
                         attribution.ProducerCount)
            {
                model.GenerationRateComplete =
                    true;

                model.GenerationEcPerSecond =
                    Math.Max(
                        0.0,
                        attribution.KnownCurrentGenerationEcPerSecond);
            }
            else
            {
                model.State =
                    ElectricalLoadInferenceState.GenerationIncomplete;

                return model;
            }

            model.AttributedCurrentLoadEcPerSecond =
                attribution != null
                    ? Math.Max(
                        0.0,
                        attribution.KnownCurrentConsumptionEcPerSecond)
                    : 0.0;

            /*
             * Upper-bound observability rule.
             *
             * If storage is at/near practical capacity, known generation is
             * positive, and the EC reservoir is not measurably discharging,
             * dEC/dt cannot distinguish true vessel demand from rejected
             * generation. Keep generation and directly attributed load, but do
             * not claim an inferred total demand or attribution coverage.
             *
             * A measurable discharge is sufficient evidence that the upper
             * boundary is no longer limiting the observed EC balance, so normal
             * inference resumes even at high reserve.
             */
            bool upperBoundaryLimited =
                flow.CapacityEc > 0.000001 &&
                flow.ChargePercent >=
                    SaturationReservePercent &&
                model.GenerationRateComplete &&
                model.GenerationEcPerSecond >
                    PositiveGenerationThresholdEcPerSecond &&
                model.StorageRateEcPerSecond >=
                    DischargeProofThresholdEcPerSecond;

            if (upperBoundaryLimited)
            {
                model.State =
                    ElectricalLoadInferenceState.StorageSaturated;

                return model;
            }

            model.InferredTotalLoadEcPerSecond =
                Math.Max(
                    0.0,
                    model.GenerationEcPerSecond -
                    model.StorageRateEcPerSecond);

            model.HasInferredTotalLoad =
                true;

            double unattributed =
                model.InferredTotalLoadEcPerSecond -
                model.AttributedCurrentLoadEcPerSecond;

            model.AttributionExceedsInferredLoad =
                unattributed <
                -0.005;

            model.UnattributedLoadEcPerSecond =
                Math.Max(
                    0.0,
                    unattributed);

            model.State =
                ElectricalLoadInferenceState.Available;

            return model;
        }
    }
}
