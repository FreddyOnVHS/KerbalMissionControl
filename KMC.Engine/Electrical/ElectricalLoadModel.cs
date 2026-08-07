using System;

namespace KMC.Engine.Electrical
{
    public enum ElectricalLoadInferenceState
    {
        Unavailable = 0,
        WaitingForFlow,
        GenerationIncomplete,
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

            model.InferredTotalLoadEcPerSecond =
                Math.Max(
                    0.0,
                    model.GenerationEcPerSecond -
                    model.StorageRateEcPerSecond);

            model.HasInferredTotalLoad =
                true;

            model.AttributedCurrentLoadEcPerSecond =
                attribution != null
                    ? Math.Max(
                        0.0,
                        attribution.KnownCurrentConsumptionEcPerSecond)
                    : 0.0;

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
