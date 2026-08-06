using System;
using System.IO;
using System.Text;

namespace KMC.Shared.Topology
{
    public static class VesselTopologyPacketCodec
    {
        public const int TopologyPort = 5082;
        public const int CurrentVersion = 2;

        private const int Magic = 0x4B4D4354;
        private const int MinimumSupportedVersion = 1;

        public static byte[] Encode(
            VesselTopology topology)
        {
            if (topology == null)
            {
                throw new ArgumentNullException(
                    nameof(topology));
            }

            using (MemoryStream stream =
                new MemoryStream())
            using (BinaryWriter writer =
                new BinaryWriter(
                    stream,
                    Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(CurrentVersion);

                WriteString(writer, topology.VesselId);
                WriteString(writer, topology.VesselName);
                writer.Write(topology.RootPartId);
                writer.Write(topology.HasRootPart);
                writer.Write(topology.PartCount);
                writer.Write(topology.MaximumInverseStage);
                writer.Write(topology.CurrentStage);
                writer.Write(topology.StructuralBranchCount);
                writer.Write(topology.SymmetryGroupCount);
                writer.Write(topology.SeparationBoundaryCount);
                writer.Write(topology.Revision);
                writer.Write(topology.Nodes.Count);

                for (int index = 0;
                     index < topology.Nodes.Count;
                     index++)
                {
                    WriteNode(
                        writer,
                        topology.Nodes[index]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryDecode(
            byte[] data,
            out VesselTopology topology)
        {
            topology = null;

            if (data == null ||
                data.Length < 12)
            {
                return false;
            }

            try
            {
                using (MemoryStream stream =
                    new MemoryStream(data, false))
                using (BinaryReader reader =
                    new BinaryReader(
                        stream,
                        Encoding.UTF8))
                {
                    if (reader.ReadInt32() != Magic)
                    {
                        return false;
                    }

                    int version =
                        reader.ReadInt32();

                    if (version < MinimumSupportedVersion ||
                        version > CurrentVersion)
                    {
                        return false;
                    }

                    VesselTopology result =
                        new VesselTopology
                        {
                            TransportVersion = version,
                            VesselId = ReadString(reader),
                            VesselName = ReadString(reader),
                            RootPartId = reader.ReadUInt32(),
                            HasRootPart = reader.ReadBoolean(),
                            PartCount = reader.ReadInt32(),
                            MaximumInverseStage = reader.ReadInt32(),
                            CurrentStage = reader.ReadInt32(),
                            StructuralBranchCount = reader.ReadInt32(),
                            SymmetryGroupCount = reader.ReadInt32(),
                            SeparationBoundaryCount = reader.ReadInt32(),
                            Revision = reader.ReadInt64()
                        };

                    int nodeCount =
                        ReadCount(reader, 10000);

                    for (int index = 0;
                         index < nodeCount;
                         index++)
                    {
                        result.Nodes.Add(
                            ReadNode(
                                reader,
                                version));
                    }

                    topology = result;
                    return true;
                }
            }
            catch
            {
                topology = null;
                return false;
            }
        }

        private static void WriteNode(
            BinaryWriter writer,
            VesselTopologyNode node)
        {
            writer.Write(node.PartId);
            writer.Write(node.ParentPartId);
            writer.Write(node.HasParent);
            WriteString(writer, node.PartName);
            WriteString(writer, node.PartTitle);
            writer.Write(node.InverseStage);
            writer.Write((int)node.AttachmentType);
            writer.Write((int)node.Category);
            writer.Write((int)node.Roles);
            writer.Write(node.DryMassTonnes);
            writer.Write(node.ResourceMassTonnes);
            writer.Write(node.VesselX);
            writer.Write(node.VesselY);
            writer.Write(node.VesselZ);
            writer.Write(node.StructuralDepth);
            writer.Write(node.SymmetryGroupId);
            writer.Write(node.BranchRootPartId);
            writer.Write(node.ActivationStage);
            writer.Write(node.SeparationStage);
            writer.Write(node.IsSeparationBoundary);
            writer.Write(node.WillSeparateOnNextStage);
            writer.Write(node.AllowsCrossFeed);

            WriteUIntList(writer, node.ChildPartIds);
            WriteUIntList(writer, node.StackChildPartIds);
            WriteUIntList(writer, node.SurfaceChildPartIds);
            WriteUIntList(writer, node.SymmetryPartIds);
            WriteStringList(writer, node.StoredResourceNames);

            writer.Write(node.Resources.Count);

            for (int index = 0;
                 index < node.Resources.Count;
                 index++)
            {
                VesselResourceState resource =
                    node.Resources[index];

                writer.Write(resource.ResourceId);
                WriteString(writer, resource.Name);
                writer.Write(resource.Amount);
                writer.Write(resource.Capacity);
                writer.Write(resource.DensityTonnesPerUnit);
                writer.Write(resource.FlowEnabled);
            }

            writer.Write(
                node.PropellantRequirements.Count);

            for (int index = 0;
                 index < node.PropellantRequirements.Count;
                 index++)
            {
                VesselPropellantRequirement requirement =
                    node.PropellantRequirements[index];

                writer.Write(requirement.ResourceId);
                WriteString(writer, requirement.Name);
                writer.Write(requirement.Ratio);
                writer.Write(requirement.DensityTonnesPerUnit);
                WriteString(writer, requirement.RawFlowMode);
                WriteUIntList(
                    writer,
                    requirement.ReachableSourcePartIds);
            }

            writer.Write(node.Modules.Count);

            for (int index = 0;
                 index < node.Modules.Count;
                 index++)
            {
                WriteModule(
                    writer,
                    node.Modules[index]);
            }
        }

        private static VesselTopologyNode ReadNode(
            BinaryReader reader,
            int version)
        {
            VesselTopologyNode node =
                new VesselTopologyNode
                {
                    PartId = reader.ReadUInt32(),
                    ParentPartId = reader.ReadUInt32(),
                    HasParent = reader.ReadBoolean(),
                    PartName = ReadString(reader),
                    PartTitle = ReadString(reader),
                    InverseStage = reader.ReadInt32(),
                    AttachmentType =
                        (VesselAttachmentType)
                        reader.ReadInt32(),
                    Category =
                        (VesselNodeCategory)
                        reader.ReadInt32(),
                    Roles =
                        (VesselNodeRole)
                        reader.ReadInt32(),
                    DryMassTonnes = reader.ReadDouble(),
                    ResourceMassTonnes = reader.ReadDouble(),
                    VesselX = reader.ReadDouble(),
                    VesselY = reader.ReadDouble(),
                    VesselZ = reader.ReadDouble(),
                    StructuralDepth = reader.ReadInt32(),
                    SymmetryGroupId = reader.ReadUInt32(),
                    BranchRootPartId = reader.ReadUInt32(),
                    ActivationStage = reader.ReadInt32(),
                    SeparationStage = reader.ReadInt32(),
                    IsSeparationBoundary = reader.ReadBoolean(),
                    WillSeparateOnNextStage = reader.ReadBoolean(),
                    AllowsCrossFeed = reader.ReadBoolean()
                };

            ReadUIntList(reader, node.ChildPartIds);
            ReadUIntList(reader, node.StackChildPartIds);
            ReadUIntList(reader, node.SurfaceChildPartIds);
            ReadUIntList(reader, node.SymmetryPartIds);
            ReadStringList(reader, node.StoredResourceNames);

            int resourceCount =
                ReadCount(reader, 1000);

            for (int index = 0;
                 index < resourceCount;
                 index++)
            {
                node.Resources.Add(
                    new VesselResourceState
                    {
                        ResourceId = reader.ReadInt32(),
                        Name = ReadString(reader),
                        Amount = reader.ReadDouble(),
                        Capacity = reader.ReadDouble(),
                        DensityTonnesPerUnit = reader.ReadDouble(),
                        FlowEnabled = reader.ReadBoolean()
                    });
            }

            int requirementCount =
                ReadCount(reader, 1000);

            for (int index = 0;
                 index < requirementCount;
                 index++)
            {
                VesselPropellantRequirement requirement =
                    new VesselPropellantRequirement
                    {
                        ResourceId = reader.ReadInt32(),
                        Name = ReadString(reader),
                        Ratio = reader.ReadDouble(),
                        DensityTonnesPerUnit = reader.ReadDouble(),
                        RawFlowMode = ReadString(reader)
                    };

                ReadUIntList(
                    reader,
                    requirement.ReachableSourcePartIds);

                node.PropellantRequirements.Add(
                    requirement);
            }

            if (version >= 2)
            {
                int moduleCount =
                    ReadCount(reader, 1000);

                for (int index = 0;
                     index < moduleCount;
                     index++)
                {
                    node.Modules.Add(
                        ReadModule(reader));
                }
            }

            return node;
        }

        private static void WriteModule(
            BinaryWriter writer,
            VesselModuleDescriptor module)
        {
            WriteString(writer, module.ModuleName);
            WriteString(writer, module.ModuleTypeName);
            WriteString(writer, module.DisplayName);
            writer.Write(module.IsEnabled);
            writer.Write(module.HasActiveState);
            writer.Write(module.IsActive);
            WriteString(writer, module.StatusText);
            WriteModuleResources(writer, module.InputResources);
            WriteModuleResources(writer, module.OutputResources);
        }

        private static VesselModuleDescriptor ReadModule(
            BinaryReader reader)
        {
            VesselModuleDescriptor module =
                new VesselModuleDescriptor
                {
                    ModuleName = ReadString(reader),
                    ModuleTypeName = ReadString(reader),
                    DisplayName = ReadString(reader),
                    IsEnabled = reader.ReadBoolean(),
                    HasActiveState = reader.ReadBoolean(),
                    IsActive = reader.ReadBoolean(),
                    StatusText = ReadString(reader)
                };

            ReadModuleResources(
                reader,
                module.InputResources);

            ReadModuleResources(
                reader,
                module.OutputResources);

            return module;
        }

        private static void WriteModuleResources(
            BinaryWriter writer,
            System.Collections.Generic.IList<VesselModuleResource> values)
        {
            writer.Write(
                values != null
                    ? values.Count
                    : 0);

            if (values == null)
            {
                return;
            }

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                WriteString(
                    writer,
                    values[index].Name);

                writer.Write(
                    values[index].Ratio);
            }
        }

        private static void ReadModuleResources(
            BinaryReader reader,
            System.Collections.Generic.IList<VesselModuleResource> values)
        {
            int count =
                ReadCount(reader, 1000);

            for (int index = 0;
                 index < count;
                 index++)
            {
                values.Add(
                    new VesselModuleResource
                    {
                        Name = ReadString(reader),
                        Ratio = reader.ReadDouble()
                    });
            }
        }

        private static void WriteUIntList(
            BinaryWriter writer,
            System.Collections.Generic.IList<uint> values)
        {
            writer.Write(
                values != null
                    ? values.Count
                    : 0);

            if (values == null)
            {
                return;
            }

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                writer.Write(values[index]);
            }
        }

        private static void ReadUIntList(
            BinaryReader reader,
            System.Collections.Generic.IList<uint> values)
        {
            int count =
                ReadCount(reader, 10000);

            for (int index = 0;
                 index < count;
                 index++)
            {
                values.Add(reader.ReadUInt32());
            }
        }

        private static void WriteStringList(
            BinaryWriter writer,
            System.Collections.Generic.IList<string> values)
        {
            writer.Write(
                values != null
                    ? values.Count
                    : 0);

            if (values == null)
            {
                return;
            }

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                WriteString(writer, values[index]);
            }
        }

        private static void ReadStringList(
            BinaryReader reader,
            System.Collections.Generic.IList<string> values)
        {
            int count =
                ReadCount(reader, 10000);

            for (int index = 0;
                 index < count;
                 index++)
            {
                values.Add(ReadString(reader));
            }
        }

        private static void WriteString(
            BinaryWriter writer,
            string value)
        {
            writer.Write(
                value ??
                string.Empty);
        }

        private static string ReadString(
            BinaryReader reader)
        {
            return reader.ReadString() ??
                string.Empty;
        }

        private static int ReadCount(
            BinaryReader reader,
            int maximum)
        {
            int count =
                reader.ReadInt32();

            if (count < 0 ||
                count > maximum)
            {
                throw new InvalidDataException(
                    "Invalid topology collection count.");
            }

            return count;
        }
    }
}
