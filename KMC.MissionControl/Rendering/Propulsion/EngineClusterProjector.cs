using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Builds a top-down engine layout from the actual vessel-space
    /// coordinates transmitted by KSP.
    ///
    /// KSP vessel orientation can vary with root-part orientation, so the
    /// projector chooses the pair of coordinate axes with the greatest
    /// two-dimensional spread. That makes the cluster useful for ordinary
    /// rockets without hard-coding X/Z or X/Y.
    /// </summary>
    public sealed class EngineClusterProjector
    {
        private const double MinimumSpread = 0.0001;
        private const double CollisionDistance = 0.16;

        public EngineClusterProjection Build(
            PropulsionRenderGraph graph)
        {
            EngineClusterProjection result =
                new EngineClusterProjection();

            if (graph == null)
            {
                return result;
            }

            List<PropulsionGraphNode> engines =
                graph.Nodes
                    .Where(
                        node =>
                            node.Category ==
                            VesselNodeCategory.Engine)
                    .ToList();

            if (engines.Count == 0)
            {
                return result;
            }

            int selectedStage =
                SelectStage(
                    engines,
                    graph.CurrentStage);

            List<PropulsionGraphNode> selected =
                engines
                    .Where(
                        node =>
                            node.ActivationStage ==
                            selectedStage)
                    .OrderBy(node => node.PartId)
                    .ToList();

            if (selected.Count == 0)
            {
                selected = engines
                    .OrderBy(node => node.PartId)
                    .ToList();
            }

            result.ActivationStage =
                selectedStage;

            result.SeparationStage =
                selected
                    .Where(
                        node =>
                            node.SeparationStage >= 0)
                    .Select(
                        node => node.SeparationStage)
                    .DefaultIfEmpty(-1)
                    .First();

            result.DisplayName =
                CreateClusterName(selected);

            AxisPair pair =
                ChooseAxisPair(selected);

            result.UsedFallbackAxis =
                pair.IsFallback;

            double centerA =
                selected.Average(
                    node => pair.ReadA(node));

            double centerB =
                selected.Average(
                    node => pair.ReadB(node));

            double maximumRadius = 0.0;

            for (int index = 0;
                 index < selected.Count;
                 index++)
            {
                double a =
                    pair.ReadA(selected[index]) -
                    centerA;

                double b =
                    pair.ReadB(selected[index]) -
                    centerB;

                maximumRadius =
                    Math.Max(
                        maximumRadius,
                        Math.Sqrt(a * a + b * b));
            }

            if (maximumRadius <
                MinimumSpread)
            {
                maximumRadius = 1.0;
            }

            for (int index = 0;
                 index < selected.Count;
                 index++)
            {
                PropulsionGraphNode node =
                    selected[index];

                EngineProjectionPoint point =
                    new EngineProjectionPoint
                    {
                        PartId = node.PartId,
                        DisplayName =
                            CreateEngineName(node),
                        ActivationStage =
                            node.ActivationStage,
                        SeparationStage =
                            node.SeparationStage,
                        NormalizedX =
                            (pair.ReadA(node) -
                             centerA) /
                            maximumRadius,
                        NormalizedY =
                            -(pair.ReadB(node) -
                              centerB) /
                            maximumRadius,
                        DisplayNumber =
                            index + 1
                    };

                ResolveCollision(
                    point,
                    result.Engines);

                result.Engines.Add(point);
            }

            return result;
        }

        private static int SelectStage(
            IList<PropulsionGraphNode> engines,
            int currentStage)
        {
            IEnumerable<int> stages =
                engines
                    .Where(
                        node =>
                            node.ActivationStage >= 0)
                    .Select(
                        node => node.ActivationStage)
                    .Distinct();

            int[] values =
                stages.ToArray();

            if (values.Length == 0)
            {
                return -1;
            }

            /*
             * Prefer the stage that is currently active or was most recently
             * activated. KSP's stage cursor may be one above the engine's
             * inverse stage immediately before activation.
             */
            return values
                .OrderBy(
                    stage =>
                        Math.Min(
                            Math.Abs(stage - currentStage),
                            Math.Abs(
                                stage -
                                (currentStage - 1))))
                .ThenByDescending(stage => stage)
                .First();
        }

        private static AxisPair ChooseAxisPair(
            IList<PropulsionGraphNode> engines)
        {
            AxisPair[] pairs =
            {
                AxisPair.XZ,
                AxisPair.XY,
                AxisPair.ZY
            };

            AxisPair best =
                pairs[0];

            double bestArea = -1.0;

            for (int index = 0;
                 index < pairs.Length;
                 index++)
            {
                AxisPair pair =
                    pairs[index];

                double spreadA =
                    Spread(
                        engines,
                        pair.ReadA);

                double spreadB =
                    Spread(
                        engines,
                        pair.ReadB);

                double area =
                    spreadA * spreadB;

                if (area > bestArea)
                {
                    bestArea = area;
                    best = pair;
                }
            }

            if (bestArea <
                MinimumSpread)
            {
                return AxisPair.Fallback;
            }

            return best;
        }

        private static double Spread(
            IList<PropulsionGraphNode> nodes,
            Func<PropulsionGraphNode, double> reader)
        {
            double minimum =
                double.MaxValue;

            double maximum =
                double.MinValue;

            for (int index = 0;
                 index < nodes.Count;
                 index++)
            {
                double value =
                    reader(nodes[index]);

                minimum =
                    Math.Min(
                        minimum,
                        value);

                maximum =
                    Math.Max(
                        maximum,
                        value);
            }

            return maximum - minimum;
        }

        private static void ResolveCollision(
            EngineProjectionPoint point,
            IList<EngineProjectionPoint> existing)
        {
            for (int attempt = 0;
                 attempt < 12;
                 attempt++)
            {
                bool collision = false;

                for (int index = 0;
                     index < existing.Count;
                     index++)
                {
                    double dx =
                        point.NormalizedX -
                        existing[index].NormalizedX;

                    double dy =
                        point.NormalizedY -
                        existing[index].NormalizedY;

                    if (Math.Sqrt(
                            dx * dx +
                            dy * dy) <
                        CollisionDistance)
                    {
                        collision = true;
                        break;
                    }
                }

                if (!collision)
                {
                    return;
                }

                double angle =
                    (point.DisplayNumber +
                     attempt) *
                    Math.PI *
                    0.61803398875;

                double radius =
                    CollisionDistance *
                    (1.0 +
                     attempt * 0.16);

                point.NormalizedX +=
                    Math.Cos(angle) *
                    radius;

                point.NormalizedY +=
                    Math.Sin(angle) *
                    radius;
            }
        }

        private static string CreateClusterName(
            IList<PropulsionGraphNode> engines)
        {
            if (engines.Count == 0)
            {
                return "NO ENGINES";
            }

            string name =
                CreateEngineName(
                    engines[0]);

            bool same =
                engines.All(
                    node =>
                        string.Equals(
                            CreateEngineName(node),
                            name,
                            StringComparison
                                .OrdinalIgnoreCase));

            return same
                ? engines.Count +
                  " × " +
                  name
                : engines.Count +
                  " ENGINE CLUSTER";
        }

        private static string CreateEngineName(
            PropulsionGraphNode node)
        {
            string title =
                node.Title ?? string.Empty;

            int quoteStart =
                title.IndexOf('"');

            if (quoteStart >= 0)
            {
                int quoteEnd =
                    title.IndexOf(
                        '"',
                        quoteStart + 1);

                if (quoteEnd >
                    quoteStart)
                {
                    return title.Substring(
                            quoteStart + 1,
                            quoteEnd -
                            quoteStart -
                            1)
                        .ToUpperInvariant();
                }
            }

            string[] words =
                title.Split(
                    new[] { ' ' },
                    StringSplitOptions
                        .RemoveEmptyEntries);

            return words.Length > 0
                ? words[0]
                    .ToUpperInvariant()
                : "ENGINE";
        }

        private sealed class AxisPair
        {
            public static readonly AxisPair XZ =
                new AxisPair(
                    node => node.VesselX,
                    node => node.VesselZ,
                    false);

            public static readonly AxisPair XY =
                new AxisPair(
                    node => node.VesselX,
                    node => node.VesselY,
                    false);

            public static readonly AxisPair ZY =
                new AxisPair(
                    node => node.VesselZ,
                    node => node.VesselY,
                    false);

            public static readonly AxisPair Fallback =
                new AxisPair(
                    node => node.VesselX,
                    node => node.VesselZ,
                    true);

            public AxisPair(
                Func<PropulsionGraphNode, double> readA,
                Func<PropulsionGraphNode, double> readB,
                bool isFallback)
            {
                ReadA = readA;
                ReadB = readB;
                IsFallback = isFallback;
            }

            public Func<PropulsionGraphNode, double>
                ReadA { get; private set; }

            public Func<PropulsionGraphNode, double>
                ReadB { get; private set; }

            public bool IsFallback { get; private set; }
        }
    }
}
