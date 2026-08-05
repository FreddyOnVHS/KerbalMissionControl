using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Projects every propulsion engine currently attached to the vessel.
    /// This is a physical hardware view, so it includes liquid engines and
    /// solid boosters from every stage until topology reports their removal.
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
                                VesselNodeCategory.Engine ||
                            node.Category ==
                                VesselNodeCategory.SolidBooster)
                    .OrderByDescending(
                        node => node.ActivationStage)
                    .ThenBy(
                        node => node.SeparationStage)
                    .ThenBy(
                        node => node.PartId)
                    .ToList();

            if (engines.Count == 0)
            {
                result.DisplayName = "NO ENGINES";
                result.ActivationStage = -1;
                result.SeparationStage = -1;
                return result;
            }

            result.ActivationStage =
                GetCommonStage(
                    engines,
                    node => node.ActivationStage);

            result.SeparationStage =
                GetCommonStage(
                    engines,
                    node => node.SeparationStage);

            result.DisplayName =
                CreateClusterName(
                    engines);

            AxisPair pair =
                ChooseAxisPair(
                    engines);

            result.UsedFallbackAxis =
                pair.IsFallback;

            double centerA =
                engines.Average(
                    node => pair.ReadA(node));

            double centerB =
                engines.Average(
                    node => pair.ReadB(node));

            double maximumRadius =
                0.0;

            for (int index = 0;
                 index < engines.Count;
                 index++)
            {
                double a =
                    pair.ReadA(engines[index]) -
                    centerA;

                double b =
                    pair.ReadB(engines[index]) -
                    centerB;

                maximumRadius =
                    Math.Max(
                        maximumRadius,
                        Math.Sqrt(
                            a * a +
                            b * b));
            }

            if (maximumRadius <
                MinimumSpread)
            {
                maximumRadius =
                    1.0;
            }

            for (int index = 0;
                 index < engines.Count;
                 index++)
            {
                PropulsionGraphNode node =
                    engines[index];

                EngineProjectionPoint point =
                    new EngineProjectionPoint
                    {
                        PartId =
                            node.PartId,

                        DisplayName =
                            CreateEngineName(
                                node),

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

                result.Engines.Add(
                    point);
            }

            return result;
        }

        private static int GetCommonStage(
            IList<PropulsionGraphNode> engines,
            Func<PropulsionGraphNode, int> selector)
        {
            int stage =
                selector(
                    engines[0]);

            for (int index = 1;
                 index < engines.Count;
                 index++)
            {
                if (selector(
                        engines[index]) !=
                    stage)
                {
                    return -1;
                }
            }

            return stage;
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

            double bestArea =
                -1.0;

            for (int index = 0;
                 index < pairs.Length;
                 index++)
            {
                AxisPair pair =
                    pairs[index];

                double area =
                    Spread(
                        engines,
                        pair.ReadA) *
                    Spread(
                        engines,
                        pair.ReadB);

                if (area >
                    bestArea)
                {
                    bestArea =
                        area;

                    best =
                        pair;
                }
            }

            return bestArea <
                MinimumSpread
                    ? AxisPair.Fallback
                    : best;
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
                    reader(
                        nodes[index]);

                minimum =
                    Math.Min(
                        minimum,
                        value);

                maximum =
                    Math.Max(
                        maximum,
                        value);
            }

            return maximum -
                minimum;
        }

        private static void ResolveCollision(
            EngineProjectionPoint point,
            IList<EngineProjectionPoint> existing)
        {
            for (int attempt = 0;
                 attempt < 12;
                 attempt++)
            {
                bool collision =
                    false;

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
                        collision =
                            true;

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
                node.Title ??
                string.Empty;

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

            if (words.Length > 0)
            {
                return words[0]
                    .ToUpperInvariant();
            }

            return node.Category ==
                VesselNodeCategory.SolidBooster
                    ? "BOOSTER"
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
                ReadA =
                    readA;

                ReadB =
                    readB;

                IsFallback =
                    isFallback;
            }

            public Func<PropulsionGraphNode, double>
                ReadA
                {
                    get;
                    private set;
                }

            public Func<PropulsionGraphNode, double>
                ReadB
                {
                    get;
                    private set;
                }

            public bool IsFallback
            {
                get;
                private set;
            }
        }
    }
}
