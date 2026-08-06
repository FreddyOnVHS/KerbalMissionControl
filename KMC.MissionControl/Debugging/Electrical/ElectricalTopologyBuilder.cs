using System;
using System.Collections.Generic;
using System.Linq;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Debugging.Electrical
{
    public static class ElectricalTopologyBuilder
    {
        private sealed class SectionAccumulator
        {
            public SectionAccumulator(
                string key)
            {
                Key =
                    key;

                Nodes =
                    new List<
                        VesselTopologyNode>();

                Parts =
                    new List<
                        ElectricalPartModel>();
            }

            public string Key { get; private set; }

            public List<
                VesselTopologyNode> Nodes
            {
                get;
                private set;
            }

            public List<
                ElectricalPartModel> Parts
            {
                get;
                private set;
            }
        }

        public static ElectricalTopologyModel Build(
            VesselTopology topology)
        {
            ElectricalTopologyModel model =
                new ElectricalTopologyModel();

            if (topology == null)
            {
                model.Diagnostics.Add(
                    "No vessel topology has been received.");

                return model;
            }

            model.VesselName =
                topology.VesselName;

            model.TopologyRevision =
                topology.Revision;

            model.CurrentStage =
                topology.CurrentStage;

            Dictionary<
                uint,
                VesselTopologyNode> nodeById =
                    new Dictionary<
                        uint,
                        VesselTopologyNode>();

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node != null)
                {
                    nodeById[node.PartId] =
                        node;
                }
            }

            Dictionary<
                string,
                SectionAccumulator> sections =
                    new Dictionary<
                        string,
                        SectionAccumulator>(
                            StringComparer.Ordinal);

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                string sectionKey =
                    ResolveSectionKey(
                        topology,
                        node,
                        nodeById);

                SectionAccumulator accumulator;

                if (!sections.TryGetValue(
                        sectionKey,
                        out accumulator))
                {
                    accumulator =
                        new SectionAccumulator(
                            sectionKey);

                    sections.Add(
                        sectionKey,
                        accumulator);
                }

                ElectricalPartModel part =
                    CreatePartModel(
                        node,
                        sectionKey);

                accumulator.Nodes.Add(
                    node);

                accumulator.Parts.Add(
                    part);

                model.Parts.Add(
                    part);
            }

            List<
                ElectricalSectionModel> builtSections =
                    new List<
                        ElectricalSectionModel>();

            foreach (
                SectionAccumulator accumulator
                in sections.Values)
            {
                builtSections.Add(
                    BuildSection(
                        accumulator));
            }

            builtSections =
                builtSections
                    .OrderByDescending(
                        section =>
                            section.AverageY)
                    .ThenByDescending(
                        section =>
                            section.IsCommandSection)
                    .ThenBy(
                        section =>
                            section.Key,
                        StringComparer.Ordinal)
                    .ToList();

            int radialIndex =
                0;

            int coreIndex =
                0;

            for (int index = 0;
                 index < builtSections.Count;
                 index++)
            {
                ElectricalSectionModel section =
                    builtSections[index];

                section.DisplayOrder =
                    index;

                if (section.IsCommandSection)
                {
                    section.Name =
                        "COMMAND SECTION";
                }
                else if (section.IsRadialSection)
                {
                    radialIndex++;

                    section.Name =
                        "RADIAL GROUP " +
                        ToAlphabeticLabel(
                            radialIndex);
                }
                else
                {
                    coreIndex++;

                    section.Name =
                        "STACK SECTION " +
                        coreIndex.ToString("00");
                }

                model.Sections.Add(
                    section);
            }

            AddDiagnostics(
                model,
                topology);

            return model;
        }

        private static string ResolveSectionKey(
            VesselTopology topology,
            VesselTopologyNode node,
            Dictionary<
                uint,
                VesselTopologyNode> nodeById)
        {
            bool radial =
                IsRadialBranch(
                    topology,
                    node,
                    nodeById);

            uint branchRoot =
                node.BranchRootPartId;

            if (branchRoot == 0)
            {
                branchRoot =
                    topology.HasRootPart
                        ? topology.RootPartId
                        : node.PartId;
            }

            if (radial)
            {
                uint groupingId =
                    node.SymmetryGroupId != 0
                        ? node.SymmetryGroupId
                        : branchRoot;

                return
                    "RADIAL:" +
                    node.SeparationStage +
                    ":" +
                    groupingId;
            }

            return
                "CORE:" +
                node.SeparationStage;
        }

        private static bool IsRadialBranch(
            VesselTopology topology,
            VesselTopologyNode node,
            Dictionary<
                uint,
                VesselTopologyNode> nodeById)
        {
            if (node == null)
            {
                return false;
            }

            if (node.AttachmentType ==
                VesselAttachmentType.Surface)
            {
                return true;
            }

            uint branchRoot =
                node.BranchRootPartId;

            if (branchRoot == 0 ||
                !topology.HasRootPart ||
                branchRoot ==
                    topology.RootPartId)
            {
                return false;
            }

            VesselTopologyNode root;

            if (!nodeById.TryGetValue(
                    branchRoot,
                    out root) ||
                root == null)
            {
                return false;
            }

            return
                root.AttachmentType ==
                    VesselAttachmentType.Surface ||
                root.SymmetryGroupId != 0;
        }

        private static ElectricalPartModel
            CreatePartModel(
                VesselTopologyNode node,
                string sectionKey)
        {
            double electricAmount;
            double electricCapacity;

            ReadElectricCharge(
                node,
                out electricAmount,
                out electricCapacity);

            bool command =
                node.HasRole(
                    VesselNodeRole.Command);

            bool battery =
                node.HasRole(
                    VesselNodeRole.StoresElectricCharge) ||
                electricCapacity > 0.0001;

            bool solar =
                node.HasRole(
                    VesselNodeRole.SolarGeneration);

            bool generator =
                node.HasRole(
                    VesselNodeRole.ElectricalGeneration);

            bool fuelCell =
                node.HasRole(
                    VesselNodeRole.FuelCell);

            bool docking =
                node.HasRole(
                    VesselNodeRole.DockingPort);

            return new ElectricalPartModel
            {
                PartId =
                    node.PartId,

                ParentPartId =
                    node.ParentPartId,

                HasParent =
                    node.HasParent,

                SectionKey =
                    sectionKey,

                Title =
                    node.PartTitle,

                Roles =
                    node.Roles.ToString(),

                ElectricalRole =
                    BuildElectricalRole(
                        command,
                        battery,
                        solar,
                        generator,
                        fuelCell,
                        docking),

                ActivationStage =
                    node.ActivationStage,

                SeparationStage =
                    node.SeparationStage,

                StructuralDepth =
                    node.StructuralDepth,

                BranchRootPartId =
                    node.BranchRootPartId,

                SymmetryGroupId =
                    node.SymmetryGroupId,

                VesselX =
                    node.VesselX,

                VesselY =
                    node.VesselY,

                VesselZ =
                    node.VesselZ,

                ElectricChargeAmount =
                    electricAmount,

                ElectricChargeCapacity =
                    electricCapacity,

                IsElectricalPart =
                    command ||
                    battery ||
                    solar ||
                    generator ||
                    fuelCell ||
                    docking,

                IsCommand =
                    command,

                IsBattery =
                    battery,

                IsSolar =
                    solar,

                IsGenerator =
                    generator,

                IsFuelCell =
                    fuelCell,

                IsDockingPort =
                    docking
            };
        }

        private static ElectricalSectionModel
            BuildSection(
                SectionAccumulator accumulator)
        {
            ElectricalSectionModel section =
                new ElectricalSectionModel();

            section.Key =
                accumulator.Key;

            section.PartCount =
                accumulator.Nodes.Count;

            section.MinimumY =
                accumulator.Nodes.Count > 0
                    ? accumulator.Nodes.Min(
                        node =>
                            node.VesselY)
                    : 0.0;

            section.MaximumY =
                accumulator.Nodes.Count > 0
                    ? accumulator.Nodes.Max(
                        node =>
                            node.VesselY)
                    : 0.0;

            section.AverageY =
                accumulator.Nodes.Count > 0
                    ? accumulator.Nodes.Average(
                        node =>
                            node.VesselY)
                    : 0.0;

            section.SeparationStage =
                ResolveRepresentativeStage(
                    accumulator.Nodes,
                    true);

            section.ActivationStage =
                ResolveRepresentativeStage(
                    accumulator.Nodes,
                    false);

            section.IsRadialSection =
                accumulator.Key.StartsWith(
                    "RADIAL:",
                    StringComparison.Ordinal);

            for (int index = 0;
                 index < accumulator.Parts.Count;
                 index++)
            {
                ElectricalPartModel part =
                    accumulator.Parts[index];

                section.SourcePartIds.Add(
                    part.PartId);

                section.ElectricChargeAmount +=
                    part.ElectricChargeAmount;

                section.ElectricChargeCapacity +=
                    part.ElectricChargeCapacity;

                if (part.IsElectricalPart)
                {
                    section.ElectricalPartCount++;
                }

                if (part.IsCommand)
                {
                    section.CommandPartCount++;
                    section.IsCommandSection =
                        true;
                }

                if (part.IsBattery)
                {
                    section.BatteryPartCount++;
                }

                if (part.IsSolar)
                {
                    section.SolarPartCount++;
                }

                if (part.IsGenerator)
                {
                    section.GeneratorPartCount++;
                }

                if (part.IsFuelCell)
                {
                    section.FuelCellPartCount++;
                }

                if (part.IsDockingPort)
                {
                    section.DockingPortCount++;
                }

                if (section.BranchRootPartId == 0 &&
                    part.BranchRootPartId != 0)
                {
                    section.BranchRootPartId =
                        part.BranchRootPartId;
                }

                if (section.SymmetryGroupId == 0 &&
                    part.SymmetryGroupId != 0)
                {
                    section.SymmetryGroupId =
                        part.SymmetryGroupId;
                }
            }

            return section;
        }

        private static int ResolveRepresentativeStage(
            List<
                VesselTopologyNode> nodes,
            bool separation)
        {
            List<int> values =
                new List<int>();

            for (int index = 0;
                 index < nodes.Count;
                 index++)
            {
                int value =
                    separation
                        ? nodes[index]
                            .SeparationStage
                        : nodes[index]
                            .ActivationStage;

                if (value >= 0)
                {
                    values.Add(
                        value);
                }
            }

            if (values.Count == 0)
            {
                return -1;
            }

            return
                values
                    .GroupBy(
                        value =>
                            value)
                    .OrderByDescending(
                        group =>
                            group.Count())
                    .ThenByDescending(
                        group =>
                            group.Key)
                    .First()
                    .Key;
        }

        private static void ReadElectricCharge(
            VesselTopologyNode node,
            out double amount,
            out double capacity)
        {
            amount =
                0.0;

            capacity =
                0.0;

            if (node == null)
            {
                return;
            }

            for (int index = 0;
                 index < node.Resources.Count;
                 index++)
            {
                VesselResourceState resource =
                    node.Resources[index];

                if (resource != null &&
                    string.Equals(
                        resource.Name,
                        "ElectricCharge",
                        StringComparison.OrdinalIgnoreCase))
                {
                    amount +=
                        Math.Max(
                            0.0,
                            resource.Amount);

                    capacity +=
                        Math.Max(
                            0.0,
                            resource.Capacity);
                }
            }
        }

        private static string BuildElectricalRole(
            bool command,
            bool battery,
            bool solar,
            bool generator,
            bool fuelCell,
            bool docking)
        {
            List<string> roles =
                new List<string>();

            if (command)
            {
                roles.Add(
                    "COMMAND");
            }

            if (battery)
            {
                roles.Add(
                    "BATTERY");
            }

            if (solar)
            {
                roles.Add(
                    "SOLAR");
            }

            if (generator)
            {
                roles.Add(
                    "GENERATOR");
            }

            if (fuelCell)
            {
                roles.Add(
                    "FUEL CELL");
            }

            if (docking)
            {
                roles.Add(
                    "DOCKING");
            }

            return
                roles.Count > 0
                    ? string.Join(
                        ", ",
                        roles.ToArray())
                    : "--";
        }

        private static void AddDiagnostics(
            ElectricalTopologyModel model,
            VesselTopology topology)
        {
            model.Diagnostics.Add(
                "Grouping is a first-pass candidate model.");

            model.Diagnostics.Add(
                "Core sections are grouped by SeparationStage.");

            model.Diagnostics.Add(
                "Surface/radial branches are grouped by SeparationStage and symmetry/branch root.");

            model.Diagnostics.Add(
                "Vertical ordering uses average VesselY.");

            model.Diagnostics.Add(
                "Solar deployment and generation output are not yet present in the topology stream.");

            if (model.Sections.Count == 0)
            {
                model.Diagnostics.Add(
                    "No sections were generated.");
            }

            int commandSections =
                model.Sections.Count(
                    section =>
                        section.IsCommandSection);

            if (commandSections == 0)
            {
                model.Diagnostics.Add(
                    "No command section was detected.");
            }
            else if (commandSections > 1)
            {
                model.Diagnostics.Add(
                    "Multiple command sections detected: " +
                    commandSections);
            }

            int electricalParts =
                model.Parts.Count(
                    part =>
                        part.IsElectricalPart);

            model.Diagnostics.Add(
                "Topology parts: " +
                topology.Nodes.Count);

            model.Diagnostics.Add(
                "Generated sections: " +
                model.Sections.Count);

            model.Diagnostics.Add(
                "Electrical parts: " +
                electricalParts);
        }

        private static string ToAlphabeticLabel(
            int index)
        {
            index =
                Math.Max(
                    1,
                    index);

            string result =
                string.Empty;

            while (index > 0)
            {
                index--;

                result =
                    (char)(
                        'A' +
                        index %
                        26) +
                    result;

                index /=
                    26;
            }

            return result;
        }
    }
}
