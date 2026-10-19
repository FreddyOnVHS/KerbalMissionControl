using System;
using System.IO;
using System.Text;

namespace KMC.Shared.Topology
{
    public static class VesselTopologyPacketCodec
    {
        public const int TopologyPort = 5082;
        public const int CurrentVersion = 5;

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

                WriteSchematic(writer, topology);
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

                    if (version >= 5) ReadSchematic(reader, result);
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

        private static void WriteSchematic(BinaryWriter writer, VesselTopology model)
        {
            byte[] pixels = model.SchematicPixels;
            bool valid = pixels != null && model.SchematicWidth > 0 &&
                model.SchematicHeight > 0 && model.SchematicWidth <= 256 &&
                model.SchematicHeight <= 384 &&
                pixels.Length == model.SchematicWidth * model.SchematicHeight;
            writer.Write(valid);
            if (!valid) return;
            writer.Write(model.SchematicWidth);
            writer.Write(model.SchematicHeight);
            writer.Write(model.SchematicMinH);
            writer.Write(model.SchematicMaxH);
            writer.Write(model.SchematicMinV);
            writer.Write(model.SchematicMaxV);
            // Three-byte RLE: 1 byte code and 2 byte run length.
            using (MemoryStream compressed = new MemoryStream())
            using (BinaryWriter runs = new BinaryWriter(compressed))
            {
                for (int pos = 0; pos < pixels.Length;)
                {
                    byte value = pixels[pos];
                    int count = 1;
                    while (pos + count < pixels.Length &&
                           pixels[pos + count] == value && count < 65535)
                        count++;
                    runs.Write(value);
                    runs.Write((ushort)count);
                    pos += count;
                }
                runs.Flush();
                byte[] bytes = compressed.ToArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        private static void ReadSchematic(BinaryReader reader, VesselTopology model)
        {
            if (!reader.ReadBoolean()) return;
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            double minH = reader.ReadDouble();
            double maxH = reader.ReadDouble();
            double minV = reader.ReadDouble();
            double maxV = reader.ReadDouble();
            int len = reader.ReadInt32();
            if (width <= 0 || height <= 0 || width > 256 || height > 384 ||
                len < 0 || len > 300000 || len % 3 != 0 ||
                !(maxH > minH) || !(maxV > minV))
                throw new InvalidDataException("Invalid SCHEM raster header.");
            byte[] packed = reader.ReadBytes(len);
            if (packed.Length != len) throw new EndOfStreamException();
            byte[] pixels = new byte[width * height];
            int pos = 0;
            for (int i = 0; i < packed.Length; i += 3)
            {
                byte value = packed[i];
                int count = packed[i + 1] | (packed[i + 2] << 8);
                if (value > 2 || count == 0 || count > pixels.Length - pos)
                    throw new InvalidDataException("Invalid SCHEM RLE.");
                for (int k = 0; k < count; k++) pixels[pos++] = value;
            }
            if (pos != pixels.Length) throw new InvalidDataException("Incomplete SCHEM RLE.");
            model.SchematicWidth = width;
            model.SchematicHeight = height;
            model.SchematicMinH = minH;
            model.SchematicMaxH = maxH;
            model.SchematicMinV = minV;
            model.SchematicMaxV = maxV;
            model.SchematicPixels = pixels;
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

            writer.Write(node.GeometryAvailable);
            writer.Write(node.BoundsCenterX);
            writer.Write(node.BoundsCenterY);
            writer.Write(node.BoundsCenterZ);
            writer.Write(node.BoundsSizeX);
            writer.Write(node.BoundsSizeY);
            writer.Write(node.BoundsSizeZ);
            writer.Write(node.AxisRightX);
            writer.Write(node.AxisRightY);
            writer.Write(node.AxisRightZ);
            writer.Write(node.AxisUpX);
            writer.Write(node.AxisUpY);
            writer.Write(node.AxisUpZ);
            writer.Write(node.AxisForwardX);
            writer.Write(node.AxisForwardY);
            writer.Write(node.AxisForwardZ);

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
            WriteOutlinePoints(
                writer,
                node.SideOutlinePoints);
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
                    VesselZ = reader.ReadDouble()
                };

            if (version >= 3)
            {
                node.GeometryAvailable =
                    reader.ReadBoolean();

                node.BoundsCenterX =
                    reader.ReadDouble();
                node.BoundsCenterY =
                    reader.ReadDouble();
                node.BoundsCenterZ =
                    reader.ReadDouble();

                node.BoundsSizeX =
                    reader.ReadDouble();
                node.BoundsSizeY =
                    reader.ReadDouble();
                node.BoundsSizeZ =
                    reader.ReadDouble();

                node.AxisRightX =
                    reader.ReadDouble();
                node.AxisRightY =
                    reader.ReadDouble();
                node.AxisRightZ =
                    reader.ReadDouble();

                node.AxisUpX =
                    reader.ReadDouble();
                node.AxisUpY =
                    reader.ReadDouble();
                node.AxisUpZ =
                    reader.ReadDouble();

                node.AxisForwardX =
                    reader.ReadDouble();
                node.AxisForwardY =
                    reader.ReadDouble();
                node.AxisForwardZ =
                    reader.ReadDouble();
            }

            node.StructuralDepth =
                reader.ReadInt32();

            node.SymmetryGroupId =
                reader.ReadUInt32();

            node.BranchRootPartId =
                reader.ReadUInt32();

            node.ActivationStage =
                reader.ReadInt32();

            node.SeparationStage =
                reader.ReadInt32();

            node.IsSeparationBoundary =
                reader.ReadBoolean();

            node.WillSeparateOnNextStage =
                reader.ReadBoolean();

            node.AllowsCrossFeed =
                reader.ReadBoolean();

            /*
             * The properties above were moved out of the initializer in v3
             * so v1/v2 packets can still be decoded without consuming fields
             * that do not exist in those transports.
             */

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

            if (version >= 4)
            {
                ReadOutlinePoints(
                    reader,
                    node.SideOutlinePoints);
            }

            return node;
        }

        private static void WriteOutlinePoints(
            BinaryWriter writer,
            System.Collections.Generic.IList<VesselOutlinePoint> points)
        {
            writer.Write(
                points != null
                    ? points.Count
                    : 0);

            if (points == null)
            {
                return;
            }

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                writer.Write(
                    points[index].Horizontal);

                writer.Write(
                    points[index].Vertical);
            }
        }

        private static void ReadOutlinePoints(
            BinaryReader reader,
            System.Collections.Generic.IList<VesselOutlinePoint> points)
        {
            int count =
                ReadCount(
                    reader,
                    128);

            for (int index = 0;
                 index < count;
                 index++)
            {
                points.Add(
                    new VesselOutlinePoint
                    {
                        Horizontal =
                            reader.ReadDouble(),

                        Vertical =
                            reader.ReadDouble()
                    });
            }
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
    // KMC 14.23.11 CHUNKED TOPOLOGY: bounded UDP framing around the unchanged v5 codec.
    // Supports interleaved vessels, stale-fragment eviction, size limits, and FNV-1a integrity.
    public sealed class VesselTopologyFragmentAssembler
    {
        private const int FragmentMagic = unchecked((int)0x4B4D4346); // KMCF
        private const int HeaderSize = 44;
        private const int FragmentPayload = 1156;
        private const int MaxTotalBytes = 1048576;
        private const int MaxFragments = 1024;
        private const int LifetimeSeconds = 8;
        private readonly object _sync = new object();
        private readonly System.Collections.Generic.Dictionary<Guid, Pending> _pending =
            new System.Collections.Generic.Dictionary<Guid, Pending>();

        private sealed class Pending
        {
            public DateTime LastUtc;
            public int Total;
            public int Count;
            public uint Checksum;
            public byte[][] Pieces;
            public int Received;
        }

        private static uint Hash(byte[] bytes)
        {
            uint hash = 2166136261u;
            unchecked { foreach (byte b in bytes) hash = (hash ^ b) * 16777619u; }
            return hash;
        }

        public static byte[][] Split(byte[] encoded)
        {
            if (encoded == null || encoded.Length == 0 || encoded.Length > MaxTotalBytes)
                throw new ArgumentOutOfRangeException("encoded", "Topology packet size exceeds 1 MiB limit.");
            int count = (encoded.Length + FragmentPayload - 1) / FragmentPayload;
            if (count > MaxFragments) throw new InvalidDataException("Too many topology fragments.");
            Guid id = Guid.NewGuid();
            byte[] idBytes = id.ToByteArray();
            uint checksum = Hash(encoded);
            byte[][] chunks = new byte[count][];
            for (int i = 0; i < count; ++i)
            {
                int start = i * FragmentPayload;
                int size = Math.Min(FragmentPayload, encoded.Length - start);
                using (MemoryStream ms = new MemoryStream(HeaderSize + size))
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(FragmentMagic);
                    bw.Write(1); // envelope version
                    bw.Write(idBytes);
                    bw.Write(encoded.Length);
                    bw.Write(count);
                    bw.Write(i);
                    bw.Write(checksum);
                    bw.Write(size);
                    bw.Write(encoded, start, size);
                    bw.Flush();
                    chunks[i] = ms.ToArray();
                }
            }
            return chunks;
        }

        // Returns false only if the datagram is unrelated to the fragment protocol.
        // When true, a null completed result means incomplete or invalid input.
        public bool Accept(byte[] datagram, out byte[] completed)
        {
            completed = null;
            if (datagram == null || datagram.Length < 4 || BitConverter.ToInt32(datagram, 0) != FragmentMagic)
                return false;
            if (datagram.Length < HeaderSize || datagram.Length > HeaderSize + FragmentPayload) return true;
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(datagram, false)))
                {
                    r.ReadInt32();
                    if (r.ReadInt32() != 1) return true;
                    Guid id = new Guid(r.ReadBytes(16));
                    int total = r.ReadInt32();
                    int count = r.ReadInt32();
                    int index = r.ReadInt32();
                    uint checksum = r.ReadUInt32();
                    int length = r.ReadInt32();
                    if (total < 1 || total > MaxTotalBytes || count < 1 || count > MaxFragments ||
                        count != (total + FragmentPayload - 1) / FragmentPayload ||
                        index < 0 || index >= count || length < 1 || length > FragmentPayload ||
                        length != Math.Min(FragmentPayload, total - index * FragmentPayload) ||
                        datagram.Length != HeaderSize + length) return true;
                    byte[] payload = r.ReadBytes(length);
                    lock (_sync)
                    {
                        DateTime now = DateTime.UtcNow;
                        var expired = new System.Collections.Generic.List<Guid>();
                        foreach (var item in _pending)
                            if ((now - item.Value.LastUtc).TotalSeconds > LifetimeSeconds) expired.Add(item.Key);
                        foreach (Guid key in expired) _pending.Remove(key);
                        Pending pending;
                        if (!_pending.TryGetValue(id, out pending))
                        {
                            if (_pending.Count >= 8) _pending.Clear();
                            pending = new Pending { LastUtc = now, Total = total, Count = count,
                                Checksum = checksum, Pieces = new byte[count][] };
                            _pending.Add(id, pending);
                        }
                        if (pending.Total != total || pending.Count != count || pending.Checksum != checksum)
                        { _pending.Remove(id); return true; }
                        pending.LastUtc = now;
                        if (pending.Pieces[index] == null) { pending.Pieces[index] = payload; pending.Received++; }
                        else if (!Equal(pending.Pieces[index], payload)) { _pending.Remove(id); return true; }
                        if (pending.Received != count) return true;
                        byte[] result = new byte[total];
                        int position = 0;
                        foreach (byte[] piece in pending.Pieces)
                        {
                            if (piece == null || position + piece.Length > total) { _pending.Remove(id); return true; }
                            Buffer.BlockCopy(piece, 0, result, position, piece.Length);
                            position += piece.Length;
                        }
                        _pending.Remove(id);
                        if (position == total && Hash(result) == checksum) completed = result;
                        return true;
                    }
                }
            }
            catch (Exception) { return true; }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
