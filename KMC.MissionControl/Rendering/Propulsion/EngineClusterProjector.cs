using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Projects the propulsion engines relevant to the current propulsion
    /// phase using a stable engine-bell view and the live telemetry stage.
    /// Future-stage, separated, and older propulsion groups are hidden.
    /// </summary>
    public sealed class EngineClusterProjector
    {
        private const double MinimumSpread = 0.0001;

        public EngineClusterProjection Build(
            PropulsionRenderGraph graph)
        {
            return Build(
                graph,
                graph != null
                    ? graph.CurrentStage
                    : -1);
        }

        public EngineClusterProjection Build(
            PropulsionRenderGraph graph,
            int liveCurrentStage)
        {
            EngineClusterProjection result =
                new EngineClusterProjection();

            if (graph == null)
            {
                return result;
            }

            /*
             * Topology can remain stale briefly after a staging event. Use
             * the live stage to reject engines whose separation event has
             * already occurred before selecting a propulsion stage.
             */
            List<PropulsionGraphNode> allEngines =
                graph.Nodes
                    .Where(
                        node =>
                            (node.Category ==
                                VesselNodeCategory.Engine ||
                             node.Category ==
                                VesselNodeCategory.SolidBooster) &&
                            !HasSeparated(
                                node,
                                liveCurrentStage))
                    .ToList();

            int selectedStage =
                SelectRelevantActivationStage(
                    allEngines,
                    liveCurrentStage);

            List<PropulsionGraphNode> engines =
                selectedStage >= 0
                    ? allEngines
                        .Where(
                            node =>
                                node.ActivationStage ==
                                selectedStage)
                        .OrderBy(
                            node => node.SeparationStage)
                        .ThenBy(
                            node => node.PartId)
                        .ToList()
                    : new List<PropulsionGraphNode>();

            if (engines.Count == 0)
            {
                result.DisplayName =
                    "NO CURRENT STAGE ENGINES";

                result.ActivationStage =
                    selectedStage;

                result.SeparationStage =
                    -1;

                return result;
            }

            result.ActivationStage =
                selectedStage;

            result.SeparationStage =
                GetCommonStage(
                    engines,
                    node => node.SeparationStage);

            result.DisplayName =
                CreateClusterName(
                    engines);

            /*
             * VesselTopologyBuilder stores positions in
             * vessel.ReferenceTransform coordinates.
             *
             * Local Y is the vehicle longitudinal nose-to-tail axis.
             * The engine-bell view must therefore project onto local X/Z.
             * Including Y makes engines at different heights appear radially
             * displaced, which is what placed the center engine below the
             * two boosters in the previous display.
             */
            AxisPair pair =
                AxisPair.XZ;

            result.UsedFallbackAxis =
                false;

            List<EngineLayoutPoint> layout =
                BuildRadialLayout(
                    engines,
                    pair);

            for (int index = 0;
                 index < layout.Count;
                 index++)
            {
                EngineLayoutPoint item =
                    layout[index];

                PropulsionGraphNode node =
                    item.Node;

                result.Engines.Add(
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
                            item.NormalizedX,

                        NormalizedY =
                            item.NormalizedY,

                        DisplayNumber =
                            index + 1
                    });
            }

            return result;
        }

        private static List<EngineLayoutPoint> BuildRadialLayout(
            IList<PropulsionGraphNode> engines,
            AxisPair pair)
        {
            List<EngineLayoutPoint> points =
                new List<EngineLayoutPoint>();

            /*
             * Center the projection on the current engine cluster itself.
             * Vessel ReferenceTransform origin is not guaranteed to pass
             * through the propulsion cluster, especially on unusual craft.
             */
            double centerX =
                engines.Average(
                    node =>
                        pair.ReadA(node));

            double centerY =
                engines.Average(
                    node =>
                        -pair.ReadB(node));

            double maximumRadius =
                0.0;

            for (int index = 0;
                 index < engines.Count;
                 index++)
            {
                double x =
                    pair.ReadA(
                        engines[index]) -
                    centerX;

                double y =
                    -pair.ReadB(
                        engines[index]) -
                    centerY;

                double radius =
                    Math.Sqrt(
                        x * x +
                        y * y);

                maximumRadius =
                    Math.Max(
                        maximumRadius,
                        radius);

                points.Add(
                    new EngineLayoutPoint
                    {
                        Node =
                            engines[index],

                        RawRadius =
                            radius,

                        Angle =
                            NormalizeAngle(
                                Math.Atan2(
                                    y,
                                    x))
                    });
            }

            if (maximumRadius <
                MinimumSpread)
            {
                ArrangeCentralGroup(
                    points);

                return points
                    .OrderBy(
                        item => item.Angle)
                    .ThenBy(
                        item => item.Node.PartId)
                    .ToList();
            }

            double centerThreshold =
                Math.Max(
                    0.05,
                    maximumRadius *
                    0.12);

            List<EngineLayoutPoint> central =
                points
                    .Where(
                        item =>
                            item.RawRadius <=
                            centerThreshold)
                    .OrderBy(
                        item => item.Node.PartId)
                    .ToList();

            List<EngineLayoutPoint> radial =
                points
                    .Where(
                        item =>
                            item.RawRadius >
                            centerThreshold)
                    .OrderBy(
                        item => item.RawRadius)
                    .ThenBy(
                        item => item.Angle)
                    .ToList();

            ArrangeCentralGroup(
                central);

            List<List<EngineLayoutPoint>> rings =
                BuildRadiusGroups(
                    radial,
                    maximumRadius);

            for (int ringIndex = 0;
                 ringIndex < rings.Count;
                 ringIndex++)
            {
                List<EngineLayoutPoint> ring =
                    rings[ringIndex]
                        .OrderBy(
                            item => item.Angle)
                        .ThenBy(
                            item => item.Node.PartId)
                        .ToList();

                double displayRadius =
                    rings.Count == 1
                        ? 0.78
                        : 0.48 +
                          0.42 *
                          ringIndex /
                          Math.Max(
                              1,
                              rings.Count - 1);

                ArrangePerfectRing(
                    ring,
                    displayRadius);

                rings[ringIndex] =
                    ring;
            }

            List<EngineLayoutPoint> result =
                new List<EngineLayoutPoint>();

            result.AddRange(
                central
                    .OrderBy(
                        item => item.Angle)
                    .ThenBy(
                        item => item.Node.PartId));

            for (int ringIndex = 0;
                 ringIndex < rings.Count;
                 ringIndex++)
            {
                result.AddRange(
                    rings[ringIndex]);
            }

            return result;
        }

        /// <summary>
        /// Snaps a physical engine ring to exact equal angular spacing while
        /// retaining the ring's overall vehicle-relative rotation.
        /// </summary>
        private static void ArrangePerfectRing(
            IList<EngineLayoutPoint> ring,
            double displayRadius)
        {
            if (ring == null ||
                ring.Count == 0)
            {
                return;
            }

            if (ring.Count == 1)
            {
                ring[0].NormalizedX =
                    Math.Cos(
                        ring[0].Angle) *
                    displayRadius;

                ring[0].NormalizedY =
                    Math.Sin(
                        ring[0].Angle) *
                    displayRadius;

                return;
            }

            /*
             * Find the circular mean of each engine's actual angle minus its
             * ideal equally spaced angle. This supplies a stable orientation
             * offset without allowing construction tolerances to distort the
             * displayed ring.
             */
            double sine =
                0.0;

            double cosine =
                0.0;

            for (int index = 0;
                 index < ring.Count;
                 index++)
            {
                double idealAngle =
                    Math.PI *
                    2.0 *
                    index /
                    ring.Count;

                double offset =
                    NormalizeAngle(
                        ring[index].Angle -
                        idealAngle);

                sine +=
                    Math.Sin(
                        offset);

                cosine +=
                    Math.Cos(
                        offset);
            }

            double orientation =
                Math.Atan2(
                    sine,
                    cosine);

            for (int index = 0;
                 index < ring.Count;
                 index++)
            {
                double angle =
                    NormalizeAngle(
                        orientation +
                        Math.PI *
                        2.0 *
                        index /
                        ring.Count);

                ring[index].Angle =
                    angle;

                ring[index].NormalizedX =
                    Math.Cos(
                        angle) *
                    displayRadius;

                ring[index].NormalizedY =
                    Math.Sin(
                        angle) *
                    displayRadius;
            }
        }

        private static double NormalizeAngle(
            double angle)
        {
            while (angle <=
                   -Math.PI)
            {
                angle +=
                    Math.PI *
                    2.0;
            }

            while (angle >
                   Math.PI)
            {
                angle -=
                    Math.PI *
                    2.0;
            }

            return angle;
        }

        private static List<List<EngineLayoutPoint>>
            BuildRadiusGroups(
                IList<EngineLayoutPoint> radial,
                double maximumRadius)
        {
            List<List<EngineLayoutPoint>> groups =
                new List<List<EngineLayoutPoint>>();

            double tolerance =
                Math.Max(
                    0.04,
                    maximumRadius *
                    0.10);

            for (int index = 0;
                 index < radial.Count;
                 index++)
            {
                EngineLayoutPoint item =
                    radial[index];

                if (groups.Count == 0)
                {
                    groups.Add(
                        new List<EngineLayoutPoint>());
                }
                else
                {
                    List<EngineLayoutPoint> current =
                        groups[
                            groups.Count - 1];

                    double averageRadius =
                        current.Average(
                            existing =>
                                existing.RawRadius);

                    if (Math.Abs(
                            item.RawRadius -
                            averageRadius) >
                        tolerance)
                    {
                        groups.Add(
                            new List<EngineLayoutPoint>());
                    }
                }

                groups[
                    groups.Count - 1]
                    .Add(
                        item);
            }

            return groups;
        }

        private static void ArrangeCentralGroup(
            IList<EngineLayoutPoint> central)
        {
            if (central == null ||
                central.Count == 0)
            {
                return;
            }

            if (central.Count == 1)
            {
                central[0].NormalizedX =
                    0.0;

                central[0].NormalizedY =
                    0.0;

                central[0].Angle =
                    0.0;

                return;
            }

            double radius =
                central.Count <= 4
                    ? 0.16
                    : 0.23;

            for (int index = 0;
                 index < central.Count;
                 index++)
            {
                double angle =
                    -Math.PI / 2.0 +
                    Math.PI *
                    2.0 *
                    index /
                    central.Count;

                central[index].Angle =
                    angle;

                central[index].NormalizedX =
                    Math.Cos(
                        angle) *
                    radius;

                central[index].NormalizedY =
                    Math.Sin(
                        angle) *
                    radius;
            }
        }

        private sealed class EngineLayoutPoint
        {
            public PropulsionGraphNode Node;
            public double RawRadius;
            public double Angle;
            public double NormalizedX;
            public double NormalizedY;
        }

        /// <summary>
        /// Returns true once the live staging sequence has reached an
        /// engine's separation stage.
        ///
        /// KSP stage numbers decrease as staging progresses. An engine with
        /// SeparationStage 1 is attached while CurrentStage is 2, but it must
        /// be treated as detached as soon as CurrentStage becomes 1—even if
        /// the topology packet still contains the old part briefly.
        /// </summary>
        private static bool HasSeparated(
            PropulsionGraphNode node,
            int liveCurrentStage)
        {
            if (node == null ||
                node.SeparationStage < 0 ||
                liveCurrentStage < 0)
            {
                return false;
            }

            return liveCurrentStage <=
                node.SeparationStage;
        }

        /// <summary>
        /// Selects the current propulsion stage.
        ///
        /// KSP stage numbers decrease as staging progresses:
        /// - Attached engines at or above CurrentStage have already activated.
        /// - Engines immediately below CurrentStage are armed for the next
        ///   staging event.
        ///
        /// The newest activated attached propulsion stage is preferred. If no
        /// attached propulsion stage has activated yet, the nearest armed
        /// propulsion stage is returned so the cluster is visible before
        /// ignition.
        /// </summary>
        private static int SelectRelevantActivationStage(
            IList<PropulsionGraphNode> engines,
            int currentStage)
        {
            int activatedStage =
                int.MaxValue;

            int armedStage =
                int.MinValue;

            for (int index = 0;
                 index < engines.Count;
                 index++)
            {
                int activationStage =
                    engines[index]
                        .ActivationStage;

                if (activationStage < 0)
                {
                    continue;
                }

                if (activationStage >=
                    currentStage)
                {
                    if (activationStage <
                        activatedStage)
                    {
                        activatedStage =
                            activationStage;
                    }
                }
                else if (activationStage >
                         armedStage)
                {
                    /*
                     * Highest stage number below CurrentStage is the next
                     * propulsion stage that will activate.
                     */
                    armedStage =
                        activationStage;
                }
            }

            if (activatedStage !=
                int.MaxValue)
            {
                return activatedStage;
            }

            return armedStage !=
                int.MinValue
                    ? armedStage
                    : -1;
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
