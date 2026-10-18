param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path,[string]$text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

$builderPath =
    Join-Path $RepoRoot "KMC.Plugin\Topology\VesselTopologyBuilder.cs"

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$builder =
    ReadText $builderPath

$tests =
    ReadText $testPath

# ---------------------------------------------------------------------------
# Preflight: require the 14.23.2a/2b renderer filtering block.
# ---------------------------------------------------------------------------
$oldLoop = @'
                    Bounds bounds =
                        renderer.bounds;

                    Vector3 center =
                        bounds.center;

                    Vector3 extents =
                        bounds.extents;

                    for (int cornerIndex = 0;
                         cornerIndex < 8;
                         cornerIndex++)
                    {
                        Vector3 world =
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

                        Vector3 local =
                            part.transform.InverseTransformPoint(
                                world);

                        if (!hasBounds)
                        {
                            localMin =
                                local;

                            localMax =
                                local;

                            hasBounds =
                                true;
                        }
                        else
                        {
                            localMin =
                                Vector3.Min(
                                    localMin,
                                    local);

                            localMax =
                                Vector3.Max(
                                    localMax,
                                    local);
                        }
                    }
'@

$newLoop = @'
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
'@

if ($builder.Contains($oldLoop)) {
    $builder =
        $builder.Replace(
            $oldLoop,
            $newLoop)
}
elseif (-not $builder.Contains("TryGetRendererLocalGeometryBounds(")) {
    throw "Expected 14.23.2 world-space renderer.bounds block was not found. No file changed."
}

# ---------------------------------------------------------------------------
# Add local mesh/skinned-mesh helpers.
# Avoid direct compile-time references to split Unity renderer modules.
# ---------------------------------------------------------------------------
if (-not $builder.Contains("private static bool TryGetRendererLocalGeometryBounds(")) {
    $marker = @'
        private static VesselAttachmentType GetAttachmentType(
'@

    if (-not $builder.Contains($marker)) {
        throw "Could not locate VesselTopologyBuilder helper insertion point."
    }

    $helpers = @'
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

'@

    $builder =
        $builder.Replace(
            $marker,
            $helpers + $marker)
}

# ---------------------------------------------------------------------------
# Regression: ensure the bad world-space AABB path cannot silently return.
# ---------------------------------------------------------------------------
if (-not $tests.Contains('Run("SCHEM geometry uses native local mesh bounds"')) {
    $anchor =
        '        Run("SCHEM oriented geometry survives topology transport", VesselSchematicGeometryRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.23.2 geometry regression registration."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("SCHEM geometry uses native local mesh bounds", VesselSchematicNativeBoundsRegression);')
}

if (-not $tests.Contains("private static void VesselSchematicNativeBoundsRegression()")) {
    $anchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests insertion point."
    }

    $method = @'
    private static void VesselSchematicNativeBoundsRegression()
    {
        string builder =
            File.ReadAllText(
                Path.Combine(
                    Environment.CurrentDirectory,
                    "KMC.Plugin",
                    "Topology",
                    "VesselTopologyBuilder.cs"));

        Check(
            builder.IndexOf(
                "TryGetRendererLocalGeometryBounds",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "AccumulateRendererLocalBounds",
                StringComparison.Ordinal) >= 0,
            "SCHEM native-local geometry helpers missing");

        Check(
            builder.IndexOf(
                "GetComponent(" +
                Environment.NewLine +
                "                        \"MeshFilter\")",
                StringComparison.Ordinal) >= 0 ||
            builder.IndexOf(
                "GetComponent(" +
                "\r\n" +
                "                        \"MeshFilter\")",
                StringComparison.Ordinal) >= 0 ||
            builder.IndexOf(
                "\"MeshFilter\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM MeshFilter local-bounds path missing");

        Check(
            builder.IndexOf(
                "\"localBounds\"",
                StringComparison.Ordinal) >= 0 &&
            builder.IndexOf(
                "\"sharedMesh\"",
                StringComparison.Ordinal) >= 0,
            "SCHEM skinned/mesh local bounds lookup missing");

        Check(
            builder.IndexOf(
                "Bounds bounds =" +
                Environment.NewLine +
                "                        renderer.bounds",
                StringComparison.Ordinal) < 0 &&
            builder.IndexOf(
                "Bounds bounds =" +
                "\r\n" +
                "                        renderer.bounds",
                StringComparison.Ordinal) < 0,
            "world-space renderer.bounds AABB path still present");
    }


'@

    $tests =
        $tests.Replace(
            $anchor,
            $method + $anchor)
}

# Only write after all transforms succeeded.
WriteText $builderPath $builder
WriteText $testPath $tests

Write-Host "Applied KMC 14.23.2c Native Local Mesh Bounds."
Write-Host "SCHEM no longer derives part geometry from renderer.bounds world-space AABBs."
Write-Host "Rebuild and replace KMC.Plugin.dll before runtime testing."
