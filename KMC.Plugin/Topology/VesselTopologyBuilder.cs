using System;
using System.Collections.Generic;
using KMC.Shared.Topology;
using UnityEngine;

namespace KMC.Plugin.Topology
{
    internal sealed class VesselTopologyBuilder
    {
        public VesselTopology Build(
            Vessel vessel,
            long revision)
        {
            VesselTopology topology =
                new VesselTopology
                {
                    Revision =
                        revision
                };

            if (vessel == null)
            {
                return topology;
            }

            topology.VesselId =
                vessel.id.ToString();

            topology.VesselName =
                vessel.vesselName ??
                string.Empty;

            topology.CurrentStage =
                Math.Max(
                    0,
                    vessel.currentStage);

            IList<Part> parts =
                vessel.parts;

            if (parts == null)
            {
                return topology;
            }

            topology.PartCount =
                parts.Count;

            Dictionary<uint, VesselTopologyNode> nodes =
                new Dictionary<uint, VesselTopologyNode>();

            for (int index = 0;
                 index < parts.Count;
                 index++)
            {
                Part part =
                    parts[index];

                if (part == null)
                {
                    continue;
                }

                VesselTopologyNode node =
                    CreateNode(
                        vessel,
                        part);

                nodes[node.PartId] =
                    node;

                topology.Nodes.Add(
                    node);

                topology.MaximumInverseStage =
                    Math.Max(
                        topology.MaximumInverseStage,
                        node.InverseStage);
            }

            for (int index = 0;
                 index < parts.Count;
                 index++)
            {
                Part part =
                    parts[index];

                if (part == null)
                {
                    continue;
                }

                VesselTopologyNode node;

                if (!nodes.TryGetValue(
                        part.flightID,
                        out node))
                {
                    continue;
                }

                AddChildren(
                    part,
                    node,
                    nodes);

                AddSymmetryCounterparts(
                    part,
                    node,
                    nodes);
            }

            Part rootPart =
                vessel.rootPart;

            if (rootPart != null)
            {
                topology.RootPartId =
                    rootPart.flightID;

                topology.HasRootPart =
                    true;
            }
            else
            {
                FindRootFromNodes(
                    topology);
            }

            VesselTopologyAnalyzer.Analyze(
                topology);

            return topology;
        }

        private static VesselTopologyNode CreateNode(
            Vessel vessel,
            Part part)
        {
            VesselTopologyNode node =
                new VesselTopologyNode
                {
                    PartId =
                        part.flightID,

                    InverseStage =
                        Math.Max(
                            0,
                            part.inverseStage),

                    DryMassTonnes =
                        GetDryMassTonnes(
                            part),

                    ResourceMassTonnes =
                        GetResourceMassTonnes(
                            part),

                    AttachmentType =
                        GetAttachmentType(
                            part)
                };

            if (part.parent != null)
            {
                node.ParentPartId =
                    part.parent.flightID;

                node.HasParent =
                    true;
            }

            if (part.partInfo != null)
            {
                node.PartName =
                    part.partInfo.name ??
                    string.Empty;

                node.PartTitle =
                    part.partInfo.title ??
                    node.PartName;
            }
            else
            {
                node.PartName =
                    part.name ??
                    string.Empty;

                node.PartTitle =
                    node.PartName;
            }

            VesselPartClassifier.Classify(
                part,
                node);

            ReadVesselPosition(
                vessel,
                part,
                node);

            return node;
        }

        private static void AddChildren(
            Part part,
            VesselTopologyNode node,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            if (part.children == null)
            {
                return;
            }

            for (int index = 0;
                 index < part.children.Count;
                 index++)
            {
                Part child =
                    part.children[index];

                if (child == null ||
                    !nodes.ContainsKey(
                        child.flightID))
                {
                    continue;
                }

                AddUnique(
                    node.ChildPartIds,
                    child.flightID);
            }
        }

        private static void AddSymmetryCounterparts(
            Part part,
            VesselTopologyNode node,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            if (part.symmetryCounterparts == null)
            {
                return;
            }

            for (int index = 0;
                 index < part.symmetryCounterparts.Count;
                 index++)
            {
                Part counterpart =
                    part.symmetryCounterparts[index];

                if (counterpart == null ||
                    counterpart.flightID ==
                        part.flightID ||
                    !nodes.ContainsKey(
                        counterpart.flightID))
                {
                    continue;
                }

                AddUnique(
                    node.SymmetryPartIds,
                    counterpart.flightID);
            }
        }

        private static void ReadVesselPosition(
            Vessel vessel,
            Part part,
            VesselTopologyNode node)
        {
            if (vessel == null ||
                part == null ||
                node == null ||
                part.transform == null)
            {
                return;
            }

            try
            {
                Transform reference =
                    vessel.ReferenceTransform;

                Vector3 position =
                    reference != null
                        ? reference.InverseTransformPoint(
                            part.transform.position)
                        : part.transform.localPosition;

                node.VesselX =
                    position.x;

                node.VesselY =
                    position.y;

                node.VesselZ =
                    position.z;
            }
            catch
            {
            }
        }

        private static VesselAttachmentType GetAttachmentType(
            Part part)
        {
            if (part == null)
            {
                return VesselAttachmentType.Unknown;
            }

            if (part.parent == null)
            {
                return VesselAttachmentType.Root;
            }

            try
            {
                return part.attachMode ==
                    AttachModes.SRF_ATTACH
                    ? VesselAttachmentType.Surface
                    : VesselAttachmentType.Stack;
            }
            catch
            {
                return VesselAttachmentType.Unknown;
            }
        }

        private static double GetDryMassTonnes(
            Part part)
        {
            if (part == null)
            {
                return 0.0;
            }

            double dryMass =
                part.mass;

            if (part.Modules != null)
            {
                for (int index = 0;
                     index < part.Modules.Count;
                     index++)
                {
                    IPartMassModifier modifier =
                        part.Modules[index] as
                        IPartMassModifier;

                    if (modifier == null)
                    {
                        continue;
                    }

                    try
                    {
                        dryMass +=
                            modifier.GetModuleMass(
                                part.mass,
                                ModifierStagingSituation.CURRENT);
                    }
                    catch
                    {
                    }
                }
            }

            return SanitizeNonNegative(
                dryMass);
        }

        private static double GetResourceMassTonnes(
            Part part)
        {
            if (part == null ||
                part.Resources == null)
            {
                return 0.0;
            }

            double resourceMass =
                0.0;

            for (int index = 0;
                 index < part.Resources.Count;
                 index++)
            {
                PartResource resource =
                    part.Resources[index];

                if (resource == null ||
                    resource.info == null)
                {
                    continue;
                }

                double mass =
                    resource.amount *
                    resource.info.density;

                if (IsFinite(mass) &&
                    mass > 0.0)
                {
                    resourceMass +=
                        mass;
                }
            }

            return resourceMass;
        }

        private static void FindRootFromNodes(
            VesselTopology topology)
        {
            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (!node.HasParent)
                {
                    topology.RootPartId =
                        node.PartId;

                    topology.HasRootPart =
                        true;

                    return;
                }
            }
        }

        private static void AddUnique(
            IList<uint> values,
            uint value)
        {
            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (values[index] ==
                    value)
                {
                    return;
                }
            }

            values.Add(
                value);
        }

        private static double SanitizeNonNegative(
            double value)
        {
            return IsFinite(value)
                ? Math.Max(
                    0.0,
                    value)
                : 0.0;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
