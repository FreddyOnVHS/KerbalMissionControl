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
                    Revision = revision,
                    TransportVersion =
                        VesselTopologyPacketCodec.CurrentVersion
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

            VesselResourceAnalyzer.Analyze(
                vessel,
                topology);

            // Rendering only. Any modded mesh failure preserves topology telemetry.
            try { BuildVisibleSideRaster(vessel, topology); }
            catch { topology.SchematicPixels = null; }

            return topology;
        }

        // 14.23.7: CPU z-buffer in vessel side plane (horizontal Z, vertical Y,
        // depth X looking toward -X). Store only visible faces and boundaries.
        private static void BuildVisibleSideRaster(Vessel vessel, VesselTopology topology)
        {
            Transform reference = vessel.ReferenceTransform;
            if (reference == null || topology.Nodes.Count == 0) return;
            double minH = double.PositiveInfinity, maxH = double.NegativeInfinity;
            double minV = double.PositiveInfinity, maxV = double.NegativeInfinity;
            foreach (VesselTopologyNode node in topology.Nodes)
            {
                foreach (VesselOutlinePoint pt in node.SideOutlinePoints)
                {
                    minH = Math.Min(minH, pt.Horizontal);
                    maxH = Math.Max(maxH, pt.Horizontal);
                    minV = Math.Min(minV, pt.Vertical);
                    maxV = Math.Max(maxV, pt.Vertical);
                }
            }
            if (double.IsInfinity(minH) || double.IsInfinity(minV) ||
                maxH - minH < 0.001 || maxV - minV < 0.001) return;
            double padH = Math.Max(0.1, (maxH - minH) * 0.02);
            double padV = Math.Max(0.1, (maxV - minV) * 0.02);
            minH -= padH; maxH += padH; minV -= padV; maxV += padV;
            const int width = 160, height = 256;
            int[] owners = new int[width * height];
            float[] depth = new float[width * height];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.NegativeInfinity;
            int triangleBudget = 240000;
            foreach (Part part in vessel.parts)
            {
                if (part == null || triangleBudget <= 0) break;
                Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
                if (renderers == null) continue;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy ||
                        renderer.GetComponentInParent<Part>() != part) continue;
                    Mesh mesh = null;
                    Transform meshTransform = renderer.transform;
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                    else
                    {
                        SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                        if (skin != null) mesh = skin.sharedMesh;
                    }
                    if (mesh == null || !mesh.isReadable) continue;
                    try
                    {
                        Vector3[] vertices = mesh.vertices;
                        int[] triangles = mesh.triangles;
                        if (vertices == null || triangles == null) continue;
                        Vector3[] points = new Vector3[vertices.Length];
                        for (int v = 0; v < vertices.Length; v++)
                            points[v] = reference.InverseTransformPoint(
                                meshTransform.TransformPoint(vertices[v]));
                        for (int t = 0; t + 2 < triangles.Length && triangleBudget > 0; t += 3)
                        {
                            triangleBudget--;
                            int a = triangles[t], b = triangles[t+1], c = triangles[t+2];
                            if (a < 0 || b < 0 || c < 0 || a >= points.Length ||
                                b >= points.Length || c >= points.Length) continue;
                            RasterTriangle(points[a], points[b], points[c],
                                (int)part.flightID, minH, maxH, minV, maxV,
                                width, height, depth, owners);
                        }
                    }
                    catch { /* unreadable or mod-provided mesh: nonfatal */ }
                }
            }
            byte[] pixels = new byte[owners.Length];
            int visible = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (owners[i] == 0) continue;
                    visible++;
                    int id = owners[i];
                    bool boundary = x == 0 || x == width-1 || y == 0 || y == height-1 ||
                        owners[i-1] != id || owners[i+1] != id ||
                        owners[i-width] != id || owners[i+width] != id;
                    pixels[i] = boundary ? (byte)2 : (byte)1;
                }
            if (visible == 0) return;
            topology.SchematicWidth = width;
            topology.SchematicHeight = height;
            topology.SchematicMinH = minH;
            topology.SchematicMaxH = maxH;
            topology.SchematicMinV = minV;
            topology.SchematicMaxV = maxV;
            topology.SchematicPixels = pixels;
        }

        private static void RasterTriangle(Vector3 a, Vector3 b, Vector3 c, int owner,
            double minH, double maxH, double minV, double maxV,
            int width, int height, float[] depths, int[] owners)
        {
            double ax = (a.z - minH) / (maxH - minH) * (width - 1);
            double bx = (b.z - minH) / (maxH - minH) * (width - 1);
            double cx = (c.z - minH) / (maxH - minH) * (width - 1);
            double ay = (maxV - a.y) / (maxV - minV) * (height - 1);
            double by = (maxV - b.y) / (maxV - minV) * (height - 1);
            double cy = (maxV - c.y) / (maxV - minV) * (height - 1);
            double area = (bx-ax)*(cy-ay) - (by-ay)*(cx-ax);
            if (Math.Abs(area) < 0.000001) return;
            int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
            int x1 = Math.Min(width-1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
            int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
            int y1 = Math.Min(height-1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double wb = ((px-ax)*(cy-ay)-(py-ay)*(cx-ax))/area;
                    double wc = ((bx-ax)*(py-ay)-(by-ay)*(px-ax))/area;
                    double wa = 1.0 - wb - wc;
                    if (wa < -0.000001 || wb < -0.000001 || wc < -0.000001) continue;
                    float d = (float)(wa*a.x + wb*b.x + wc*c.x);
                    int i = y * width + x;
                    if (d > depths[i]) { depths[i] = d; owners[i] = owner; }
                }
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

            VesselModuleDiscoveryAnalyzer.AnalyzePart(
                part,
                node);

            ReadVesselPosition(
                vessel,
                part,
                node);

            ReadVesselGeometry(
                vessel,
                part,
                node);

            BuildSideOutline(
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

        private static void ReadVesselGeometry(
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

                if (reference == null)
                {
                    return;
                }

                Vector3 right =
                    reference.InverseTransformDirection(
                        part.transform.right).normalized;

                Vector3 up =
                    reference.InverseTransformDirection(
                        part.transform.up).normalized;

                Vector3 forward =
                    reference.InverseTransformDirection(
                        part.transform.forward).normalized;

                Renderer[] renderers =
                    part.GetComponentsInChildren<Renderer>(
                        true);

                if (renderers == null ||
                    renderers.Length == 0)
                {
                    return;
                }

                bool hasBounds =
                    false;

                Vector3 localMin =
                    Vector3.zero;

                Vector3 localMax =
                    Vector3.zero;

                for (int index = 0;
                     index < renderers.Length;
                     index++)
                {
                    Renderer renderer =
                        renderers[index];

                    if (renderer == null)
                    {
                        continue;
                    }

                    /*
                     * Avoid hard references to optional Unity split modules
                     * such as UnityEngine.ParticleSystemModule. KSP's managed
                     * assembly set forwards ParticleSystemRenderer there, but
                     * KMC.Plugin does not otherwise need that module.
                     */
                    string rendererTypeName =
                        renderer.GetType().Name;

                    if (string.Equals(
                            rendererTypeName,
                            "ParticleSystemRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            rendererTypeName,
                            "LineRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            rendererTypeName,
                            "TrailRenderer",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    /*
                     * GetComponentsInChildren<Renderer>() on a Part transform
                     * can include renderer hierarchies belonging to attached
                     * child Parts. Only measure renderers whose nearest Part
                     * ancestor is the Part currently being sampled.
                     */
                    Part rendererOwner =
                        renderer.GetComponentInParent<Part>();

                    if (rendererOwner != part)
                    {
                        continue;
                    }

                    Bounds localRendererBounds;
                    Transform geometryTransform;

                    if (!TryGetRendererLocalGeometryBounds(
                            renderer,
                            out localRendererBounds,
                            out geometryTransform))
                    {
                        continue;
                    }

                    AccumulateRendererLocalBounds(
                        part,
                        geometryTransform,
                        localRendererBounds,
                        ref hasBounds,
                        ref localMin,
                        ref localMax);
                }

                if (!hasBounds)
                {
                    return;
                }

                Vector3 size =
                    localMax -
                    localMin;

                if (!IsFinite(size.x) ||
                    !IsFinite(size.y) ||
                    !IsFinite(size.z) ||
                    size.sqrMagnitude < 1.0e-8f)
                {
                    return;
                }

                Vector3 centerLocal =
                    (localMin + localMax) *
                    0.5f;

                node.GeometryAvailable =
                    true;

                node.BoundsCenterX =
                    centerLocal.x;
                node.BoundsCenterY =
                    centerLocal.y;
                node.BoundsCenterZ =
                    centerLocal.z;

                node.BoundsSizeX =
                    Math.Abs(size.x);
                node.BoundsSizeY =
                    Math.Abs(size.y);
                node.BoundsSizeZ =
                    Math.Abs(size.z);

                node.AxisRightX =
                    right.x;
                node.AxisRightY =
                    right.y;
                node.AxisRightZ =
                    right.z;

                node.AxisUpX =
                    up.x;
                node.AxisUpY =
                    up.y;
                node.AxisUpZ =
                    up.z;

                node.AxisForwardX =
                    forward.x;
                node.AxisForwardY =
                    forward.y;
                node.AxisForwardZ =
                    forward.z;
            }
            catch
            {
                /*
                 * Geometry is presentation data only. A modded part with a
                 * renderer edge case must never break topology telemetry.
                 */
                node.GeometryAvailable =
                    false;
            }
        }
        private static void BuildSideOutline(
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

            Transform reference =
                vessel.ReferenceTransform;

            if (reference == null)
            {
                return;
            }

            List<OutlineSample> samples =
                new List<OutlineSample>();

            try
            {
                Renderer[] renderers =
                    part.GetComponentsInChildren<Renderer>(
                        true);

                if (renderers == null)
                {
                    return;
                }

                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer =
                        renderers[rendererIndex];

                    if (renderer == null)
                    {
                        continue;
                    }

                    Part rendererOwner =
                        renderer.GetComponentInParent<Part>();

                    if (rendererOwner != part)
                    {
                        continue;
                    }

                    string typeName =
                        renderer.GetType().Name;

                    if (string.Equals(
                            typeName,
                            "ParticleSystemRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            typeName,
                            "LineRenderer",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            typeName,
                            "TrailRenderer",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    AddRendererOutlineSamples(
                        renderer,
                        reference,
                        samples);
                }
            }
            catch
            {
                return;
            }

            if (samples.Count < 3)
            {
                return;
            }

            List<OutlineSample> hull =
                BuildConvexHull(
                    samples);

            if (hull.Count < 3)
            {
                return;
            }

            hull =
                ReduceHull(
                    hull,
                    24);

            for (int index = 0;
                 index < hull.Count;
                 index++)
            {
                node.SideOutlinePoints.Add(
                    new VesselOutlinePoint
                    {
                        Horizontal =
                            hull[index].Horizontal,

                        Vertical =
                            hull[index].Vertical
                    });
            }
        }

        private static void AddRendererOutlineSamples(
            Renderer renderer,
            Transform reference,
            IList<OutlineSample> samples)
        {
            if (renderer == null ||
                reference == null ||
                samples == null)
            {
                return;
            }

            string typeName =
                renderer.GetType().Name;

            if (string.Equals(
                    typeName,
                    "SkinnedMeshRenderer",
                    StringComparison.Ordinal))
            {
                try
                {
                    System.Reflection.PropertyInfo sharedMeshProperty =
                        renderer.GetType().GetProperty(
                            "sharedMesh");

                    object mesh =
                        sharedMeshProperty != null
                            ? sharedMeshProperty.GetValue(
                                renderer,
                                null)
                            : null;

                    if (mesh != null)
                    {
                        AddMeshVertices(
                            mesh,
                            renderer.transform,
                            reference,
                            samples);

                        return;
                    }
                }
                catch
                {
                }
            }

            try
            {
                Component meshFilter =
                    renderer.GetComponent(
                        "MeshFilter");

                if (meshFilter == null)
                {
                    return;
                }

                System.Reflection.PropertyInfo sharedMeshProperty =
                    meshFilter.GetType().GetProperty(
                        "sharedMesh");

                object mesh =
                    sharedMeshProperty != null
                        ? sharedMeshProperty.GetValue(
                            meshFilter,
                            null)
                        : null;

                if (mesh == null)
                {
                    return;
                }

                AddMeshVertices(
                    mesh,
                    meshFilter.transform,
                    reference,
                    samples);
            }
            catch
            {
            }
        }

        private static void AddMeshVertices(
            object mesh,
            Transform geometryTransform,
            Transform reference,
            IList<OutlineSample> samples)
        {
            if (mesh == null ||
                geometryTransform == null ||
                reference == null)
            {
                return;
            }

            try
            {
                System.Reflection.PropertyInfo verticesProperty =
                    mesh.GetType().GetProperty(
                        "vertices");

                if (verticesProperty == null)
                {
                    return;
                }

                Vector3[] vertices =
                    verticesProperty.GetValue(
                        mesh,
                        null) as
                    Vector3[];

                if (vertices == null ||
                    vertices.Length == 0)
                {
                    return;
                }

                int stride =
                    Math.Max(
                        1,
                        vertices.Length /
                        512);

                for (int index = 0;
                     index < vertices.Length;
                     index += stride)
                {
                    Vector3 world =
                        geometryTransform.TransformPoint(
                            vertices[index]);

                    Vector3 vesselPoint =
                        reference.InverseTransformPoint(
                            world);

                    if (!IsFinite(vesselPoint.y) ||
                        !IsFinite(vesselPoint.z))
                    {
                        continue;
                    }

                    samples.Add(
                        new OutlineSample(
                            vesselPoint.z,
                            vesselPoint.y));
                }
            }
            catch
            {
            }
        }

        private static List<OutlineSample> BuildConvexHull(
            IList<OutlineSample> source)
        {
            List<OutlineSample> points =
                new List<OutlineSample>();

            for (int index = 0;
                 index < source.Count;
                 index++)
            {
                points.Add(
                    source[index]);
            }

            points.Sort(
                delegate(
                    OutlineSample left,
                    OutlineSample right)
                {
                    int horizontal =
                        left.Horizontal.CompareTo(
                            right.Horizontal);

                    return horizontal != 0
                        ? horizontal
                        : left.Vertical.CompareTo(
                            right.Vertical);
                });

            List<OutlineSample> unique =
                new List<OutlineSample>();

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                if (unique.Count == 0 ||
                    Math.Abs(
                        points[index].Horizontal -
                        unique[unique.Count - 1].Horizontal) >
                        1.0e-5 ||
                    Math.Abs(
                        points[index].Vertical -
                        unique[unique.Count - 1].Vertical) >
                        1.0e-5)
                {
                    unique.Add(
                        points[index]);
                }
            }

            if (unique.Count <= 2)
            {
                return unique;
            }

            List<OutlineSample> lower =
                new List<OutlineSample>();

            for (int index = 0;
                 index < unique.Count;
                 index++)
            {
                while (lower.Count >= 2 &&
                       Cross(
                           lower[lower.Count - 2],
                           lower[lower.Count - 1],
                           unique[index]) <= 0.0)
                {
                    lower.RemoveAt(
                        lower.Count - 1);
                }

                lower.Add(
                    unique[index]);
            }

            List<OutlineSample> upper =
                new List<OutlineSample>();

            for (int index = unique.Count - 1;
                 index >= 0;
                 index--)
            {
                while (upper.Count >= 2 &&
                       Cross(
                           upper[upper.Count - 2],
                           upper[upper.Count - 1],
                           unique[index]) <= 0.0)
                {
                    upper.RemoveAt(
                        upper.Count - 1);
                }

                upper.Add(
                    unique[index]);
            }

            lower.RemoveAt(
                lower.Count - 1);

            upper.RemoveAt(
                upper.Count - 1);

            lower.AddRange(
                upper);

            return lower;
        }

        private static List<OutlineSample> ReduceHull(
            IList<OutlineSample> hull,
            int maximumPoints)
        {
            List<OutlineSample> result =
                new List<OutlineSample>();

            if (hull == null ||
                hull.Count == 0)
            {
                return result;
            }

            if (hull.Count <= maximumPoints)
            {
                for (int index = 0;
                     index < hull.Count;
                     index++)
                {
                    result.Add(
                        hull[index]);
                }

                return result;
            }

            double step =
                (double)hull.Count /
                maximumPoints;

            for (int index = 0;
                 index < maximumPoints;
                 index++)
            {
                int sourceIndex =
                    Math.Min(
                        hull.Count - 1,
                        (int)Math.Floor(
                            index * step));

                result.Add(
                    hull[sourceIndex]);
            }

            return result;
        }

        private static double Cross(
            OutlineSample origin,
            OutlineSample left,
            OutlineSample right)
        {
            return
                (left.Horizontal -
                 origin.Horizontal) *
                (right.Vertical -
                 origin.Vertical) -
                (left.Vertical -
                 origin.Vertical) *
                (right.Horizontal -
                 origin.Horizontal);
        }

        private struct OutlineSample
        {
            public OutlineSample(
                double horizontal,
                double vertical)
            {
                Horizontal =
                    horizontal;

                Vertical =
                    vertical;
            }

            public double Horizontal;
            public double Vertical;
        }
        private static bool TryGetRendererLocalGeometryBounds(
            Renderer renderer,
            out Bounds bounds,
            out Transform geometryTransform)
        {
            bounds =
                new Bounds();

            geometryTransform =
                null;

            if (renderer == null ||
                renderer.transform == null)
            {
                return false;
            }

            /*
             * Skinned meshes have a localBounds value in the renderer's own
             * transform space. Use reflection so KMC.Plugin does not need a
             * hard reference to another optional Unity split module.
             */
            Type rendererType =
                renderer.GetType();

            if (string.Equals(
                    rendererType.Name,
                    "SkinnedMeshRenderer",
                    StringComparison.Ordinal))
            {
                try
                {
                    System.Reflection.PropertyInfo localBoundsProperty =
                        rendererType.GetProperty(
                            "localBounds");

                    if (localBoundsProperty != null &&
                        localBoundsProperty.PropertyType ==
                            typeof(Bounds))
                    {
                        object value =
                            localBoundsProperty.GetValue(
                                renderer,
                                null);

                        if (value is Bounds)
                        {
                            bounds =
                                (Bounds)value;

                            geometryTransform =
                                renderer.transform;

                            return IsUsableBounds(
                                bounds);
                        }
                    }
                }
                catch
                {
                }
            }

            /*
             * Ordinary KSP model renderers are normally MeshRenderer +
             * MeshFilter. Read sharedMesh.bounds, which is native mesh-local
             * geometry and has not been inflated into a world-space AABB.
             *
             * Use string/reflection lookup to remain tolerant of Unity's
             * split assembly layout.
             */
            try
            {
                Component meshFilter =
                    renderer.GetComponent(
                        "MeshFilter");

                if (meshFilter == null)
                {
                    return false;
                }

                System.Reflection.PropertyInfo sharedMeshProperty =
                    meshFilter.GetType().GetProperty(
                        "sharedMesh");

                if (sharedMeshProperty == null)
                {
                    return false;
                }

                object mesh =
                    sharedMeshProperty.GetValue(
                        meshFilter,
                        null);

                if (mesh == null)
                {
                    return false;
                }

                System.Reflection.PropertyInfo boundsProperty =
                    mesh.GetType().GetProperty(
                        "bounds");

                if (boundsProperty == null ||
                    boundsProperty.PropertyType !=
                        typeof(Bounds))
                {
                    return false;
                }

                object value =
                    boundsProperty.GetValue(
                        mesh,
                        null);

                if (!(value is Bounds))
                {
                    return false;
                }

                bounds =
                    (Bounds)value;

                geometryTransform =
                    meshFilter.transform;

                return IsUsableBounds(
                    bounds);
            }
            catch
            {
                return false;
            }
        }

        private static void AccumulateRendererLocalBounds(
            Part part,
            Transform geometryTransform,
            Bounds bounds,
            ref bool hasBounds,
            ref Vector3 localMin,
            ref Vector3 localMax)
        {
            if (part == null ||
                part.transform == null ||
                geometryTransform == null)
            {
                return;
            }

            Vector3 center =
                bounds.center;

            Vector3 extents =
                bounds.extents;

            for (int cornerIndex = 0;
                 cornerIndex < 8;
                 cornerIndex++)
            {
                Vector3 geometryLocal =
                    center +
                    new Vector3(
                        (cornerIndex & 1) != 0
                            ? extents.x
                            : -extents.x,
                        (cornerIndex & 2) != 0
                            ? extents.y
                            : -extents.y,
                        (cornerIndex & 4) != 0
                            ? extents.z
                            : -extents.z);

                /*
                 * Transform the native local mesh corner through the model
                 * transform, then into the owning Part's local frame.
                 * This preserves the actual model rotation/scale without
                 * passing through renderer.bounds (world-space AABB).
                 */
                Vector3 world =
                    geometryTransform.TransformPoint(
                        geometryLocal);

                Vector3 partLocal =
                    part.transform.InverseTransformPoint(
                        world);

                if (!IsFinite(partLocal.x) ||
                    !IsFinite(partLocal.y) ||
                    !IsFinite(partLocal.z))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    localMin =
                        partLocal;

                    localMax =
                        partLocal;

                    hasBounds =
                        true;
                }
                else
                {
                    localMin =
                        Vector3.Min(
                            localMin,
                            partLocal);

                    localMax =
                        Vector3.Max(
                            localMax,
                            partLocal);
                }
            }
        }

        private static bool IsUsableBounds(
            Bounds bounds)
        {
            Vector3 size =
                bounds.size;

            return
                IsFinite(size.x) &&
                IsFinite(size.y) &&
                IsFinite(size.z) &&
                size.x >= 0.0f &&
                size.y >= 0.0f &&
                size.z >= 0.0f &&
                size.sqrMagnitude >
                    1.0e-10f;
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
