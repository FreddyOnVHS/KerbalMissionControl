using System;
using System.Reflection;
using KMC.Shared;

namespace KMC.Plugin
{
    /// <summary>
    /// KMC Build 14.18.10 production cockpit-lighting scope.
    ///
    /// Exposes one RasterPropMonitor external variable:
    ///
    ///     KMC_MK1_BACKLIGHT_ALLOW
    ///
    /// It returns real KMC ESS power truth only when KMC can positively
    /// identify the current IVA as DE_mk1CockpitInternal.
    ///
    /// Every unsupported / unknown cockpit and every KMC-link-loss case returns
    /// 1.0 (ALLOW), preserving native ASET backlighting.
    ///
    /// This keeps the successful Rev-C ASET output gate behaviorally scoped to
    /// the Mk1 reference cockpit without modifying ASET props individually.
    /// </summary>
    public sealed class KmcRpmLightingScopeVariableHandler :
        PartModule
    {
        private const double MinimumPoweredBusVoltage =
            18.0;

        private const string ReferenceInternalName =
            "DE_mk1CockpitInternal";

        public object ProcessVariable(
            string variableName)
        {
            if (!string.Equals(
                    variableName,
                    "KMC_MK1_BACKLIGHT_ALLOW",
                    StringComparison.Ordinal))
            {
                return null;
            }

            /*
             * Profile detection must be positive before KMC is allowed to
             * electrically affect the ASET backlight.
             *
             * Unknown profile => fail open.
             */
            if (!IsReferenceMk1Iva())
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

            return
                IsBusPowered(
                    status.EssentialVoltage,
                    status.EssentialState)
                    ? 1.0
                    : 0.0;
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

        private bool IsReferenceMk1Iva()
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

            if (IsReferenceName(
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
                IsReferenceName(
                    modelName);
        }

        private static bool IsReferenceName(
            string value)
        {
            return
                string.Equals(
                    (value ?? string.Empty).Trim(),
                    ReferenceInternalName,
                    StringComparison.Ordinal);
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
