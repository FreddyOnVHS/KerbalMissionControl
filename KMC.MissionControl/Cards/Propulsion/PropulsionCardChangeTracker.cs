using System;
using System.Globalization;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Cards.Propulsion
{
    /// <summary>
    /// Tracks the values that are actually visible on each PROP card.
    ///
    /// Values are quantized to the same precision used by the renderer. This
    /// prevents card bitmap rebuilds for telemetry changes too small to alter
    /// any displayed text, status, or tank percentage.
    /// </summary>
    public sealed class PropulsionCardChangeTracker
    {
        private bool _initialized;

        private string _engineClusterSignature;
        private string _performanceSignature;
        private string _flowSignature;
        private string _footerSignature;

        public PropulsionCardChangeSet Evaluate(
            MissionTelemetry telemetry,
            PropulsionRenderGraph graph)
        {
            if (telemetry == null)
            {
                return PropulsionCardChangeSet.All;
            }

            string engineCluster =
                BuildEngineClusterSignature(
                    telemetry);

            string performance =
                BuildPerformanceSignature(
                    telemetry,
                    graph);

            string flow =
                BuildFlowSignature(
                    telemetry);

            string footer =
                BuildFooterSignature(
                    telemetry,
                    graph);

            if (!_initialized)
            {
                _initialized = true;

                _engineClusterSignature =
                    engineCluster;

                _performanceSignature =
                    performance;

                _flowSignature =
                    flow;

                _footerSignature =
                    footer;

                return PropulsionCardChangeSet.All;
            }

            PropulsionCardChangeSet result =
                new PropulsionCardChangeSet
                {
                    EngineClusterChanged =
                        !string.Equals(
                            _engineClusterSignature,
                            engineCluster,
                            StringComparison.Ordinal),

                    PerformanceChanged =
                        !string.Equals(
                            _performanceSignature,
                            performance,
                            StringComparison.Ordinal),

                    FlowChanged =
                        !string.Equals(
                            _flowSignature,
                            flow,
                            StringComparison.Ordinal),

                    FooterChanged =
                        !string.Equals(
                            _footerSignature,
                            footer,
                            StringComparison.Ordinal)
                };

            _engineClusterSignature =
                engineCluster;

            _performanceSignature =
                performance;

            _flowSignature =
                flow;

            _footerSignature =
                footer;

            return result;
        }

        public void Reset()
        {
            _initialized =
                false;

            _engineClusterSignature =
                null;

            _performanceSignature =
                null;

            _flowSignature =
                null;

            _footerSignature =
                null;
        }

        private static string BuildEngineClusterSignature(
            MissionTelemetry telemetry)
        {
            return Join(
                telemetry.ProducingThrustEngineCount > 0
                    ? 1L
                    : 0L);
        }

        private static string BuildPerformanceSignature(
            MissionTelemetry telemetry,
            PropulsionRenderGraph graph)
        {
            return Join(
                telemetry.CurrentStage,
                telemetry.EngineCount,
                telemetry.ProducingThrustEngineCount,
                Quantize(
                    telemetry.Throttle,
                    100.0),
                Quantize(
                    telemetry.CurrentThrust,
                    10.0),
                Quantize(
                    telemetry.ThrustToWeightRatio,
                    100.0),
                Quantize(
                    telemetry.AverageSpecificImpulse,
                    10.0),
                graph != null
                    ? graph.TopologyRevision
                    : -1L,
                telemetry.FlameoutEngineCount,
                telemetry.IgnitedEngineCount,
                IsEmpty(
                    telemetry.StageLiquidFuelAmount,
                    telemetry.StageLiquidFuelCapacity),
                IsEmpty(
                    telemetry.StageOxidizerAmount,
                    telemetry.StageOxidizerCapacity));
        }

        private static string BuildFlowSignature(
            MissionTelemetry telemetry)
        {
            return Join(
                PercentKey(
                    telemetry.StageLiquidFuelAmount,
                    telemetry.StageLiquidFuelCapacity),
                PercentKey(
                    telemetry.TotalLiquidFuelAmount,
                    telemetry.TotalLiquidFuelCapacity),
                PercentKey(
                    telemetry.StageOxidizerAmount,
                    telemetry.StageOxidizerCapacity),
                PercentKey(
                    telemetry.TotalOxidizerAmount,
                    telemetry.TotalOxidizerCapacity),
                PercentKey(
                    telemetry.StageMonopropellantAmount,
                    telemetry.StageMonopropellantCapacity),
                PercentKey(
                    telemetry.TotalMonopropellantAmount,
                    telemetry.TotalMonopropellantCapacity));
        }

        private static string BuildFooterSignature(
            MissionTelemetry telemetry,
            PropulsionRenderGraph graph)
        {
            return Join(
                telemetry.CurrentStage,
                Quantize(
                    telemetry.Throttle,
                    100.0),
                Quantize(
                    telemetry.CurrentThrust,
                    10.0),
                Quantize(
                    telemetry.ThrustToWeightRatio,
                    100.0),
                Quantize(
                    telemetry.AverageSpecificImpulse,
                    1.0),
                telemetry.EngineCount,
                PercentKey(
                    telemetry.StageLiquidFuelAmount,
                    telemetry.StageLiquidFuelCapacity),
                PercentKey(
                    telemetry.StageOxidizerAmount,
                    telemetry.StageOxidizerCapacity),
                graph != null
                    ? graph.TopologyRevision
                    : -1L);
        }

        private static long PercentKey(
            double amount,
            double capacity)
        {
            if (capacity <= 0.0)
            {
                return 0L;
            }

            double fraction =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        amount / capacity));

            return Quantize(
                fraction,
                100.0);
        }

        private static long IsEmpty(
            double amount,
            double capacity)
        {
            return
                capacity > 0.0 &&
                amount <= 0.0001
                    ? 1L
                    : 0L;
        }

        private static long Quantize(
            double value,
            double scale)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return long.MinValue;
            }

            return Convert.ToInt64(
                Math.Round(
                    value * scale,
                    MidpointRounding.AwayFromZero));
        }

        private static string Join(
            params long[] values)
        {
            if (values == null ||
                values.Length == 0)
            {
                return string.Empty;
            }

            string[] parts =
                new string[values.Length];

            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                parts[index] =
                    values[index].ToString(
                        CultureInfo.InvariantCulture);
            }

            return string.Join(
                "|",
                parts);
        }
    }

    public sealed class PropulsionCardChangeSet
    {
        public static PropulsionCardChangeSet All
        {
            get
            {
                return new PropulsionCardChangeSet
                {
                    EngineClusterChanged = true,
                    PerformanceChanged = true,
                    FlowChanged = true,
                    FooterChanged = true
                };
            }
        }

        public bool EngineClusterChanged { get; set; }

        public bool PerformanceChanged { get; set; }

        public bool FlowChanged { get; set; }

        public bool FooterChanged { get; set; }
    }
}
