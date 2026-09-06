using System;
using System.Reflection;
using KMC.Shared;

namespace KMC.Plugin
{
    /// <summary>
    /// KMC Build 14.20.6 supported-DE-IVA cockpit-lighting scope.
    ///
    /// Exposes RasterPropMonitor external variables:
    ///
    ///     KMC_DE_IVA_BACKLIGHT_ALLOW
    ///     KMC_MK1_BACKLIGHT_ALLOW (legacy alias)
    ///
    /// Both return real KMC ESS power truth only when KMC can positively
    /// identify the current IVA as one of the supported DE_IVAExtension
    /// interiors brought to Mk1-reference electrical parity through 14.20.5.
    ///
    /// Every unsupported / unknown cockpit and every KMC-link-loss case returns
    /// 1.0 (ALLOW), preserving native ASET backlighting.
    ///
    /// KMC never changes the crew's PERSISTENT_BackLight command and never
    /// manipulates Unity renderers, materials, textures, meshes, or Light objects.
    /// </summary>
    public sealed class KmcRpmLightingScopeVariableHandler :
        PartModule
    {
        private const double MinimumPoweredBusVoltage =
            18.0;

        private static readonly string[] SupportedInternalNames =
            new[]
            {
                "DE_mk1CockpitInternal",
                "DE_mk1pod_IVA",
                "DE_mk1InlineInternal",
                "DE_Mk1-3",
                "DE_landerCabinSmallInternal",
                "DE_mk2LanderCanInternal",
                "DE_cupolaInternal",
                "DE_KV1_ASET_IVA_Internal",
                "DE_KV2_ASET_IVA_Internal",
                "DE_KV3_ASET_IVA_Internal",
                "DE_MEM_ASET_IVA_Internal",
                "DE_MK2POD_ASET_IVA_Internal",
                "DE_mk2CockpitStandardInternals",
                "DE_mk2InlineInternal",
                "DE_MK3_Cockpit_Int"
            };

        public object ProcessVariable(
            string variableName)
        {
            bool generalizedVariable =
                string.Equals(
                    variableName,
                    "KMC_DE_IVA_BACKLIGHT_ALLOW",
                    StringComparison.Ordinal);

            bool legacyMk1Variable =
                string.Equals(
                    variableName,
                    "KMC_MK1_BACKLIGHT_ALLOW",
                    StringComparison.Ordinal);

            if (!generalizedVariable &&
                !legacyMk1Variable)
            {
                return null;
            }

            /*
             * Profile detection must be positive before KMC is allowed to
             * electrically affect the ASET backlight.
             *
             * Unknown profile => fail open.
             */
            if (!IsSupportedDeIva())
            {
                return 1.0;
            }

            KmcMfdStatusPacket status;

            if (!TryGetStatus(
                    out status))
            {
                /*
                 * KMC disappearance / stale status => fail open.
                 */
                return 1.0;
            }

            bool essPowered =
                IsBusPowered(
                    status.EssentialVoltage,
                    status.EssentialState);

            if (!essPowered)
            {
                return 0.0;
            }

            /*
             * ESS can remain energized while BRK_LIGHTING_ESS is
             * tripped. The system-authority lease is the existing
             * breaker-specific lighting truth used by exterior lights.
             * Missing / stale authority evidence fails open.
             */
            if (KmcSystemAuthorityReceiver.IsAuthorityInhibited(
                    vessel,
                    SystemAuthorityKind.Lights))
            {
                return 0.0;
            }

            return 1.0;
        }

        private bool TryGetStatus(
            out KmcMfdStatusPacket status)
        {
            status = null;

            if (part == null ||
                part.vessel == null)
            {
                return false;
            }

            return
                KmcMfdStatusReceiver.TryGetStatus(
                    part.vessel.id.ToString(),
                    out status);
        }

        private bool IsSupportedDeIva()
        {
            if (part == null)
            {
                return false;
            }

            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            /*
             * KSP versions / API surfaces expose internal identity in slightly
             * different places. Probe the Part first, then its InternalModel.
             * No resolved identity means FAIL OPEN.
             */
            string directName =
                ReadStringMember(
                    part,
                    Flags,
                    "internalModelName",
                    "InternalModelName",
                    "internalName",
                    "InternalName");

            if (IsSupportedName(
                    directName))
            {
                return true;
            }

            object internalModel =
                ReadObjectMember(
                    part,
                    Flags,
                    "internalModel",
                    "InternalModel");

            if (internalModel == null)
            {
                return false;
            }

            string modelName =
                ReadStringMember(
                    internalModel,
                    Flags,
                    "internalName",
                    "InternalName",
                    "name",
                    "Name");

            return
                IsSupportedName(
                    modelName);
        }

        private static bool IsSupportedName(
            string value)
        {
            string normalized =
                (value ?? string.Empty).Trim();

            for (int i = 0;
                 i < SupportedInternalNames.Length;
                 ++i)
            {
                if (string.Equals(
                        normalized,
                        SupportedInternalNames[i],
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadStringMember(
            object target,
            BindingFlags flags,
            params string[] names)
        {
            object value =
                ReadObjectMember(
                    target,
                    flags,
                    names);

            return
                value == null
                    ? string.Empty
                    : value.ToString();
        }

        private static object ReadObjectMember(
            object target,
            BindingFlags flags,
            params string[] names)
        {
            if (target == null ||
                names == null)
            {
                return null;
            }

            Type type =
                target.GetType();

            for (int i = 0;
                 i < names.Length;
                 ++i)
            {
                string name =
                    names[i];

                if (string.IsNullOrWhiteSpace(
                        name))
                {
                    continue;
                }

                try
                {
                    FieldInfo field =
                        type.GetField(
                            name,
                            flags);

                    if (field != null)
                    {
                        object value =
                            field.GetValue(
                                target);

                        if (value != null)
                        {
                            return value;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    PropertyInfo property =
                        type.GetProperty(
                            name,
                            flags);

                    if (property != null &&
                        property.CanRead &&
                        property.GetIndexParameters()
                            .Length == 0)
                    {
                        object value =
                            property.GetValue(
                                target,
                                null);

                        if (value != null)
                        {
                            return value;
                        }
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool IsBusPowered(
            double voltage,
            string state)
        {
            if (double.IsNaN(
                    voltage) ||
                double.IsInfinity(
                    voltage) ||
                voltage <
                    MinimumPoweredBusVoltage)
            {
                return false;
            }

            string normalized =
                string.IsNullOrWhiteSpace(
                    state)
                    ? string.Empty
                    : state.Trim()
                        .ToUpperInvariant();

            return
                !string.Equals(
                    normalized,
                    "UNPOWERED",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    normalized,
                    "FAILED",
                    StringComparison.Ordinal);
        }
    }
}
