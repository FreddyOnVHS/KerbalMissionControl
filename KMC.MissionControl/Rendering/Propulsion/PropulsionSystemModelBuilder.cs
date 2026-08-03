using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class PropulsionSystemModelBuilder
    {
        public PropulsionSystemModel Build(
            PropulsionRenderGraph graph)
        {
            PropulsionSystemModel model =
                new PropulsionSystemModel();

            if (graph == null)
            {
                return model;
            }

            model.VesselName =
                graph.VesselName ?? string.Empty;

            model.Revision =
                graph.TopologyRevision;

            model.CurrentStage =
                graph.CurrentStage;

            Dictionary<string, PropulsionEngineGroup>
                engineGroups =
                    new Dictionary<string, PropulsionEngineGroup>(
                        StringComparer.OrdinalIgnoreCase);

            HashSet<int> separationStages =
                new HashSet<int>();

            for (int index = 0;
                 index < graph.Nodes.Count;
                 index++)
            {
                PropulsionGraphNode node =
                    graph.Nodes[index];

                CountCategory(
                    model,
                    node);

                ReadResources(
                    model,
                    node);

                if (node.IsSeparationBoundary &&
                    node.SeparationStage >= 0)
                {
                    separationStages.Add(
                        node.SeparationStage);
                }

                if (node.Category ==
                    VesselNodeCategory.Engine)
                {
                    string name =
                        CreateEngineName(node);

                    string key =
                        name +
                        "|" +
                        node.ActivationStage +
                        "|" +
                        node.SeparationStage;

                    PropulsionEngineGroup group;

                    if (!engineGroups.TryGetValue(
                            key,
                            out group))
                    {
                        group =
                            new PropulsionEngineGroup
                            {
                                DisplayName = name,
                                ActivationStage =
                                    node.ActivationStage,
                                SeparationStage =
                                    node.SeparationStage
                            };

                        engineGroups.Add(
                            key,
                            group);
                    }

                    group.Count++;
                }
            }

            foreach (PropulsionEngineGroup group
                in engineGroups.Values
                    .OrderByDescending(
                        item => item.ActivationStage)
                    .ThenBy(
                        item => item.DisplayName))
            {
                model.EngineGroups.Add(group);
            }

            foreach (int stage
                in separationStages
                    .OrderByDescending(
                        value => value))
            {
                model.SeparationStages.Add(stage);
            }

            return model;
        }

        private static void CountCategory(
            PropulsionSystemModel model,
            PropulsionGraphNode node)
        {
            switch (node.Category)
            {
                case VesselNodeCategory.Command:
                    model.CommandCount++;
                    break;

                case VesselNodeCategory.Payload:
                    model.PayloadCount++;
                    break;

                case VesselNodeCategory.RcsThruster:
                    model.RcsThrusterCount++;
                    break;

                case VesselNodeCategory.Battery:
                    model.BatteryCount++;
                    break;

                case VesselNodeCategory.Generator:
                case VesselNodeCategory.SolarPanel:
                    model.PowerSourceCount++;
                    break;

                case VesselNodeCategory.DockingPort:
                    model.DockingPortCount++;
                    break;
            }
        }

        private static void ReadResources(
            PropulsionSystemModel model,
            PropulsionGraphNode node)
        {
            for (int index = 0;
                 index < node.ResourceNames.Count;
                 index++)
            {
                string resource =
                    node.ResourceNames[index];

                if (EqualsName(
                        resource,
                        "LiquidFuel"))
                {
                    model.HasLiquidFuel = true;
                }
                else if (EqualsName(
                             resource,
                             "Oxidizer"))
                {
                    model.HasOxidizer = true;
                }
                else if (EqualsName(
                             resource,
                             "MonoPropellant"))
                {
                    model.HasMonopropellant = true;
                }
                else if (EqualsName(
                             resource,
                             "SolidFuel"))
                {
                    model.HasSolidFuel = true;
                }
            }
        }

        private static string CreateEngineName(
            PropulsionGraphNode node)
        {
            string title =
                node.Title ?? string.Empty;

            if (title.Length == 0)
            {
                return "ENGINE";
            }

            int quoteStart =
                title.IndexOf('"');

            if (quoteStart >= 0)
            {
                int quoteEnd =
                    title.IndexOf(
                        '"',
                        quoteStart + 1);

                if (quoteEnd > quoteStart)
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

            return "ENGINE";
        }

        private static bool EqualsName(
            string left,
            string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
